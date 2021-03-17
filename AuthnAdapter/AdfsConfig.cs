using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;

namespace MobileId.Adfs
{
    public class AdfsConfig
    {
        ulong _webClientMaxRequest = 100;
        string _adAttrMobile = "mobile";  // LDAP attribute 0.9.2342.19200300.100.1.41, see https://msdn.microsoft.com/en-us/library/ms677119.aspx
        string _adAttrMidSerialNumber = "serialNumber".ToLower(); // LDAP attribute 2.5.4.5, see https://msdn.microsoft.com/en-us/library/ms679771.aspx
        // string _defaultLoginPrompt = "Login with Mobile ID ({0})?";
        bool _ssoOnCancel = false;
        int _sessionTimeoutSeconds = 300;
        int _sessionMaxTries = 5;
        bool _showDebugMsg = false;
        bool _expShowWSignOut = false;
        int _loginNonceLength = 5;

        Dictionary<UserLanguage, string> _loginPrompt = new Dictionary<UserLanguage, string>(); // override the text in resource

        /// <summary>
        /// A WebClient can be re-used to send requests. If the number of requests exceed this number, 
        /// the WebClient must be re-cycled (i.e. closed and re-created). Default is 100.
        /// </summary>
        public ulong WebClientMaxRequest { 
            get { return _webClientMaxRequest; }
            set {
                if (value > 0)
                    _webClientMaxRequest = value;
                else
                    throw new ArgumentOutOfRangeException("WebClientMaxRequest", value, "value must be positive"); 
            }
        }

        /// <summary>
        /// Name of AD attribute which contains the Mobile Number of the user.
        /// The name is case-insensitive and converted to lower case internally.
        /// If the AD attribute has multiple values, the last returned value will be used.
        /// Default is "mobile".
        /// </summary>
        public string AdAttrMobile { 
            get { return _adAttrMobile; }
            set {
                if (!String.IsNullOrWhiteSpace(value) && !value.Contains(" "))
                    _adAttrMobile = value.ToLower(System.Globalization.CultureInfo.InvariantCulture);
                else
                    throw new ArgumentOutOfRangeException("AdAttrMobile");
            }
        }

        /// <summary>
        /// Name of AD attribute which contains the Serial Number (e.g. "MID0123456789ABC") of the 
        /// Mobile ID Token. The Serial Number is part of the Subject of the Mobile ID Certificate.
        /// The AD attribute name is case-insensitive and converted to lower case internally.
        /// If the AD attribute has multiple values, the last returned value will be used.
        /// Default is "serialNumber".
        /// </summary>
        public string AdAttrMidSerialNumber {
            get { return _adAttrMidSerialNumber;}
            set {
                if (!String.IsNullOrWhiteSpace(value) && !value.Contains(" "))
                    _adAttrMidSerialNumber = value.ToLower(System.Globalization.CultureInfo.InvariantCulture);
                else
                    throw new ArgumentOutOfRangeException("AdAttrMidSerialNumber");
            }
        }

        ///// <summary>
        ///// Default text to be sent via Mobile ID Service to user's mobile phone.
        ///// The text may contain the place holder {0}, which will be expanded to 5-char random string.
        ///// The text must not exceed the maximum length acceptable by Mobile ID Service (239 chars if encodable in gsm338 charset, 119 otherwise).
        ///// </summary>
        //public string DefaultLoginPrompt {
        //    get { return _defaultLoginPrompt; }
        //    set { if (!String.IsNullOrEmpty(value)) {
        //        int maxLength = MobileId.Util.maxDtbsLength(value);
        //        if (value.Length <= (value.Contains("{0}") ? maxLength-2 : maxLength)) {
        //            // {0} will be expaned to a 5-char string
        //            _defaultLoginPrompt = value;
        //        };
        //    }}
        //}

        /// <summary>
        /// If true, the Cancel button in Sign In pages will initiate a Single Sign Out of all sites.
        /// Otherwise, the button will initiate a Local Sign Out.
        /// </summary>
        public bool SsoOnCancel {
            get { return _ssoOnCancel; }
            set { _ssoOnCancel = value;}
        }

        /// <summary>
        /// Experimental feature: Enable the WSignout button in the Retry-Or-Cancel page if this property is true.
        /// </summary>
        public bool ExpShowWSignOut {
            get { return _expShowWSignOut; }
            set { _expShowWSignOut = value; }
        }

        /// <summary>
        /// Maximum duration of an Mobile ID authentication session (0 or more (re)tries)
        /// </summary>
        public int SessionTimeoutSeconds {
            get { return _sessionTimeoutSeconds; }
            set {
                if (value > 0)
                    _sessionTimeoutSeconds = value;
                else
                    throw new ArgumentOutOfRangeException("SessionTimeoutSeconds", value, "value must be positive");
            }
        }

        /// <summary>
        /// Maximum number of tries (i.e. invocation of MobileId.IAuthentication.RequestSignature) during an Mobile ID authentication session
        /// </summary>
        public int SessionMaxTries {
            get { return _sessionMaxTries;}
            set {
                if (value > 0)
                    _sessionMaxTries = value;
                else
                    throw new ArgumentOutOfRangeException("SessionMaxTries", value, "value must be positive");
            }
        }

