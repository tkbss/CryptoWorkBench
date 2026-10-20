using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class OperationList
    {
        public static List<string> Operations { get; set; } = new List<string>()
        {
            "Parameters",
            "CreateHeader",
            "Share", "Secret", "Restore",
            "Save",
            "GenerateKey",
            "Encrypt",
            "Decrypt",
            "Mac",
            "Wrap",
            "Unwrap",
            "PinBlockTranslate",
            "Print",
            "Info",
            "Compare",
            "Check",
        };
        public static int MaximalOperationTextLenght()
        {
            return Operations.Max(x => x.Length);
        }
    }
}
