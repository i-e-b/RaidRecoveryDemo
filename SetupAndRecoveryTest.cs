using System;
using NUnit.Framework;

namespace RaidRecoverDemo
{
    [TestFixture]
    public class SetupAndRecoveryTest
    {

        [Test]
        public void can_setup_and_recover_a_dataset()
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
            
            // TODO: null out various chunks and get them recovered
            
            // 1) Parity recovery
            raidSet.ParityData = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            Assert.That(raidSet.ParityData, Is.Not.Null, "Parity was not recovered");
            
            // 2) Reed-Solomon recovery
            raidSet.ReedSolomon = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            Assert.That(raidSet.ReedSolomon, Is.Not.Null, "Parity was not recovered");
            
            // 3a) Single data recovery
            raidSet.Data1 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            var result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
            
            // 3b) Single data recovery
            raidSet.Data2 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
            
            // 3c) Single data recovery
            raidSet.Data3 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
            
            
            // 4a) Double data recovery
            raidSet.Data1 = null;
            raidSet.Data2 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
            
            // 4b) Double data recovery
            raidSet.Data1 = null;
            raidSet.Data3 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
            
            // 4c) Double data recovery
            raidSet.Data2 = null;
            raidSet.Data3 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            // Check data is back:
            result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
        }
    }
}