using System;
using System.Diagnostics;
using Microsoft.IdentityServer.Web.Authentication.External;
using System.Resources;
using System.Globalization;
using System.Text.RegularExpressions;
using MobileId;
using System.Collections.Generic;

namespace MobileId.Adfs
{
    class AdapterPresentation : IAdapterPresentation, IAdapterPresentationForm
    {
        private static TraceSource logger = new TraceSource("MobileId.Adfs.AuthnAdapter");
        static ResourceManager resMgr = new ResourceManager("MobileId.Adfs.WebUI", typeof(AdapterPresentation).Assembly);

        // keys for resource
        private const string RES_LANGUAGE = "AppletLanguage";
        private const string RES_WEB_TITLE = "LoginPageTitle";
        private const string RES_WEB_CHK_MOBILE = "CheckYourMobile";
        private const string RES_CANCEL_BUTTON = "CancelButton";
        private const string RES_RETRY_BUTTON = "RetryButton";

        private AuthView viewId;        // determines which message should be should
        private string param;           // additional parameter, semantics depends on viewId
        private string param2;          // additional parameter, semantics depends on viewId
        private int intParam;           // additional parameter, semantics depends on viewId
        private AdfsConfig adfsConfig;
//        private ServiceStatus rspStatus;
        private AuthResponseDto rspDto;

        public AdapterPresentation(AuthView currentState, AdfsConfig adfsConfig)
        {
            viewId = currentState;
            this.adfsConfig = adfsConfig;
            param = null;
            rspDto = null;
        }

        public AdapterPresentation(AuthView currentState, AdfsConfig adfsConfig, string param, string param2, int intParam)
        {
            viewId = currentState;
            this.adfsConfig = adfsConfig;
            this.param = param;
            this.param2 = param2;
            this.intParam = intParam;
            rspDto = null;
        }

        public AdapterPresentation(AuthView currentState, AdfsConfig adfsConfig, string param)
        {
            viewId = currentState;
            this.adfsConfig = adfsConfig;
            this.param = param;
            rspDto = null;
        }

        //public AdapterPresentation(AuthView currentState, AdfsConfig adfsConfig, ServiceStatus svcStatus, string svcDetail)
        //{
        //    viewId = currentState;
        //    this.adfsConfig = adfsConfig;
        //    rspStatus = svcStatus;
        //    param = svcDetail;
        //    rspDto = null;
        //}

        public AdapterPresentation(AuthView currentState, AdfsConfig adfsConfig, AuthResponseDto rspDto)
        {
            viewId = currentState;
            this.adfsConfig = adfsConfig;
//            rspStatus = rspDto.Status;
            this.rspDto = rspDto;
            param = (string) rspDto.Detail;
        }

        // MS API: Returns the title string for the web page which presents the HTML form content to the end user
        public string GetPageTitle(int lcid)
        {
            return resMgr.GetString(RES_WEB_TITLE, new CultureInfo(lcid)); // "Login with Mobile ID"
        }

