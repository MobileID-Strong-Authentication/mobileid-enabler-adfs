using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MobileId
{
    /// <summary>Encapsulation of input parameters of <paramref name="RequestSignature"/></summary>
    /// <remarks>
    /// The class implements input validation and provides default values for missing input parameters. 
    /// </remarks>
    public class AuthRequestDto
    {
        public const int MAX_GSM338_CHARS_DTBS = 239;
        public const int MAX_UTF8_CHARS_DTBS = 119;
        public const int MAX_MSISDN_DIGITS = 15; // defined by E.164, although Germany has reportely 17-digit MSISDNs (https://github.com/googlei18n/libphonenumber), not sure whether it is a mobile number
        public const int MIN_MSISDN_DIGITS = 7;  // the length of a mobile number is 7 in "Country" Niue, https://en.wikipedia.org/wiki/List_of_mobile_phone_number_series_by_country

        string _apId;
        string _phoneNumber;
        string _msgToBeSigned;
        UserLanguage _userLanguage;
        int _timeout = 80;
        string _instant;
        string _transIdPrefix = "";
        string _transId;
        bool _srvSideValidation = false;
        string _userSerialNumber = null;

        /// <summary>
        /// Check the completeness of the object.
        /// </summary>
        /// <returns>Return true if all mandatory properties have been defined.</returns>
        public bool IsComplete()
        {
            return _phoneNumber != null
                && _msgToBeSigned != null
//                && _apId != null
                ;
        }

        /// <summary>
        /// String representation. Optional fields are not generated/populated by this method.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(256); // TODO: update the capacity on code change
            // fields are ordered alphabetically by name
            sb.Append("{ApId:").Append(Util.Str(_apId));
            sb.Append(", Instant:").Append(Util.Str(_instant));
            sb.Append(", MsgToBeSigned:").Append(Util.Str(_msgToBeSigned));
            sb.Append(", PhoneNumber:").Append(Util.Str(_phoneNumber));
            sb.Append(", TimeOut:").Append(_timeout);
            sb.Append(", TransId:").Append(Util.Str(_transId));
            sb.Append(", TransIdPrefix:").Append(Util.Str(_transIdPrefix));
            sb.Append(", SrvSideValidation:").Append(_srvSideValidation);
            sb.Append(", UserLanguage:").AppendFormat("{0:G}", _userLanguage);
            sb.Append(", UserSerialNumber:").Append(_userSerialNumber);
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>Phone Number that should receive the authentication/signing request. See example for syntax.</summary>
        /// <example>+41791234567</example>
        public string PhoneNumber
        {
            get { return _phoneNumber; }
            set
            {
                if (string.IsNullOrEmpty(value) ) throw new ArgumentNullException("PhoneNumberIsNullOrEmpty");

                string normalizedValue = value.Replace("-", string.Empty).Replace(" ", string.Empty);
                if (!Regex.Match(normalizedValue, @"^\+?\d{7,15}$").Success) throw new ArgumentException("PhoneNumberIsIllFormed: '" + normalizedValue + "'");
                _phoneNumber = normalizedValue;
            }
        }

        /// <summary>Text to be displayed (and signed) in mobile phone</summary>
        public string DataToBeSigned
        {
            get { return _msgToBeSigned; }
            set
            {
                if (value == null) throw new ArgumentNullException("DataToBeSignedIsNull");
                // TODO: handle GSM 338 input
                if (value.Length <= 0 || value.Length >= MAX_UTF8_CHARS_DTBS) throw new ArgumentException("DataToBeSignedHasBadSize");
                _msgToBeSigned = value;
            }
        }

        /// <summary>Language of user interface (e.g. dialog text, button text) for the Mobile ID application running in mobile phone</summary>
        public UserLanguage UserLanguage
        {
            get { return _userLanguage; }
            set { _userLanguage = value; }
        }

        /// <summary>Identifier of Application Provider, s. AP_ID in Mobile ID Reference Guide</summary>
        public string ApId { 
            get { return _apId; }
            set {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException("ApIdIsNullOrEmpty");
                if (!Util.IsXmlSafe(value)) throw new ArgumentException("ApIdIsIllFormed");
                _apId = value;
            }
        }

        /// <summary>Timeout value in secondes. Default is 80.</summary>
        public int TimeOut { 
            get { return _timeout; }
            set { if (value <= 0) throw new ArgumentException("TimeoutIsNotPossive"); _timeout = value; }
        }

        /// <summary>s. AP_TransID in MobileID Reference Guide. If not specified, a default one will be generated.</summary>
        /// <example>AP.TEST.2015-02-15T19:39:11.123456Z</example>
        public string TransId
        {
            get {
                return _transId;
            }
            set {
                if (value != null && ! Util.IsXmlSafe(value)) throw new ArgumentException("TransIdHasBadChar");
                _transId = value;
            }
        }

        /// <summary>
        /// If <paramref name="TransId"/> is not defined, a default TransId is constructed from this property and current timestamp.
        /// </summary>
        public string TransIdPrefix { 
            get {
              return _transIdPrefix;
            }
            set {
              if ( value == null ) throw new ArgumentNullException("TransIdPrefixIsNull");
              if (! Util.IsXmlSafe(value) ) throw new ArgumentException("TransIdPrefixHasBadChar");
              _transIdPrefix = value;
            }
        }

        /// <summary>
        /// By default, a timestamp (s. AP_INSTANT in Mobile ID Reference Guide) will be generated when the authentication request is sent.
        /// This property can be used to override the timestamp.</summary>
        public string Instant {
            get {
                // if (_instant == null) {
                //     _instant = _currentTimeStamp();
                // };
                return _instant;
            }
            set {
                if (value != null && !Regex.Match(value, @"^[0-9TZ:-]+$").Success) throw new ArgumentException("InstantIsIllFormed"); // TODO: better regex
                _instant = value;
            }
        }

        /// <summary>
        /// The expected Serial Number in the Subject attribute of user's certificate.
        /// </summary>
        public string UserSerialNumber {
            get { return _userSerialNumber;}
            set { _userSerialNumber = value; }
        }

        // (deprecated)
        public bool SrvSideValidation { 
            get {
                return _srvSideValidation;
            } set {
                _srvSideValidation = value;
            }
        }

    }
}
