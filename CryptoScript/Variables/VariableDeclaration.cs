using CryptoScript.Model;
using Newtonsoft.Json;

namespace CryptoScript.Variables
{
    public class VariableDeclaration : Statement
    {
        public string Id { get; set; } = string.Empty;
        public virtual CryptoType? Type { get; set; } = null;
        public virtual string Value { get; set; }=string.Empty;
        public string ValueFormat { get; set; } = string.Empty;        
        public virtual string PrintOutput() {  return string.Empty; }
        //public string Serialize()
        //{
        //    return JsonConvert.SerializeObject(this);
        //}

        //public static VariableDeclaration Deserialize(string json)
        //{
        //    return JsonConvert.DeserializeObject<VariableDeclaration>(json);
        //}

    }
}

