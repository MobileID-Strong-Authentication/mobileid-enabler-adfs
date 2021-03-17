using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MobileId
{
    public enum EventId
    {
        /// <summary>
        /// configuration data
        /// </summary>
        Config = 0,

        /// <summary>
        /// audit relevant info
        /// </summary>
        Audit,

        /// <summary>
        /// service call
        /// </summary>
        Service,

        /// <summary>
        /// attack detected
        /// </summary>
        Hacking,

        /// <summary>
        /// SSL setup of transport
        /// </summary>
        TransportSsl,

        /// <summary>
        /// Key Management
        /// </summary>
        KeyManagement,

        /// <summary>
        /// communication at HTTP level
        /// </summary>
        Transport,

        /// <summary>
        /// communication at SOAP level
        /// </summary>
        TransportSoap


    }
}