        /// <summary>
        /// If true, display verbose messages in web browser in case of error; otherwise, less error details are leaked in browser in case of error.
        /// </summary>
        public bool ShowDebugMsg {
            get { return _showDebugMsg; }
            set { _showDebugMsg = value; }
        }

        /// <summary>
        /// Length of the unique not-guessable string that should be included in the message sent to mobile device.
        /// </summary>
        public int LoginNonceLength {
            get { return _loginNonceLength; }
            set {
                if (value > 0 && value < AuthRequestDto.MAX_UTF8_CHARS_DTBS)
                    _loginNonceLength = value;
                else
                    throw new ArgumentOutOfRangeException("LoginNonceLength", value, "value must be between 1 and " + AuthRequestDto.MAX_UTF8_CHARS_DTBS);
            }
        }

        /// <summary>
        /// Return the login prompt text for the specified language, excluding the language-independent prefix.
        /// The text (and a prepended prefix) will be displayed in user's mobile device.
        /// </summary>
        /// <param name="language">one of the supported language</param>
        /// <returns></returns>
        public string GetLoginPrompt(UserLanguage language) {
            string s;
            return (_loginPrompt.TryGetValue(language, out s)) ? s : null;
        }

        public void SetLoginPrompt(UserLanguage language, string value) {
            if (value != null)
            {
                _loginPrompt[language] = value;
            }
        }

        public static AdfsConfig CreateConfig(TextReader cfgStream)
        {
            if (cfgStream == null)
            {
                throw new ArgumentNullException("input stream is null");
            }

            AdfsConfig cfg = new AdfsConfig();
            XmlReaderSettings xmlSetting = new XmlReaderSettings();
            xmlSetting.CloseInput = true;
            xmlSetting.IgnoreProcessingInstructions = true;
            xmlSetting.IgnoreWhitespace = true;
            using (XmlReader xml = XmlReader.Create(cfgStream, xmlSetting))
            {
                String s;
                while (xml.Read())
                {
                    // we process only attributes of the <mobileIdAdfs .../> element and ignore everything else
                    if (xml.Name == "mobileIdAdfs")
                    {
                        if (!String.IsNullOrWhiteSpace(s = xml["AdAttrMobile"]))
                           cfg.AdAttrMobile = s;
                        if (!String.IsNullOrWhiteSpace(s = xml["WebClientMaxRequest"]))
                           cfg.WebClientMaxRequest = ulong.Parse(s);
                        if (!String.IsNullOrWhiteSpace(s = xml["AdAttrMidSerialNumber"]))
                           cfg.AdAttrMidSerialNumber = s;
                        // cfg.DefaultLoginPrompt = xml["DefaultLoginPrompt"]; // TODO: deprecated
                        if (!String.IsNullOrWhiteSpace(s = xml["SsoOnCancel"]))
                            cfg.SsoOnCancel = bool.Parse(s);
                        if (!String.IsNullOrWhiteSpace(s = xml["SessionTimeoutSeconds"]))
                            cfg.SessionTimeoutSeconds = int.Parse(s);
                        if (!String.IsNullOrWhiteSpace(s = xml["SessionMaxTries"]))
                            cfg.SessionMaxTries = int.Parse(s);
                        if (!String.IsNullOrWhiteSpace(s = xml["ShowDebugMsg"]))
                            cfg.ShowDebugMsg = bool.Parse(s);
                        if (!String.IsNullOrWhiteSpace(s = xml["ExpShowWSignout"]))
                            cfg.ExpShowWSignOut = bool.Parse(s);
                        if (!String.IsNullOrWhiteSpace(s = xml["LoginNonceLength"]))
                            cfg.LoginNonceLength = int.Parse(s);
                        foreach (UserLanguage lang in new UserLanguage[] {UserLanguage.en, 
                            UserLanguage.de, UserLanguage.fr, UserLanguage.it}) {
                            cfg.SetLoginPrompt(lang, xml["LoginPrompt." + lang]);
                        };
                        // TODO: update on change
                        break;
                    }
                }
                xml.Close();
            }
            return cfg;
        }

        public static AdfsConfig CreateConfig(string cfgContent)
        {
            if (String.IsNullOrWhiteSpace(cfgContent))
                throw new ArgumentNullException("cfgContent is null or whitespace");
            using (TextReader stream = new StringReader(cfgContent))
            {
                return CreateConfig(stream);
            }

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(256); // TODO: update on change
            // sorted alphabetically in name. No output for experimental features.
            sb.Append("{AdAttrMobile: \"").Append(_adAttrMobile);
            sb.Append("\"; AdAttrMidSerialNumber: \"").Append(_adAttrMidSerialNumber);
            // sb.Append("\"; DefaultLoginPrompt: \"").Append(_defaultLoginPrompt);
            sb.Append("\"; LoginNonceLength: ").Append(_loginNonceLength);
            sb.Append("; SessionMaxTries: ").Append(_sessionMaxTries);
            sb.Append("; SessionTimeoutSeconds: ").Append(_sessionTimeoutSeconds);
            sb.Append("; ShowDebugMsg: ").Append(_showDebugMsg);
            sb.Append("; SsoOnCancel: ").Append(_ssoOnCancel);
            sb.Append("; WebClientMaxRequest: ").Append(_webClientMaxRequest);
            sb.Append("}");
            // TODO: update on change
            return sb.ToString();
        }
    }
}
