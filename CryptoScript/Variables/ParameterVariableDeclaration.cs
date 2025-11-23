using Antlr4.Runtime;
using CryptoScript.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{

    
    public class ParameterVariableDeclaration : VariableDeclaration
    {
        public void SetInstance(string parameters)
        {            
            
            // Split by '#' and remove empty entries
            var parts = parameters.Split('#', StringSplitOptions.RemoveEmptyEntries);  
            foreach (var part in parts)
            {
                // Split each part by ':' to separate key and value
                var kv = part.Split(':', 2);
                if (kv.Length == 2)
                {
                    if (kv[0] == "MECH")
                        Mechanism = kv[1];
                    var key = "#" + kv[0];    // Add '#' back to the key                    
                    var value = kv[1];
                    SetParameter(key, value);

                }
            }

         }
        string _mechanism = string.Empty;
        public string Mechanism 
        {
            get { return _mechanism; }
            set { _mechanism = value;SetParameter("MECH",_mechanism); }
        } 
        
        
        
        Dictionary<string, string> ParameterTypeValue = new Dictionary<string, string>();
        public Dictionary<string, string> GetParameters() 
        {
            return ParameterTypeValue;
        }
        public ParameterVariableDeclaration() 
        {
            Type = new CryptoTypeParameters();
            ValueFormat = FormatConversions.PAR;
        }
        private void BuildValue() 
        {
            ParameterMechanismen(Mechanism);
            //_value = string.Empty;
            Value= string.Empty;
            foreach (var item in ParameterTypeValue)
            {
                Value += item.Key + ":" + item.Value;
            }
            
        }
        private void ParameterMechanismen(string mech)
        {
            if (mech.Contains("#"))
            {
                var kv = mech.Split(':', 2);
                Mechanism = kv[1];
            }
            if (!ParameterTypeValue.ContainsKey("#MECH"))
                ParameterTypeValue.Add("#MECH", mech);
            
        }
        public void SetParameter(string type, string value)
        {            
            string tu = type.ToUpper();
            string? t = ParameterTypeList.Instance.ParameterTypes.Find(item => item.Contains(tu));
            if (ParameterTypeValue.ContainsKey(t))
            {
                ParameterTypeValue[t] = value;
            }
            else
            {
                ParameterTypeValue.Add(t, value);
            }
            BuildValue();
        }
        public void SetParameter(string parameter)
        {
            string[] parts = parameter.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException("Error: invalid parameter format");
            SetParameter(parts[0], parts[1]);
        }
        public string GetParameter(string type)
        {
            string tu = type.ToUpper();
            string? t = ParameterTypeList.Instance.ParameterTypes.Find(item => item.Contains(tu));
            if (ParameterTypeValue.ContainsKey(t))
            {
                return ParameterTypeValue[t];
            }
            else
            {
                return string.Empty;
            }
        }

        public override string PrintOutput()
        {
            BuildValue();
            return Value;
        }
        public void SetParameter(ArgumentParameter p)
        {
            SetParameter(p.Type, p.Value);            

        }
        
    }
}
