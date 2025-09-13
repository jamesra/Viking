using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.SSL
{

    public struct SSLOptions
    {
        /// <summary>
        /// Port to use for SSL
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 1st Check: File path of certificate
        /// </summary>
        public string CertificatePath { get; set; }


        /// <summary>
        /// 2nd Check: DNS Name of certificate (Subject Alternative Name or Subject)
        /// </summary>
        public string DnsName { get; set; }

        /// <summary>
        /// 3rd Check: Serial Number of certificate
        /// </summary>
        public string SerialNumber { get; set; } 

        /// <summary>
        /// Password to access certificate (if needed)
        /// </summary>
        public string Password { get; set; }
    }
}
