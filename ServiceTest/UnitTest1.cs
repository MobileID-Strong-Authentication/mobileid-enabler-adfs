using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MobileId;

namespace ServiceTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void T00_Util()
        {
            Assert.IsTrue(Util.IsXmlSafe(""), "empty string");
            Assert.IsTrue(Util.IsXmlSafe("a"));
            Assert.IsTrue(Util.IsXmlSafe("a:b"));
            Assert.IsFalse(Util.IsXmlSafe("<"));
            Assert.IsFalse(Util.IsXmlSafe("a:<b"));
            Assert.IsFalse(Util.IsXmlSafe(">"));
            Assert.IsFalse(Util.IsXmlSafe("a:>b"));
            Assert.IsFalse(Util.IsXmlSafe("\""));
        }

        [TestMethod]
        public void T01_AuthRequestDto()
        {
            AuthRequestDto dto = new AuthRequestDto();
            Assert.IsFalse(dto.IsComplete(), "empty dto");

            dto.ApId = "http://changeme.swisscom.ch";
            Assert.AreEqual("http://changeme.swisscom.ch", dto.ApId, "ApId");
            Assert.IsFalse(dto.IsComplete(), "dto(ApId) incomplete");

            try
            {
                dto.PhoneNumber = "";
                Assert.Fail("PhoneNumber('')");
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.GetType() == typeof(ArgumentNullException), "Expect ArgumentNullException on empty PhoneNumber");
            }

            try
            {
                dto.PhoneNumber = "garbarge";
                Assert.Fail("PhoneNumber('garbarge')");
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.GetType() == typeof(ArgumentException), "Expect ArgumentException on garbarge PhoneNumber");
                Assert.AreEqual("PhoneNumberIsIllFormed: 'garbarge'", e.Message, "Expect PhoneNumberIsIllFormed on garbarge PhoneNumber");
            }

            dto.PhoneNumber = "+41791234567";
            Assert.AreEqual("+41791234567", dto.PhoneNumber, "dto.PhoneNumber");
            Assert.IsFalse(dto.IsComplete(), "dto(ApId,PhoneNumber) incomplete");

        }

        [TestMethod]
        public void T02_AuthRequestDto_ToString()
        {
            AuthRequestDto dto = new AuthRequestDto();
            Assert.AreEqual("{ApId:null, Instant:null, MsgToBeSigned:null, PhoneNumber:null, TimeOut:80, TransId:null, TransIdPrefix:\"\", SrvSideValidation:False, UserLanguage:en, UserSerialNumber:}", dto.ToString(), "ToString(empty_dto)");

            dto.ApId = "http://changeme.swisscom.ch";
            dto.PhoneNumber = "+41791234567";
            dto.DataToBeSigned = "Hello, Mobile ID"; 
#if DEBUG
            Console.WriteLine("dto.Length=" + dto.ToString().Length);
#endif
            Assert.AreEqual("{ApId:\"http://changeme.swisscom.ch\", Instant:null, MsgToBeSigned:\"Hello, Mobile ID\", PhoneNumber:\"+41791234567\", TimeOut:80, TransId:null, TransIdPrefix:\"\", SrvSideValidation:False, UserLanguage:en, UserSerialNumber:}", dto.ToString(), "ToString(minimal_valid_dto)");
        }

        [TestMethod]
        public void T03_AuthRspServiceStatus_GetDisplayMesage_105()
        {
            // https://msdn.microsoft.com/en-us/library/ms912047(v=winembedded.10).aspx contains a list of Locale ID

            string tstEnglish = "To be able to use Mobile ID";
            string tstDefault = tstEnglish;
            string tstGerman = "diese Rufnummer noch nicht";
            string s;
            ServiceStatus svcStatus = new ServiceStatus(ServiceStatusCode.UNKNOWN_CLIENT);
            Assert.AreEqual("{Code:105, Reason:UNKNOWN_CLIENT, Color:Yellow}", svcStatus.ToString(), "dto(105)");

            s = svcStatus.GetDisplayMessage( new System.Globalization.CultureInfo("en").LCID );
            // Console.WriteLine(s);
            Assert.IsTrue(s.StartsWith(tstEnglish), "GetDisplayMessage(...) for existing locale 'en'");

            s = svcStatus.GetDisplayMessage(1033);
            Assert.IsTrue(s.StartsWith(tstEnglish), "GetDisplayMessage(...) for existing locale 1033 'en-US'");

            s = svcStatus.GetDisplayMessage(new System.Globalization.CultureInfo("de").LCID);
            // Console.WriteLine(s);
            Assert.IsTrue(s.IndexOf(tstGerman) > 0, "GetDisplayMessage(...) for existing locale 'de'");

            s = svcStatus.GetDisplayMessage(1031);
            Assert.IsTrue(s.IndexOf(tstGerman) > 0, "GetDisplayMessage(...) for existing locale 'de-DE'");

            s = svcStatus.GetDisplayMessage(2055);
            Assert.IsTrue(s.IndexOf(tstGerman) > 0, "GetDisplayMessage(...) for existing locale 'de-CH'");

            s = svcStatus.GetDisplayMessage(new System.Globalization.CultureInfo("ar").LCID); // arabic
            Assert.IsTrue(s.StartsWith(tstDefault), "GetDisplayMessage(...) default");

            s = svcStatus.GetDisplayMessage(1091);  // uzbek (latin), unsupported
            Assert.IsTrue(s.StartsWith(tstDefault), "GetDisplayMessage(...) default");

            s = svcStatus.GetDisplayMessage(0); // CultureNotFoundException
            Assert.IsTrue(s.StartsWith(tstDefault), "GetDisplayMessage(...) default");

            s = svcStatus.GetDisplayMessage(-1); // invalid LCID, ArgumentOutOfRangeException
            Assert.IsTrue(s.StartsWith(tstDefault), "GetDisplayMessage(...) default");
        }

        [TestMethod]
        public void T04_AuthRspServiceStatus_GetDefaultDisplayMesage()
        {
            string s;
            s = ServiceStatus.GetDefaultErrorMessage(new System.Globalization.CultureInfo("en").LCID);
            Assert.IsTrue(s.IndexOf("Mobile ID can not be used") >= 0, "en");

            s = ServiceStatus.GetDefaultErrorMessage(new System.Globalization.CultureInfo("fr").LCID);
            Assert.IsTrue(s.IndexOf("Mobile ID ne peut actuellement pas ") >= 0, "fr");

            s = ServiceStatus.GetDefaultErrorMessage(new System.Globalization.CultureInfo("de").LCID);
            Assert.IsTrue(s.IndexOf("Mobile ID kann zurzeit nicht genutzt werden") >= 0, "de");

            s = ServiceStatus.GetDefaultErrorMessage(new System.Globalization.CultureInfo("it").LCID);
            Assert.IsTrue(s.IndexOf("Mobile ID attualmente non può essere utilizzato") >= 0, "it");

        }
        
        
    }
}
