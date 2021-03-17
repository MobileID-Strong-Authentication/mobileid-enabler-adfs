
namespace MobileId
{
    // number range:
    //
    // <0: status not covered in ETSI standard, the absolute value follows the HTTP / SMTP-style status code semantics
    // -5XX: server side error
    // -4XX: client side error
    // -3XX: redirection / continuation / transport
    // -2XX: success ?
    // -1XX: informational
    // 
    // 0: reserved
    // -99 .. 99: avoid (its string representation has 1-2 chars, which can lead to unnecessary extra work)
    //
    // > 0: server status code (as is as received by client). Names are in upper case and match the Reason element in SOAP response.
    // 100-900: ETSI official status code
    //
    public enum ServiceStatusCode
    {
        /// <summary>
        /// The server response does not contain user's certificate in the signature.
        /// </summary>
        UserCertAbsent = -508,

        /// <summary>
        /// The user's certificate has already expired according to the time at the client side.
        /// </summary>
        UserCertExpired = -507,

        /// <summary>
        /// The user's certificate is not yet valid according to the time at the client side.
        /// </summary>
        UserCertNotYetValid = -506,

        /// <summary>
        /// The signature in Response is invalid or cannot be verified.
        /// </summary>
        /// <remarks>
        /// This code is similar to <paramref name="INVALID_SIGNATURE"/> except that the verification is done at the client side.
        /// </remarks>
        InvalidResponseSignature = -505,

        /// <summary>
        /// Response has an unknown format
        /// </summary>
        UnknownResponse = -504,

        /// <summary>
        /// MSISDN in service response does not match the MSISDN in service request
        /// </summary>
        MismatchedMsisdn = -503,

        /// <summary>
        /// AP_TransID in service response does not match the AP_TransID in service request
        /// </summary>
        MismatchedApTransId = -502,

        /// <summary>
        /// Mobile Service client has received a known but unexpected Fault Code or Status Code
        /// </summary>
        IllegalStatusCode = -501, 

        /// <summary>
        /// Mobile Service client has received an unsupported Fault Code or Status Code
        /// </summary>
        UnsupportedStatusCode = -500,

        /// <summary>
        /// User's Serial Number is not registered in the Application Provider.
        /// </summary>
        UserSerialNumberNotRegistered = -404,

        /// <summary>
        /// User's Serial Number in MID Server Response does not match the one registered by the Application Provider.
        /// </summary>
        UserSerialNumberMismatch = -403,

        /// <summary>
        /// Client-side error in configurations
        /// </summary>
        ConfigError = -402,

        /// <summary>
        /// Input parameters of a service call are invalid or incomplete
        /// </summary>
        InvalidInput = -401,

        /// <summary>
        /// General error in client side, e.g. bug in Mobile ID client source code.
        /// </summary>
        GeneralClientError = -400,

        /*
                /// <summary>
                /// Error in setting up a connection at TLS layer, i.e. SSL handshake failed
                /// </summary>
                SslSetupError = -304,

                /// <summary>
                /// Error occured in an established connection
                /// </summary>
                TcpipTimeout,

                TcpipBroken,

                /// <summary>
                /// Error in setting up a network connection at TCP/IP layer, e.g. the connection is reset by a firewall.
                /// </summary>
                TcpipSetupError = -302,

                /// <summary>
                /// No response is received during a certain time span. It may be caused at the tcpip, ssl, or application layer.
                /// </summary>
                CommTimeout = -301,

                /// <summary>
                /// General error in an established network connection. The error can occurs in tcp, ssl, or application layer.
                /// </summary>
                CommBroken,
        */

        /// <summary>
        /// General error in establishing a network connection. The error can occurs in tcp, ssl, or application layer.
        /// </summary>
        CommSetupError = -300,

        /// <summary>
        /// Reserved status code
        /// </summary>
        DoNotUse = 0,

        // For value > 0, see Mobile ID Reference Guide, Chap. 7.8
        // It should include only the code which has been implemented (a subset of ETSI standard code).
        // If a code is not included here but has been implemented on the server-side, the client will detect it and issues warning.

        REQUEST_OK = 100,
        WRONG_PARAM = 101,
        MISSING_PARAM = 102,
        WRONG_DATA_LENGTH = 103,
        UNAUTHORIZED_ACCESS = 104,
        UNKNOWN_CLIENT = 105,
        INAPPROPRIATE_DATA = 107,
        INCOMPATIBLE_INTERFACE = 108,
        UNSUPPORTED_PROFILE = 109,

        EXPIRED_TRANSACTION = 208,
        OTA_ERROR = 209,

        USER_CANCEL = 401,
        PIN_NR_BLOCKED = 402,
        CARD_BLOCKED = 403,
        NO_KEY_FOUND = 404,
        PB_SIGNATURE_PROCESS = 406,
        NO_CERT_FOUND = 422,

        SIGNATURE = 500,
        REVOKED_CERTIFICATE = 501,
        VALID_SIGNATURE = 502,
        INVALID_SIGNATURE = 503,
        OUSTANDING_TRANSACTION = 504,

        INTERNAL_ERROR = 900
    }
}
