using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MobileId
{
    /// <summary>
    /// Implement the <paramref name="IAuthentication" by posting a SOAP request via WebClient, and parse the XML response by hand /> 
    /// </summary>
    public class WebClientImpl : IAuthentication
    {
        static TraceSource logger = new TraceSource("MobileId.WebClient");

        WebClientConfig _cfg;
        X509Certificate2 sslClientCert;
        X509Certificate2 sslCACert;

        public int GetClientVersion()
        {
            return 2;
        }

        public WebClientImpl(WebClientConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException("WebClientConfig");
            _cfg = cfg;
            sslClientCert = null;
            sslCACert = null;
        }

        protected string _formatSignReqAsSoap(AuthRequestDto req, bool async)
        {
            return string.Format(
#if DEBUG
#region MSS_Signature SOAP Request Template
@"<?xml version=""1.0"" encoding=""UTF-8""?>
  <soapenv:Envelope
    xmlns:soapenv=""http://www.w3.org/2003/05/soap-envelope"" 
    xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
    xmlns:mss=""http://uri.etsi.org/TS102204/v1.1.2#"" 
    xmlns:fi=""http://mss.ficom.fi/TS102204/v1.0.0#""
    soap:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <soapenv:Body>
      <MSS_Signature>
        <mss:MSS_SignatureReq MajorVersion=""1"" MinorVersion=""1"" MessagingMode=""{9}"" TimeOut=""{0}"">
          <mss:AP_Info AP_ID=""{1}"" AP_PWD="""" AP_TransID=""{2}"" Instant=""{3}""/>
          <mss:MSSP_Info><mss:MSSP_ID><mss:URI>http://mid.swisscom.ch/</mss:URI></mss:MSSP_ID></mss:MSSP_Info>
          <mss:MobileUser><mss:MSISDN>{4}</mss:MSISDN></mss:MobileUser>
          <mss:DataToBeSigned MimeType=""text/plain"" Encoding=""UTF-8"">{5}</mss:DataToBeSigned>
          <mss:SignatureProfile><mss:mssURI>http://mid.swisscom.ch/MID/v1/AuthProfile1</mss:mssURI></mss:SignatureProfile>
          <mss:AdditionalServices>
            {6}
            <mss:Service><mss:Description><mss:mssURI>http://mss.ficom.fi/TS102204/v1.0.0#userLang</mss:mssURI></mss:Description><fi:UserLang>{7:G}</fi:UserLang></mss:Service>
            {8}
          </mss:AdditionalServices>
        </mss:MSS_SignatureReq>
      </MSS_Signature>
    </soapenv:Body>
  </soapenv:Envelope>"
#endregion
#else
            #region MSS_Signature SOAP Request Template
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope
soap:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"" 
xmlns:soapenv=""http://www.w3.org/2003/05/soap-envelope"" 
xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
xmlns:mss=""http://uri.etsi.org/TS102204/v1.1.2#"" 
xmlns:fi=""http://mss.ficom.fi/TS102204/v1.0.0#"">
<soapenv:Body>
<MSS_Signature>
<mss:MSS_SignatureReq MajorVersion=""1"" MinorVersion=""1"" MessagingMode=""{9}"" TimeOut=""{0}"">
<mss:AP_Info AP_ID=""{1}"" AP_PWD="""" AP_TransID=""{2}"" Instant=""{3}""/>
<mss:MSSP_Info><mss:MSSP_ID><mss:URI>http://mid.swisscom.ch/</mss:URI></mss:MSSP_ID></mss:MSSP_Info>
<mss:MobileUser><mss:MSISDN>{4}</mss:MSISDN></mss:MobileUser>
<mss:DataToBeSigned MimeType=""text/plain"" Encoding=""UTF-8"">{5}</mss:DataToBeSigned>
<mss:SignatureProfile><mss:mssURI>http://mid.swisscom.ch/MID/v1/AuthProfile1</mss:mssURI></mss:SignatureProfile><mss:AdditionalServices>
{6}<mss:Service><mss:Description><mss:mssURI>http://mss.ficom.fi/TS102204/v1.0.0#userLang</mss:mssURI></mss:Description><fi:UserLang>{7:G}</fi:UserLang></mss:Service>
{8}</mss:AdditionalServices></mss:MSS_SignatureReq></MSS_Signature></soapenv:Body></soapenv:Envelope>"
            #endregion
#endif
, (req.TimeOut > 0 ? req.TimeOut : _cfg.RequestTimeOutSeconds)
                , (req.ApId != null ? req.ApId : _cfg.ApId)
                , (req.TransId != null ? req.TransId : (req.TransId =  "X" + Util.Build64bitRandomHex(_cfg.SeedApTransId)))
                , (req.Instant != null ? req.Instant : Util.CurrentTimeStampString())
                , req.PhoneNumber
                , req.DataToBeSigned
                , (req.SrvSideValidation ? @"<Service><Description><mssURI>http://uri.etsi.org/TS102204/v1.1.2#validate<mssURI></Description></Service>" : "")
                , req.UserLanguage
                , (_cfg.EnableSubscriberInfo ? @"<mss:Service><mss:Description><mss:mssURI>http://mid.swisscom.ch/as#subscriberInfo</mss:mssURI></mss:Description></mss:Service>" : "")
                , (async ? "asynchClientServer" : "synch")
            );
        }

        private X509Certificate2 _retrieveCert(StoreLocation storeLocation, StoreName storeName, X509FindType x509FindType, string findValue)
        {
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(storeName, storeLocation);
                certStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                X509Certificate2Collection certs = certStore.Certificates.Find(x509FindType, findValue, true /* only valid cert */);
                certStore.Close();
                certStore = null;
                if (certs.Count == 0)
                {
                    // TODO: retrieve cert again without the "OnlyValidCert" flag, in order to differentiate 
                    // between NotFound and InValid errors and provide better diagnostic messages
                    logger.TraceEvent(TraceEventType.Verbose, (int)EventId.KeyManagement,
                        "Cert Not Found or Invalid: storeLocation={0}, storeName={1}, fileType='{2}', findValue='{3}'",
                        storeLocation, storeName, x509FindType, findValue);
                    Logging.Log.KeyManagementCertNotFound(storeLocation, storeName, x509FindType, findValue);
                    return null;
                }
                else if (certs.Count > 1)
                {
                    logger.TraceEvent(TraceEventType.Verbose, (int)EventId.KeyManagement,
                        "Multiple valid certs found, the 0-th one is used: storeLocation={0}, storeName={1}, fileType='{2}', findValue='{3}'",
                        storeLocation, storeName, x509FindType, findValue);
                    Logging.Log.KeyManagementMultiCertFound(storeLocation, storeName, x509FindType, findValue);
                    return certs[0];
                }
                else
                {
                    logger.TraceEvent(TraceEventType.Verbose, (int)EventId.KeyManagement,
                        "Found Cert:  storeLocation={0}, storeName={1}, fileType='{2}', findValue='{3}'",
                        storeLocation, storeName, x509FindType, findValue);
                    Logging.Log.KeyManagementCertFound(storeLocation, storeName, x509FindType, findValue);
                    return certs[0];
                };
            }
            catch (Exception ex)
            {
                logger.TraceData(TraceEventType.Error, (int)EventId.KeyManagement, ex);
                logger.TraceEvent(TraceEventType.Error, (int)EventId.KeyManagement,
                    "Technical error in retrieving cert: storeLocation={0}, storeName={1}, findType={2}, findValue={3}",
                    storeLocation, storeName, x509FindType, findValue);
                Logging.Log.KeyManagementStoreError(storeLocation, storeName, x509FindType, findValue, ex.Message);
                return null;
            }
            finally
            {
                if (certStore != null)
                    certStore.Close();
            }
        }

        private AuthResponseDto _parse500Response(RspStatusAndBody rsp, bool asynchronous)
        {
            if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage("_parse500Response");

            #region Sample SOAP Fault response
            /*  Sample output:
            <?xml version="1.0" encoding="utf-8"?>
            <soapenv:Envelope xmlns:soapenv="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <soapenv:Body>
                <soapenv:Fault>
                  <soapenv:Code>
                    <soapenv:Value>soapenv:Sender</soapenv:Value>
                    <soapenv:Subcode xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#">
                      <soapenv:Value>mss:_105</soapenv:Value>
                    </soapenv:Subcode>
                  </soapenv:Code>
                  <soapenv:Reason>
                    <soapenv:Text xml:lang="en">UNKNOWN_CLIENT</soapenv:Text>
                  </soapenv:Reason>
                  <soapenv:Detail>
                    <ns1:detail xmlns:ns1="http://kiuru.methics.fi/mssp">Unknown or inactive user</ns1:detail>
                    <ns2:UserAssistance xmlns:ns2="http://www.swisscom.ch/TS102204/ext/v1.0.0">
                      <PortalUrl xmlns="http://www.swisscom.ch/TS102204/ext/v1.0.0">http://mobileid.ch?msisdn=41762108228</PortalUrl>
                    </ns2:UserAssistance>
                  </soapenv:Detail>
                </soapenv:Fault>
              </soapenv:Body>
            </soapenv:Envelope>
            */
            #endregion
            string rspBody = rsp.Body;
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
                try {
                    cursor = "UserAssistance";
                    sPortalUrl = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/soapenv:Fault/soapenv:Detail/sc1:UserAssistance/sc1:PortalUrl", manager).InnerText;
                } 
                catch (NullReferenceException) {} // i.e. sPortalUrl = null;
            }
            catch (NullReferenceException)
            {
                logger.TraceEvent(TraceEventType.Error, (int)EventId.TransportSoap,
                    "ParseError: err=NoFaultCode, httpStatusCode='{0}', rspBody={1}", rsp.StatusCode, rspBody);
                Logging.Log.ServerResponseFaultCodeUnknown((int)rsp.StatusCode, Logging.Shorten(rspBody));
                return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "Missing <" + cursor + "> in SOAP Fault");
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, (int)EventId.TransportSoap,
                    "ParseError: err=NoFaultCode, rspStatus='{0}', rspBody={1}", rsp.StatusCode, rspBody);
                Logging.Log.ServerResponseFormatUnknown((int)rsp.StatusCode, cursor, Logging.Shorten(rspBody), ex.Message);
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
                    logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service, "Unknown Soap Fault Code: " + s);
                    Logging.Log.ServerResponseStatusCodeOverflow(sCode, sReason, sDetail);
                    return new AuthResponseDto(ServiceStatusCode.UnsupportedStatusCode, s);
                };
                if (! asynchronous) {
                    if (/* rc == ServiceStatusCode.EXPIRED_TRANSACTION || */ rc == ServiceStatusCode.OUSTANDING_TRANSACTION || rc == ServiceStatusCode.REQUEST_OK)
                    {
                        logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Illegal Status Code: " + rc);
                        Logging.Log.ServerResponseStatusCodeIllegal(((int)rc).ToString(),
                            ((int)ServiceStatusCode.OUSTANDING_TRANSACTION).ToString() + '|' + ((int)ServiceStatusCode.REQUEST_OK).ToString(), Logging.Shorten(rspBody));
                        return new AuthResponseDto(ServiceStatusCode.IllegalStatusCode, rc.ToString());
                    };
                }
            }
            else
            {
                string s = "code='" + sCode + "', reason='" + sReason + "', detail='" + sDetail + "', portalUrl='" + sPortalUrl + "'";
                logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service, "Illformed Fault Code: {0}\nResponse Body:\n", s, rspBody);
                Logging.Log.ServerResponseStatusCodeUnsupported(sCode, sReason, sDetail, Logging.Shorten(rspBody));
                return new AuthResponseDto(ServiceStatusCode.UnsupportedStatusCode, s);
            };

            // SOAP Fault Reason: check for consistency (server's sReason should match the registered ServiceStatus.Message)
            if (sReason != rc.ToString()) {
                logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service,
                    "SOAP Fault Reason ({0}) does not match registered Reason ({1})", sReason, rc.ToString());
                Logging.Log.ServerResponseStatusTextChanged((int)rc, rc.ToString(), sReason);
            }

            AuthResponseDto rspDto = new AuthResponseDto(rc, sDetail);
            if (sPortalUrl != null)
                rspDto.Extensions[AuthResponseExtension.UserAssistencePortalUrl] = sPortalUrl;
            return rspDto;
        }

        private AuthResponseDto _parseSignSync200Response(string httpRspBody, AuthRequestDto inDto)
        {
            if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage2("_parseSignSync200Response", inDto.TransId);

            #region Sample Response
            /* Sample success response
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            <soapenv:Body>
                <MSS_SignatureResponse xmlns="">
                    <mss:MSS_SignatureResp xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#" MajorVersion="1" MinorVersion="1" MSSP_TransID="hb9j7">
                    <mss:AP_Info AP_ID="mid://xyz.org" AP_TransID="X87D1B5A0AF2597965446211FE437B36B" AP_PWD="" Instant="2015-02-26T09:40:16.257+01:00"/>
                    <mss:MSSP_Info Instant="2015-02-26T09:40:54.597+01:00">
                        <mss:MSSP_ID>
                            <mss:URI>http://mid.swisscom.ch/</mss:URI>
                        </mss:MSSP_ID>
                    </mss:MSSP_Info>
                    <mss:MobileUser>
                        <mss:MSISDN>+4179123456</mss:MSISDN>
                    </mss:MobileUser>
                    <mss:MSS_Signature>
                        <mss:Base64Signature>MIIHqwYJKoZIhvcNAQcCoIIHnDCCB5gCAQExCzAJBgUrDgMCGgUAMBoGCSqGSIb3DQEHAaANBAtoZWxsbyB3b3JsZKCCBV8wggVbMIIEQ6ADAgECAhEApTBhESHOqF2sOXFhcq7rhjANBgkqhkiG9w0BAQsFADBqMQswCQYDVQQGEwJjaDERMA8GA1UEChMIU3dpc3Njb20xJTAjBgNVBAsTHERpZ2l0YWwgQ2VydGlmaWNhdGUgU2VydmljZXMxITAfBgNVBAMTGFN3aXNzY29tIFRFU1QgUnViaW4gQ0EgMjAeFw0xNDA5MzAwNzIzMTJaFw0xNzA5MzAwNzIzMTJaMDkxGTAXBgNVBAUTEE1JRENIRTI4UkNMUlgwNzcxHDAaBgNVBAMTE01JRENIRTI4UkNMUlgwNzc6UE4wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCcxWi95x5KWwmXFDGbjHMuwXtwrEkRyUs7BsxFNMgdlGFze1uiWfcLDGxvA7R52sDSBnyRjvZXq8mYieAR47a7sYWbuGVXOovD65/CFshn9Wr0OTUmlrLdFfLLvam05cNbdQQRefJQcZelmuYrFmo9YSLQ8dWf5o7f5w2FZwJP0K09sgdvuNKM6QtjjyITjKak4UB1eExjp3iLfMU7QCfAIC7QxlbRPTbGtPv7e+J7HK7jZCxaVqABzh+6o1Tvvj5Cn9xxSawsm/1wJO9W9DdKkSY6dJmH/RYBGiP9natICTs25qZMyYoP/D62EVEHuaTCofiMdHd9vR+HaGspO18ZAgMBAAGjggIrMIICJzCBhQYIKwYBBQUHAQEEeTB3MDgGCCsGAQUFBzABhixodHRwOi8vb2NzcC5wcmUuc3dpc3NkaWdpY2VydC5jaC9zZGNzLXJ1YmluMjA7BggrBgEFBQcwAoYvaHR0cDovL2FpYS5wcmUuc3dpc3NkaWdpY2VydC5jaC9zZGNzLXJ1YmluMi5jcnQwHwYDVR0jBBgwFoAU4kASzKwcX0bPNRWWqO0lczITHA4wZwYDVR0gBGAwXjBcBgZghXQBUwcwUjAwBggrBgEFBQcCARYkaHR0cDovL3d3dy5wcmUuc3dpc3NkaWdpY2VydC5jaC9jcHMvMB4GCCsGAQUFBwICMBIaEFN3aXNzY29tIFRFU1QgQ0Ewgc4GA1UdHwSBxjCBwzA1oDOgMYYvaHR0cDovL2NybC5wcmUuc3dpc3NkaWdpY2VydC5jaC9zZGNzLXJ1YmluMi5jcmwwgYmggYaggYOGgYBsZGFwOi8vbGRhcC5wcmUuc3dpc3NkaWdpY2VydC5jaC9DTj1Td2lzc2NvbSUyMFRFU1QlMjBSdWJpbiUyMENBJTIwMixkYz1ydWJpbjIsZGM9c3dpc3NkaWdpY2VydCxkYz1jaD9jZXJ0aWZpY2F0ZVJldm9jYXRpb25MaXN0PzATBgNVHSUEDDAKBggrBgEFBQcDAjAOBgNVHQ8BAf8EBAMCBaAwHQYDVR0OBBYEFKIjPqg7rayt19ADRI2fnKc8OogBMA0GCSqGSIb3DQEBCwUAA4IBAQBlW5241Yuy3fIjCZSR3u3uwnhQI57wYLbElYaNtn4wPGGyMhH/ECDRa9/CcFGfP7B7NmyuezP87XCeCEq/qybknaqJtz/7HjMzguJQ8514dGOz4FWPZBs1CJnsSfZvd7B13R5JEB1S0uDuQKgjIPKOklR/PStMKGFTdkEf/OZ/ZCpokVIDv9nAcd/B+dQ580vBV43WFG9FNIVBulN1miyTnvcqej2XFgQEfpTlKMqm8ww8iPrf98bFopD4xa7b/9+xP0Eu798A4yHNbB+UHy3qDkKtopDYYr/JoQtnXprv3nW3kftxsEqw5nc9dpsTthveedoHKydj84pT4zBpBm7XMYICBTCCAgECAQEwfzBqMQswCQYDVQQGEwJjaDERMA8GA1UEChMIU3dpc3Njb20xJTAjBgNVBAsTHERpZ2l0YWwgQ2VydGlmaWNhdGUgU2VydmljZXMxITAfBgNVBAMTGFN3aXNzY29tIFRFU1QgUnViaW4gQ0EgMgIRAKUwYREhzqhdrDlxYXKu64YwCQYFKw4DAhoFAKBdMBgGCSqGSIb3DQEJAzELBgkqhkiG9w0BBwEwHAYJKoZIhvcNAQkFMQ8XDTE1MDIyNjA4NDAxN1owIwYJKoZIhvcNAQkEMRYEFCqubDXJT8+0FdvpX0CLnOke6EbtMA0GCSqGSIb3DQEBAQUABIIBAGYiIjQZG9lQQhfHPHh3xf+ZVQxiTDbOl8JzYy7HWqoYP49rhJ2rsuNX5nD2LmD6oxq3fTC2YtQpDrMhOnWQgHeNqJIGxfnBauXaPSh5lpGR/JjrXtBlxDYrpyP9DHLm57JO7ZhBfMN29Ls1slZ8hLjc9wJuGDCUyLJPB3W+KepWKIXNmG0om+30p0RVADxUdgtx91jf1xakmO/GQrzfJMqSvve8Nf6EO/7+y8vYpJX6wgysg9g3AqcaLkfZyZxhkZxCf+qKwqHGgMO+m6jZ8/EV5GHaa8TY2XB/bLXIH0Fnb7CAEO/xHYnl6vR27igrBIbaI10o771ETE0qJXuDoJU=</mss:Base64Signature>
                    </mss:MSS_Signature>
                    <mss:SignatureProfile>
                        <mss:mssURI>http://mid.swisscom.ch/MID/v1/AuthProfile1</mss:mssURI>
                    </mss:SignatureProfile>
                    <mss:Status>
                        <mss:StatusCode Value="500"/>
                        <mss:StatusMessage>SIGNATURE</mss:StatusMessage>
                        <mss:StatusDetail>
                            <fi:ServiceResponses>
                                <fi:ServiceResponse>
                                    <fi:Description>
                                        <mss:mssURI>http://mid.swisscom.ch/as#subscriberInfo</mss:mssURI>
                                    </fi:Description>
                                    <ns1:SubscriberInfo xmlns:ns1="http://mid.swisscom.ch/TS102204/as/v1.0">
                                    <ns1:Detail id="1901" value="22801"/>
                                </ns1:SubscriberInfo>
                            </fi:ServiceResponse>
                        </fi:ServiceResponses>
                    </mss:StatusDetail>
                </mss:Status>
            </mss:MSS_SignatureResp>
            </MSS_SignatureResponse>
            </soapenv:Body>
            </soapenv:Envelope>
            */
            #endregion

            AuthResponseDto rspDto = null;
            String s, cursor;

            // Load response in xml document
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(new StringReader(httpRspBody));

            // Namespace manager
            XmlNamespaceManager manager = new XmlNamespaceManager(doc.NameTable);
            manager.AddNamespace("soapenv", "http://www.w3.org/2003/05/soap-envelope");
            manager.AddNamespace("mss", "http://uri.etsi.org/TS102204/v1.1.2#");

            cursor = "unknown";
            try
            {
                // extract AP_TransID from XML
                XmlNodeList nodeList = doc.SelectNodes("//@AP_TransID");
                s = null;
                foreach (XmlNode node in nodeList)
                    s = node.Value;
                if (s != inDto.TransId)
                {
                    string errMsg = String.Format("Mismatched AP_TransId: req.AP_TransId='{0}', rsp.AP_TransId='{1}'", inDto.TransId,s);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Hacking, "{0}, rsp={1}", errMsg, httpRspBody);
                    Logging.Log.ServerResponseApTrxIdMismatch(inDto.TransId, s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.MismatchedApTransId,errMsg);
                }

                // MSISDN
                cursor = "MSISDN";
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:MobileUser/mss:MSISDN", manager).InnerText;
                if (s != inDto.PhoneNumber)
                {
                    string errMsg = String.Format("Mismatched MSISDN: req.PhoneNumber='{0}', rsp.PhoneNumber='{1}'",  inDto.PhoneNumber, s);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Hacking, "{0}, rsp={1}", errMsg, s);
                    Logging.Log.ServerResponseMsisdnMismatch(inDto.TransId, inDto.PhoneNumber, s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.MismatchedMsisdn,errMsg);
                }

                // Code
                cursor = "StatusCode";
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:Status/mss:StatusCode", manager).Attributes["Value"].Value;
                if (s != "500")
                {
                    string errMsg = String.Format("error=IllegalStatusCode, expect=500, seen={0}, response={1}", s, httpRspBody);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, errMsg);
                    Logging.Log.ServerResponseCodeMismatch(s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.IllegalStatusCode, errMsg);
                }

                // Message
                cursor = "Message";
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:Status/mss:StatusMessage", manager).InnerText;
                if (s != "SIGNATURE") {
                    logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service, "Service has changed StatusMessage of code 500 to {0} (expected: 'SIGNATURE')", s);
                    Logging.Log.ServerResponseStatusTextChanged(500, "SIGNATURE", s);
                }

                // Signature
                cursor = "Base64Signature";
                byte[] dtbs_signature;
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:MSS_Signature/mss:Base64Signature", manager).InnerText;
                if (String.IsNullOrEmpty(s))
                {
                    string errMsg = String.Format("error=EmptySignature, response={0}", httpRspBody);
                    Logging.Log.ServerResponseEmptySignature(Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.UnknownResponse,errMsg);
                }
                else
                {
                    dtbs_signature = Convert.FromBase64String(s);
                    if (! _isValidSignature(inDto.DataToBeSigned, dtbs_signature))
                    {
                        logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Response Signature is invalid");
                        Logging.Log.ServerResponseInvalidSignature(inDto.TransId, inDto.PhoneNumber, Logging.Shorten(httpRspBody));
                        return new AuthResponseDto(ServiceStatusCode.InvalidResponseSignature);
                    }
                }

                // MSSP_TransID
                cursor = "MSS_SignatureResp@MSSP_TransID";
                string msspTransid = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp", manager).Attributes["MSSP_TransID"].Value;
                if (string.IsNullOrEmpty(msspTransid))
                {
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Service response has no MSS_TransID");
                    Logging.Log.ServerResponseEmptyMssTrxId(inDto.TransId, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "MSS_TransID is missing");
                };

                // At this point, the request is considered correct (subjected serial number check)

                rspDto = new AuthResponseDto(ServiceStatusCode.SIGNATURE);
                rspDto.MsspTransId = msspTransid;
                rspDto.Signature = dtbs_signature;

                if (!_verifyUserSerialNumber(inDto, rspDto)) 
                {
                    logger.TraceEvent(TraceEventType.Error, 0, rspDto.ToString());
                    Logging.Log.UserSerialNumberNotAccepted(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, rspDto.UserSerialNumber);
                    return rspDto; // rspDto is modified in this case
                }
            }
            catch (NullReferenceException ex)
            {
                logger.TraceData(TraceEventType.Error, (int)EventId.Service, ex);
                Logging.Log.ServerResponseMissingElement(cursor, Logging.Shorten(httpRspBody));
                return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "Missing element " + cursor);
            }

            _enrichAuthRspDto(rspDto);
            return rspDto;

        }

        private bool _isUserCertTimeValid(AuthRequestDto inDto, AuthResponseDto outDto)
        {
            X509Certificate2 userCert = outDto.UserCertificate;
            if (userCert == null)
            {
                Logging.Log.MssRequestSignatureWarning((int)ServiceStatusCode.UserCertAbsent, inDto.TransId, outDto.MsspTransId,
                    inDto.PhoneNumber, inDto.PhoneNumber, outDto.ToString());
                outDto.Status = new ServiceStatus(ServiceStatusCode.UserCertAbsent);
                return false;
            };
            DateTime now = DateTime.Now;
            if (now < userCert.NotBefore)
            {
                Logging.Log.MssRequestSignatureWarning((int)ServiceStatusCode.UserCertNotYetValid, inDto.TransId, outDto.MsspTransId,
                    inDto.PhoneNumber, inDto.UserSerialNumber, Convert.ToBase64String(userCert.RawData));
                outDto.Status = new ServiceStatus(ServiceStatusCode.UserCertNotYetValid);
                return false;
            }
            else if (now > userCert.NotAfter)
            {
                Logging.Log.MssRequestSignatureWarning((int)ServiceStatusCode.UserCertExpired, inDto.TransId, outDto.MsspTransId,
                    inDto.PhoneNumber, inDto.UserSerialNumber, Convert.ToBase64String(userCert.RawData));
                outDto.Status = new ServiceStatus(ServiceStatusCode.UserCertExpired);
                return false;
            }
            return true;
        }

        // verify serial number, return true on success, false on failure. In case of fasle, outDto is assigned with a new appropriate AuthResponseDto.
        private bool _verifyUserSerialNumber(AuthRequestDto inDto, AuthResponseDto outDto)
        {
            if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage2("_verifyUserSerialNumber", inDto.TransId);

            if (string.IsNullOrEmpty(outDto.UserSerialNumber)) {
                Logging.Log.UserSerialNumberNotAccepted(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, outDto.UserSerialNumber);
                return false;
            }

            if (_cfg.DisableSignatureValidation || _cfg.DisableSignatureCertValidation) { // verify the time-validity of user cert, if not yet done
                if (!_isUserCertTimeValid(inDto, outDto)) {
                    return false;
                }
            }

            UserSerialNumberPolicy policy = _cfg.UserSerialNumberPolicy; // in future, we allow a requester to specify the desired policy and defaults to configured policy

            if (outDto.UserSerialNumber == inDto.UserSerialNumber) {
                return true;
            }

            if (string.IsNullOrWhiteSpace(inDto.UserSerialNumber)) {
                logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "User has empty Serial Number in attribute store");
                if (policy.HasFlag(UserSerialNumberPolicy.allowAbsence))
                {
                    if (policy.HasFlag(UserSerialNumberPolicy.warnMismatch)) Logging.Log.UserSerialNumberNotInStore(inDto.TransId, inDto.PhoneNumber, outDto.UserSerialNumber);
                    return true;
                } else {
                    Logging.Log.UserSerialNumberNotAccepted(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, outDto.UserSerialNumber);
                    outDto.Status = new ServiceStatus(ServiceStatusCode.UserSerialNumberNotRegistered);
                    return false;
                }
            }

            logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "User's Serial Numbers mismatch: mobile=" + inDto.PhoneNumber + ", response='" + outDto.UserSerialNumber + "', store='" + inDto.UserSerialNumber + "'");
            if (policy.HasFlag(UserSerialNumberPolicy.allowMismatch))
            {
                if (policy.HasFlag(UserSerialNumberPolicy.warnMismatch)) Logging.Log.UserSerialNumberMismatch(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, outDto.UserSerialNumber);
                return true;
            } else {
                Logging.Log.UserSerialNumberMismatch(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, outDto.UserSerialNumber);
                outDto.Status = new ServiceStatus(ServiceStatusCode.UserSerialNumberMismatch);
                return false;
            }
        }

        private AuthResponseDto _parseSignAsync200Response(string httpRspBody, AuthRequestDto inDto)
        {
            if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage2("_parseSigAsync200Response",inDto.TransId);
            #region Sample Response
            /* Sample success response
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            <soapenv:Body>
                <MSS_SignatureResponse xmlns="">
                    <mss:MSS_SignatureResp xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#" MajorVersion="1" MinorVersion="1" MSSP_TransID="hbafr">
                    <mss:AP_Info AP_ID="mid://xyz.org" AP_TransID="X87D1B5A0AF2597965446211FE437B36B" AP_PWD="" Instant="2015-02-26T13:37:52.919+01:00"/>
                    <mss:MSSP_Info Instant="2015-02-26T13:37:54.253+01:00">
                        <mss:MSSP_ID>
                            <mss:URI>http://mid.swisscom.ch/</mss:URI>
                        </mss:MSSP_ID>
                    </mss:MSSP_Info>
                    <mss:MobileUser>
                        <mss:MSISDN>+41791234567</mss:MSISDN>
                    </mss:MobileUser>
                    <mss:SignatureProfile>
                        <mss:mssURI>http://mid.swisscom.ch/MID/v1/AuthProfile1</mss:mssURI>
                    </mss:SignatureProfile>
                    <mss:Status>
                        <mss:StatusCode Value="100"/>
                        <mss:StatusMessage>REQUEST_OK</mss:StatusMessage>
                    </mss:Status>
                </mss:MSS_SignatureResp>
            </MSS_SignatureResponse>
            </soapenv:Body>
            </soapenv:Envelope>
            */
            #endregion

            AuthResponseDto rspDto = null;
            String s, cursor;

            // Load response in xml document
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(new StringReader(httpRspBody));

            // Namespace manager
            XmlNamespaceManager manager = new XmlNamespaceManager(doc.NameTable);
            manager.AddNamespace("soapenv", "http://www.w3.org/2003/05/soap-envelope");
            manager.AddNamespace("mss", "http://uri.etsi.org/TS102204/v1.1.2#");

            cursor = "unknown";
            try
            {
                // retrieve AP_TransID
                XmlNodeList nodeList = doc.SelectNodes("//@AP_TransID");
                s = null;
                foreach (XmlNode node in nodeList)
                    s = node.Value;
                if (s != inDto.TransId)
                {
                    string errMsg = String.Format("Mismatched AP_TransId: req.AP_TransId='{0}', rsp.AP_TransId='{1}'", inDto.TransId, s);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Hacking, "{0}, rsp={1}", errMsg, s);
                    Logging.Log.ServerResponseApTrxIdMismatch(inDto.TransId, s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.MismatchedApTransId, errMsg);
                }

                // MSISDN
                cursor = "MSISDN";
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:MobileUser/mss:MSISDN", manager).InnerText;
                if (s != inDto.PhoneNumber)
                {
                    string errMsg = String.Format("Mismatched MSISDN: req.PhoneNumber='{0}', rsp.PhoneNumber='{1}'", inDto.PhoneNumber, s);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Hacking, "{0}, rsp={1}", errMsg, s);
                    Logging.Log.ServerResponseMsisdnMismatch(inDto.TransId, inDto.PhoneNumber, s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.MismatchedMsisdn, errMsg);
                }

                // MSSP_TransID
                cursor = "MSSP_TransID";
                string msspTransid = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp", manager).Attributes["MSSP_TransID"].Value;
                if (string.IsNullOrEmpty(msspTransid))
                {
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Service response has no MSS_TransID");
                    Logging.Log.ServerResponseEmptyMssTrxId(inDto.TransId, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "MSS_TransID is missing");
                };

                // (StatusCode,StatusMessage) can be (500,SIGNATURE) or (100,REQUEST_OK)
                string statusCode, statusMessage;

                // StatusCode
                cursor = "StatusCode";
                statusCode = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:Status/mss:StatusCode", manager).Attributes["Value"].Value;

                // StatusMessage
                cursor = "StatusMessage";
                statusMessage = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:Status/mss:StatusMessage", manager).InnerText;

                if (statusCode == "100")
                {
                    if (statusMessage != "REQUEST_OK") {
                        logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service,
                            "StatusMessage of 100 changed to " + statusMessage + " (expected: REQUEST_OK)");
                        Logging.Log.ServerResponseStatusTextChanged(100, "REQUEST_OK", statusMessage);
                    }
                    rspDto = new AuthResponseDto(ServiceStatusCode.REQUEST_OK);
                    rspDto.MsspTransId = msspTransid;
                    return rspDto;
                }
                else if (statusCode != "500")
                {
                    string errMsg = String.Format("error=IllegalStatusCode, expect=100|500, seen={0}, response={1}", statusCode, httpRspBody);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, errMsg);
                    Logging.Log.ServerResponseStatusCodeIllegal(statusCode, "500", Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.IllegalStatusCode, errMsg);
                }

                // StatusMessage
                if (statusMessage != "SIGNATURE")
                {
                    logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service,
                        "Service has changed StatusMessage of code 500 to " + statusMessage + " (expected: 'SIGNATURE')");
                    Logging.Log.ServerResponseStatusTextChanged(500, "SIGNATURE", statusMessage);
                }

                // Signature
                cursor = "Base64Signature";
                byte[] dtbs_signature;
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/MSS_SignatureResponse/mss:MSS_SignatureResp/mss:MSS_Signature/mss:Base64Signature", manager).InnerText;
                if (String.IsNullOrEmpty(s))
                {
                    string errMsg = String.Format("error=EmptySignature, response={0}", httpRspBody);
                    return new AuthResponseDto(ServiceStatusCode.UnknownResponse, errMsg);
                }
                else
                {
                    dtbs_signature = Convert.FromBase64String(s);
                    if (!_isValidSignature(inDto.DataToBeSigned, dtbs_signature))
                    {
                        logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Response Signature is invalid");
                        Logging.Log.ServerResponseInvalidSignature(inDto.TransId, inDto.PhoneNumber, Logging.Shorten(httpRspBody));
                        return new AuthResponseDto(ServiceStatusCode.InvalidResponseSignature);
                    }
                }

                // At this point, the request is considered correct (subjected to serial number check)

                rspDto = new AuthResponseDto(ServiceStatusCode.SIGNATURE);
                rspDto.MsspTransId = msspTransid;
                rspDto.Signature = dtbs_signature;

                if (!_verifyUserSerialNumber(inDto, rspDto))
                {
                    logger.TraceEvent(TraceEventType.Verbose, 0, rspDto.ToString());
                    Logging.Log.UserSerialNumberNotAccepted(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, rspDto.UserSerialNumber);
                    return rspDto; // rspDto is modified in this case
                }

            }
            catch (NullReferenceException ex)
            {
                logger.TraceData(TraceEventType.Error, (int)EventId.Service, ex);
                Logging.Log.ServerResponseMissingElement(cursor, Logging.Shorten(httpRspBody));
                return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "Missing element " + cursor);
            }

            _enrichAuthRspDto(rspDto);
            return rspDto;

        }

        private void _initCerts()
        {
            // retrieve SSL client cert from certstore:///CurrentUser/My
            sslClientCert = null;
            sslClientCert = _retrieveCert(_cfg.SslKeystore, StoreName.My, X509FindType.FindByThumbprint, _cfg.SslCertThumbprint);
            if (sslClientCert == null)
                throw new Exception("No valid SSL client cert found");
            if (!sslClientCert.HasPrivateKey)
                throw new Exception("Found SSL client cert has no private key");
            else {
                logger.TraceEvent(TraceEventType.Verbose, (int)EventId.KeyManagement, "SSL client cert retrieved");
                Logging.Log.KeyManagementCertRetrieved("SSL client cert");
            }

            // retrieve CA cert from LocalMachine or CurrentUser
            sslCACert = null;
            foreach (StoreLocation sl in new StoreLocation[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
            {
                sslCACert = _retrieveCert(sl, StoreName.Root, X509FindType.FindBySubjectDistinguishedName, _cfg.SslRootCaCertDN);
                if (sslCACert != null) break;
            };
            if (sslCACert == null)
                throw new Exception("No valid SSL Server Root CA cert found");
            else {
                logger.TraceEvent(TraceEventType.Verbose, (int)EventId.KeyManagement, "SSL Server Root CA cert retrieved");
                Logging.Log.KeyManagementCertRetrieved("SSL root CA cert");
            }

        }

        /// <summary>
        /// encapsulate response body and response status
        /// </summary>
        class RspStatusAndBody
        {
            public string Body { get; set; } // contains error message in case of communication exception
            public bool ExceptionOccured { get; set; }
            public HttpStatusCode StatusCode { get; set; }

            public RspStatusAndBody(bool hasCommException, HttpStatusCode statusCode, string body)
            {
                this.ExceptionOccured = hasCommException;
                this.StatusCode = statusCode;
                this.Body = body;
            }
        }

        /// <summary>
        /// Establish a connection to MID server, 
        /// prepare HTTP request header,
        /// send the specified string as HTTP request body,
        /// read the resulting HTTP response status and body
        /// </summary>
        /// <param name="soapPortName">one of MSS_Signature, MSS_StatusQuery, MSS_Receipt, MSS_Profile</param>
        /// <param name="body">body in HTTP request</param>
        /// <returns>an object that contains HTTP response code, HTTP response body, whether network exception occured</returns>
        private RspStatusAndBody _sendRequest(string soapPortName, string body)
        {
            UTF8Encoding encoding = new UTF8Encoding();
            byte[] data = encoding.GetBytes(body);

            // retrieve certs if not yet done
            if (sslClientCert == null) try
            {
                _initCerts();
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, (int)EventId.KeyManagement, "Cannot load certificates. " + ex.Message);
                Logging.Log.KeyManagementCertException(ex.Message);
                throw ex;
            }

            // prepare connection
            ServicePointManager.SecurityProtocol = _cfg.SecurityProtocolType;
            HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(_cfg.ServiceUrlPrefix + soapPortName + "Port");
            // TODO: add HTTP proxy support
            httpReq.ClientCertificates.Add(sslClientCert);
            httpReq.ClientCertificates.Add(sslCACert);
            httpReq.Method = WebRequestMethods.Http.Post;
            httpReq.ContentType = "text/xml";
            httpReq.ContentLength = data.Length;
            httpReq.Headers.Add("SOAPAction", "#" + soapPortName);

            // send request
            try
            {
                logger.TraceEvent(TraceEventType.Verbose, (int)EventId.Transport, "Sending HTTP Request");
                Logging.Log.HttpRequestStart(data.Length);
                Stream postStream = httpReq.GetRequestStream();
                postStream.Write(data, 0, data.Length);
                postStream.Close();
                Logging.Log.HttpRequestSend();
            }
            catch (WebException ex)
            {
                logger.TraceData(TraceEventType.Error, (int)EventId.Transport, ex);
                Logging.Log.HttpRequestException(ex.Message);
                Logging.Log.HttpRequestStop();
                return new RspStatusAndBody(true, 0, ex.Message);
            }

            // read response
            logger.TraceEvent(TraceEventType.Verbose, (int)EventId.Transport, "Reading HTTP Response");
            string httpRspBody = string.Empty;
            try
            {
                Logging.Log.HttpRequestReceive();
                using (HttpWebResponse httpRsp = (HttpWebResponse)httpReq.GetResponse())
                using (StreamReader sr = new StreamReader(httpRsp.GetResponseStream(), Encoding.GetEncoding(httpRsp.CharacterSet)))
                {
                    httpRspBody = sr.ReadToEnd();
                    logger.TraceEvent(TraceEventType.Verbose, (int)EventId.Transport,
                        "HTTP Response Code: " + httpRsp.StatusCode + "\nHTTP Response Body:\n" + httpRspBody);
                    if (Logging.Log.IsDebugEnabled()) 
                        Logging.Log.DebugMessage3("HttpResponseCodeAndBody", httpRsp.StatusCode.ToString(), Logging.Shorten(httpRspBody));
                    return new RspStatusAndBody(false, httpRsp.StatusCode, httpRspBody);
                }
            }
            catch (WebException ex)
            {
                using (HttpWebResponse response = (HttpWebResponse) ex.Response)
                using (Stream data2 = response.GetResponseStream())
                {
                    string rspBody = new StreamReader(data2).ReadToEnd();
                    logger.TraceEvent(TraceEventType.Verbose, (int)EventId.TransportSoap, "HTTP Response Body:\n" + rspBody);
                    Logging.Log.HttpResponseException(ex.Message, Logging.Shorten(rspBody));
                    return new RspStatusAndBody(true, response.StatusCode, rspBody);
                }
            }
            finally
            {
                Logging.Log.HttpRequestStop();
            }
        }

        public AuthResponseDto RequestSignature(AuthRequestDto req, bool asynchronous)
        {
            // Guid guid = Guid.NewGuid();
            Logging.Log.MssRequestSignatureStart(/*guid,*/ req.ToString(), asynchronous.ToString());
            AuthResponseDto ret = requestSignature(req, asynchronous);
            Logging.Log.MssRequestSignatureStop(/*guid,*/ (int)ret.Status.Code);
            if (ret.Status.Code == ServiceStatusCode.SIGNATURE || ret.Status.Code == ServiceStatusCode.VALID_SIGNATURE) {
                Logging.Log.MssRequestSignatureSuccess(req.TransId, ret.MsspTransId, req.PhoneNumber, ret.UserSerialNumber);
            } else if (ret.Status.Code == ServiceStatusCode.REQUEST_OK) {
                Logging.Log.MssRequestSignaturePending(req.TransId, ret.MsspTransId, req.PhoneNumber);
            } else if (ret.Status.Color == ServiceStatusColor.Yellow || ret.Status.Color == ServiceStatusColor.Green) {
                Logging.Log.MssRequestSignatureWarning((int)ret.Status.Code, req.TransId, ret.MsspTransId, req.PhoneNumber, ret.UserSerialNumber, (string) ret.Detail);
            } else {
                Logging.Log.MssRequestSignatureError((int)ret.Status.Code, req.TransId, ret.MsspTransId, req.PhoneNumber, ret.UserSerialNumber, (string)ret.Detail);
            };
            return ret;
        }

        private AuthResponseDto requestSignature(AuthRequestDto req, bool asynchronous)
        {
            logger.TraceEvent(TraceEventType.Verbose, 0, "RequestSignature(req={0}, async={1})", req, asynchronous);
            if (!req.IsComplete()) {
                return new AuthResponseDto(ServiceStatusCode.InvalidInput, "Input is incomplete");
            };
            if (! _cfg.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowAbsence) && string.IsNullOrWhiteSpace(req.UserSerialNumber))
                return new AuthResponseDto(ServiceStatusCode.UserSerialNumberNotRegistered);

            // build request SOAP body
            string httpReqBody = _formatSignReqAsSoap(req, asynchronous);
            logger.TraceEvent(TraceEventType.Verbose, (int)EventId.Transport, "HTTP Request Body (prior to utf8-encoding):\n" + httpReqBody);
            if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage2("RequestSignatureBody", httpReqBody);

            // send request and retrieve SOAP body
            RspStatusAndBody rspSB = _sendRequest("MSS_Signature", httpReqBody);

            // parse response status & SOAP body, return parsed response
            AuthResponseDto outDto;
            if (!rspSB.ExceptionOccured) {
                outDto = asynchronous ? _parseSignAsync200Response(rspSB.Body, req) : _parseSignSync200Response(rspSB.Body, req);
                return outDto;
            }
            if (rspSB.StatusCode != 0)
                return _parse500Response(rspSB, asynchronous);
            return new AuthResponseDto(ServiceStatusCode.CommSetupError, rspSB.Body);
        }

        AuthResponseDto _parsePoll200Response(string httpRspBody, AuthRequestDto inDto)
        {
            #region Sample Response OUTSTANDING_TRANSACTION
            /* Sample Response 1: OUTSTANDING_TRANSACTION
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            <soapenv:Body>
              <MSS_StatusQueryResponse xmlns="http://uri.etsi.org/TS102204/etsi204-kiuru.wsdl">
                <MSS_StatusResp xmlns="" MajorVersion="1" MinorVersion="1">
                  <mss:AP_Info xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#" AP_ID="mid://xyz.org" AP_TransID="X87D1B5A0AF2597965446211FE437B36B" AP_PWD="" Instant="2015-02-27T13:04:45.072+01:00"/>
                  <mss:MSSP_Info xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#" Instant="2015-02-27T13:04:45.001+01:00">
                    <mss:MSSP_ID><mss:URI>http://mid.swisscom.ch/</mss:URI></mss:MSSP_ID>
                  </mss:MSSP_Info>
                  <mss:MobileUser xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#">
                    <mss:MSISDN>+41791234567</mss:MSISDN>
                  </mss:MobileUser>
                  <mss:Status xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#">
                     <mss:StatusCode Value="504"/>
                     <mss:StatusMessage>OUTSTANDING_TRANSACTION</mss:StatusMessage>
                  </mss:Status>
                </MSS_StatusResp>
              </MSS_StatusQueryResponse>
            </soapenv:Body>
            </soapenv:Envelope>     
            */
            #endregion
            #region Sample Response SIGNATURE
            /*
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://www.w3.org/2003/05/soap-envelope" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            <soapenv:Body>
              <MSS_StatusQueryResponse xmlns="http://uri.etsi.org/TS102204/etsi204-kiuru.wsdl">
                <MSS_StatusResp xmlns="" MajorVersion="1" MinorVersion="1">
                  <mss:AP_Info xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#" AP_ID="mid://xyz.org" AP_TransID="X87D1B5A0AF2597965446211FE437B36B" AP_PWD="" Instant="2015-02-27T15:00:15.210+01:00"/>
                  <mss:MSSP_Info xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#" Instant="2015-02-27T15:00:15.396+01:00">
                    <mss:MSSP_ID>
                      <mss:URI>http://mid.swisscom.ch/</mss:URI>
                    </mss:MSSP_ID>
                  </mss:MSSP_Info>
                  <mss:MobileUser xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#">
                    <mss:MSISDN>+41791234567</mss:MSISDN>
                  </mss:MobileUser>
                  <mss:MSS_Signature xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#">
                    <mss:Base64Signature>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</mss:Base64Signature>
                  </mss:MSS_Signature>
                  <mss:Status xmlns:mss="http://uri.etsi.org/TS102204/v1.1.2#" xmlns:fi="http://mss.ficom.fi/TS102204/v1.0.0#">
                    <mss:StatusCode Value="500"/>
                    <mss:StatusMessage>SIGNATURE</mss:StatusMessage>
                    <mss:StatusDetail>
                      <fi:ServiceResponses>
                        <fi:ServiceResponse>
                          <fi:Description><mss:mssURI>http://mid.swisscom.ch/as#subscriberInfo</mss:mssURI></fi:Description>
                          <ns1:SubscriberInfo xmlns:ns1="http://mid.swisscom.ch/TS102204/as/v1.0"><ns1:Detail id="1901" value="22801"/></ns1:SubscriberInfo>
                        </fi:ServiceResponse>
                      </fi:ServiceResponses>
                    </mss:StatusDetail>
                  </mss:Status>
                </MSS_StatusResp>
              </MSS_StatusQueryResponse>
            </soapenv:Body>
            </soapenv:Envelope>
            */
            #endregion

            AuthResponseDto rspDto = null;
            String s, cursor;

            // Load response in xml document
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.Load(new StringReader(httpRspBody));

            // Namespace manager
            XmlNamespaceManager manager = new XmlNamespaceManager(doc.NameTable);
            manager.AddNamespace("soapenv", "http://www.w3.org/2003/05/soap-envelope");
            manager.AddNamespace("mss", "http://uri.etsi.org/TS102204/v1.1.2#");
            manager.AddNamespace("ets", "http://uri.etsi.org/TS102204/etsi204-kiuru.wsdl");

            cursor = "unknown";
            try
            {
                // extract AP_TransID from XML response and compare it with request
                XmlNodeList nodeList = doc.SelectNodes("//@AP_TransID");
                s = null;
                foreach (XmlNode node in nodeList)
                    s = node.Value;
                if (s != inDto.TransId)
                {
                    string errMsg = String.Format("Mismatched AP_TransId: req.AP_TransId='{0}', rsp.AP_TransId='{1}'", inDto.TransId, s);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Hacking, "{0}, rsp={1}", errMsg, s);
                    Logging.Log.ServerResponseApTrxIdMismatch(inDto.TransId, s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.MismatchedApTransId, errMsg);
                }

                // extract MSISDN and compare with request
                cursor = "MSISDN";
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/ets:MSS_StatusQueryResponse/MSS_StatusResp/mss:MobileUser/mss:MSISDN", manager).InnerText;
                if (s != inDto.PhoneNumber)
                {
                    string errMsg = String.Format("Mismatched MSISDN: req.PhoneNumber='{0}', rsp.PhoneNumber='{1}'", inDto.PhoneNumber, s);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Hacking, "{0}, rsp={1}", errMsg, s);
                    Logging.Log.ServerResponseMsisdnMismatch(inDto.TransId, inDto.PhoneNumber, s, Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.MismatchedMsisdn, errMsg);
                }

                // StatusCode & StatusMessage
                cursor = "StatusCode@Value";
                string statusCode = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/ets:MSS_StatusQueryResponse/MSS_StatusResp/mss:Status/mss:StatusCode", manager).Attributes["Value"].Value;

                cursor = "StatusMessage";
                string statusMsg = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/ets:MSS_StatusQueryResponse/MSS_StatusResp/mss:Status/mss:StatusMessage", manager).InnerText;

                if (statusCode == "504")
                {
                    if (statusMsg != "OUTSTANDING_TRANSACTION") {
                        logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service, "Service has changed StatusMessage of code 504 to {0} (expected: 'OUTSTANDING_TRANSACTION')", statusMsg);
                        Logging.Log.ServerResponseStatusTextChanged(504, "OUTSTANDING_TRANSACTION", statusMsg);
                    }
                    return new AuthResponseDto(ServiceStatusCode.OUSTANDING_TRANSACTION);
                }
                else if (statusCode != "500")
                {
                    string errMsg = String.Format("error=IllegalStatusCode, expect=500|504, seen={0}, response={1}", s, httpRspBody);
                    logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, errMsg);
                    Logging.Log.ServerResponseStatusCodeIllegal(statusCode, "500", Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.IllegalStatusCode, errMsg);
                }

                if (statusMsg != "SIGNATURE") {
                    logger.TraceEvent(TraceEventType.Warning, (int)EventId.Service, "Service has changed StatusMessage of code 500 to {0} (expected: 'SIGNATURE')", statusMsg);
                    Logging.Log.ServerResponseStatusTextChanged(500, "SIGNATURE", statusMsg);
                }

                // Signature
                cursor = "Base64Signature";
                byte[] dtbs_signature;
                s = doc.SelectSingleNode("/soapenv:Envelope/soapenv:Body/ets:MSS_StatusQueryResponse/MSS_StatusResp/mss:MSS_Signature/mss:Base64Signature", manager).InnerText;
                if (String.IsNullOrEmpty(s))
                {
                    string errMsg = String.Format("error=EmptySignature, response={0}", httpRspBody);
                    Logging.Log.ServerResponseEmptySignature(Logging.Shorten(httpRspBody));
                    return new AuthResponseDto(ServiceStatusCode.UnknownResponse, errMsg);
                }
                else
                {
                    dtbs_signature = Convert.FromBase64String(s);
                    if (!_isValidSignature(inDto.DataToBeSigned, dtbs_signature))
                    {
                        logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Response Signature is invalid");
                        Logging.Log.ServerResponseInvalidSignature(inDto.TransId, inDto.PhoneNumber, Logging.Shorten(httpRspBody));
                        return new AuthResponseDto(ServiceStatusCode.InvalidResponseSignature);
                    }
                }

                // At this point, the request is considered correct (subjected to serial number check)

                rspDto = new AuthResponseDto(ServiceStatusCode.SIGNATURE);
                rspDto.Signature = dtbs_signature;
                if (!_verifyUserSerialNumber(inDto, rspDto))
                {
                    logger.TraceEvent(TraceEventType.Verbose, 0, rspDto.ToString());
                    Logging.Log.UserSerialNumberNotAccepted(inDto.TransId, inDto.PhoneNumber, inDto.UserSerialNumber, rspDto.UserSerialNumber);
                    return rspDto; // rspDto is modified in this case
                }

            }
            catch (NullReferenceException ex)
            {
                logger.TraceData(TraceEventType.Error, (int)EventId.Service, ex);
                Logging.Log.ServerResponseMissingElement(cursor, Logging.Shorten(httpRspBody));
                return new AuthResponseDto(ServiceStatusCode.UnknownResponse, "Missing element " + cursor);
            }

            _enrichAuthRspDto(rspDto);
            return rspDto;

        }

        /// <summary>
        /// Invoke <c>MSS_SignatureReq</c>
        /// </summary>
        /// <param name="req">Request object. The property <c>ApId</c> must be defined.</param>
        /// <param name="msspTransId">A non-empty string that was returned in the previous asynchrous MSS_SignatureReq call</param>
        /// <returns>AuthResponseDto object</returns>
        /// <remarks>If you want to let PollSignagure regenerates a AP_TransId, set req.TransId to null.</remarks>
        public AuthResponseDto PollSignature(AuthRequestDto req, string msspTransId)
        {
            Logging.Log.MssPollSignatureStart(req.ToString(), msspTransId); // TODO: ActivityId, PerfCounter
            AuthResponseDto ret = pollSignature(req, msspTransId);
            Logging.Log.MssPollSignatureStop((int)ret.Status.Code);
            if (ret.Status.Code == ServiceStatusCode.VALID_SIGNATURE || ret.Status.Code == ServiceStatusCode.SIGNATURE) {
                Logging.Log.MssPollSignatureSuccess(req.TransId, msspTransId, req.PhoneNumber, req.UserSerialNumber);
            } else if (ret.Status.Code == ServiceStatusCode.OUSTANDING_TRANSACTION) {
                Logging.Log.MssPollSignaturePending(req.TransId, msspTransId, req.PhoneNumber);
            } else if (ret.Status.Color == ServiceStatusColor.Yellow || ret.Status.Color == ServiceStatusColor.Green) {
                Logging.Log.MssPollSignatureWarning((int)ret.Status.Code, req.TransId, msspTransId, req.PhoneNumber, ret.UserSerialNumber, (string)ret.Detail);
            } else {
                Logging.Log.MssPollSignatureError((int)ret.Status.Code, req.TransId, msspTransId, req.PhoneNumber, ret.UserSerialNumber, (string)ret.Detail);
            }
            return ret;
        }

        private AuthResponseDto pollSignature(AuthRequestDto req, string msspTransId)
        {
            logger.TraceEvent(TraceEventType.Verbose, 0, "PollSignature(req={0}, msspTransId={1})", req, Util.Str(msspTransId));
            if (!req.IsComplete())
                return new AuthResponseDto(ServiceStatusCode.InvalidInput, "Input is incomplete");
            if (String.IsNullOrEmpty(msspTransId))
                return new AuthResponseDto(ServiceStatusCode.InvalidInput, "msspTransId is null or empty");

            // build web request Body
            string httpReqBody = String.Format(
#if DEBUG
@"<soap:Envelope
xmlns:soap=""http://www.w3.org/2003/05/soap-envelope""
xmlns:ets=""http://uri.etsi.org/TS102204/etsi204-kiuru.wsdl""
xmlns:v1=""http://uri.etsi.org/TS102204/v1.1.2#"">
<soap:Body>
  <ets:MSS_StatusQuery>
    <MSS_StatusReq MajorVersion=""1"" MinorVersion=""1"" MSSP_TransID=""{0}"">
      <v1:AP_Info AP_ID=""{1}"" AP_PWD="""" AP_TransID=""{2}"" Instant=""{3}""/>
        <v1:MSSP_Info>
          <v1:MSSP_ID>
            <v1:URI>http://mid.swisscom.ch/</v1:URI>
          </v1:MSSP_ID>
        </v1:MSSP_Info>
      </MSS_StatusReq>
    </ets:MSS_StatusQuery>
  </soap:Body>
</soap:Envelope>",
#else
@"<soap:Envelope
xmlns:soap=""http://www.w3.org/2003/05/soap-envelope""
xmlns:ets=""http://uri.etsi.org/TS102204/etsi204-kiuru.wsdl""
xmlns:v1=""http://uri.etsi.org/TS102204/v1.1.2#"">
<soap:Body><ets:MSS_StatusQuery>
<MSS_StatusReq MajorVersion=""1"" MinorVersion=""1"" MSSP_TransID=""{0}"">
<v1:AP_Info AP_ID=""{1}"" AP_PWD="""" AP_TransID=""{2}"" Instant=""{3}""/>
<v1:MSSP_Info><v1:MSSP_ID><v1:URI>http://mid.swisscom.ch/</v1:URI></v1:MSSP_ID>
</v1:MSSP_Info></MSS_StatusReq></ets:MSS_StatusQuery></soap:Body></soap:Envelope>",
#endif
                 msspTransId,
                 _cfg.ApId,
                (req.TransId != null ? req.TransId : (req.TransId =  "X" + Util.Build64bitRandomHex(_cfg.SeedApTransId))),
                (req.Instant != null ? req.Instant : Util.CurrentTimeStampString())
            );
            logger.TraceEvent(TraceEventType.Verbose, (int)EventId.Transport, "HTTP Request Body (prior to utf8-encoding):\n" + httpReqBody);
            Logging.Log.DebugMessage2("HttpReqBody", httpReqBody);

            // send SOAP request and read (unparsed) SOAP response
            RspStatusAndBody rspSB = _sendRequest("MSS_StatusQuery", httpReqBody);

            if (rspSB.ExceptionOccured)
            {
                if (rspSB.StatusCode == 0)
                {
                    return new AuthResponseDto(ServiceStatusCode.CommSetupError, rspSB.Body);
                }
                else
                {
                    return _parse500Response(rspSB, true /* asynch */); 
                }
            }
            else
            {
                return _parsePoll200Response(rspSB.Body, req);
            }
        }

        private bool _isValidSignature(string dataToBeSigned, byte[] signature)
        {
            if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage2("_isValidSignature", dataToBeSigned);
            if (dataToBeSigned == null) {
                logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "Input dataToBeSigned is null");
                Logging.Log.ServerResponseMessageError("isValidSignature", "Input dataToBeSigned is null");
                return false;
            };

            SignedCms signedCms = new SignedCms();
            try
            {
                signedCms.Decode(signature);
                byte[] dtbs_cms = signedCms.ContentInfo.Content;
                if (Encoding.UTF8.GetString(dtbs_cms) != dataToBeSigned) {
                    logger.TraceEvent(TraceEventType.Error, (int) EventId.Service, "DataToBeSigned differs: req='" + dataToBeSigned
                        + "', rsp_hex=" + BitConverter.ToString(dtbs_cms));
                    Logging.Log.ServerResponseMessageError("isValidSignature", "dataToBeSigned differs: req='" + dataToBeSigned
                        + "', rsp_hex=" + BitConverter.ToString(dtbs_cms));
                    return false;
                };
                signedCms.CheckSignature(_cfg.DisableSignatureCertValidation);
                logger.TraceEvent(TraceEventType.Verbose, (int)EventId.Service, "Signature Verified: signer_0='" 
                    + signedCms.SignerInfos[0].Certificate.Subject + "', noChainValidation=" + _cfg.DisableSignatureCertValidation);
                if (Logging.Log.IsDebugEnabled()) Logging.Log.DebugMessage3("ValidSignature", _cfg.DisableSignatureCertValidation.ToString(), signedCms.SignerInfos[0].Certificate.Subject);
                return true;
            }
            catch (Exception e)
            {
                logger.TraceEvent(TraceEventType.Error, (int)EventId.Service, "INVALID_SIGNATURE: " + e.Message);
                Logging.Log.ServerResponseMessageError("InvalidSiganture", e.Message);
                return false;
            }
        }

        private void _enrichAuthRspDto(AuthResponseDto rspDto)
        {
            // parse more attribute from signature and update rspDto if needed
            return;
        }

    }

}
