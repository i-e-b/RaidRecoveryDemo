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
            raidSet.Data1 = null;
            recoveredData = raidSet.Recover();
            Assert.That(recoveredData, Is.EqualTo(originalData).AsCollection!, "Recovered data is not the same as before");
            
            // Check data is back:
            var result = Raid6Calculator.UnsliceData(raidSet.Data1!, raidSet.Data2!, raidSet.Data3!);
            Assert.That(result, Is.EqualTo(originalData).AsCollection!, "Recover did not reset data");
            
        }
    }
}