using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MobileId;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;

namespace ServiceTest
{
    [TestClass]
    public class UnitTest3
    {
        // copied from WebClientImpl._parse500Response(RspStatusAndBody,bool)
        AuthResponseDto _parse500Response(string rspBody, bool asynchronous)
        {
            string sCode, sReason, sDetail;
            string sPortalUrl = null;

            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(new StringReader(rspBody));

            XmlNamespaceManager manager = new XmlNamespaceManager(doc.NameTable);
            manager.AddNamespace("soapenv", "http://www.w3.org/2003/05/soap-envelope");
            manager.AddNamespace("mss", "http://uri.etsi.org/TS102204/v1.1.2#");
            manager.AddNamespace("ki", "http://kiuru.methics.fi/mssp");
            manager.AddNamespace("sc1", "http://www.swisscom.ch/TS102204/ext/v1.0.0");
            string cursor = null;
            try
            {
                cursor = "Subcode";
                sCode = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/soapenv:Fault/soapenv:Code/soapenv:Subcode/soapenv:Value", manager).InnerText;
                cursor = "Reason";
                sReason = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/soapenv:Fault/soapenv:Reason/soapenv:Text", manager).InnerText;
                cursor = "Detail";
                sDetail = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/soapenv:Fault/soapenv:Detail", manager).InnerText;
                //sDetail = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/soapenv:Fault/soapenv:Detail/ki:detail", manager).InnerText;
                try
                {
                    cursor = "UserAssistance";
                    sPortalUrl = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/soapenv:Fault/soapenv:Detail/sc1:UserAssistance/sc1:PortalUrl", manager).InnerText;
                }
                catch (NullReferenceException) { } // sPortalUrl = null;
            }
            catch (NullReferenceException)
            {
                return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "Missing <" + cursor + "> in SOAP Fault");
            }
            catch (Exception ex)
            {
                return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "Soap Fault parsing error (cursor=<" + cursor + ">): " + ex.Message);
            }

            // SOAP Fault Subcode: check the allowed values
            ServiceStatusCode rc;
            Match match = new Regex(@"^mss:_(\d\d\d)$").Match(sCode);
            if (match.Success)
            {
                try
                {
                    rc = (ServiceStatusCode)Enum.Parse(typeof(ServiceStatusCode), match.Groups[1].Value);
                }
                catch (OverflowException)
                {
                    string s = "code='" + sCode + "', reason='" + sReason + "', detail='" + sDetail + "'";
                    return new AuthResponseDto(ServiceStatusCode.UnsupportedStatusCode, s);
                };
                if (!asynchronous)
                {
                    if (/* rc == ServiceStatusCode.EXPIRED_TRANSACTION || */ rc == ServiceStatusCode.OUSTANDING_TRANSACTION || rc == ServiceStatusCode.REQUEST_OK)
                    {
                        return new AuthResponseDto(ServiceStatusCode.IllegalStatusCode, rc.ToString());
                    };
                }
            }
            else
            {
                string s = "code='" + sCode + "', reason='" + sReason + "', detail='" + sDetail + "', portalUrl='" + sPortalUrl + "'";
                return new AuthResponseDto(ServiceStatusCode.UnsupportedStatusCode, s);
            };

            AuthResponseDto rspDto = new AuthResponseDto(rc, sDetail);
            if (sPortalUrl != null)
              rspDto.Extensions[AuthResponseExtension.UserAssistencePortalUrl] = sPortalUrl;
            return rspDto;

        }

        [TestMethod]
        public void T20_AuthRspDto_PortalUrl()
        {
           WebClientImpl client = new WebClientImpl(new WebClientConfig());

           string s;
           AuthResponseDto rspDto;
           s = File.ReadAllText("sign_404_nourl.rsp.xml");
           rspDto = _parse500Response(s, false);
           Assert.IsNotNull(rspDto);
           Assert.AreEqual(ServiceStatusCode.NO_KEY_FOUND, rspDto.Status.Code, "sync, code == 404");
           Assert.IsFalse(rspDto.Extensions.ContainsKey(AuthResponseExtension.UserAssistencePortalUrl), "sync, no user assistence url");

           rspDto = _parse500Response(s, true);
           Assert.IsNotNull(rspDto);
           Assert.AreEqual(ServiceStatusCode.NO_KEY_FOUND, rspDto.Status.Code, "async, code == 404");
           Assert.IsFalse(rspDto.Extensions.ContainsKey(AuthResponseExtension.UserAssistencePortalUrl), "sync, no user assistence url");

           s = File.ReadAllText("sign_404_url.rsp.xml");
           rspDto = _parse500Response(s, false);
           Assert.IsNotNull(rspDto);
           Assert.AreEqual(ServiceStatusCode.NO_KEY_FOUND, rspDto.Status.Code, "sync+url, code == 404");
           Assert.AreEqual("http://mobileid.ch?msisdn=41000092404", rspDto.Extensions[AuthResponseExtension.UserAssistencePortalUrl], "sync+url, user assistence url");

           rspDto = _parse500Response(s, true);
           Assert.IsNotNull(rspDto);
           Assert.AreEqual(ServiceStatusCode.NO_KEY_FOUND, rspDto.Status.Code, "async+url, code == 404");
           Assert.AreEqual("http://mobileid.ch?msisdn=41000092404", rspDto.Extensions[AuthResponseExtension.UserAssistencePortalUrl], "sync+url, user assistence url");

        }
    }
}
