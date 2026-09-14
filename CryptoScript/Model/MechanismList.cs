namespace CryptoScript.Model
{
    public class MechanismList
    {
        private static MechanismList? instance = null;
        private static readonly object padlock = new object();
        public List<string> Mechanisms { get; set; }
        MechanismList()
        {
            Mechanisms = AntlrLanguageMetadata.GetMechanisms();
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
