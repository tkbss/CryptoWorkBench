using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public class Type : Statement
    {
        public string Id { get; set; } 
        public void Check(string type) 
        {
            type = type.ToLower();
            switch(type) 
            {
                case "var":
                    return;
                case "key":
                    return;
                case "data":
                    return;
                default: throw new Exception();
            }
        }
    }
}
