using GameX.Platforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace GameX.Base
{
    [TestClass]
    public class PlatformTest
    {
        [TestMethod]
        public void Bootstrap_CanRegisterAnotherStartup()
        {
            lock (this)
            {
                Platform.Startups.Clear();
                Assert.AreEqual(0, Platform.Startups.Count, "None registered");
                Platform.Startups.Add(SomePlatform.Startup);
                Family.Touch();
                Assert.AreEqual(1, Platform.Startups.Count, "Single Startup");
                Assert.AreEqual(SomePlatform.Startup, Platform.Startups.First(), $"Default is {nameof(SomePlatform.Startup)}");
            }
        }
    }
}
