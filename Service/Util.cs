using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace MobileId
{
    /// <summary>
    /// Helper methods used by various parts of Mobile ID client
    /// </summary>
    public static class Util
    {
        public static string CurrentTimeStampString() {
             return string.Format("{0:yyyy-MM-ddTHH:mm:ss.ffffffZ}", System.DateTime.UtcNow);
        }

        public static bool IsXmlSafe(string data)
        {
            return data != null && ! Regex.Match(data, "[<>\"]").Success;
        }

        public static string Str(string s)
        {
            return s != null ? ("\"" + s + "\"") : "null";
        }

        public static UserLanguage ParseUserLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException("ParseUserLanguage");
            string s = language.ToLower();
            switch (s)
            {
                case "en": return UserLanguage.en;
                case "de": return UserLanguage.de;
                case "it": return UserLanguage.it;
                case "fr": return UserLanguage.fr;
                default: throw new ArgumentOutOfRangeException("ParseUserLanguage");
            }
        }

        public static StoreLocation ParseKeyStoreLocation(string s)
        {
            if (string.IsNullOrEmpty(s)) throw new ArgumentNullException();
            switch (s) {
                case "CurrentUser": return StoreLocation.CurrentUser;
                case "LocalMachine": return StoreLocation.LocalMachine;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static RandomNumberGenerator cprng;

        /// <summary>
        /// return a 32-hexchar random string with 64-128 bit randomness.
        /// </summary>
        /// <param name="seed"></param>
        /// <returns></returns>
        public static string Build64bitRandomHex(string seed)
        {
            MD5 md5 = MD5.Create();
            byte[] seedBytes = md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes(seed)); // 64-bit seed
            byte[] rndBytes = new byte[8]; // 64-bit random
            byte[] buffer = new byte[16];
            if (cprng == null)
                cprng = new RNGCryptoServiceProvider();
            cprng.GetBytes(rndBytes);
            for (int i = 0; i < 8; i++)
                buffer[i] = seedBytes[i];
            for (int i = 8; i < 16; i++)
                buffer[i] = rndBytes[i - 8];
            byte[] hash = md5.ComputeHash(buffer);

            StringBuilder sb = new StringBuilder(32);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("X2"));
            return sb.ToString();
        }

        /// <summary>
        /// Return a random string from the Base64-chars alphabet.
        /// </summary>
        /// <param name="charLength">length of the string to be returned</param>
        /// <returns></returns>
        public static string BuildRandomBase64Chars(int charLength)
        {
            if (charLength <= 0)
                throw new ArgumentOutOfRangeException("charLength must be possible");
            int bytesLength = (int) System.Math.Ceiling(0.75 * charLength);
            byte[] rndBytes = new byte[bytesLength];
            if (cprng == null)
                cprng = new RNGCryptoServiceProvider();
            cprng.GetBytes(rndBytes);
            string s = Convert.ToBase64String(rndBytes);
            return s.Substring(0,charLength);
        }

        /// <summary>
        /// The acceptable length of Data-To-Be-Signed (DTBS) depends on the encoding of text. 
        /// This method calculate the acceptable length of a DTBS string.
        /// </summary>
        /// <param name="dtbs"></param>
        /// <returns>Maximum number of characters that can be sent to a mobile phone via Mobile ID service</returns>
        public static int maxDtbsLength(string dtbs)
        {
            // TODO
            return AuthRequestDto.MAX_UTF8_CHARS_DTBS;
        }

        /// <summary>
        /// Extract the Serial Number (OID 2.5.4.5) from a X500 Distinguished Name (DN).
        /// </summary>
        /// <param name="dn"></param>
        /// <returns>
        /// If DN contains exactly one Serial Number attribute, return its value.
        /// If DN does not contain Serial Number, return <c>null</c>.
        /// If DN contains multiple Serial Number, return the first value, in the order in ASN1 encoding.
        /// </returns>
        public static string ExtractFirstSnFromDn(X500DistinguishedName dn)
        {
            string snName = new Oid("2.5.4.5").FriendlyName;
            string dnMultiLine = dn.Decode(X500DistinguishedNameFlags.UseNewLines);
            int iStart = dnMultiLine.IndexOf(snName);
            if (iStart == -1)
            {
                return null;
            };
            iStart += snName.Length + 1;
            int iEnd = dnMultiLine.IndexOf(Environment.NewLine, iStart);
            if (iEnd != -1)
            {
                return dnMultiLine.Substring(iStart, iEnd - iStart);
            }
            else
            {
                return dnMultiLine.Substring(iStart);
            }
        }

        public static string SanitizePhoneNumber(string displayedPhoneNumber, WebClientConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException("WebClientConfig");
            if (!cfg.SanitizePhoneNumber)
                return displayedPhoneNumber;
            string msisdn = cfg.SanitizePhoneNumberRegex.Replace(displayedPhoneNumber, cfg.SanitizePhoneNumberReplacement);
            int len = msisdn.Length;
            if (len < AuthRequestDto.MIN_MSISDN_DIGITS || len > AuthRequestDto.MAX_MSISDN_DIGITS)
            {
                throw new ArgumentOutOfRangeException("lengthMsisdnSanitized", len.ToString() );
            };
            return msisdn;
        }


    }
}
