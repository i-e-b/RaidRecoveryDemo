using System;

namespace RaidRecoverDemo;

public class Raid6
{
    private readonly int _sliceSize;
    public byte[]? Data1 { get; set; }
    public byte[]? Data2 { get; set; }
    public byte[]? Data3 { get; set; }
    public byte[]? ParityData { get; set; }
    public byte[]? ReedSolomon { get; set; }

    public static Raid6 CalculateRaidSet(byte[] originalData)
    {
        var (slice1, slice2, slice3) = Raid6Calculator.SliceData(originalData);
        var pd = Raid6Calculator.CalculatePd(slice1, slice2, slice3);
        var rs = Raid6Calculator.CalculateRs(slice1, slice2, slice3);
        return new Raid6(slice1, slice2, slice3, pd, rs);
    }

    public Raid6(byte[] d1, byte[] d2, byte[] d3, byte[] pd, byte[] rs)
    {
        Data1 = d1;
        Data2 = d2;
        Data3 = d3;
        ParityData = pd;
        ReedSolomon = rs;
        _sliceSize = d1.Length;
    }

    public byte[] Recover()
    {
        if (Data1 != null && Data2 != null && Data3 != null) // all data is in place
        {
            ParityData ??= Raid6Calculator.CalculatePd(Data1, Data2, Data3);
            ReedSolomon ??= Raid6Calculator.CalculateRs(Data1, Data2, Data3);
            ErrorCorrectRound();
            return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
        }

        var lostParity      = ParityData == null;
        var lostReedSolomon = ReedSolomon == null;
        var lostRecovery    = lostParity && lostReedSolomon;
        var lostData        = 0;
        if (Data1 == null) lostData++;
        if (Data2 == null) lostData++;
        if (Data3 == null) lostData++;

        // Reject impossible cases
        if (lostData > 2) throw new Exception("Too much data lost. Recovery not possible");
        if (lostData > 0 && lostRecovery) throw new Exception("Data and recovery codes lost. Recovery not possible");
        if (lostData > 1 && (lostParity || lostReedSolomon)) throw new Exception("Too much data and recovery info lost. Recovery not possible");

        // Possible cases left:
        // 1) 2 data, PD
        // 2) 2 data, RS
        // 3) 1 data, PD & RS

        if (lostData == 1 && !lostParity)
        {
            RecoverSingleLostDataFromParity();
            ReedSolomon ??= Raid6Calculator.CalculateRs(Data1!, Data2!, Data3!);
            ErrorCorrectRound();
            return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
        }

        if (lostData == 1 && !lostReedSolomon)
        {
            RecoverSingleLostDataFromReedSolomon();
            ParityData ??= Raid6Calculator.CalculatePd(Data1!, Data2!, Data3!);
            ErrorCorrectRound();
            return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
        }

        if (lostData == 2)
        {
            RecoverDoubleLostData();
            ErrorCorrectRound();
            return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
        }

        throw new NotImplementedException("Unexpected state");
    }

