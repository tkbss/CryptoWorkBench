using Antlr4.Runtime;
using CryptoScript.Model;
using Newtonsoft.Json;
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
            // parts = [ "MECH:AES-CBC", "IV:0x(ed5ec439881464b03215cb6434eef91a)", "PAD:PKCS-7" ]

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
        
        
        string _value = string.Empty;
        public override string Value
        {
            get { return _value; }
            //set { _value = value; }
        } 
        Dictionary<string, string> ParameterTypeValue = new Dictionary<string, string>();
        public ParameterVariableDeclaration() 
        {
            Type = new CryptoTypeParameters();
        }
        private void BuildValue() 
        {
            ParameterMechanismen(Mechanism);
            _value = string.Empty;
            foreach (var item in ParameterTypeValue)
            {
                _value += item.Key + ":" + item.Value;
            }
            
        }
        private void ParameterMechanismen(string mech)
        {
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
