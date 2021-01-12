using System;

namespace RaidRecoverDemo
{
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
            if (Data1 != null && Data2 != null && Data3 != null)
            {
                // TODO: check the data is CORRECT against the PD, use RS to recover
                
                ParityData ??= Raid6Calculator.CalculatePd(Data1, Data2, Data3);
                ReedSolomon ??= Raid6Calculator.CalculateRs(Data1, Data2, Data3);
                return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
            }
            
            var lostParity = ParityData == null;
            var lostReedSolomon = ReedSolomon == null;
            var lostRecovery = lostParity && lostReedSolomon;
            var lostData = 0;
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
                return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
            }
            
            if (lostData == 1 && !lostReedSolomon)
            {
                RecoverSingleLostDataFromReedSolomon();
                ParityData ??= Raid6Calculator.CalculatePd(Data1!, Data2!, Data3!);
                return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
            }
            
            if (lostData == 2)
            {
                RecoverDoubleLostData();
                return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
            }
            

            throw new NotImplementedException("This recovery not yet implemented");
        }


        private void RecoverDoubleLostData()
        {
            throw new NotImplementedException();
        }
        
        private void RecoverSingleLostDataFromReedSolomon()
        {
            var recovered = new byte[_sliceSize];
            var rs = ReedSolomon;

            byte f1, f2, fDead, inv = 1;
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

        private void RecoverSingleLostDataFromParity()
        {
            var recovered = new byte[_sliceSize];
            var parity = ParityData;
            var good1 = Data1 ?? Data2;
            var good2 = Data3 ?? Data2;

            if (parity == null || good1 == null || good2 == null) throw new Exception("Recover data from parity called with invalid state");
            for (var i = 0; i < _sliceSize; i++)
            {
                recovered[i] = (byte)(good1[i] ^ good2[i] ^ parity[i]);
            }
            
            Data1 ??= recovered;
            Data2 ??= recovered;
            Data3 ??= recovered;
        }
    }
}