namespace CryptoScript.Model
{
    public class MechanismList
    {
        private static MechanismList? instance = null;
        private static readonly object padlock = new object();
        public List<string> Mechanisms { get; set; }
        MechanismList()
        {
            Mechanisms = new List<string>
            {
                "AES-ECB",
                "AES-CBC",
                "AES-CTR",
                "AES-CMAC",
                "AES-GCM",
                "AES-CCM",
                "AES-GMAC",
                "DES3-ECB",
                "DES3-CBC",
                "DES3-RETAIL",
                "DES3-CMAC",
                "WRAP-AES-TR31",
                "WRAP-DES3-TR31",
                "WRAP-AES",
                "WRAP-DES3",
                "BIND-XOR",
                "BIND-CMAC"
            };
        }
        public static MechanismList Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new MechanismList();
                    }
                    return instance;
                }
            }
        }
    }
}
