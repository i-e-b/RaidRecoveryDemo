using System;
using System.Collections.Generic;
using System.Linq;

namespace RaidRecoverDemo;

/// <summary>
/// An alternative Reed-Solomon implementation.
/// This is not for use with Raid6, but rather
/// provided for comparison and testing.
/// <p/>
/// Adapted from https://github.com/cho45/reedsolomon.js
/// </summary>
public class GenericReedSolomon
{
    private readonly GenericGaloisField _field;
    private readonly List<GfPoly>      _genStack;

    public GenericReedSolomon(int primitive, int size, int genBase = 1, int genAlpha = 2)
    {
        _field = new GenericGaloisField(primitive, size, genBase, genAlpha);
        _genStack = new List<GfPoly>();
        _genStack.Add(new GfPoly(_field, [1]));
    }

    /// <summary> <c>int</c> or <c>byte</c>. 8 bit code </summary>
    public static GenericReedSolomon QrCode() => new(285, 256, 0);

    /// <summary> <c>int</c> only. 12 bit code </summary>
    public static GenericReedSolomon Aztec12() => new(4201, 4096);

    /// <summary> <c>int</c> only. 10 bit code </summary>
    public static GenericReedSolomon Aztec10() => new(1033, 1024);

    /// <summary> <c>int</c> or <c>byte</c>. 8 bit coded </summary>
    public static GenericReedSolomon Aztec8() => new(301, 256);

    /// <summary> <c>int</c> or <c>byte</c>. 6 bit code </summary>
    public static GenericReedSolomon Aztec6() => new(67, 64);

    /// <summary> <c>int</c> or <c>byte</c>. 4 bit code </summary>
    public static GenericReedSolomon Aztec4() => new(19, 16);

    /// <summary>
    /// Add error correction symbols to the end of data.
    /// The collection is modified in place.
    /// </summary>
    /// <param name="data">Original data</param>
    /// <param name="correctionSymbolCount">Count of EC symbols to add</param>
    public void AddErrorCorrection(ICollection<int> data, int correctionSymbolCount)
    {
        if (correctionSymbolCount < 1) return; // no symbols to add
        if (data.Count < 1) return; // nothing to correct

        var gen   = Generator(correctionSymbolCount);

        var dataP = new GfPoly(_field, data);
        dataP = dataP.Mul(correctionSymbolCount, 1);

        var mod   = dataP.Mod(gen);
        var coe   = mod.Coefficients.ToArray();

        var zeros = correctionSymbolCount - coe.Length;
        for (var i = 0; i < zeros; i++) { data.Add(0); }

        foreach (var sym in coe) data.Add(sym);
    }

    /// <summary>
    /// Add error correction symbols to the end of data.
    /// The collection is modified in place.
    /// The field 'size' must be 256 or less.
    /// </summary>
    /// <param name="data">Original data</param>
    /// <param name="correctionSymbolCount">Count of EC symbols to add</param>
    public void AddErrorCorrection(ICollection<byte> data, int correctionSymbolCount)
    {
        if (_field.Size > 256) throw new InvalidOperationException("Field size is over 8 bits");
        if (correctionSymbolCount < 1) return; // no symbols to add
        if (data.Count < 1) return; // nothing to correct

        var gen   = Generator(correctionSymbolCount);

        var dataP = new GfPoly(_field, data.Select(b=>(int)b));
        dataP = dataP.Mul(correctionSymbolCount, 1);

        var mod = dataP.Mod(gen);
        var coe = mod.Coefficients.ToArray();

        var zeros = correctionSymbolCount - coe.Length;
        for (var i = 0; i < zeros; i++) { data.Add(0); }

        foreach (var sym in coe) data.Add((byte)sym);
    }

    /// <summary>
    /// Try to decode and correct the encoded message.
    /// Corrections are made in place.
    /// Returns <c>true</c> if data had no errors, or if all errors were corrected.
    /// Returns <c>false</c> if data cannot be decoded correctly.
    /// </summary>
    /// <param name="message">Encoded data, as produced by <see cref="AddErrorCorrection(System.Collections.Generic.ICollection{int},int)"/></param>
    /// <param name="correctionSymbolCount">Count of EC symbols in message</param>
    public bool Decode(IList<int> message, int correctionSymbolCount)
    {
        var msgEnd = message.Count - 1;
        if (msgEnd < 1) return correctionSymbolCount < 1;

        var poly      = new GfPoly(_field, message);
        var syndrome  = new int[correctionSymbolCount]; // non-zero where faults are detected

        var errorCount = CalculateSyndrome(correctionSymbolCount, poly, syndrome);

        if (errorCount < 1) return true; // All ok.

        var synPoly = new GfPoly(_field, syndrome);
        var ok = Gcd(_field.Monomial(correctionSymbolCount, 1), synPoly, correctionSymbolCount,
            out var sigma, out var omega);

        if (!ok) return false; // GCD algorithm failed

        ok = FindErrorLocations(sigma, out var locations);
        if (!ok) return false; // too many errors

        var magnitudes = FindErrorMagnitudes(omega, locations);

        for (int i = 0; i < locations.Length; i++)
        {
            var pos = msgEnd - _field.Log(locations[i]);
            if (pos < 0 || pos > msgEnd) return false; // can't calculate a good location

            message[pos] = _field.Add(message[pos], magnitudes[i]);
        }

        // Check the syndrome again
        poly = new GfPoly(_field, message);
        errorCount = CalculateSyndrome(correctionSymbolCount, poly, syndrome);
        return errorCount == 0;
    }

