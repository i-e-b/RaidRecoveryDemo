using System;
using NUnit.Framework;

namespace RaidRecoverDemo
{
    [TestFixture]
    public class SetupAndRecoveryTest
    {
        [Test]
        public void can_setup_and_read_a_dataset()
        {
            // Based on https://anadoxin.org/blog/error-recovery-in-raid6.html/
            var rnd = new Random();

            var originalData = new byte[3072]; // easy to slice 3 ways. TODO: handle non-factor-3 sizes
            rnd.NextBytes(originalData);

            // Calculate the recovery data
            var raidSet = Raid6.CalculateRaidSet(originalData);
            Assert.That(raidSet, Is.Not.Null, "Failed to generate raid set");

            // Main 'reading' call
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
        }

        [Test]
        public void recover_parity_data()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            // 1) Parity recovery
            raidSet.ParityData = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            Assert.That(raidSet.ParityData, Is.Not.Null, "Parity was not recovered");
        }

        [Test]
        public void recover_reed_solomon_data()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            // 2) Reed-Solomon recovery
            raidSet.ReedSolomon = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            Assert.That(raidSet.ReedSolomon, Is.Not.Null, "Reed-Solomon was not recovered");
        }

        [Test]
        public void recover_all_checksum_data_from_complete_real_data()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            // 3) Loss of both recovery data sets
            raidSet.ReedSolomon = null;
            raidSet.ParityData = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            Assert.That(raidSet.ReedSolomon, Is.Not.Null, "Reed-Solomon was not recovered");
            Assert.That(raidSet.ParityData, Is.Not.Null, "Parity was not recovered");
        }

        [Test]
        public void recover_single_data_1()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data1 = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data1, Is.Not.Null, "Recover did not reset data");
        }

        [Test]
        public void recover_single_data_2()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data2 = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data1, Is.Not.Null, "Recover did not reset data");
        }

        [Test]
        public void recover_single_data_3()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data3 = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data1, Is.Not.Null, "Recover did not reset data");
        }

        
        [Test]
        public void recover_reed_solomon_and_one_data_1()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data1 = null;
            raidSet.ReedSolomon = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data1, Is.Not.Null, "Recover did not reset data");
            Assert.That(raidSet.ReedSolomon, Is.Not.Null, "Recover did not recover Reed-Solomon data");
        }
        [Test]
        public void recover_reed_solomon_and_one_data_2()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data2 = null;
            raidSet.ReedSolomon = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data2, Is.Not.Null, "Recover did not reset data");
            Assert.That(raidSet.ReedSolomon, Is.Not.Null, "Recover did not recover Reed-Solomon data");
        }
        [Test]
        public void recover_reed_solomon_and_one_data_3()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data3 = null;
            raidSet.ReedSolomon = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data3, Is.Not.Null, "Recover did not reset data");
            Assert.That(raidSet.ReedSolomon, Is.Not.Null, "Recover did not recover Reed-Solomon data");
        }

        
        [Test]
        public void recover_parity_and_one_data_1()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data1 = null;
            raidSet.ParityData = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data1, Is.Not.Null, "Recover did not reset data");
            Assert.That(raidSet.ParityData, Is.Not.Null, "Recover did not recover parity data");
        }
        [Test]
        public void recover_parity_and_one_data_2()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data2 = null;
            raidSet.ParityData = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data2, Is.Not.Null, "Recover did not reset data");
            Assert.That(raidSet.ParityData, Is.Not.Null, "Recover did not recover parity data");
        }
        [Test]
        public void recover_parity_and_one_data_3()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            raidSet.Data3 = null;
            raidSet.ParityData = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            Assert.That(raidSet.Data3, Is.Not.Null, "Recover did not reset data");
            Assert.That(raidSet.ParityData, Is.Not.Null, "Recover did not recover parity data");
        }


        [Test]
        public void recover_double_data_1()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            // 4a) Double data recovery
            raidSet.Data1 = null;
            raidSet.Data2 = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            var result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
        }

        [Test]
        public void recover_double_data_2()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            // 4b) Double data recovery
            raidSet.Data1 = null;
            raidSet.Data3 = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            var result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
        }

        [Test]
        public void recover_double_data_3()
        {
            var originalData = GetRandomBytes();
            var raidSet = Raid6.CalculateRaidSet(originalData);

            // 4c) Double data recovery
            raidSet.Data2 = null;
            raidSet.Data3 = null;
            var recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            var result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
        }


        private static byte[] GetRandomBytes()
        {
            var rnd = new Random();
            var originalData = new byte[3072];
            rnd.NextBytes(originalData);
            return originalData;
        }
    }
}