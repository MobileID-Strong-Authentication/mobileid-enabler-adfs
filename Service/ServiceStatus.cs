
using System.Text;
using System.Resources;
using System.Reflection;

namespace MobileId
{
    public class ServiceStatus
    {
        static readonly string ResourceName = "MobileId.MssError";

        // static ResourceManager resMgr = new ResourceManager(typeof(MobileId.ServiceStatus)); // search for resource name "MobileId.ServiceStatus"
        static ResourceManager resMgr = new ResourceManager(ResourceName, typeof(MobileId.ServiceStatus).Assembly);  // or Assembly.GetExecutingAssembly()
        /// <summary>
        /// Green, Yellow, Red. See Mobile ID Reference Guide
        /// </summary>
        public ServiceStatusColor Color {  
            get {
                return mapColorFromCode(this.Code);
            }
        }

        /// <summary>
        /// Example: 500
        /// </summary>
        public ServiceStatusCode Code { get; set; }
           

        /// <summary>
        /// Example: SIGNATURE
        /// </summary>
        public string Message { 
            get {
                return this.Code.ToString();
            }
        }

        private static ServiceStatusColor mapColorFromCode(ServiceStatusCode code)
        {
            switch (code)
            {
                // green
                case ServiceStatusCode.SIGNATURE:
                case ServiceStatusCode.VALID_SIGNATURE:
                case ServiceStatusCode.USER_CANCEL:
                case ServiceStatusCode.REQUEST_OK:
                    return ServiceStatusColor.Green;

                // yellow
                case ServiceStatusCode.UNKNOWN_CLIENT:
                case ServiceStatusCode.PIN_NR_BLOCKED:
                case ServiceStatusCode.CARD_BLOCKED:
                case ServiceStatusCode.NO_KEY_FOUND:
                case ServiceStatusCode.NO_CERT_FOUND:
                case ServiceStatusCode.EXPIRED_TRANSACTION:
                case ServiceStatusCode.PB_SIGNATURE_PROCESS:
                case ServiceStatusCode.REVOKED_CERTIFICATE:
                case ServiceStatusCode.INVALID_SIGNATURE:
                case ServiceStatusCode.OTA_ERROR:
                case ServiceStatusCode.UserSerialNumberNotRegistered:
                case ServiceStatusCode.UserSerialNumberMismatch:
                case ServiceStatusCode.UserCertAbsent:
                case ServiceStatusCode.UserCertNotYetValid:
                case ServiceStatusCode.UserCertExpired:
                    return ServiceStatusColor.Yellow;

                 // the rest is red
                default: 
                    return ServiceStatusColor.Red;
            }
        }

        //public static string MapCodeToMessage(ServiceStatusCode code)
        //{
        //    return code.ToString();
        //}

        public ServiceStatus(ServiceStatusCode code, string message)
        {
            this.Code = code;
            if ((message != null) && (message != this.Message))
                throw new System.OverflowException("ServiceStatus.Message does not match the registered Message");

        }

        public ServiceStatus(ServiceStatusCode code)
        {
            this.Code = code;
        }

        public string GetDisplayMessage(int lcid)
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
            try {culture = new System.Globalization.CultureInfo(lcid);}
            catch (System.ArgumentOutOfRangeException) { }
            catch (System.Globalization.CultureNotFoundException) { }
            return resMgr.GetString("mss_" + (int) this.Code, culture);
        }

        public static string GetDefaultErrorMessage(int lcid)
        {
            return resMgr.GetString("mss_000", new System.Globalization.CultureInfo(lcid));
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(64); // max length 57 chars
            sb.Append("{Code:").Append((int) Code);
            sb.Append(", Reason:").Append(Message);
            sb.Append(", Color:").Append(Color);
            sb.Append("}");
            return sb.ToString();
        }
    }
}
