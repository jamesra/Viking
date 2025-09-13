using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.SSL
{

    public struct SSLOptions
    {
        public string SerialNumber { get; set; }

        public string DnsName { get; set; }

        public string CertificatePath { get; set; }

        public string Password { get; set; }
    }
}