    /// <summary>
    /// Either all data is present, or it has been recovered.
    /// Now we check the data against PD & RS. If anything is wrong,
    /// we try to correct the data more than one way.
    /// If the ways agree, we've recovered everything the best we can.
    /// If not, then we know that the data is corrupt beyond recovery
    /// </summary>
    private void ErrorCorrectRound()
    {
        if (ParityData == null || Data1 == null || Data2 == null ||
            Data3 == null || ReedSolomon == null) throw new Exception("Error correct round called with invalid state");

        for (var i = 0; i < _sliceSize; i++)
        {
            var d1 = Data1[i];
            var d2 = Data2[i];
            var d3 = Data3[i];

            // what the data thinks parity and reed solomon should be:
            var pd = Raid6Calculator.BytePd(d1, d2, d3);
            var rs = Raid6Calculator.ByteRs(d1, d2, d3);

            if (pd == ParityData[i] && rs == ReedSolomon[i]) continue; // this byte is good

            // If here, one of the 5 bytes are bad.
            // Try recovering each in turn from the others.
            // Agreement wins, otherwise throw.

            // Case 1: Data and Parity agree -- RS is wrong
            if (pd == ParityData[i])
            {
                ReedSolomon[i] = rs;
                continue;
            }

            // Case 2: Data and RS agree -- parity is wrong
            if (rs == ReedSolomon[i])
            {
                ParityData[i] = pd;
                continue;
            }

            // Case 3: Data does not agree with RS or PD
            // what the parity thinks the data should be:
            var parity1 = (byte)(Data3[i] ^ Data2[i] ^ ParityData[i]);
            var parity2 = (byte)(Data1[i] ^ Data3[i] ^ ParityData[i]);
            var parity3 = (byte)(Data1[i] ^ Data2[i] ^ ParityData[i]);

            // what the ecc thinks the data should be:
            var reedSolomon1 = RecoverByteFromRsAndData23(d2, d3, ReedSolomon[i]);
            var reedSolomon2 = RecoverByteFromRsAndData13(d1, d3, ReedSolomon[i]);
            var reedSolomon3 = RecoverByteFromRsAndData12(d1, d2, ReedSolomon[i]);

            // now check each one. Any 2 votes is good.
            if (parity1 == reedSolomon1) Data1[i] = parity1;
            if (parity2 == reedSolomon2) Data2[i] = parity2;
            if (parity3 == reedSolomon3) Data3[i] = parity3;

            // Check the sums again:
            d1 = Data1[i];
            d2 = Data2[i];
            d3 = Data3[i];

            pd = Raid6Calculator.BytePd(d1, d2, d3);
            rs = Raid6Calculator.ByteRs(d1, d2, d3);

            if (pd == ParityData[i] && rs == ReedSolomon[i]) continue; // this group has been repaired successfully

            throw new Exception("Unrecoverable data at index "+i);
        }
    }

    private byte RecoverByteFromRsAndData12(byte data1, byte data2, byte rs)
    {
        var  fDead = GaloisMath.Factor(3);
        var  f1    = GaloisMath.Factor(1);
        var  f2    = GaloisMath.Factor(2);
        byte inv   = 1;

        var partial = GaloisMath.Add(
            f1.Mul(data1),
            rs, // instead of our dead drive
            f2.Mul(data2)
        );

        return inv.Div(fDead).Mul(partial);
    }


    private byte RecoverByteFromRsAndData13(byte data1, byte data3, byte rs)
    {
        var  fDead = GaloisMath.Factor(2);
        var  f1    = GaloisMath.Factor(1);
        var  f2    = GaloisMath.Factor(3);
        byte inv   = 1;

        var partial = GaloisMath.Add(
            f1.Mul(data1),
            rs, // instead of our dead drive
            f2.Mul(data3)
        );

        return inv.Div(fDead).Mul(partial);
    }

    private byte RecoverByteFromRsAndData23(byte data2, byte data3, byte rs)
    {
        var  fDead = GaloisMath.Factor(1);
        var  f1    = GaloisMath.Factor(2);
        var  f2    = GaloisMath.Factor(3);
        byte inv   = 1;

        var partial = GaloisMath.Add(
            f1.Mul(data2),
            rs, // instead of our dead drive
            f2.Mul(data3)
        );

        return inv.Div(fDead).Mul(partial);
    }

    private void RecoverSingleLostDataFromParity()
    {
        var recovered = new byte[_sliceSize];
        var parity    = ParityData;
        var good1     = Data1 ?? Data2;
        var good2     = Data3 ?? Data2;

        if (parity == null || good1 == null || good2 == null) throw new Exception("Recover data from parity called with invalid state");
        for (var i = 0; i < _sliceSize; i++)
        {
            recovered[i] = (byte)(good1[i] ^ good2[i] ^ parity[i]);
        }

        Data1 ??= recovered;
        Data2 ??= recovered;
        Data3 ??= recovered;
    }

