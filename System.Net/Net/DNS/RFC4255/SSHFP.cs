using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC4255
{

    [DNSRecord(DNSClass.IN, 0x2C)]
    public class SSHFP : DNSResponseDetail
    {
        [DNSField]
        public SSHAlgorithm Algorithm { get; set; }

        [DNSField]
        public SSHFingerprintType FingerPrintType { get; set; }

        [DNSField]
        public byte[] FingerPrint { get; set; }
    
    }
}
