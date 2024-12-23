using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class KeyVariableDeclaration : VariableDeclaration
    {
        public string Mechanism { get; set; } = string.Empty;
        public string KeySize { get; set; } = string.Empty;
        public string KeyValue { get; set; } = string.Empty;
        public override string Value { get => base.Value; set => base.Value = value; }
        public override string PrintOutput()
        {
            return base.PrintOutput();
        }
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static KeyVariableDeclaration Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<KeyVariableDeclaration>(json);
        }
    }
}
