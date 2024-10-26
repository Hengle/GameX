using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GameX.Base
{
    [TestClass]
    public class FamilyManagerTest
    {
        [TestMethod]
        public void ShouldFamily()
        {
            Assert.AreEqual(1, FamilyManager.Families.Count);
        }

        [TestMethod]
        public void ShouldGetFamily()
        {
            Assert.ThrowsException<ArgumentNullException>(() => FamilyManager.GetFamily(null));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => FamilyManager.GetFamily("Missing"));
        }

        [TestMethod]
        public void ShouldParseFamily()
        {
            //Assert.ThrowsException<ArgumentNullException>(() => FamilyManager.ParseFamily(null));
            //Assert.IsNotNull(FamilyManager.ParseFamily(Some.FamilyJson.Replace("'", "\"")));
        }
    }
}
