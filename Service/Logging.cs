using System;
using Microsoft.Diagnostics.Tracing;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MobileId
{

    [EventSource(Name = "Swisscom-MobileID-Client12")]
    public sealed class Logging : EventSource
    {
        static readonly public Logging Log = new Logging();

        // ETW has a size limitation of 64 KByte per event, and string is stored as unicode (2 byte/char).
        // If the event is oversized, perfview.exe will displayed the truncated event while eventvwr.exe 
        // display an EventSource exception (event id is 0).
        // Oversized event should only occur in event with Level verbose (e.g. event containing SOAP response with signature).
        // We opt to truncate the event and keep the TraceSouce tracing implementation (which was implemented before EventSource).
        public const int MaximalLoggedStringLength = 24576; 

        [NonEvent]
        public static string Shorten(String s)
        {
            if (s == null) {
                return "<null>";
            } else if (s.Length <= MaximalLoggedStringLength ) { 
                return s;
            } else {
                return s.Substring(0, MaximalLoggedStringLength - 2) + "..";
            }
        }

        [NonEvent]
        public string _s(string nullableString) { return nullableString != null ? nullableString : "<null>"; }

        [NonEvent]
        public bool IsDebugEnabled() { return IsEnabled(EventLevel.Verbose, EventKeywords.All); }

        // General Debug

        [Event(1, /* Keywords = Keywords.All, */ Level = EventLevel.Verbose, Channel = EventChannel.Debug,
            Message = "{0}")]
        public void DebugMessage(string Message) {
            WriteEvent(1, Message); }

        [Event(2, /* Keywords = Keywords.All, */ Level = EventLevel.Verbose, Channel = EventChannel.Debug,
            Message = "{0}:{1}")]
        public void DebugMessage2(string MessageKeyword, string Message) {
            WriteEvent(2, MessageKeyword, _s(Message)); }

        [Event(3, /* Keywords = Keywords.All, */ Level = EventLevel.Verbose, Channel = EventChannel.Debug,
            Message = "{0}:{1}\n{2}")]
        public void DebugMessage3(string MessageKeyword, string Message, string Message2) {
            WriteEvent(3, MessageKeyword, _s(Message), _s(Message2)); }

        // KeyManagement

        [Event(9, Keywords = Keywords.KeyManagement | Keywords.Audit, Level = EventLevel.Informational, Channel = EventChannel.Admin,
            Message = "Found Cert:  storeLocation={0}, storeName={1}, fileType='{2}', findValue='{3}'")]
        public void KeyManagementCertFound(StoreLocation StoreLocation, StoreName StoreName, X509FindType X509FindType, string FindValue) {
            WriteEvent(9, StoreLocation, StoreName, X509FindType, FindValue); }

        [Event(8, Keywords = Keywords.KeyManagement, Level = EventLevel.Informational, Channel = EventChannel.Admin,
            Message =  "Multiple valid certs found, the first one is used: storeLocation={0}, storeName={1}, fileType='{2}', findValue='{3}'")]
        public void KeyManagementMultiCertFound(StoreLocation StoreLocation, StoreName StoreName, X509FindType X509FindType, string FindValue) {
            WriteEvent(8, StoreLocation, StoreName, X509FindType, FindValue); }

        [Event(7, Keywords = Keywords.KeyManagement, Level = EventLevel.Informational, Channel = EventChannel.Admin, 
            Message="No valid cert found: storeLocation={0}, storeName={1}, fileType='{2}', findValue='{3}'")]
        public void KeyManagementCertNotFound(StoreLocation StoreLocation, StoreName StoreName, X509FindType X509FindType, string FindValue) {
            WriteEvent(7, StoreLocation, StoreName, X509FindType, FindValue); }

        [Event(6, Keywords = Keywords.KeyManagement, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Technical error while retrieving cert: storeLocation={0}, storeName={1}, findType={2}, findValue={3}, exceptionMessage={4}")]
        public void KeyManagementStoreError(StoreLocation StoreLocation, StoreName StoreName, X509FindType X509FindType, string FindValue, string ExceptionMessage) {
            WriteEvent(6, StoreLocation, StoreName, X509FindType, FindValue, ExceptionMessage); }

        [Event(5, Keywords = Keywords.KeyManagement, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Load Cert failed: exceptionMessage='{0}'")]
        public void KeyManagementCertException(string ExceptionMessage) {
            WriteEvent(5, ExceptionMessage); }

        [Event(4, Keywords = Keywords.KeyManagement, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message = "Retrieved Cert:  certname='{0}'")]
        public void KeyManagementCertRetrieved(string CertName) {
            WriteEvent(4, CertName); }

        // HTTP Transport

        [Event(10, Keywords = Keywords.Transport, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.HttpRequest, Opcode = EventOpcode.Start,
            Message = "HTTP Request Start: contentLength='{0}'")]
        public void HttpRequestStart(int ContentLength) {
            WriteEvent(10, ContentLength); }

        [Event(11, Keywords = Keywords.Transport, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.HttpRequest, Opcode = EventOpcode.Stop,
            Message = "HTTP Request Stop")]
        public void HttpRequestStop() {
            WriteEvent(11); }

        [Event(12, Keywords = Keywords.Transport, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.HttpRequest, Opcode = EventOpcode.Send,
            Message = "HTTP Request Sent.")]
        public void HttpRequestSend() {
            WriteEvent(12); }

        [Event(13, Keywords = Keywords.Transport, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.HttpRequest, Opcode = EventOpcode.Receive,
            Message = "HTTP Request Received.")]
        public void HttpRequestReceive() {
            WriteEvent(13); }

        [Event(14, Keywords = Keywords.Transport, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "HTTP Request Error: exceptionMessage='{0}'")]
        public void HttpRequestException(string ExceptionMessage) {
            WriteEvent(14, ExceptionMessage); }

        [Event(15, Keywords = Keywords.Transport, Level = EventLevel.Warning, Channel = EventChannel.Analytic,
            Message = "HTTP Request Error: exceptionMessage='{0}', payload='{1}'")]
        public void HttpResponseException(string ExceptionMessage, string Payload) { WriteEvent(15, ExceptionMessage, Logging.Shorten(Payload)); }

        // id 16-18 reserved

        // Config

        [Event(19, Keywords = Keywords.Config, Level = EventLevel.Informational, Channel = EventChannel.Admin,
            Message = "Load Config: codeVersion={0}, cfg={1}")]
        public void ConfigInfo(int CodeVersion, string Content) { WriteEvent(19, CodeVersion, Content); }

        // SOAP Message

        [Event(20, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error=NoFaultCode, httpStatus='{0}', rspBody={1}")]
        public void ServerResponseFaultCodeUnknown(int HttpStatusCode, string ResponseBody) { 
            WriteEvent(20, HttpStatusCode, Shorten(ResponseBody)); }

        [Event(21, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error=BadFaultCode, httpStatus='{0}', cursor='{1}', rspBody='{2}', exception='{3}'")]
        public void ServerResponseFormatUnknown(int HttpStatusCode, string Cursor, string ResponseBody, string ExceptionMessage) {
            WriteEvent(21, HttpStatusCode, Cursor, Shorten(ResponseBody), ExceptionMessage); }

        [Event(22, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error=BadStatusCode, code='{0}', reason='{1}', detail='{2}'")]
        public void ServerResponseStatusCodeOverflow(string Code, string Reason, string Detail) {
            WriteEvent(22, Code, Reason, Shorten(Detail)); }

        [Event(23, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error=IllegalStatusCode, code='{0}', codeExpected='{1}', rspBody='{2}'")]
        public void ServerResponseStatusCodeIllegal(string Code, string ExpectedCodes, string ResponseBody) {
            WriteEvent(23, Code, ExpectedCodes, Shorten(ResponseBody)); }

        [Event(24, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error=UnsupportedStatusCode, code='{0}', reason='{1}', detail='{2}', rspBody='{3}'")]
        public void ServerResponseStatusCodeUnsupported(string Code, string Reason, string Detail, string ResponseBody) {
            WriteEvent(24, Code, Reason, Detail, Shorten(ResponseBody)); }

        [Event(25, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error='emptyMssTrxId', apTransId='{0}', rspBody='{1}'")]
        public void ServerResponseEmptyMssTrxId(string ApTransId, string ResponseBody) {
            WriteEvent(25, ApTransId, Shorten(ResponseBody)); }

        [Event(26, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error='missingElement', cursor='{0}', rspBody='{1}'")]
        public void ServerResponseMissingElement(string Cursor, string ResponseBody) {
            WriteEvent(26, Cursor, Shorten(ResponseBody)); }

        [Event(27, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error='illegalStatusCode', codeExpected='500', codeResponse='{0}', rspBody='{1}'")]
        public void ServerResponseCodeMismatch(string Code, string ResponseBody) { 
            WriteEvent(27, Code, Shorten(ResponseBody)); }

        [Event(28, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response ParseError: error='emptySignature', rspBody='{0}'")]
        public void ServerResponseEmptySignature(string ResponseBody) {
            WriteEvent(28, Shorten(ResponseBody)); }

        [Event(29, Keywords = Keywords.Message, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Response Error: context='{0}', error='{1}'")]
        public void ServerResponseMessageError(string Context, string Error) { WriteEvent(29, Context, _s(Error)); }

        [Event(30, Keywords = Keywords.Message | Keywords.Attack, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Hacking Attempt: error='mismatched AP_TransId', req.AP_TransId='{0}', rsp.AP_TransId='{1}', rspBody='{2}'")]
        public void ServerResponseApTrxIdMismatch(string ApTransIdRequest, string ApTransIdResponse, string ResponseBody) {
            WriteEvent(30, ApTransIdRequest, ApTransIdResponse, Shorten(ResponseBody)); }

        [Event(31, Keywords = Keywords.Message | Keywords.Attack, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Hacking Attempt: error='mismatched MSISDN', req.AP_TransId='{0}', req.MSISDN='{1}', rsp.MSISDN='{2}', rspBody='{3}'")]
        public void ServerResponseMsisdnMismatch(string ApTransId, string MsisdnRequest, string MsisdnResponse, string ResponseBody) {
            WriteEvent(31, ApTransId, MsisdnRequest, MsisdnResponse, Shorten(ResponseBody)); }

        [Event(32, Keywords = Keywords.Message | Keywords.Attack, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Hacking Attempt: error='invalidSignature', apTransId='{0}', phoneNumber='{1}', rspBody='{2}'")]
        public void ServerResponseInvalidSignature(string ApTransId, string PhoneNumber, string ResponseBody) {
            WriteEvent(32, ApTransId, PhoneNumber, Shorten(ResponseBody)); }

        [Event(33, Keywords = Keywords.Message, Level = EventLevel.Verbose , Channel = EventChannel.Debug,
            Message = "SOAP Fault Reason ({2}) for code {0} does not match registered Reason ({1})")]
        // the implementation uses only the numerical StatusCode, not the string variant. Thus the severity of change is low.
        public void ServerResponseStatusTextChanged(int StatusCode, string ExpectedText, string SeenText) { WriteEvent(33, StatusCode, ExpectedText, Shorten(SeenText)); }

        // id 34-36 reserved

        // User Attributes

        [Event(39, Keywords = Keywords.AttrStore, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "Invalid Serial Number: apTransId='{0}', phoneNumber='{1}', userSerialNumberInStore='{2}', userSerialNumberInResponse='{3}'")]
        public void UserSerialNumberNotAccepted(string ApTransId, string PhoneNumber, string UserSerialNumberRequest, string UserSerialNumberResponse) {
            WriteEvent(39, ApTransId, PhoneNumber, _s(UserSerialNumberRequest), _s(UserSerialNumberResponse)); }

        [Event(38, Keywords = Keywords.AttrStore, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "Empty User Serial Number in Attribute Store: apTransId='{0}', phoneNumber='{1}', userSerialNumberInResponse='{2}'")]
        public void UserSerialNumberNotInStore(string ApTransId, string PhoneNumber, string UserSerialNumberResponse) {
            WriteEvent(38, ApTransId, _s(PhoneNumber), _s(UserSerialNumberResponse)); }

        [Event(37, Keywords = Keywords.AttrStore, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "User Serial Numbers Mismatched: apTransId='{0}', phoneNumber='{1}', userSerialNumberInStore='{2}', userSerialNumberInResponse='{3}'")]
        public void UserSerialNumberMismatch(string ApTransId, string PhoneNumber, string UserSerialNumberRequest, string UserSerialNumberResponse) {
            WriteEvent(37, ApTransId, PhoneNumber, _s(UserSerialNumberRequest), _s(UserSerialNumberResponse)); }

        // Service boundary

        [Event(40, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.MssRequestSignature, Opcode = EventOpcode.Start,
            Message = "requestParams={0}; asynchronous={1}")]
        public void MssRequestSignatureStart(string RequestParams, string Asynchronous) {
            WriteEvent(40, RequestParams, Asynchronous);}

        [Event(41, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.MssRequestSignature, Opcode = EventOpcode.Stop,
            Message="statusCode={0}")]
        public void MssRequestSignatureStop(int StatusCode) {
            WriteEvent(41, StatusCode); }

        [Event(42, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditSuccess, Level = EventLevel.Informational, Channel = EventChannel.Admin, Task = Tasks.MssRequestSignature, Opcode = EventOpcode.Info,
            Message = "Signature Success: apTransId='{0}', msspTransId='{1}', phoneNumber='{2}', userSerialNumber='{3}'")]
        public void MssRequestSignatureSuccess(string ApTransId, string MsspTransId, string PhoneNumber, string UserSerialNumber) {
            WriteEvent(42, ApTransId, MsspTransId, PhoneNumber, _s(UserSerialNumber));}

        [Event(43, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message = "apTransId='{0}', msspTransId='{1}', phoneNumber='{2}'")]
        public void MssRequestSignaturePending(string ApTransId, string MsspTransId, string PhoneNumber){
            WriteEvent(43, ApTransId, MsspTransId, PhoneNumber); }

        [Event(44, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "Signature Failure: statusCode={0}, apTransId={1}, msspTransId={2}, phoneNumber={3}, userSerialNumber={4}, detail='{5}'")]
        public void MssRequestSignatureWarning(int StatusCode, string ApTransId, string MsspTransId, string PhoneNumber, string UserSerialNumber, string Detail) {
            WriteEvent(44, StatusCode, ApTransId, _s(MsspTransId), _s(PhoneNumber), _s(UserSerialNumber), Shorten(Detail)); }

        [Event(45, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Signature Error: statusCode={0}, apTransId={1}, msspTransId={2}, phoneNumber={3}, userSerialNumber={4}, detail='{5}'")]
        public void MssRequestSignatureError(int StatusCode, string ApTransId, string MsspTransId, string PhoneNumber, string UserSerialNumber, string Detail) {
            WriteEvent(45, StatusCode, ApTransId, _s(MsspTransId), _s(PhoneNumber), _s(UserSerialNumber), Shorten(Detail)); }

        [Event(46, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.MssPollSignature, Opcode = EventOpcode.Start,
            Message = "requestParams={0}, msspTransId='{1}'")]
        public void MssPollSignatureStart(string RequestParams, string MsspTransId) { 
            WriteEvent(46, RequestParams, MsspTransId); }

        [Event(47, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.MssPollSignature, Opcode = EventOpcode.Stop,
            Message="statusCode={0}")]
        public void MssPollSignatureStop(int StatusCode) {
            WriteEvent(47, StatusCode); }

        [Event(48, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditSuccess, Level = EventLevel.Informational, Channel = EventChannel.Admin, Task = Tasks.MssPollSignature, Opcode = EventOpcode.Info,
            Message = "Signature Success: apTransId={0}, msspTransId={1}, phoneNumber={2}, userSerialNumber={3}")]
        public void MssPollSignatureSuccess(string ApTransId, string MsspTransId, string PhoneNumber, string UserSerialNumber) {
            WriteEvent(48, ApTransId, MsspTransId, PhoneNumber, _s(UserSerialNumber)); }

        [Event(49, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message = "apTransId={0}, msspTransId={1}, phoneNumber={2}")]
        public void MssPollSignaturePending(string ApTransId, string MsspTransId, string PhoneNumber) {
            WriteEvent(49, ApTransId, MsspTransId, PhoneNumber); }

        [Event(50, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "Signature Failure: statusCode={0}, apTransId={1}, msspTransId={2}, phoneNumber={3}, userSerialNumber={4}, detail='{5}'")]
        public void MssPollSignatureWarning(int StatusCode, string ApTransId, string MsspTransId, string PhoneNumber, string UserSerialNumber, string Detail) {
            WriteEvent(50, StatusCode, ApTransId, _s(MsspTransId), _s(PhoneNumber), _s(UserSerialNumber), Shorten(Detail)); }

        [Event(51, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "Signature Error: statusCode={0}, apTransId={1}, msspTransId={2}, phoneNumber={3}, userSerialNumber={4}, detail='{5}'")]
        public void MssPollSignatureError(int StatusCode, string ApTransId, string MsspTransId, string PhoneNumber, string UserSerialNumber, string Detail) {
            WriteEvent(51, StatusCode, ApTransId, _s(MsspTransId), _s(PhoneNumber), _s(UserSerialNumber), Shorten(Detail));}

        public class Keywords
        {
            public const EventKeywords Audit = (EventKeywords)0x0001L;
            public const EventKeywords Config = (EventKeywords)0x0002L;
            public const EventKeywords KeyManagement = (EventKeywords)0x0004L;
            public const EventKeywords Transport = (EventKeywords)0x0008L;   // tcpip-network, ssl-establishment
            public const EventKeywords Message = (EventKeywords)0x0010L; // soap/json
            public const EventKeywords Service = (EventKeywords)0x0020L;
            public const EventKeywords AttrStore = (EventKeywords)0x0040L;
            public const EventKeywords Attack = (EventKeywords)0x0080L; // possible hacking attack

            public string AsString(EventKeywords keywords)
            {
                StringBuilder sb = new StringBuilder();
                string[] names = new string[] { "Audit", "Config", "KeyManagement", "Transport", "Message", "Service", "AttrStore", "Attack" };
                for (int i=0, k=1; k <= 0x80; i++, k*=2) {
                    if ((k & (long)keywords) != 0) {
                        if (sb.Length > 0) sb.Append(",");
                        sb.Append(names[i]);
                    }
                }
                return sb.ToString();
            }
        }

        public class Tasks
        {
            public const EventTask HttpRequest = (EventTask) 0x1;
            public const EventTask MssRequestSignature = (EventTask) 0x2;
            public const EventTask MssPollSignature = (EventTask) 0x3;
        }
    }
   
}
