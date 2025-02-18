using System.Text;
using NUnit.Framework;
using RaidRecoverDemo;

namespace UnitTests;

[TestFixture]
public class Rs2Tests
{

    [Test]
    public void can_add_correction_codes_to_byte_data()
    {
        var data = "Hello, world!"u8.ToArray();

        var result = data.ToList();

        var rs = GenericReedSolomon.QrCode();

        rs.AddErrorCorrection(result, 4);

        Console.WriteLine(Encoding.UTF8.GetString(data));
        Console.WriteLine(string.Join(" ", data.Select(v => v.ToString("X2"))));
        Console.WriteLine(string.Join(" ", result.Select(v => v.ToString("X2"))));
        Console.WriteLine(Encoding.UTF8.GetString(result.ToArray()));

        Assert.That(result.Count, Is.EqualTo(data.Length + 4), "should add symbols");
    }

    [Test]
    public void can_add_correction_codes_to_int_data()
    {
        var data = "Hello, world!"u8.ToArray().Select(b=>b * 4).ToArray();

        var result = data.ToList();

        var rs = GenericReedSolomon.Aztec12();

        rs.AddErrorCorrection(result, 4);

        Console.WriteLine(string.Join(" ", data.Select(v => v.ToString("X3"))));
        Console.WriteLine(string.Join(" ", result.Select(v => v.ToString("X3"))));

        Assert.That(result.Count, Is.EqualTo(data.Length + 4), "should add symbols");
    }


    [Test]
    public void undamaged_data_is_decoded_successfully()
    {
        const int eccSymCount = 4;

        var data = "Hello, world!"u8.ToArray().Select(b=>(int)b).ToArray();
        var result = data.ToList();

        var rs = GenericReedSolomon.QrCode();

        rs.AddErrorCorrection(result, eccSymCount);

        Console.WriteLine(string.Join(" ", data.Select(v => v.ToString("X2"))));
        Console.WriteLine(string.Join(" ", result.Select(v => v.ToString("X2"))));

        Assert.That(result.Count, Is.EqualTo(data.Length + eccSymCount));

        var ok = rs.Decode(result, eccSymCount);

        Console.WriteLine(string.Join(" ", result.Take(result.Count - eccSymCount).Select(v => v.ToString("X2"))));

        Assert.That(ok, Is.True, "undamaged data should decode ok");
    }


    [Test]
    public void slightly_damaged_data_can_be_decoded_successfully()
    {
        const int eccSymCount = 4;

        var data = "Hello, world!"u8.ToArray().Select(b => (int)b).ToArray();
        var len  = data.Length + eccSymCount;
        var rs = GenericReedSolomon.Aztec12();

        for (int errorPos = 0; errorPos < len; errorPos++)
        {
            Console.WriteLine($"== Error at {errorPos} ==");
            Console.WriteLine(string.Join(" ", data.Select(v => v.ToString("X2"))));

            var result = data.ToList();
            rs.AddErrorCorrection(result, eccSymCount);

            result[errorPos] ^= 0x55; // flip half the bits
            Console.WriteLine(string.Join(" ", result.Select(v => v.ToString("X2"))));

            var ok = rs.Decode(result, eccSymCount);

            Console.WriteLine(string.Join(" ", result.Take(result.Count - eccSymCount).Select(v => v.ToString("X2"))));

            Assert.That(ok, Is.True, $"data damaged at {errorPos} should decode ok");
            Assert.That(result.Take(data.Length), Is.EqualTo(data).AsCollection, "restored data should be correct");
        }
    }
}