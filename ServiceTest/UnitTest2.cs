using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MobileId;

namespace ServiceTest
{
    [TestClass]
    public class UnitTest2
    {
        [TestMethod]
        public void T10_WebClientAuthConfig()
        {
            WebClientConfig cfg = WebClientConfig.CreateConfigFromFile("WebClientAuthConfig01.xml");
            Assert.IsNotNull(cfg, "cfg not null");
            Assert.AreEqual("http://changeme.swisscom.ch", cfg.ApId);
        }

        [TestMethod]
        public void T11_WebClientAuthConfig()
        {
            WebClientConfig cfg = WebClientConfig.CreateConfigFromFile("WebClientAuthConfig02.xml");
            Assert.IsNotNull(cfg, "cfg not null");
            Assert.AreEqual("http://changeme.swisscom.ch", cfg.ApId);
            Assert.AreEqual("<\"#>", cfg.DtbsPrefix, "DtbsPrefix");
            Assert.AreEqual(99, cfg.RequestTimeOutSeconds, "RequestTimeOutSeconds");
            Assert.AreEqual("http://changeme.swisscom.ch/services", cfg.ServiceUrlPrefix, "ServiceUrlPrefix");
            Assert.AreEqual("http://mid.swisscom.ch/MID/v1/AuthProfile1", cfg.SignatureProfile, "SignatureProfile");
            Assert.AreEqual(false, cfg.SrvSideValidation, "SrvSideValidation");
            Assert.AreEqual("ABcd12", cfg.SslMidClientCertThumbprint, "SslMidClientCertThumbprint");
            Assert.AreEqual(System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser, cfg.SslMidClientKeystore, "SslMidClientKeystore");
            Assert.AreEqual("CN=Some CA, C=CH", cfg.SslRootCaCertDN, "SslRootCaCertDN");
            Assert.AreEqual(true, cfg.EnableSubscriberInfo, "EnableSubscriberInfo");
            Assert.AreEqual("", cfg.SeedApTransId, "SeedApTransId");
            Assert.AreEqual(2, cfg.PollResponseDelaySeconds, "PollResponseDelaySeconds");
            Assert.AreEqual(1, cfg.PollResponseIntervalSeconds, "PollResponseIntervalSeconds");
            Assert.AreEqual(3, (int)cfg.UserSerialNumberPolicy, "UserSerialNumberPolicy");
        }

        [TestMethod]
        public void T12_WebClientAuthConfig()
        {
            WebClientConfig cfg = WebClientConfig.CreateConfigFromFile("WebClientAuthConfig03.xml");
            Assert.AreEqual(UserSerialNumberPolicy.warnMismatch | UserSerialNumberPolicy.allowAbsence, 
                cfg.UserSerialNumberPolicy, "UserSerialNumberPolicy");
        }

        [TestMethod]
        public void T13_WebClientAuthConfig()
        {
            WebClientConfig cfg = WebClientConfig.CreateConfigFromFile("WebClientAuthConfig04.xml");
            Assert.AreEqual(UserSerialNumberPolicy.warnMismatch | UserSerialNumberPolicy.allowAbsence,
                cfg.UserSerialNumberPolicy, "UserSerialNumberPolicy");
        }
    }
}
