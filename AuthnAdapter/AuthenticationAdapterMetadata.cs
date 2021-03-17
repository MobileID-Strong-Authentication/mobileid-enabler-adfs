using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityServer.Web.Authentication.External;
using System.Globalization;

namespace MobileId.Adfs
{
    class AuthenticationAdapterMetadata : IAuthenticationAdapterMetadata
    {
        public static readonly string VERSION = "1.2";

        // Returns the name of the provider that will be shown in the AD FS management UI (not visible to end users)
        public string AdminName
        {
            get { return "Mobile ID Authentication v" + VERSION; }
        }

        // Returns an array of strings containing URIs indicating the set of authentication methods implemented by the adapter 
        // AD FS requires that, if authentication is successful, the method actually employed will be returned by the
        // final call to TryEndAuthentication(). If no authentication method is returnd, or the method returned is not
        // one of the methods listed in this property, the authentication attempt will fail.
        // Refer to  http://msdn.microsoft.com/en-us/library/microsoft.identitymodel.claims.authenticationmethods_members.aspx
        public string[] AuthenticationMethods
        {
            get { return new string[] { "http://schemas.microsoft.com/ws/2008/06/identity/authenticationmethod/hardwaretoken" }; }
        }

        // Returns an array indicating which languages are supported by the provider. AD FS uses this information
        // to determine the best language\locale to display to the user.
        public int[] AvailableLcids
        {
            get
            {
                return new[]
                {
                    new CultureInfo("en").LCID,
                    new CultureInfo("de").LCID,
                    new CultureInfo("fr").LCID,
                    new CultureInfo("it").LCID
                };
            }
        }

        // Returns a Dictionary containing the set of localized descriptions (hover over help) of the provider, indexed by lcid. 
        // These descriptions are displayed in the "choice page" offered to the user when there is more than one 
        // secondary authentication provider available.
        public Dictionary<int, string> Descriptions
        {
            get
            {
                Dictionary<int, string> result = new Dictionary<int, string>();
                result.Add(new CultureInfo("en").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                result.Add(new CultureInfo("de").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                result.Add(new CultureInfo("fr").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                result.Add(new CultureInfo("it").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                return result;
            }
        }

        // Returns a Dictionary containg the set of localized friendy names of the provider, indexed by lcid. 
        // These Friendly Names are displayed in the "choice page" offered to the user when there is more than 
        // one secondary authentication provider available.
        public Dictionary<int, string> FriendlyNames
        {
            get
            {
                Dictionary<int, string> result = new Dictionary<int, string>();
                result.Add(new CultureInfo("en").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                result.Add(new CultureInfo("de").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                result.Add(new CultureInfo("fr").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                result.Add(new CultureInfo("it").LCID, "Mobile ID Authentication Adapter v" + VERSION);
                return result;
            }
        }

        // Returns an array indicating the type of claim that that the adapter uses to identify the user being authenticated.
        // Note that although the property is an array, only the first element is currently used.
        // MUST BE ONE OF THE FOLLOWING
        // "http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname"
        // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn"
        // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
        // "http://schemas.microsoft.com/ws/2008/06/identity/claims/primarysid"
        public string[] IdentityClaims
        {
            get { return new string[] { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn" }; }
        }

        // All external providers must return a value of "true" for this property.
        public bool RequiresIdentity
        {
            get { return true; }
        }
    }
}
