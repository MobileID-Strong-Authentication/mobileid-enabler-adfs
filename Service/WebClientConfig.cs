using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MobileId
{
    //    public class WebClientConfig : ConfigurationSection (does not work)

    /// <summary>
    /// Encapsulation of all configuration parameters needed by WebClientImpl
    /// </summary>
    public class WebClientConfig
    {
        // mandatory input from caller
        string _apId = null;
        string _sslCertThumbprint = null;

        // optional input from caller
        string _sslCaCertDN = "CN=Swisscom Root CA 2, OU=Digital Certificate Services, O=Swisscom, C=ch";
        string _signRespCertFiles = string.Empty;
        StoreLocation _sslKeyStore = StoreLocation.CurrentUser;
        UserLanguage _userLanguageDefault = UserLanguage.en;
        string _serviceUrlPrefix = "https://mobileid.swisscom.com/soap/services/";
        string _dtbsPrefix = "";
        bool _srvSideValidation = false;
        int _requestTimeOutSeconds = 80;
        string _signatureProfile = "http://mid.swisscom.ch/MID/v1/AuthProfile1";
        string _seedApTransId = "Some ASCII text to be used to build the unique AP_TransId in request";
        bool _enableSubscriberInfo = false;
        bool _ignoreUserSn = false;
        bool _ignoreUserSnChange = false;
        int _pollResponseDelaySeconds = 3;
        int _pollResponseIntervalSeconds = 1;
        UserSerialNumberPolicy _userSericalNumberPolicy = UserSerialNumberPolicy.allowAbsence | UserSerialNumberPolicy.allowMismatch;
        bool _disableSignRespValidation = false;
        bool _disableSignRespCertValidation = false;
        bool _sanitizePhoneNumber = false;
        Regex _sanitizePhoneNumberRegex = new Regex("\\D", RegexOptions.Compiled, TimeSpan.FromSeconds(1.0));
        string _sanitizePhoneNumberReplacement = "";
        SecurityProtocolType _securityProtocolType = SecurityProtocolType.Tls;

        public static WebClientConfig CreateConfigFromFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException("config fileName is null or empty");
            }
            using (FileStream fs = File.OpenRead(fileName))
            using (TextReader tr = new StreamReader(fs))
            {
                return CreateConfig(tr);
            };
        }

        public static WebClientConfig CreateConfig(string cfgContent)
        {
            if (String.IsNullOrEmpty(cfgContent))
                throw new ArgumentNullException("cfgContent is null or empty");
            using (TextReader stream = new StringReader(cfgContent)) {
                return CreateConfig(stream);
            }
        }

        public static WebClientConfig CreateConfig(TextReader cfgStream)
        {
            if (cfgStream == null)
            {
                throw new ArgumentNullException("input stream is null");
            }
            WebClientConfig cfg = new WebClientConfig();

            XmlReaderSettings xmlSetting = new XmlReaderSettings();
            xmlSetting.CloseInput = true;
            xmlSetting.IgnoreProcessingInstructions = true;
            xmlSetting.IgnoreWhitespace = true;
            using (XmlReader xml = XmlReader.Create(cfgStream, xmlSetting))
            {
                String s;
                while (xml.Read())
                {
                    // we process only the <mobileIdClient .../> element
                    if (xml.Name == "mobileIdClient")
                    {
                        // Warning: Due to input validation, the processing order of the attributes may be important.
                        cfg.ApId = xml["AP_ID"];
                        cfg.DtbsPrefix = xml["DtbsPrefix"];
                        if (!string.IsNullOrWhiteSpace(s = xml["RequestTimeOutSeconds"]))
                            cfg.RequestTimeOutSeconds = int.Parse(s);
                        cfg.ServiceUrlPrefix = xml["ServiceUrlPrefix"];
                        cfg.SignatureProfile = xml["SignatureProfile"];
                        if (!string.IsNullOrEmpty(s = xml["SrvSideValidation"]))
                            cfg.SrvSideValidation = bool.Parse(s);
                        cfg.SslCertThumbprint = xml["SslCertThumbprint"];
                        if (!string.IsNullOrEmpty(s = xml["SslKeystore"]))
                            cfg.SslKeystore = Util.ParseKeyStoreLocation(s);
                        if (!string.IsNullOrEmpty(s = xml["SslRootCaCertDN"]))
                            cfg.SslRootCaCertDN = s;
                        if (!string.IsNullOrWhiteSpace(s = xml["SignRespCertFiles"]))
                            cfg.SignRespCertFiles = s;
                        if (!string.IsNullOrEmpty(s = xml["EnableSubscriberInfo"]))
                            cfg.EnableSubscriberInfo = Boolean.Parse(s);
                        cfg.SeedApTransId = xml["SeedApTransId"];
                        // PollResponse* must be parsed after RequestTimeOutSeconds due to input validation
                        if (!string.IsNullOrWhiteSpace(s = xml["PollResponseDelaySeconds"]))
                            cfg.PollResponseDelaySeconds = int.Parse(s);
                        if (!string.IsNullOrWhiteSpace(s = xml["PollResponseIntervalSeconds"]))
                            cfg.PollResponseIntervalSeconds = int.Parse(s);
                        if (!string.IsNullOrWhiteSpace(s = xml["UserSerialNumberPolicy"]))
                            cfg.UserSerialNumberPolicy = (UserSerialNumberPolicy)Enum.Parse(typeof(UserSerialNumberPolicy), s, true);
                        if (!string.IsNullOrWhiteSpace(s = xml["DisableSignRespValidation"]))
                            cfg.DisableSignRespValidation = Boolean.Parse(s);
                        if (!string.IsNullOrWhiteSpace(s = xml["DisableSignRespCertValidation"]))
                            cfg.DisableSignRespCertValidation = Boolean.Parse(s);
                        if ((s = xml["SanitizePhoneNumber"]) != null)
                            cfg.SanitizePhoneNumber = bool.Parse(s);
                        if (cfg.SanitizePhoneNumber)
                        {
                            if ((s = xml["SanitizePhoneNumberPattern"]) != null)
                                cfg.SanitizePhoneNumberPattern = s;
                            if ((s = xml["SanitizePhoneNumberReplacement"]) != null)
                                cfg.SanitizePhoneNumberReplacement = s;
                        };
                        if (!string.IsNullOrWhiteSpace(s = xml["SecurityProtocolType"]))
                            cfg.SecurityProtocolType = (SecurityProtocolType)Enum.Parse(typeof(SecurityProtocolType), s, true);

                        break;
                    }
                }
                xml.Close();
            }

            return cfg;
        }

        // TODO: input validation of config parameters

        /// <summary>
        /// AP_ID of the client, s. Reference Guide of Mobile ID
        /// </summary>
        [ConfigurationProperty("ApId", IsRequired = true)]
        public string ApId { 
            get {
                return _apId;
            } 
            set {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException("ApId is null or empty");
                if (! Util.IsXmlSafe(value))
                    throw new ArgumentException("ApId contains bad chars");
                _apId = value;
            }
        }

        public UserLanguage UserLanguageDefault 
        { 
            get { return _userLanguageDefault; }
            set { _userLanguageDefault = value; } 
        }

        [ConfigurationProperty("SslKeystore", IsRequired = false, DefaultValue = "CurrentUser")]
        public StoreLocation SslKeystore
        { 
            get {return _sslKeyStore;}
            set {
                switch (value.ToString())
                {
                    case "CurrentUser" : _sslKeyStore = StoreLocation.CurrentUser; break;
                    case "LocalMachine" : _sslKeyStore = StoreLocation.LocalMachine; break;
                    default: throw new ArgumentOutOfRangeException("SslKeystore is neither 'CurrentUser' nor 'LocalMachine'");
                }
            }
        }

        [ConfigurationProperty("SignatureProfile", IsRequired = false, DefaultValue = "http://mid.swisscom.ch/MID/v1/AuthProfile1")]
        public string SignatureProfile {
            get { return _signatureProfile; }
            set { if (!string.IsNullOrEmpty(value)) _signatureProfile = value; }
        }
        

        [ConfigurationProperty("SslCertThumbprint", IsRequired = true, DefaultValue = "CurrentUser")]
        public string SslCertThumbprint {
            get { return _sslCertThumbprint; }
            set { if (value != null)
                _sslCertThumbprint = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", ""); 
            }
        }

        /// <summary>
        /// Distinguished Name of Root CA Certificate in the CA Chain of the SSL Server Certificate for Mobile ID Service
        /// </summary>
        [ConfigurationProperty("SslRootCaCertDN", IsRequired = true)]
        public string SslRootCaCertDN {
            get { return _sslCaCertDN; }
            set { if (! string.IsNullOrEmpty(value)) _sslCaCertDN = value;  }
        }

        /// <summary>
        /// Filename of Root CA Certificate in the CA Chain of the SSL Server Certificate for Mobile ID Service. If the Certificate is not loaded from the 
        /// </summary>
        [ConfigurationProperty("SignRespCertFiles", IsRequired = false, DefaultValue = "")]
        public string SignRespCertFiles{
            get { return _signRespCertFiles; }
            set { if (! string.IsNullOrEmpty(value)) _signRespCertFiles = value; }
        }

        public string ServiceUrlPrefix {
            get { return _serviceUrlPrefix; }
            set { if (! string.IsNullOrEmpty(value)) _serviceUrlPrefix = value;}
        }

        public string DtbsPrefix {
            get { return _dtbsPrefix; }
            set { if (value != null) { _dtbsPrefix = value; } }
        }

        public bool SrvSideValidation { 
            get { return _srvSideValidation; }
            set { _srvSideValidation = value; }
        }

        public string SeedApTransId {
            get { return _seedApTransId; }
            set { if (value != null) _seedApTransId = value; }
        }

        public bool EnableSubscriberInfo { 
            get { return _enableSubscriberInfo;}
            set { _enableSubscriberInfo = value; }
        }

        public bool IgnoreUserSn {
            get { return _ignoreUserSn; }
            set { _ignoreUserSn = value; }
        }

        public bool IgnoreUserSnChange
        {
            get { return _ignoreUserSnChange; }
            set { _ignoreUserSnChange = value; }
        }

        public int RequestTimeOutSeconds
        {
            get { return _requestTimeOutSeconds; }
            set { if (value > 0 && value < 300) _requestTimeOutSeconds = value; }
        }

        /// <summary>
        /// Timespan, in seconds, between the asynchronous RequestSignature(...) and the first PollSignature(...)
        /// Must be an integer between 1 and RequestTimeOutSeconds.
        /// </summary>
        public int PollResponseDelaySeconds
        {
            get { return _pollResponseDelaySeconds; }
            set { if (value > 0 && value < _requestTimeOutSeconds) _pollResponseDelaySeconds = value; }
        }

        /// <summary>
        /// Timespan, in seconds, between two consecutive PollSignature(...).
        /// Must be an integer between 1 and RequestTimeOutSeconds.
        /// </summary>
        public int PollResponseIntervalSeconds
        {
            get { return _pollResponseIntervalSeconds; }
            set { if (value > 0 && value < _requestTimeOutSeconds) _pollResponseIntervalSeconds = value; }
        }

        public UserSerialNumberPolicy UserSerialNumberPolicy {
            get { return _userSericalNumberPolicy; }
            set { _userSericalNumberPolicy = value; }
        }

        public bool DisableSignRespValidation {
            get { return _disableSignRespValidation; }
            set { _disableSignRespValidation = value; }
        }

        public bool DisableSignRespCertValidation {
            get { return _disableSignRespCertValidation; }
            set { _disableSignRespCertValidation = value; }
        }

        /// <summary>
        /// If true, phone numbers read from the attribute store are transformed before use in Mobile ID calls. 
        /// By default, the sanitization removes all non-digits from the input strings. The sanitization is done by
        /// applying a regular expression on the input. The regex matching and replace are defined by 
        /// <paramref name="SanitizePhoneNumberPattern"/> and <paramref name="SanitizePhoneNumberReplacement"/>.
        /// </summary>
        public bool SanitizePhoneNumber
        {
            get { return _sanitizePhoneNumber; }
            set { _sanitizePhoneNumber = value; }
        }

        /// <summary>
        /// The pattern used to create the Regex for sanitization of phone number. 
        /// Only effective when <paramref name="SanitizePhoneNumber"/> is true.
        /// </summary>
        public string SanitizePhoneNumberPattern
        {
            get { return _sanitizePhoneNumberRegex.ToString(); }
            set
            {
                _sanitizePhoneNumberRegex = new Regex(value, RegexOptions.Compiled, TimeSpan.FromSeconds(1.0));
            }
        }

        /// <summary>
        /// <paramref name="SanitizePhoneNumberPattern"/> as Regex.
        /// </summary>
        public Regex SanitizePhoneNumberRegex
        {
            get { return _sanitizePhoneNumberRegex; }
        }

        /// <summary>
        /// The replace string used in phone number sanitization.
        /// Only effective when <paramref name="SanitizePhoneNumber"/> is true.
        /// </summary>
        public string SanitizePhoneNumberReplacement
        {
            get { return _sanitizePhoneNumberReplacement; }
            set { _sanitizePhoneNumberReplacement = value; }
        }

        /// <summary>
        /// The TLS Version for the communication.
        /// </summary>
        public SecurityProtocolType SecurityProtocolType
        {
            get { return _securityProtocolType; }
            set { _securityProtocolType = value; }
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(768);  // TODO: update on change
            // sorted alphabetically in name
            sb.Append("{ApId:\"").Append(_apId);
            sb.Append("\", DtbsPrefix:\"").Append(_dtbsPrefix);
            sb.Append("\"; DisableSignRespValidation:").Append(_disableSignRespValidation);
            sb.Append("; DisableSignRespCertValidation:").Append(_disableSignRespCertValidation);
            sb.Append("; EnableSubscriberInfo:").Append(_enableSubscriberInfo);
            sb.Append("; IgnoreUserSn:").Append(_ignoreUserSn);
            sb.Append("; IgnoreUserSnChange:").Append(_ignoreUserSnChange);
            sb.Append("; PollResponseDelaySeconds:").Append(_pollResponseDelaySeconds);
            sb.Append("; PollResponseIntervalSeconds:").Append(_pollResponseIntervalSeconds);
            sb.Append("; RequestTimeOutSeconds:").Append(_requestTimeOutSeconds);
            sb.Append("; SanitizePhoneNumber: ").Append(_sanitizePhoneNumber);
            sb.Append("; SanitizePhoneNumberPattern: \"").Append(_sanitizePhoneNumberRegex.ToString());
            sb.Append("\"; SanitizePhoneNumberReplacement: \"").Append(_sanitizePhoneNumberReplacement);
            sb.Append("\"; SeedApTransId:\"").Append(_seedApTransId);
            sb.Append("\"; ServiceUrlPrefix=\"").Append(_serviceUrlPrefix);
            sb.Append("\"; SignatureProfile=\"").Append(_signatureProfile);
            sb.Append("\"; SrvSideValidation:").Append(_srvSideValidation);
            sb.Append("; SslKeystore:").Append(_sslKeyStore);
            sb.Append("; SslCertThumbprint:\"").Append(_sslCertThumbprint);
            sb.Append("\"; SslRootCaCertDN:\"").Append(_sslCaCertDN);
            sb.Append("\"; SignRespCertFiles:\"").Append(_signRespCertFiles);
            sb.Append("\"; UserLanguageDefault:\"").Append(_userLanguageDefault);
            sb.Append("\"; UserSerialNumberPolicy:").Append(_userSericalNumberPolicy);
            sb.Append("; SecurityProtocolType:").Append(_securityProtocolType);
            sb.Append("\"}");
            return sb.ToString();

        }
    }

    [FlagsAttribute]
    public enum UserSerialNumberPolicy
    {
        /// <summary>
        /// a user can be authenticated if the user has a serial number in attribute store that matches the one in MID server response.
        /// Note that a valid serial number in MID server response can never be empty (an empty serial number in MID server response 
        /// would be considered as invalid response and would result in authentication failure).
        /// </summary>
        match = 0,

        /// <summary>
        /// Write a warning message in log if a mismatch of serial number is detected.
        /// </summary>
        warnMismatch = 1,

        /// <summary>
        /// a user can also be authenticated if the user has no or an empty serial number in his/her attribute store (e.g. Active Directory).
        /// A serial number consisting of only white spaces is considered as "empty" in this context.
        /// </summary>
        allowAbsence = 2,

        /// <summary>
        /// a user can also be authenticated if his/her serial number does not matches the one in the user's attribute store (e.g. Active Directory).
        /// The string comparison is case sensitive.
        /// </summary>
        allowMismatch = 4,
    }

}
