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
            // Regular expression to match the mechanism
            string mechanismPattern = @"^\s*(\w+-\w+)";
            

            // Extract mechanism
            var mechanismMatch = Regex.Match(parameters, mechanismPattern);
            Mechanism = mechanismMatch.Success ? mechanismMatch.Groups[1].Value : string.Empty;
            var p=parameters.Remove(0,Mechanism.Length);
            p=p.TrimStart();
            string[] keyValueArray=p.Split('#');
            foreach(var item in keyValueArray)
            {
                if (string.IsNullOrEmpty(item))
                    continue;
                SetParameter(item);
            }
                        
        }
        public string Mechanism { get; set; } = string.Empty;
        
        
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
            _value = Mechanism;
            foreach (var item in ParameterTypeValue)
            {
                _value += item.Key + ":" + item.Value;
            }
        }
        public void SetParameter(string type, string value)
        {
            string tu = type.ToUpper();
            string t = ParameterTypeList.Instance.ParameterTypes.Find(item => item.Contains(tu));
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
            string t = ParameterTypeList.Instance.ParameterTypes.Find(item => item.Contains(tu));
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