    /// <summary>
    /// This returns the number of error positions, and writes
    /// syndrome values to the given array.
    /// <p/>
    /// The message polynomial should evaluate to zero at all points
    /// if the message is not damaged.
    /// </summary>
    private int CalculateSyndrome(int correctionSymbols, GfPoly poly, int[] syndrome)
    {
        var errors = 0;

        var genBase = _field.GeneratorBase;
        var synPos  = correctionSymbols - 1;
        for (int i = 0; i < correctionSymbols; i++)
        {
            var eval = poly.Eval(_field.Exp(i + genBase));
            syndrome[synPos--] = eval;
            if (eval != 0) errors++;
        }

        return errors;
    }

    /// <summary>
    /// Chien's search
    /// </summary>
    private bool FindErrorLocations(GfPoly sigma, out int[] locations)
    {
        var errors = sigma.Degree;
        if (errors == 1)
        {
            locations = [sigma[1]];
            return true;
        }

        var fieldSize = _field.Size;
        locations = new int[errors];

        var e = 0;

        for (var i = 0; i < fieldSize && e < errors; i++)
        {
            if (sigma.Eval(i) != 0) continue;

            locations[e] = _field.Inv(i);
            e++;
        }

        return e == errors;
    }

    /// <summary>
    /// Forney's formula
    /// </summary>
    private int[] FindErrorMagnitudes(GfPoly omega, int[] locations)
    {
        var end     = locations.Length;
        var result  = new int[end];
        var genBase = _field.GeneratorBase;

        if (_field.GeneratorBase == 0) ForneyZeroBase(omega, locations, end, result);
        else ForneyMulBase(omega, locations, end, result);

        return result;
    }

    private void ForneyZeroBase(GfPoly omega, int[] locations, int end, int[] result)
    {
        for (var i = 0; i < end; i++)
        {
            var inv = _field.Inv(locations[i]);
            var den = 1;
            for (int j = 0; j < end; j++)
            {
                if (i == j) continue;
                den = _field.Mul(den, _field.Add(1, _field.Mul(locations[j], inv)));
            }

            result[i] = _field.Mul(omega.Eval(inv), _field.Inv(den));
        }
    }

    private void ForneyMulBase(GfPoly omega, int[] locations, int end, int[] result)
    {
        for (var i = 0; i < end; i++)
        {
            var inv = _field.Inv(locations[i]);
            var den = 1;
            for (int j = 0; j < end; j++)
            {
                if (i == j) continue;
                den = _field.Mul(den, _field.Add(1, _field.Mul(locations[j], inv)));
            }

            result[i] = _field.Mul(_field.Mul(omega.Eval(inv), _field.Inv(den)), inv);
        }
    }

    /// <summary>
    /// Find the greatest common divisor (GCD) of two polynomials
    /// using the Euclidean algorithm.
    /// </summary>
    private bool Gcd(GfPoly ap, GfPoly bp, int count, out GfPoly sigma, out GfPoly omega)
    {
        sigma = _field.Zero;
        omega = _field.Zero;

        var (a, b) = (ap.Degree < bp.Degree) ? (bp, ap) : (ap, bp);

        var hR = count / 2;
        var r1 = a;
        var r0 = b;
        var t1 = _field.Zero;
        var t0 = _field.One;

        while (r0.Degree >= hR)
        {
            var r2 = r1;
            var t2 = t1;

            r1 = r0;
            t1 = t0;

            if (r1.IsZero) return false;

            r0 = r2;
            var q    = _field.Zero;
            var dlt  = r1[r1.Degree];
            var idlt = _field.Inv(dlt);

            while (r0.Degree >= r1.Degree && r0.NonZero)
            {
                var degDiff = r0.Degree - r1.Degree;
                var scale   = _field.Mul(r0[r0.Degree], idlt);
                q = q.Add(_field.Monomial(degDiff, scale));
                r0 = r0.Add(r1.Mul(degDiff, scale));
            }

            t0 = q.Mul(t1).Add(t2);

            if (r0.Degree >= r1.Degree) return false;
        }

        var sigmaZero = t0[0];
        if (sigmaZero == 0) return false;

        var inv = _field.Inv(sigmaZero);
        sigma = t0.Mul(inv);
        omega = r0.Mul(inv);
        return true;
    }

