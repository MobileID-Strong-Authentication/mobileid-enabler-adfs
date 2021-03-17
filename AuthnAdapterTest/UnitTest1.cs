using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using MobileId.Adfs;

namespace AuthnAdapterTest
{
    [TestClass]
    public class UnitTest1
    {
        private AdfsConfig CreateConfigFromFileName(string fileName) {
            string s = File.ReadAllText("..\\..\\" + fileName);
            return AdfsConfig.CreateConfig(s);
        }

        [TestMethod]
        public void T40_AdfsConfigDefault()
        {
            AdfsConfig cfg = CreateConfigFromFileName("AdfsMidAuthConfig00.xml");
            Assert.AreEqual("mobile", cfg.AdAttrMobile, "property AdAttrMobile");
            Assert.AreEqual("serialnumber", cfg.AdAttrMidSerialNumber, "property AdAttrMidSerialNumber");
            Assert.AreEqual(100UL, cfg.WebClientMaxRequest, "property WebClientMaxRequest");
            Assert.AreEqual(5L, cfg.SessionMaxTries, "property SessionMaxTries");
            Assert.AreEqual(300L, cfg.SessionTimeoutSeconds, "property SessionTimeoutSeconds");
            Assert.AreEqual(false, cfg.SsoOnCancel, "property SsoOnCancel");
            Assert.AreEqual(false, cfg.ShowDebugMsg, "property ShowDebugMsg");
            Assert.AreEqual(5, cfg.LoginNonceLength, "property LoginNonceLength");
        }

        [TestMethod]
        public void T41_AdfsConfigFull()
        {
            AdfsConfig cfg = CreateConfigFromFileName("AdfsMidAuthConfig01.xml");
            Assert.AreEqual("attr-1", cfg.AdAttrMobile, "property AdAttrMobile");
            Assert.AreEqual("123", cfg.AdAttrMidSerialNumber, "property AdAttrMidSerialNumber");
            Assert.AreEqual(1UL, cfg.WebClientMaxRequest, "property WebClientMaxRequest");
            Assert.AreEqual(2L, cfg.SessionMaxTries, "property SessionMaxTries");
            Assert.AreEqual(999999999L, cfg.SessionTimeoutSeconds, "property SessionTimeoutSeconds");
            Assert.AreEqual(true, cfg.SsoOnCancel, "property SsoOnCancel");
            Assert.AreEqual(true, cfg.ShowDebugMsg, "property ShowDebugMsg");
            Assert.AreEqual(10, cfg.LoginNonceLength, "property LoginNonceLength");
        }

        [TestMethod]
        public void T42_AdfsConfigFrequent()
        {
            AdfsConfig cfg = CreateConfigFromFileName("AdfsMidAuthConfig02.xml");
            Assert.AreEqual("telefon", cfg.AdAttrMobile, "property AdAttrMobile");
        }

    }
}
