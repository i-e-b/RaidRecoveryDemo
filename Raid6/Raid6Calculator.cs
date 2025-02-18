using System;

namespace RaidRecoverDemo;

public static class Raid6Calculator
{
    public static (byte[],byte[],byte[]) SliceData(byte[] src)
    {
        if (src == null || src.Length < 3) throw new Exception("Source must have length >= 3");
        var len  = (int)Math.Ceiling(src.Length / 3.0);
        var sEnd = src.Length / 3;

        var slice1 = new byte[len];
        var slice2 = new byte[len];
        var slice3 = new byte[len];
        for (int i = 0; i < sEnd; i++)
        {
            var j = i * 3;
            slice1[i] = src[j+0];
            slice2[i] = src[j+1];
            slice3[i] = src[j+2];
        }
        return (slice1,slice2,slice3);
    }

    public static byte[] UnsliceData(byte[] slice1, byte[] slice2, byte[] slice3)
    {
        if (slice1 == null || slice2 == null || slice3 == null) throw new Exception("All slices must be non-null");
        var dstLength = slice1.Length + slice2.Length + slice3.Length;

        var dst = new byte[dstLength];
        for (int i = 0; i < slice1.Length; i++)
        {
            var j = i * 3;
            dst[j+0] = slice1[i];
            dst[j+1] = slice2[i];
            dst[j+2] = slice3[i];
        }
        return dst;
    }

    public static byte BytePd(byte slice1, byte slice2, byte slice3)
    {
        return (byte) (slice1 ^ slice2 ^ slice3);
    }

    public static byte[] CalculatePd(byte[] slice1, byte[] slice2, byte[] slice3)
    {
        if (slice1 == null || slice2 == null || slice3 == null) throw new Exception("All slices must be non-null");

        var pd = new byte[slice1.Length];
        for (int i = 0; i < pd.Length; i++)
        {
            pd[i] = (byte) (slice1[i] ^ slice2[i] ^ slice3[i]);
        }
        return pd;
    }

    public static byte ByteRs(byte slice1, byte slice2, byte slice3)
    {
        var f1 = GaloisMath.Factor(1);
        var f2 = GaloisMath.Factor(2);
        var f3 = GaloisMath.Factor(3);

        return GaloisMath.Add(
            f1.Mul(slice1),
            f2.Mul(slice2),
            f3.Mul(slice3)
        );
    }

    public static byte[] CalculateRs(byte[] slice1, byte[] slice2, byte[] slice3)
    {
        var len = slice1.Length;
        var rs  = new byte[len];

        var f1 = GaloisMath.Factor(1);
        var f2 = GaloisMath.Factor(2);
        var f3 = GaloisMath.Factor(3);

        for (int i = 0; i < len; i++)
        {
            rs[i] =
                GaloisMath.Add(
                    f1.Mul(slice1[i]),
                    f2.Mul(slice2[i]),
                    f3.Mul(slice3[i])
                );
        }
        return rs;
    }
}