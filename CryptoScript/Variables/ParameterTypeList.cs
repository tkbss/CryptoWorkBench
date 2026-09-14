using CryptoScript.Model;

namespace CryptoScript.Variables
{
    public class ParameterTypeList
    {
        private static ParameterTypeList? instance = null;
        private static readonly object padlock = new object();
        public List<string> Paddings { get; set; }
        public List<string> Mechanism { get; set; }
        public List<string> ParameterTypes { get; set; }
        ParameterTypeList()
        {
            Paddings = AntlrLanguageMetadata.GetPaddings();
            ParameterTypes = AntlrLanguageMetadata.GetParameters();
            Mechanism = AntlrLanguageMetadata.GetMechanisms();
            if (!ParameterTypes.Contains("#MACLEN"))
                ParameterTypes.Add("#MACLEN");
        }
        public static ParameterTypeList Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new ParameterTypeList();
                    }
                    return instance;
                }
            }
        }
    }
}
