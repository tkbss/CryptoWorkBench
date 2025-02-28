using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class Tr31Header
    {
        public char HeaderIdentifier { get; set; } = 'B';  // Typically 'B' for TR-31
        public int BlockLength { get; set; }               // 4 bytes in ASCII
        public char VersionId { get; set; } = '1';         // e.g. '1'
        public string KeyUsage { get; set; } = string.Empty;              // e.g. "MK" for Master Key usage
        public string Algorithm { get; set; } = string.Empty;             // e.g. "AES1" or "TDES"
        public string ExportControl { get; set; } = string.Empty;       // e.g. "X0"

        // Other optional fields as needed (dates, KCV, etc.)

        /// <summary>
        /// Renders the header as a byte array (ASCII-encoded).
        /// The exact format depends on your TR-31 specification.
        /// </summary>
        public byte[] ToBytes()
        {
            // Example only—this might not match your exact field layout
            // Suppose format: B + [BlockLength(4 digits)] + VersionId + KeyUsage + Algorithm + ExportControl
            var str = $"{HeaderIdentifier}" +
                      $"{BlockLength:0000}" +
                      $"{VersionId}" +
                      $"{KeyUsage}" +
                      $"{Algorithm}" +
                      $"{ExportControl}";

            return System.Text.Encoding.ASCII.GetBytes(str);
        }
    }

}