        private string _buildErrorMessage(int lcid)
        {
            string s;
            ServiceStatus rspStatus = (this.rspDto != null) ? this.rspDto.Status : null;
            if (rspStatus != null)
            {
                s = rspStatus.GetDisplayMessage(lcid);
                if (string.IsNullOrEmpty(s))
                {
                    s = adfsConfig.ShowDebugMsg
                        ? rspStatus.Code + " (" + rspStatus.Message + ")</p><p>" + this.param
                        : ServiceStatus.GetDefaultErrorMessage(lcid);
                }
            }
            else
            {
                s = this.param;
            };
            string portalUrl = null;
            if ((this.rspDto != null) && this.rspDto.Extensions.ContainsKey(AuthResponseExtension.UserAssistencePortalUrl))
            {
                portalUrl = (string)this.rspDto.Extensions[AuthResponseExtension.UserAssistencePortalUrl];
                if (!string.IsNullOrWhiteSpace(portalUrl)) {
                    portalUrl += "&lang=" + resMgr.GetString(RES_LANGUAGE, new CultureInfo(lcid));
                    s = new Regex("#PortalUrl#", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Replace(s, portalUrl);
                }
            };
            if (string.IsNullOrWhiteSpace(portalUrl)) { // remove placeholder and <a href=...> tag around it
                s = new Regex(@"<a\s+[^>]*href=.#PortalUrl#.[^>]*>([^<]*)</a>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Replace(s, "$1"); // <a href="#PortalUrl#">foo</a> => foo
                s = new Regex("#PortalUrl#", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Replace(s, "");
            };
            return s;
        }

        private const string loginFormCommonHtml = @"<form method=""post"" id=""midLoginForm""><input id=""context"" type=""hidden"" name=""Context"" value=""%Context%""/>";
        // The next string is documented as a required field in MSDN, but provokes "duplicated authMethod field" server error response in ADFS 3.5.
        // <input id=""authMethod"" type=""hidden"" name=""AuthMethod"" value=""%AuthMethod%""/>"  
            
        // MS API: Returns the HTML Form fragment that contains the adapter user interface. This data will be included in the web page that is presented
        // to the cient.
        public string GetFormHtml(int lcid)
        {
            string s,ret;
            CultureInfo culture = new CultureInfo(lcid);
            switch (this.viewId)
            {
                case AuthView.SignRequestSent: // required params in constructor (mobid_nr, trans_id, poll_delay_millisec)
                    s = resMgr.GetString(RES_WEB_CHK_MOBILE, culture);
                    s = System.Net.WebUtility.HtmlEncode(s);
                    if (this.param != null) s = new Regex("#MobileNumber#", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Replace(s, this.param);
                    if (this.param2 != null) s = new Regex("#TransId#", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Replace(s, this.param2);
                    
                    return "<p>" + s + "</p>" + loginFormCommonHtml // A Mobile ID message has been sent to " + this.param
//                        + ". Please follow the instructions on the mobile phone.</p>" + loginFormCommonHtml
// TODO: optionally embed spin.js inline
+ @"<div class=""submitMargin"" id=""mid_Continue""><input id=""midContinueButton"" type=""submit"" name=""Action"" value=""Continue""/></div></form>
<script>
document.getElementById('mid_Continue').style.visibility='hidden';
window.setTimeout(function continueMobileIdAuth() {document.getElementById('midContinueButton').click();}," + intParam + @");
</script>
<div id=""midSpin""></div>
<script src=""/adfs/portal/script/spin.js""></script>
<script>new Spinner({lines:13, length:22, width:11, radius:25, corners:1, rotate:0,
  direction:1, color:'#000', speed:1, trail:57, shadow:false, hwaccel:false, 
  className:'spinner', zIndex:2e9, top:'70%', left:'45%'})
.spin(document.getElementById('midSpin'));
</script>";

                case AuthView.AuthError: // 2 possible invocation: (ServiceStatus, string), (string)
                    s = _buildErrorMessage(lcid);
                    return loginFormCommonHtml 
+ @"<input name=""" + (this.adfsConfig.SsoOnCancel ? "Single" : "Local") + @"SignOut"" type=""hidden"" checked=""checked"" value=""Sign Out""/>
<div class=""submitMargin error""><p>" + s + @"</p></div>
<div class=""submitMargin""><input name=""SignOut"" class=""submit"" id=""midSignOutButton"" type=""submit"" value=""" + ( resMgr.GetString(RES_CANCEL_BUTTON, culture) ) + @"""/></div>
</form>";

                case AuthView.TransferCtx:
                    return loginFormCommonHtml +
                        @"<div class=""submitMargin"" id=""mid_Continue""><input id=""midContinueButton"" type=""submit"" name=""Action"" value=""Continue""/></div></form>
<script>
document.getElementById('mid_Continue').style.visibility='hidden';
document.getElementById('midContinueButton').click();
</script>";
                case AuthView.AutoLogout:
                    return @"<form id=""midLogoutForm"" method=""post""><input id=""context"" type=""hidden"" name=""Context"" value=""%Context%""/>
<input name=""" + (this.adfsConfig.SsoOnCancel ? "Single" : "Local") + @"SignOut"" type=""hidden"" checked=""checked"" value=""Sign Out""/>
<input name=""SignOut"" class=""submit"" id=""midSignOutButton"" type=""submit"" value=""Sign Out""/>
</form>
<script>
document.getElementById('midSignOutButton').click();
</script>
";
                case AuthView.RetryOrCancel: // 2 possible invocation: (ServiceStatus, string), (string)
                    s = _buildErrorMessage(lcid);
                    ret = @"<script>
function onClickMidRetry() {document.getElementById('midHiddenSignOut').disabled=true;}
</script>
" + loginFormCommonHtml
+ @"<input name=""" + (this.adfsConfig.SsoOnCancel ? "Single" : "Local") + @"SignOut"" type=""hidden"" id=""midHiddenSignOut"" checked=""checked"" value=""Sign Out""/>
<div class=""submitMargin error""><p>" + s + @"</p></div>
<div class=""submitMargin""><input name=""SignOut"" class=""submit"" id=""midSignOutButton"" type=""submit"" value=""" + (resMgr.GetString(RES_CANCEL_BUTTON, culture) /* Cancel Login */) + @"""/>
&nbsp;<input name=""Retry"" class=""submit"" id=""midActionButton"" onclick=""onClickMidRetry()"" type=""submit"" value=""" + (resMgr.GetString(RES_RETRY_BUTTON, culture) /* Retry */) + @"""/>
</div></form>";
                    if (this.adfsConfig.ExpShowWSignOut)
                        ret += @"<form action=""/adfs/ls/?ws=wsignout1.0"" method=""post""><div class=""submitMargin"">
<input name=""WSignOut"" class=""submit"" id=""midWSignOutButton"" type=""submit"" value=""WSignOut""/>
</div></form>
";
                    return ret;
                default:
                    throw new NotSupportedException("AuthView " + this.viewId);
            };
        }

        // Return any external resources, ie references to libraries etc., that should be included in 
        // the HEAD section of the presentation form html. 
        public string GetFormPreRenderHtml(int lcid)
        {
            return "";
        }
    }

    /// <summary>
    ///  viewId relevant for presentation
    /// </summary>
    public enum AuthView
    {
        SignRequestSent = 1,
        TransferCtx = 2,
        RetryOrCancel = 3,
        AutoLogout = 4,
        AuthError = 9
    }

}
