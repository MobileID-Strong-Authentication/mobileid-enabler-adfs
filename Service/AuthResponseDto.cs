
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MobileId
{
    /// <summary>
    /// Output of RequestSignature(...) service call
    /// </summary>
    public class AuthResponseDto
    {
        public ServiceStatus Status { get; set; }

        /// <summary>
        /// optional details. Content depends on Status
        /// </summary>
        public object Detail { get; set;  }

        /// <summary>
        /// MSSP_TransID
        /// </summary>
        public string MsspTransId { get; set; }

        /// <summary>
        /// Signature
        /// </summary>
        public byte[] Signature { get; set; }

        private Dictionary<AuthResponseExtension, object> _extensions = new Dictionary<AuthResponseExtension, object> { };

        /// <summary>
        /// Additional features (e.g. SubscriberInfo, UserAssistencePortalUrl)
        /// are accessible via a Dictionary. Extensions is garanteed to be a non-null Dictionary.
        /// If the server response contains a feature,
        /// the value of the feature can be retrieved with AuthResponseDto.Extensions[featureName],
        /// otherwise the key featureName is absent in the Dictionary.
        /// </summary>
        public Dictionary<AuthResponseExtension, object> Extensions
        {
            get { return _extensions;}
            set { if (value != null) _extensions = value;}
        }

        private bool signerCertParsed = false;
        private X509Certificate2 _signerCertificate = null;
        private string _signerPsuedonym = null;
        private string _signerSerialNumber = null;

        private void _parseSignerDn() {
            _signerCertificate = new X509Certificate2(Signature);
            _signerPsuedonym = _signerCertificate.GetNameInfo(X509NameType.SimpleName, false);
            _signerSerialNumber = Util.ExtractFirstSnFromDn(_signerCertificate.SubjectName);
            signerCertParsed = true;
        }

        /// <summary>
        /// The certificate used to sign the Data-To-Be-Signed in request. If the response does not include a certificate, the property is <c>null</c>.
        /// </summary>
        public X509Certificate2 UserCertificate
        {
            get
            {
                if (Signature == null)
                {
                    return null;
                };
                if (!signerCertParsed)
                {
                    _parseSignerDn();
                };
                return _signerCertificate;
            }
        }

        /// <summary>
        /// The Common Name in Subject of <paramref name="UserCertificate"/>. If the certificate is absent, the property is <c>null</c>.
        /// </summary>
        public string UserPseudonym
        {
            get {
                if (Signature == null) {
                    return null;
                };
                if (! signerCertParsed) {
                    _parseSignerDn();
                };
                return _signerPsuedonym;
            }
        }

        /// <summary>
        /// The SerialNumber attribute in Subject of <paramref name="UserCertificate"/>. If the certificate is absent, the property is <c>null</c>.
        /// </summary>
        public string UserSerialNumber
        {
            get
            {
                if (Signature == null)
                {
                    return null;
                };
                if (!signerCertParsed)
                {
                    _parseSignerDn();
                };
                return _signerSerialNumber;
            }

        }

        /// <summary>
        /// Mainly used to construct error response.
        /// </summary>
        /// <param name="StatusCode"></param>
        /// <param name="payload"></param>
        public AuthResponseDto(ServiceStatusCode statusCode, string payload)
        {
            this.Status = new ServiceStatus(statusCode);
            this.Detail = payload;
        }

        public AuthResponseDto(ServiceStatusCode statusCode)
        {
            this.Status = new ServiceStatus(statusCode);
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append("MsspTransid=").Append(MsspTransId);
            sb.Append(", Status: {").Append(Status);
            sb.Append("}, Detail: ").Append(Detail);
            return sb.ToString();
        }

    }

    public enum AuthResponseExtension
    {
        UserAssistencePortalUrl = 1,
        SubscriberInfo = 2
    }
}
