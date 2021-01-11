using System;
using System.Linq;

namespace RaidRecoverDemo
{
    public static class GaloisMath
    {
        private static byte[]? _gfLog;
        private static byte[]? _gfiLog;

        public static byte[] GfLog {
            get {
                if (_gfLog == null) CreateTables();
                return _gfLog!;
            }
        }
        public static byte[] GfiLog {
            get {
                if (_gfiLog == null) CreateTables();
                return _gfiLog!;
            }
        }

        /// <summary>
        /// Make our Galois-field look-up tables
        /// </summary>
        private static void CreateTables()
        {
            var polynomial = 0x011d;
            var s = 8;
            var gfElements = 1 << s;
            _gfLog = new byte[gfElements];
            _gfiLog = new byte[gfElements];
            
            var b = 1;
            for (int i = 0; i < gfElements; i++)
            {
                _gfLog[b] = (byte) (i & 255);
                _gfiLog[i] = (byte) (b & 255);
                b <<= 1;
                if ((b & gfElements) > 0) b ^= polynomial;
            }
            _gfLog[1] = 0;
        }

        public static byte Add(params byte [] a)
        {
            return a.Aggregate((byte)0, (current, t) => (byte) (current ^ t));
        }

        public static byte Mul(this byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GfiLog[(GfLog[a] + GfLog[b]) % 255];
        }

        private static byte Sub(this byte a, byte b)
        {
            return (byte)(a > b 
                    ? a - b
                    : 255 - (0 - (a - b))
                );
        }

        public static byte Div(this byte a, byte b)
        {
            return GfiLog[GfLog[a].Sub(GfLog[b])];
        }

        public static byte Factor(int index)
        {
            if (index < 1 || index > 255) throw new Exception("Factor must be between 1 and 255");
            return GfiLog[index - 1];
        }
    }
}