    private GfPoly Generator(int degree)
    {
        if (degree < _genStack.Count) return _genStack[degree];

        var prev = _genStack[^1];
        for (var d = _genStack.Count; d <= degree; d++)
        {
            var next = prev.Mul(new GfPoly(_field, [1, _field.Exp(d - 1 + _field.GeneratorBase)]));
            _genStack.Add(next);
            prev = next;
        }

        return _genStack[degree];
    }

    /// <summary>
    /// Math functions for general Galois fields
    /// </summary>
    private class GenericGaloisField
    {
        public int GeneratorBase { get; }
        public GfPoly Zero { get; }
        public GfPoly One { get; }
        public int Size { get; }

        private readonly int[] _exponents;
        private readonly int[] _logarithms;

        /// <summary>
        /// Create and initialise a GF calculator based on parameters
        /// </summary>
        /// <param name="polynomial">Binary representation of the polynomial factors</param>
        /// <param name="size">Galois field size</param>
        /// <param name="genBase">Generator base parameter</param>
        /// <param name="genAlpha">Generator alpha parameter</param>
        public GenericGaloisField(int polynomial, int size, int genBase, int genAlpha)
        {
            GeneratorBase = genBase;
            Size = size;

            if (genAlpha < 2) throw new ArgumentOutOfRangeException(nameof(genAlpha));
            if (size is < 8 or > 4096) throw new ArgumentOutOfRangeException(nameof(size));

            _exponents = new int[size];
            _logarithms = new int[size];

            // Build log and exponent tables modulo 'size'
            var mask     = size - 1;
            var exponent = 1;
            for (var i = 0; i < size; i++) {
                _exponents[i] = exponent;
                exponent *= genAlpha;
                if (exponent < size) continue;

                exponent ^= polynomial;
                exponent &= mask;
            }
            for (var i = 0; i < size-1; i++) {
                _logarithms[_exponents[i]] = i;
            }

            Zero = new GfPoly(this, GfPoly.Zero);
            One = new GfPoly(this, GfPoly.One);
        }

        /// <summary>
        /// Return a polynomial in this field of a given degree
        /// with a single non-zero coefficient in the maximum position.
        /// </summary>
        public GfPoly Monomial(int degree, int coefficient)
        {
            if (degree < 0) throw new ArgumentOutOfRangeException(nameof(degree));
            if (coefficient == 0) return Zero;
            var c = new int[degree + 1]; c[0] = coefficient;
            return new GfPoly(this, c);
        }

        /// <summary> Return log⁻¹(v) </summary>
        public int Exp(int v) => _exponents[v];

        /// <summary> Return log(v) </summary>
        public int Log(int v) => _logarithms[v];

        /// <summary> Multiplicative inverse </summary>
        public int Inv(int v) => _exponents[Size - _logarithms[v] - 1];

        /// <summary> Multiply two values in the field </summary>
        public int Mul(int a, int b) => _exponents[(_logarithms[a] + _logarithms[b]) % (Size - 1)];

        /// <summary> Add or subtract two values in the field </summary>
        public int Add(int a, int b) => a ^ b; // Subtract is the same
    }

    /// <summary>
    /// Polynomial value in a single <see cref="GenericGaloisField"/>
    /// </summary>
    private class GfPoly
    {
        private readonly GenericGaloisField _field;
        private readonly int[]              _coefficients;

        public static IEnumerable<int> Zero => [0];
        public static IEnumerable<int> One => [1];

        public int Degree => _coefficients.Length - 1;
        public IEnumerable<int> Coefficients => _coefficients;
        public bool IsZero => _coefficients[0] == 0;
        public bool NonZero => _coefficients[0] != 0;

        public int this[int deg] => _coefficients[_coefficients.Length - 1 - deg];

        public GfPoly(GenericGaloisField field, IEnumerable<int> coefficients)
        {
            _field = field;

            _coefficients = ToNonZeroPrefixArray(coefficients);
            if (_coefficients.Length < 1) throw new ArgumentOutOfRangeException(nameof(coefficients));
        }

