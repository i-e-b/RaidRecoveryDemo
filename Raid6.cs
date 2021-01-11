using System;

namespace RaidRecoverDemo
{
    public class Raid6
    {
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
        }

        public byte[] Recover()
        {
            if (Data1 != null && Data2 != null && Data3 != null) return Raid6Calculator.UnsliceData(Data1!, Data2!, Data3!);
            throw new NotImplementedException("Recovery not yet implemented");
        }
    }
}