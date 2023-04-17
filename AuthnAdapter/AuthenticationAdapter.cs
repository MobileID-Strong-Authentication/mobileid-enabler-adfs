using Microsoft.IdentityServer.Web.Authentication.External;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Resources;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MobileId.Adfs
{
    public class AuthenticationAdapter : IAuthenticationAdapter
    {
        private static TraceSource logger = new TraceSource("MobileId.Adfs.AuthnAdapter");
        private readonly Claim[] ClaimsHwToken =  new Claim[] { 
          new Claim(
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationmethod",
            "http://schemas.microsoft.com/ws/2008/06/identity/authenticationmethod/hardwaretoken")
        };
        static ResourceManager resMgr = new ResourceManager("MobileId.Adfs.WebUI", typeof(AuthenticationAdapter).Assembly);

        // keys for data to be transferred via Context
        private const string MSISDN = "msisdn";  // Mobile ID Number to place the signature request
        private const string UKEYSN = "userSN";  // serial number of user's keypair, retrieved from SerialNumber of the DN of user cert
        private const string USERUPN = "userUPN"; // User Principal Name of the first authentication method, needed by TryEndAuthentication(...) for audit/logging
        private const string MSSPTRXID = "mTrxId"; // Transaction ID returned by authentication server (MSSP)
        private const string AUTHBEGIN = "authBegin"; // timestamp (UTC time in milliseconds) when sending MSS_SignatureReq
        private const string DTBS = "dtbs"; // data to be signed
        private const string STATE = "state";   // authentication state, s. activity_state_diagram.pdf
        private const string SESSBEGIN = "sb"; // timestamp when session begin (i.e. sending first MSS_SignatureReq
        private const string SESSTRIES = "st"; // number of sent MSS_SignatureReq in the session

        // keys for resource
        private const string RES_LANG = "AppletLanguage";
        private const string RES_LOGINPROMPT = "MobileLoginPromt";

        // objects re-used among authentication "sessions"
        private WebClientConfig cfgMid = null;
        private AdfsConfig cfgAdfs = null;
        private IAuthentication _webClient = null;

        // statistics, also used for recycle_webClient
        private ulong reqCount = 0;

        // private const string EVENTLOGSource = "AD FS MobileID";
        // private const string EVENTLOGGroup = "Application";

        private IAuthentication getWebClient()
        {
            int id;
            if (_webClient == null || (++reqCount % cfgAdfs.WebClientMaxRequest) == 0) {
                if (_webClient != null)
                {
                    id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_webClient);
                    // see http://stackoverflow.com/questions/5703993/how-to-print-object-id
                    logger.TraceEvent(TraceEventType.Verbose, 0, "delObj: name=WebClientImpl, reason=MaxRequest, id=" + id);
                    Logging.Log.WebClientDestroyed(id);
                }
                _webClient = new WebClientImpl(cfgMid);
                id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_webClient);
                logger.TraceEvent(TraceEventType.Verbose, 0, "newObj: name=WebClientImpl, id=" + id);
                Logging.Log.WebClientCreated(id);
            } else {
                // TODO: check lifetime, recycle _webClient on time-to-live
            };
            return _webClient;
        }

        private string _str(IAuthenticationContext ctx)
        {
            if (ctx == null)
                return "null";
            StringBuilder sb = new StringBuilder(256); // TODO: update on change
            sb.Append("{Data: {");
            foreach (var entry in ctx.Data)
                sb.Append("\"").Append(entry.Key).Append("\":\"").Append(entry.Value).Append("\", ");
            sb.Append("}, Lcid: ").Append(ctx.Lcid);
            sb.Append(", ActivityId: \"").Append(ctx.ActivityId);
            sb.Append("\", ContextId: \"").Append(ctx.ContextId);
            sb.Append("\", obj=").Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ctx));
            sb.Append("}");
            return sb.ToString();
        }

        private string _str(Claim c)
        {
            if (c == null) return "null";
            StringBuilder sb = new StringBuilder(196); // TODO: update on change
            sb.Append("{");
            ClaimsIdentity subj = c.Subject;
            if (subj == null) {
                sb.Append("Subject: null; ");
            } else {
                sb.Append("Subject: {");
                sb.Append("Name: \"").Append(subj.Name).Append("\"; ");
                sb.Append("Label: \"").Append(subj.Label).Append("\"; ");
                sb.Append("AuthenticationType: \"").Append(subj.AuthenticationType).Append("\"; ");
                sb.Append("IsAuthenticated: ").Append(subj.IsAuthenticated).Append("; " );
                sb.Append("Actor={").Append(subj.Actor).Append("}; ");
                sb.Append("}, ");
            };
            if (c.Value == null) {
                sb.Append("Value: null;");
            } else {
                sb.Append("Value: \"").Append(c.Value).Append("\"; ");
            };
            if (c.Issuer == null) {
                sb.Append("Issuer: null;");
            } else {
                sb.Append("Issuer: \"").Append(c.Issuer).Append("\"; ");
            };
            sb.Append("Properties: ").Append(_str(c.Properties));
            return sb.ToString();
        }

        private string _str(IDictionary<string,string> dict)
        {
            if (dict == null) return "Null";
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            foreach (KeyValuePair<string, string> e in dict)
            {
                sb.Append("\"").Append(e.Key).Append("\": \"").Append(e.Value).Append("\", ");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private string _str(System.Collections.Specialized.NameValueCollection c)
        {
            if (c == null) return "null";
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            foreach (string k in c) 
                sb.Append(k).Append(": \"").Append(c[k]).Append("\",");
            sb.Append("}");
            return sb.ToString();
        }

        private string _str(HttpListenerRequest req) {
            if (req == null) return "null";
            StringBuilder sb = new StringBuilder(64); // TODO: update on change
            sb.Append("{RawUrl:\"").Append(req.RawUrl).Append("\"; ");
            sb.Append("QueryString:").Append(_str(req.QueryString)).Append("; ");
            sb.Append("}");
            return sb.ToString();

        }

        private string _str(IProofData o)
        {
            if (o == null) return "null";
            StringBuilder sb = new StringBuilder(32); // TODO: update on change
            sb.Append("{");
            foreach (KeyValuePair<string, object> pair in o.Properties)
                sb.Append(pair.Key).Append(":\"").Append(pair.Value).Append("\"; ");
            sb.Append("}");
            return sb.ToString();
        }

        private string _str(System.IO.Stream s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder(64); // TODO: update on change
            sb.Append("{canRead: ").Append(s.CanRead);
            sb.Append("; canWrite: ").Append(s.CanWrite);
            if (s.CanSeek) {
                sb.Append("; canSeek: True; Length: ").Append(s.Length);
            } else {
                sb.Append("; canSeek: False");
            };
            sb.Append("}");
            return sb.ToString();
        }

        // look up the userLang-specific LoginPrompt string from (adfsConfig || resource), expand placeholders, and returns the result
        private string _buildMobileIdLoginPrompt(UserLanguage userLanguage, CultureInfo culture, string transId)
        {
            string prefix = cfgMid.DtbsPrefix;
            string prompt = cfgAdfs.GetLoginPrompt(userLanguage);
            if (prompt == null)
                prompt = resMgr.GetString(RES_LOGINPROMPT, culture);
            prompt = new Regex("#TransId#",RegexOptions.IgnoreCase).Replace(prompt, transId);
            return prefix + prompt;
        }

        public bool IsAvailableForUser(Claim identityClaim, IAuthenticationContext ctx) {
            bool rc;
            Logging.Log.IsAvailableForUserStart(_str(identityClaim), _str(ctx));
            rc = isAvailableForUser(identityClaim, ctx);
            Logging.Log.IsAvailableForUserStop(rc, identityClaim.Value);
            return rc;
        }

        // Check if mobile id authenicator is available for the user
        private bool isAvailableForUser(Claim identityClaim, IAuthenticationContext ctx)
        {
            logger.TraceEvent(TraceEventType.Verbose, 0, "IsAvailableForUser(claim=" + _str(identityClaim) + ", ctx=" + _str(ctx) + ")");

            string upn = identityClaim.Value; // UPN Claim from the mandatory Primary Authentication
            string msisdn = null;
            string snOfDN = null;
            bool needLoadSerialNumber = cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.warnMismatch) || 
                ! cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowAbsence) ||
                ! cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowMismatch);

            // Search for the user
            try
            {
                var domain = upn.Split("@".ToCharArray())[1];

                using (DirectoryEntry entry = new DirectoryEntry($"LDAP://{domain}"))
                {
                    DirectorySearcher ds = new DirectorySearcher(entry);
                    ds.SearchScope = SearchScope.Subtree;
                    ds.Filter = "(&(objectClass=user)(objectCategory=person)(userPrincipalName=" + upn + "))";
                    ds.PropertiesToLoad.Add(cfgAdfs.AdAttrMobile);
                    if (needLoadSerialNumber)
                        ds.PropertiesToLoad.Add(cfgAdfs.AdAttrMidSerialNumber);

                    SearchResult result = ds.FindOne();
                    if (result != null)
                    {
                        ResultPropertyCollection propertyCollection = result.Properties;
                        foreach (string thisProperty in propertyCollection.PropertyNames)
                        {
                            foreach (string propertyValue in propertyCollection[thisProperty])
                            {
                                if (thisProperty.ToLower(System.Globalization.CultureInfo.InvariantCulture) == cfgAdfs.AdAttrMobile)
                                {
                                    msisdn = propertyValue.ToString();
                                    string msisdnSanitized;
                                    try {
                                        msisdnSanitized = Util.SanitizePhoneNumber(msisdn, cfgMid);
                                    } catch (Exception e) {
                                        Logging.Log.AttrMobileMalformed(upn, msisdn);
                                        throw e;
                                    }
                                    ctx.Data.Add(MSISDN, msisdnSanitized); //  let it blow up if MSISDN is ambiguous
                                }
                                if (needLoadSerialNumber &&
                                    (thisProperty.ToLower(System.Globalization.CultureInfo.InvariantCulture) == cfgAdfs.AdAttrMidSerialNumber))
                                {
                                    snOfDN = propertyValue.ToString();
                                    if (cfgAdfs.AdAttrMidSerialNumber == "altsecurityidentities") {
                                        // special treatment for attribute altSecurityIdentities (1.2.840.113556.1.4.867, https://msdn.microsoft.com/en-us/library/ms677943.aspx)
                                        if (! string.IsNullOrWhiteSpace(snOfDN) && snOfDN.StartsWith("MID:<SN>", true, CultureInfo.InvariantCulture)) {
                                            ctx.Data.Add(UKEYSN, snOfDN.Substring(8)); // let it blow up if UKEYSN is ambiguous
                                        };
                                    } else {
                                        ctx.Data.Add(UKEYSN, propertyValue.ToString()); // let it blow up if UKEYSN is ambiguous
                                    }
                                }
                            }
                        }
                        //EventLog.WriteEntry(EVENTLOGSource, "Found user " + upn + " using " + ds.Filter +
                        //    " with properties " + cfgAdfs.AdAttrMobile + "=" + msisdn + "," + cfgAdfs.AdAttrMidSerialNumber + "=" + snOfDN);
                        logger.TraceEvent(TraceEventType.Verbose, 0, "AdSearch.Found: upn=" + upn + ", filter=" + ds.Filter
                            + ", " + cfgAdfs.AdAttrMobile + "=" + msisdn + ", " + cfgAdfs.AdAttrMidSerialNumber + "=" + snOfDN);
                        Logging.Log.AdSearch(upn, ds.Filter, cfgAdfs.AdAttrMobile, msisdn, cfgAdfs.AdAttrMidSerialNumber, snOfDN);
                    }
                    else
                    {
                        // EventLog.WriteEntry(EVENTLOGSource, "User not found " + upn + " using " + ds.Filter, EventLogEntryType.Error, 102);
                        logger.TraceEvent(TraceEventType.Warning, 0, "User not found in AD: upn=" + upn + ", ldapFilter=" + ds.Filter);
                        Logging.Log.AttrUserNotFound(upn, ds.Filter);
                    }
                    ds.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.TraceEvent(TraceEventType.Error, 0, "AD Search Error: " + ex.Message);
                Logging.Log.AdSearchError(ex.Message);
                return false;
            }

            if (String.IsNullOrEmpty(msisdn))
            {
                // EventLog.WriteEntry(EVENTLOGSource, "Method not available for user " + upn + " (no MSISN requireExistence)", EventLogEntryType.Error, 102);
                logger.TraceEvent(TraceEventType.Warning, 0, "Mobile ID not available for " + upn + ": mobile attribute not found in AD");
                Logging.Log.AttrMobileNotFound(upn);
                return false;
            }
            if (String.IsNullOrEmpty(snOfDN) && ! cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowAbsence))
            {
                logger.TraceEvent(TraceEventType.Information, 0, "Serial Number not found for " + upn);
                Logging.Log.AttrUserSerialNumberNotFound(upn);
                return false;
            }

            // store "session"-scope information to ctx. The life time of a "session" is identical with the lifetime of ctx.
            // It seems to begin with a BeginAuthentication(...), continue with 0+ TryEndAuthentication(...), 
            // ends when (a) TryEndAuthentication(...) returns null and claim, or (b) on browser closure, or (c) on timeout.
            ctx.Data.Add(USERUPN, upn);
            return true;
        }

        // Authentication starts here, UPN of primary login is passed as Claim. Called once per ADFS-login-session.
        public IAdapterPresentation BeginAuthentication(Claim identityClaim, HttpListenerRequest reqHttp, IAuthenticationContext ctx)
        {
            logger.TraceEvent(TraceEventType.Verbose, 0, "BeginAuthentication(claim=" + _str(identityClaim) + ", req=" + _str(reqHttp) + ", ctx=" + _str(ctx) + ")");
            CultureInfo culture = new CultureInfo(ctx.Lcid);
            string uiTrxId = MobileId.Util.BuildRandomBase64Chars(cfgAdfs.LoginNonceLength);
            bool needCheckUserSerialNumber = 
                ! cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowAbsence) ||
                ! cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowMismatch) ||
                cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.warnMismatch) ;

            // Start the asynchrous login
            AuthRequestDto req = new AuthRequestDto();
            string upn = identityClaim.Value; // for audit only

            try {
                req.PhoneNumber = (string)ctx.Data[MSISDN];
                req.UserLanguage = (UserLanguage)Enum.Parse(typeof(UserLanguage), resMgr.GetString(RES_LANG, culture));
                req.DataToBeSigned = _buildMobileIdLoginPrompt(req.UserLanguage, culture, uiTrxId);
                req.TimeOut = cfgMid.RequestTimeOutSeconds;
                if (needCheckUserSerialNumber /* cfgMid.UserSerialNumberPolicy != UserSerialNumberPolicy.ignore */ && ctx.Data.ContainsKey(UKEYSN))
                    req.UserSerialNumber = (string)ctx.Data[UKEYSN];
            } catch (Exception e) {
                Logging.Log.ConfigError("upn:\"" + upn + "\", err:\"" + e.Message + "\"");
                throw e;
                // return new AdapterPresentation(AuthView.AuthError, cfgAdfs);
            }
            ctx.Data.Add(AUTHBEGIN, DateTime.UtcNow.Ticks / 10000);
            ctx.Data.Add(SESSBEGIN, DateTime.UtcNow.Ticks / 10000);
            AuthResponseDto rsp;
            try {
                rsp = getWebClient().RequestSignature(req, true /* async */);
            } 
            catch (Exception e) {
                Logging.Log.AuthenticationTechnicalError(0, 0, upn, null, null, e.ToString());
                throw e;
                // return new AdapterPresentation(AuthView.AuthError, cfgAdfs);
            };
            ctx.Data.Add(SESSTRIES, 1);
            string logMsg = "svcStatus:" + (int)rsp.Status.Code + ", mssTransId:\"" + rsp.MsspTransId + "\", state:";

            switch (rsp.Status.Code)
            {
                case ServiceStatusCode.VALID_SIGNATURE:
                case ServiceStatusCode.SIGNATURE:
                    ctx.Data.Add(STATE, 3);
                    ctx.Data.Add(MSSPTRXID, rsp.MsspTransId);
                    logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + "3");
                    Logging.Log.AuthenticationSuccess(0, 3, upn, rsp.MsspTransId);
                    return new AdapterPresentation(AuthView.TransferCtx, cfgAdfs);
                case ServiceStatusCode.REQUEST_OK:
                    ctx.Data.Add(STATE, 1);
                    ctx.Data.Add(MSSPTRXID, rsp.MsspTransId);
                    ctx.Data.Add(DTBS, req.DataToBeSigned);
                    logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + "1");
                    Logging.Log.AuthenticationContinue(0, 1, upn, rsp.MsspTransId);
                    return new AdapterPresentation(AuthView.SignRequestSent, cfgAdfs, req.PhoneNumber, uiTrxId, cfgMid.PollResponseDelaySeconds*1000);
                case ServiceStatusCode.USER_CANCEL:
                    ctx.Data.Add(STATE, 4);
                    logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + "4");
                    Logging.Log.AuthenticationCancel(0, 4, upn, rsp.MsspTransId);
                    return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                case ServiceStatusCode.EXPIRED_TRANSACTION: // reserved mobileid can return this status immdidately
                case ServiceStatusCode.PB_SIGNATURE_PROCESS:
                    ctx.Data.Add(STATE, 5);
                    logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + "5");
                    Logging.Log.AuthenticationFail(0, 5, upn, rsp.MsspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code));
                    return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                default:
                    ctx.Data.Add(STATE, 2);
                    logger.TraceEvent((rsp.Status.Color == ServiceStatusColor.Yellow ? TraceEventType.Warning : TraceEventType.Error),
                        0, logMsg + "2, errMsg:\"" + rsp.Status.Message + "\", errDetail:\"" + rsp.Detail + "\"");
                    if (rsp.Status.Color == ServiceStatusColor.Yellow || rsp.Status.Color == ServiceStatusColor.Green) {
                        Logging.Log.AuthenticationFail(0, 2, upn, rsp.MsspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code));
                    } else {
                        Logging.Log.AuthenticationTechnicalError(0, 2, upn, rsp.MsspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code), (string)rsp.Detail);
                    }
                    string s = rsp.Status.GetDisplayMessage(ctx.Lcid);
                    if (String.IsNullOrEmpty(s)) {
                        logger.TraceEvent(TraceEventType.Warning, 0, "resource undef for {mssCode:" + (int) rsp.Status.Code + ", lcid:" + ctx.Lcid + "}");
                        Logging.Log.PresentationWarning("RESOURCE_UNDEF", "{mssCode:" + (int)rsp.Status.Code + ", lcid:" + ctx.Lcid + "}");
                        s = (string)rsp.Detail;
                    };
                    return new AdapterPresentation(AuthView.AuthError, cfgAdfs, rsp);
            }
        }

        public IAuthenticationAdapterMetadata Metadata
        {
            get { return new AuthenticationAdapterMetadata(); }
        }

        // Called when the authentication provider is loaded by AD FS into it's pipeline.
        // This is where AD FS passes us the config data as a Stream, if such data was supplied at registration of the adapter
        public void OnAuthenticationPipelineLoad(IAuthenticationMethodConfigData configData)
        {
            int id =  System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
            logger.TraceEvent(TraceEventType.Verbose, 0, "OnAuthenticationPipelineLoad(verAdapter={0}, obj={1})",
                AuthenticationAdapterMetadata.VERSION, id);
            Logging.Log.LoadAuthProviderStart(id, AuthenticationAdapterMetadata.VERSION);

            if (configData.Data != null)
            {
                try
                {
                    string cfgStr = (new System.IO.StreamReader(configData.Data)).ReadToEnd();
                    // logger.TraceEvent(TraceEventType.Verbose, 0, "Cfg:\n========\n" + cfgStr + "\n========\n");
                    configData.Data.Position = 0;
                    cfgMid = WebClientConfig.CreateConfig(cfgStr);
                    logger.TraceEvent(TraceEventType.Verbose, 0, "Config.Mid: " + cfgMid);
                    MobileId.Logging.Log.ConfigInfo(getWebClient().GetClientVersion(), cfgMid.ToString());
                    configData.Data.Position = 0;
                    cfgAdfs = AdfsConfig.CreateConfig(cfgStr);
                    logger.TraceEvent(TraceEventType.Verbose, 0, "Config.Adfs: " + cfgAdfs);
                    Logging.Log.ConfigInfo(AuthenticationAdapterMetadata.VERSION, cfgAdfs.ToString());
                }
                catch (Exception ex)
                {
                    logger.TraceData(TraceEventType.Error, 0, ex);
                    Logging.Log.ConfigError(ex.Message);
                    throw ex;
                }
            }
            else
            {
                Logging.Log.ConfigError("config is null");
                throw new ArgumentNullException("configData is null");
            }

            // Verify EventLog Source
            //if (!EventLog.SourceExists(EVENTLOGSource))
            //    EventLog.CreateEventSource(EVENTLOGSource, EVENTLOGGroup);
            //EventLog.WriteEntry(EVENTLOGSource, "Adapter loaded", EventLogEntryType.Information, 900);

            // The EventSources are created by the installer normally. If Mobile ID for ADFS was installed manually and
            // EventSource were not created, we will repair it here. It requires administrative privileges though.
            if (!EventLog.SourceExists("MobileId.Client"))
                EventLog.CreateEventSource("MobileId.Client", "Application");

            if (!EventLog.SourceExists("MobileId.Adfs"))
                EventLog.CreateEventSource("MobileId.Adfs", "Application");
        }

        // Called whenever the authentication provider is unloaded from the AD FS pipeline.
        public void OnAuthenticationPipelineUnload()
        {
            int id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
            logger.TraceEvent(TraceEventType.Verbose, 0, "OnAuthenticationPipelineUnload(obj={0})", id);
            Logging.Log.LoadAuthProviderStop(id);
            // EventLog.WriteEntry(EVENTLOGSource, "Adapter unloaded", EventLogEntryType.Information, 901);
            _webClient = null; // dispose _webClient
        }

        // Handle the errors during the authentication process during BeginAuthentication or TryEndAuthentication
        public IAdapterPresentation OnError(System.Net.HttpListenerRequest request, ExternalAuthenticationException ex)
        {
            logger.TraceData(TraceEventType.Error, 0, ex);
            Logging.Log.AuthenticationGeneralError(ex.Message);
            return new AdapterPresentation(AuthView.AuthError, cfgAdfs, ex.Message);
        }

        // Authentication should perform the actual authentication and return at least one Claim on success.
        // proofData contains a dictionnary of strings to objects that have been asked in the BeginAuthentication
        public IAdapterPresentation TryEndAuthentication(IAuthenticationContext ctx, IProofData proofData, System.Net.HttpListenerRequest request, out Claim[] claims)
        {
            string formAction,upn,msspTransId,logInfo;
            int state;
            if (proofData.Properties.ContainsKey("Retry")) {
                formAction = "Retry";
            } else {
                try {
                    formAction = (string)proofData.Properties["Action"];
                } catch (KeyNotFoundException) {
                    formAction = null;
                }
            };
            //if (formAction == null && proofData.Properties.ContainsKey("SignOut")) { 
            //    // if user modifies URL manually during a session, the Cancel action is not captured by ADFS but leaks to this method
            //    formAction = "SignOut";
            //};
            logger.TraceEvent(TraceEventType.Verbose, 0, "TryEndAuthentication(act:" + formAction + ", ctx:" + _str(ctx) + ", prf:" + _str(proofData) + ", req:" + _str(request));
            Logging.Log.TryEndAuthenticationStart(formAction, _str(ctx), _str(proofData), _str(request));
            CultureInfo culture = new CultureInfo(ctx.Lcid);

            upn = (string)ctx.Data[USERUPN];
            state = (int) ctx.Data[STATE];
            try
            {
                // msspTransId is expected to be absent in some error cases, e.g. error 107
                msspTransId = (string)ctx.Data[MSSPTRXID];
            }
            catch (KeyNotFoundException)
            {
                msspTransId = null;
            };
            logInfo = "upn:\"" + upn + "\", msspTransId:\"" + msspTransId + "\"";

            claims = null;
            if (formAction == "Continue")
            {
                switch (state)
                {
                    case 3:
                        logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_OK: " + logInfo + ", state:" + state);
                        Logging.Log.AuthenticationSuccess(state, (int)ctx.Data[STATE], upn, msspTransId);
                        claims = ClaimsHwToken;
                        return null;
                    case 1:
                    case 31:
                        // fall through for looping below
                        break;
                    default:
                        logger.TraceEvent(TraceEventType.Error, 0, "BAD_STATE: " + logInfo + ", state:" + state);
                        Logging.Log.AuthenticationFail(state, (int)ctx.Data[STATE], upn, msspTransId, "BAD_STATE");
                        return new AdapterPresentation(AuthView.AuthError, cfgAdfs, "action:\"Conitnue\"; state:" + state);
                }

                // check session age, i.e. timespan(Now, authBegin)
                int ageSeconds = (int)(( DateTime.UtcNow.Ticks / 10000 - (long) ctx.Data[AUTHBEGIN] ) / 1000);
                if ( ageSeconds >= cfgMid.RequestTimeOutSeconds) {
                    ctx.Data[STATE] = 13;
                    logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_TIMEOUT_CONT: " + logInfo + ", state:" + ctx.Data[STATE] + ", age:" + ageSeconds);
                    Logging.Log.AuthenticationTimeout(state, (int)ctx.Data[STATE], ageSeconds, upn, msspTransId);
                    return
                        ((int)ctx.Data[SESSTRIES] < cfgAdfs.SessionMaxTries) ?
                        new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, "Timeout.") : // TODO: construct new ErrorCode for easier I18N
                        new AdapterPresentation(AuthView.AuthError, cfgAdfs, "Timeout.");
                }
                
                AuthRequestDto req = new AuthRequestDto();
                req.PhoneNumber = (string)ctx.Data[MSISDN];
                req.DataToBeSigned = (string)ctx.Data[DTBS];
                bool needCheckUserSerialNumber =
                    !cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowAbsence) ||
                    !cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowMismatch) ||
                    cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.warnMismatch);

                if (needCheckUserSerialNumber /* cfgMid.UserSerialNumberPolicy != UserSerialNumberPolicy.ignore */ && ctx.Data.ContainsKey(UKEYSN))
                    req.UserSerialNumber = (string)ctx.Data[UKEYSN];
                AuthResponseDto rsp;
                for (int i = ageSeconds; i <= cfgMid.RequestTimeOutSeconds; i+= cfgMid.PollResponseIntervalSeconds ) { 
                    rsp = getWebClient().PollSignature(req, msspTransId);
                    switch (rsp.Status.Code)
                    {
                        case ServiceStatusCode.SIGNATURE:
                        case ServiceStatusCode.VALID_SIGNATURE:
                            ctx.Data[STATE] = 10;
                            logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_OK: " + logInfo + ", state:" + ctx.Data[STATE] + ", i:" + i);
                            Logging.Log.AuthenticationSuccess(state, (int)ctx.Data[STATE], upn, msspTransId);
                            // EventLog.WriteEntry(EVENTLOGSource, "Authentication success for " + upn, EventLogEntryType.SuccessAudit, 100);
                            claims = ClaimsHwToken;
                            return null;
                        case ServiceStatusCode.OUSTANDING_TRANSACTION:
                            ctx.Data[STATE] = 11;
                            logger.TraceEvent(TraceEventType.Verbose, 0, "AUTHN_PENDING: " + logInfo + ", state:" + ctx.Data[STATE] + ", i:" + i);
                            Logging.Log.AuthenticationPending(state, (int)ctx.Data[STATE], upn, msspTransId);
                            System.Threading.Thread.Sleep(1000);
                            break;
                        case ServiceStatusCode.EXPIRED_TRANSACTION:
                            ctx.Data[STATE] = 13;
                            logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_TIMEOUT_MID: " + logInfo + ", state:" + ctx.Data[STATE] + ", i:" + i);
                            Logging.Log.AuthenticationFail(state, (int)ctx.Data[STATE], upn, msspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code));
                            return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                        case ServiceStatusCode.PB_SIGNATURE_PROCESS:
                            ctx.Data[STATE] = 13;
                            logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_SIGN_PROCESS: " + logInfo + ", state:" + ctx.Data[STATE] + ", i:" + i);
                            Logging.Log.AuthenticationFail(state, (int)ctx.Data[STATE], upn, msspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code));
                            return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                        case ServiceStatusCode.USER_CANCEL:
                            ctx.Data[STATE] = 14;
                            logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_CANCEL: " + logInfo + ", state:" + ctx.Data[STATE] + ", i:" + i);
                            Logging.Log.AuthenticationCancel(state, (int)ctx.Data[STATE], upn, msspTransId);
                            return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                        default:
                            ctx.Data[STATE] = 12;
                            logger.TraceEvent(TraceEventType.Error, 0, "TECH_ERROR: " + logInfo + ", state:" + ctx.Data[STATE] + ", srvStatusCode:" + (int) rsp.Status.Code 
                                + ", srvStatusMsg:\"" + rsp.Status.Message + "\", srvStatusDetail:\"" + (string) rsp.Detail + "\"");
                            if (rsp.Status.Color == ServiceStatusColor.Yellow || rsp.Status.Color == ServiceStatusColor.Green) {
                                Logging.Log.AuthenticationFail(state, (int)ctx.Data[STATE], upn, msspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code));
                            } else {
                                Logging.Log.AuthenticationTechnicalError(state, (int)ctx.Data[STATE], upn, msspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code), (string)rsp.Detail);
                            };
                            return new AdapterPresentation(AuthView.AuthError, cfgAdfs, rsp);
                    }
                }; // for-loop

                ctx.Data[STATE] = 13;
                logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_TIMEOUT_ADFS: " + logInfo + ", state:" + ctx.Data[STATE]);
                Logging.Log.AuthenticationTimeout(state, (int)ctx.Data[STATE], cfgMid.RequestTimeOutSeconds, upn, msspTransId);
                return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, "Timeout.");

            }
            else if (formAction == "Retry")
            {
                switch (state)
                {
                    case 13:
                    case 5:
                    case 35:
                    case 4:
                    case 14:
                    case 34:
                        {   // check session age and number of retries
                            int ageSeconds = (int)((DateTime.UtcNow.Ticks / 10000 - (long) ctx.Data[SESSBEGIN]) / 1000);
                            if (ageSeconds >= cfgAdfs.SessionTimeoutSeconds) {
                                logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_SESSION_TIMEOUT: " + logInfo + ", state:" + ctx.Data[STATE] + ", age:" + ageSeconds);
                                Logging.Log.SessionTimeout(state, (int)ctx.Data[STATE], ageSeconds, upn, msspTransId);
                                ctx.Data[STATE] = 22;
                            } 
                            else if ((int)ctx.Data[SESSTRIES] >= cfgAdfs.SessionMaxTries) {
                                logger.TraceEvent(TraceEventType.Information, 0, "AUTHN_SESSION_OVERTRIES: " + logInfo + ", state:" + ctx.Data[STATE]);
                                Logging.Log.SessionTooMuchRetries(state, (int)ctx.Data[STATE], (int)ctx.Data[SESSTRIES], upn, msspTransId);
                                ctx.Data[STATE] = 22;
                            };
                            if ((int) ctx.Data[STATE] == 22) {
                                return new AdapterPresentation(AuthView.AutoLogout, cfgAdfs);
                            }
                        }
                        // start a new asynchronous RequestSignature
                        AuthRequestDto req = new AuthRequestDto();
                        req.PhoneNumber = (string) ctx.Data[MSISDN];
                        req.UserLanguage = (UserLanguage)Enum.Parse(typeof(UserLanguage), resMgr.GetString(RES_LANG, culture));
                        string uiTrxId = Util.BuildRandomBase64Chars(cfgAdfs.LoginNonceLength);
                        req.DataToBeSigned = _buildMobileIdLoginPrompt(req.UserLanguage, culture, uiTrxId);
                        req.TimeOut = cfgMid.RequestTimeOutSeconds;
                        bool needCheckUserSerialNumber =
                            !cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowAbsence) ||
                            !cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.allowMismatch) ||
                            cfgMid.UserSerialNumberPolicy.HasFlag(UserSerialNumberPolicy.warnMismatch);
                        if (needCheckUserSerialNumber /* cfgMid.UserSerialNumberPolicy != UserSerialNumberPolicy.ignore */ && ctx.Data.ContainsKey(UKEYSN))
                            req.UserSerialNumber =  (string)ctx.Data[UKEYSN];
                        ctx.Data[AUTHBEGIN] = DateTime.UtcNow.Ticks/10000;
                        AuthResponseDto rsp = getWebClient().RequestSignature(req, true /* async */);
                        ctx.Data[SESSTRIES] = (int) ctx.Data[SESSTRIES] + 1;
                        string logMsg = "svcStatus:" + (int) rsp.Status.Code + ", mssTransId:\"" + rsp.MsspTransId + "\", state:";

                        switch (rsp.Status.Code)
                        {
                            case ServiceStatusCode.VALID_SIGNATURE:
                            case ServiceStatusCode.SIGNATURE:
                                ctx.Data[STATE] = 33;
                                ctx.Data[MSSPTRXID] = rsp.MsspTransId;
                                logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + ctx.Data[STATE]);
                                Logging.Log.AuthenticationSuccess(state, (int)ctx.Data[STATE], upn, msspTransId);
                                return new AdapterPresentation(AuthView.TransferCtx, cfgAdfs);
                            case ServiceStatusCode.REQUEST_OK:
                                ctx.Data[STATE] = 31;
                                ctx.Data[MSSPTRXID] = rsp.MsspTransId;
                                ctx.Data[DTBS] = req.DataToBeSigned;
                                logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + ctx.Data[STATE]);
                                Logging.Log.AuthenticationContinue(state, (int)ctx.Data[STATE], upn, msspTransId);
                                return new AdapterPresentation(AuthView.SignRequestSent, cfgAdfs, req.PhoneNumber, uiTrxId, cfgMid.PollResponseDelaySeconds * 1000);
                            case ServiceStatusCode.USER_CANCEL:
                                ctx.Data[STATE] = 34;
                                logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + ctx.Data[STATE]);
                                Logging.Log.AuthenticationCancel(state, (int)ctx.Data[STATE], upn, msspTransId);
                                return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                            case ServiceStatusCode.EXPIRED_TRANSACTION:
                            case ServiceStatusCode.PB_SIGNATURE_PROCESS:
                                ctx.Data[STATE] = 35;
                                logger.TraceEvent(TraceEventType.Verbose, 0, logMsg + ctx.Data[STATE]);
                                Logging.Log.AuthenticationFail(state, (int)ctx.Data[STATE], upn, msspTransId, Enum.GetName(typeof(ServiceStatusCode), rsp.Status.Code));
                                return new AdapterPresentation(AuthView.RetryOrCancel, cfgAdfs, rsp);
                            default:
                                ctx.Data[STATE] = 32;
                                logger.TraceEvent((rsp.Status.Color == ServiceStatusColor.Yellow ? TraceEventType.Warning : TraceEventType.Error),
                                    0, logMsg + ctx.Data[STATE] + ", errMsg:\"" + rsp.Status.Message + "\", errDetail:\"" + rsp.Detail + "\"");
                                Logging.Log.AuthenticationTechnicalError(state, (int)ctx.Data[STATE], upn, msspTransId, Enum.GetName(typeof(ServiceStatusCode),rsp.Status.Code), rsp.Detail.ToString());
                                return new AdapterPresentation(AuthView.AuthError, cfgAdfs, rsp);
                        };
                    default:
                        logger.TraceEvent(TraceEventType.Error, 0, "BAD_STATE: " + logInfo + ", state:" + state);
                        Logging.Log.AuthenticationFail(state, (int)ctx.Data[STATE], upn, msspTransId, "BAD_STATE");
                        return new AdapterPresentation(AuthView.AuthError, cfgAdfs, "action:\"Retry\"; state:" + state);
                }
            }
            //else if (formAction == "SignOut")
            //{
            //    logger.TraceEvent(TraceEventType.Verbose, 0, "SIGNOUT: " + logInfo + "; state:" + state);
            //    return new AdapterPresentation(AuthView.AutoLogout, cfgAdfs); // could lead to endless-loop
            //}
            else
            {
                logger.TraceEvent(TraceEventType.Error, 0, "Unsupported formAction: " + formAction);
                Logging.Log.AuthenticationBadForm(state, (int)ctx.Data[STATE], upn, msspTransId, formAction);
                return new AdapterPresentation(AuthView.AuthError, cfgAdfs, new AuthResponseDto(ServiceStatusCode.GeneralClientError));
            }
        }
    }

}