        /// <summary>
        ///  Evaluate this polynomial at a given value <c>a</c>.
        /// <ul>
        /// <li>If <c>a</c> is 0, it returns the coefficient of the constant term (x⁰)</li>
        /// <li>If <c>a</c> is 1, it returns the sum of all coefficients</li>
        /// <li>Otherwise, evaluate the polynomial using Horner's method</li>
        /// </ul>
        /// </summary>
        public int Eval(int a)
        {
            if (a < 1) return this[0];
            if (a == 1) return _coefficients.Aggregate(0, _field.Add);

            var result = _coefficients[0];
            var length = _coefficients.Length;
            for (var i = 1; i < length; i++)
            {
                result = _field.Add(_field.Mul(a, result), _coefficients[i]);
            }

            return result;
        }

        /// <summary>
        /// Add or subtract polynomials.
        /// This will modify the larger of the two in-place.
        /// </summary>
        public GfPoly Add(GfPoly other)
        {
            if (other._field != _field) throw new InvalidOperationException("Polynomials must be of the same field");
            if (IsZero) return other;
            if (other.IsZero) return this;

            PickBySize(_coefficients, other._coefficients,
                out var smallC, out var largeC);

            var dLen = largeC.Length - smallC.Length;
            var maxL = largeC.Length;
            for (var i = dLen; i < maxL; i++)
            {
                largeC[i] = _field.Add(smallC[i - dLen], largeC[i]);
            }

            return new GfPoly(_field, largeC);
        }

        /// <summary>
        /// Multiply this polynomial by a scalar value
        /// </summary>
        public GfPoly Mul(int scalar)
        {
            if (scalar < 1) return _field.Zero;
            if (scalar == 1) return this;

            var len    = _coefficients.Length;
            var result = new int[len];
            for (var i = 0; i < _coefficients.Length; i++)
            {
                result[i] = _field.Mul(_coefficients[i], scalar);
            }

            return new GfPoly(_field, result);
        }

        /// <summary>
        /// Multiply this polynomial with another
        /// </summary>
        public GfPoly Mul(GfPoly other)
        {
            if (other._field != _field) throw new InvalidOperationException("Polynomials must be of the same field");
            if (IsZero || other.IsZero) return _field.Zero;

            var result = new int[_coefficients.Length + other._coefficients.Length - 1];
            var maxI   = _coefficients.Length;
            var maxJ   = other._coefficients.Length;

            for (var i = 0; i < maxI; i++)
            {
                for (var j = 0; j < maxJ; j++)
                {
                    var k = i + j;
                    result[k] = _field.Add(result[k], _field.Mul(_coefficients[i], other._coefficients[j]));
                }
            }

            return new GfPoly(_field, result);
        }

        /// <summary>
        /// Multiply this polynomial with a monomial
        /// </summary>
        public GfPoly Mul(int degree, int coefficient)
        {
            if (degree < 0) throw new ArgumentOutOfRangeException(nameof(degree));
            if (coefficient == 0) return _field.Zero;

            var maxI   = _coefficients.Length;
            var result = new int[maxI + degree];
            for (int i = 0; i < maxI; i++)
            {
                result[i] = _field.Mul(_coefficients[i], coefficient);
            }

            return new GfPoly(_field, result);
        }

        /// <summary>
        /// Return the remainder of dividing this polynomial by another.
        /// </summary>
        public GfPoly Mod(GfPoly other)
        {
            if (other._field != _field) throw new InvalidOperationException("Polynomials must be of the same field");
            if (other.IsZero) return _field.Zero; //throw new InvalidOperationException("Divide by zero in modulo");

            var q = _field.Zero;
            var r = this;

            var dlt  = other[other.Degree];
            var idlt = _field.Inv(dlt);

            while (r.Degree >= other.Degree && r.NonZero)
            {
                var dSize = r.Degree - other.Degree;
                var scale = _field.Mul(r[r.Degree], idlt);
                var term  = other.Mul(dSize, scale);
                var iq    = _field.Monomial(dSize, scale);
                q = q.Add(iq);
                r = r.Add(term);
            }

            return r;
        }

        private static void PickBySize(int[] a, int[] b, out int[] small, out int[] large)
        {
            if (a.Length > b.Length)
            {
                small = b;
                large = a;
            }
            else
            {
                small = a;
                large = b;
            }
        }

        private static int[] ToNonZeroPrefixArray(IEnumerable<int> coefficients)
        {
            // If this is a length-one set, the leading value can be zero.
            // For all other sets, the leading value must be non-zero.
            var set = new List<int>();
            var go  = false;

            foreach (var c in coefficients)
            {
                if (!go && c == 0) continue;
                set.Add(c);
                go = true;
            }

            return set.Count < 1 ? [0] : set.ToArray();
        }
    }
}