    private void RecoverSingleLostDataFromReedSolomon()
    {
        var recovered = new byte[_sliceSize];
        var rs        = ReedSolomon;

        byte   f1,    f2, fDead, inv = 1;
        byte[] good1, good2;
        if (Data1 == null)
        {
            fDead = GaloisMath.Factor(1);
            f1 = GaloisMath.Factor(2);
            f2 = GaloisMath.Factor(3);
            good1 = Data2!;
            good2 = Data3!;
        }
        else if (Data2 == null)
        {
            fDead = GaloisMath.Factor(2);
            f1 = GaloisMath.Factor(1);
            f2 = GaloisMath.Factor(3);
            good1 = Data1!;
            good2 = Data3!;
        }
        else if (Data3 == null)
        {
            fDead = GaloisMath.Factor(3);
            f1 = GaloisMath.Factor(1);
            f2 = GaloisMath.Factor(2);
            good1 = Data1!;
            good2 = Data2!;
        }
        else throw new Exception("Invalid state");

        if (rs == null || good1 == null || good2 == null) throw new Exception("Recover data from parity called with invalid state");
        for (var i = 0; i < _sliceSize; i++)
        {
            var partial = GaloisMath.Add(
                f1.Mul(good1[i]),
                rs[i], // instead of our dead drive
                f2.Mul(good2[i])
            );

            recovered[i] = inv.Div(fDead).Mul(partial);
        }

        Data1 ??= recovered;
        Data2 ??= recovered;
        Data3 ??= recovered;
    }

    private void RecoverDoubleLostData()
    {
        if (ReedSolomon == null || ParityData == null) throw new Exception("Recover double data called with invalid state");
        if (Data1 != null)
        {
            RecoverFromData1();
        }
        else if (Data2 != null)
        {
            RecoverFromData2();
        }
        else if (Data3 != null)
        {
            RecoverFromData3();
        }
        else throw new Exception("Invalid state");
    }

    private void RecoverFromData1()
    {
        if (Data1 == null || ReedSolomon == null || ParityData == null) throw new Exception("Recover double data (1) called with invalid state");

        Data2 = new byte[_sliceSize];
        Data3 = new byte[_sliceSize];
        byte inv = 1;
        var  g   = inv.Div(GaloisMath.Add(GaloisMath.Factor(2), GaloisMath.Factor(3)));
        var  f3  = GaloisMath.Factor(3);

        for (int i = 0; i < _sliceSize; i++)
        {
            var partParity      = Data1[i];
            var partReedSolomon = GaloisMath.Factor(1).Mul(Data1[i]);

            var xorPd = GaloisMath.Add(partParity, ParityData[i]);
            var mid   = GaloisMath.Add(f3.Mul(xorPd), partReedSolomon, ReedSolomon[i]);

            var r1 = mid.Mul(g);
            Data2[i] = r1;
            Data3[i] = GaloisMath.Add(r1, xorPd);
        }
    }

    private void RecoverFromData2()
    {
        if (Data2 == null || ReedSolomon == null || ParityData == null) throw new Exception("Recover double data (2) called with invalid state");

        Data1 = new byte[_sliceSize];
        Data3 = new byte[_sliceSize];
        byte inv = 1;
        var  g   = inv.Div(GaloisMath.Add(GaloisMath.Factor(1), GaloisMath.Factor(3)));
        var  f3  = GaloisMath.Factor(3);

        for (int i = 0; i < _sliceSize; i++)
        {
            var partParity      = Data2[i];
            var partReedSolomon = GaloisMath.Factor(2).Mul(Data2[i]);

            var xorPd = GaloisMath.Add(partParity, ParityData[i]);
            var mid   = GaloisMath.Add(f3.Mul(xorPd), partReedSolomon, ReedSolomon[i]);

            var r1 = mid.Mul(g);
            Data1[i] = r1;
            Data3[i] = GaloisMath.Add(r1, xorPd);
        }
    }

    private void RecoverFromData3()
    {
        if (Data3 == null || ReedSolomon == null || ParityData == null) throw new Exception("Recover double data (3) called with invalid state");

        Data1 = new byte[_sliceSize];
        Data2 = new byte[_sliceSize];
        byte inv    = 1;
        var  g      = inv.Div(GaloisMath.Add(GaloisMath.Factor(1), GaloisMath.Factor(2)));
        var  factor = GaloisMath.Factor(2);

        for (int i = 0; i < _sliceSize; i++)
        {
            var partParity      = Data3[i];
            var partReedSolomon = GaloisMath.Factor(3).Mul(Data3[i]);

            var xorPd = GaloisMath.Add(partParity, ParityData[i]);
            var mid   = GaloisMath.Add(factor.Mul(xorPd), partReedSolomon, ReedSolomon[i]);

            var r1 = mid.Mul(g);
            Data1[i] = r1;
            Data2[i] = GaloisMath.Add(r1, xorPd);
        }
    }
}