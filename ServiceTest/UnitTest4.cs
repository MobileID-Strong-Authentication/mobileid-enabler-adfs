using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MobileId;
using System.IO;

namespace ServiceTest
{
    [TestClass]
    public class UnitTest4
    {
        private void doFile(string fileName) {
            WebClientImpl client = new WebClientImpl(new WebClientConfig());
            string s = File.ReadAllText(fileName);
            AuthResponseDto rsp = new AuthResponseDto(ServiceStatusCode.SIGNATURE);
            rsp.Signature = Convert.FromBase64String(s);
            Assert.IsNotNull(rsp.Signature, "signature not null");
            Assert.IsNotNull(rsp.UserCertificate, "signer cert not null");
            Assert.AreEqual("MIDCHE28RCLRX077", rsp.UserSerialNumber, "signer serial number");
            Assert.AreEqual("MIDCHE28RCLRX077:PN", rsp.UserPseudonym, "signer pseudonym");
        }

        [TestMethod]
        public void T30_ExtractCertFromSignature()
        {
            doFile("signature_500_nosubscriberinfo.txt");
        }

        [TestMethod]
        public void T31_ExtractCertFromSignature()
        {
            doFile("signature_500_subscriberinfo.txt");
        }

        [TestMethod]
        public void T32_ExtractCertFromSignature()
        {
            doFile("signature_500_linebreak.txt");
        }
    }
}
