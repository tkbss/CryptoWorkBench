using AvaloniaEdit.CodeCompletion;
using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Models
{
    public class FunctionOverlayDictionaryModel
    {
        public  Dictionary<string, List<(string header, string description)>> functionOverloads = new Dictionary<string, List<(string header, string description)>> 
        {

            { "Encrypt", new List<(string, string)>{
                ("Encrypt(parameters, key, mechanism)", "Encrypts data using the specified parameters:" +
                "\n- parameters: The encryption algorithm (e.g., AES-CBC) and additional parameters (e.g. IV)"+
                "\n- key       : The encryption key" +
                "\n- data      : The data to encrypt")
            }},
            { "Decrypt", new List<(string, string)> {
                ("Decrypt(data, key, algorithm)", "Decrypts the given encrypted data using the specified parameters:" +
                "\n- parameters: The decryptiion algorithm (e.g., AES-CBC) and additional parameters (e.g. IV)"+
                "\n- key       : The decryption key" +
                "\n- data      : The data to decrypt")
            }},
            { "GenerateKey", new List<(string, string)> {
                ("GenerateKey(mechanism,length)", "Generates a symmetric key for a given algorithm of the specified length:" +
                "\n- mechanism: The encryption algorithm (e.g., AES-CBC)" +
                "\n- length   : Key length in bits")
            }},
            { "Parameters", new List<(string, string)> {
                ("Parameters(mechanism)", "Generates default random parameters for the given mechanism:" +
                "\n- mechanism: The algorithm to generate default parameters (e.g., AES-CBC)"),
                ("Parameters(mechanism,param-1,..,param-N)", "Generates default random parameters for the given mechanism except for the parameters supplied in function call" +
                "\n- mechanism         : The encryption algorithm (e.g., AES-CBC)" +
                "\n- param-1,..,param-N: A parameter starts with # and a id (e.g. PAD: or IV:) and a predefined value (e.g. #PAD:PKCS-7)")
            }},
            { "Print", new List<(string, string)> {
                ("Print(data)", "Prints the data to the output window:" +
                "\n- data: The data to print")
            }},
            { "Info", new List<(string, string)> {
                ("Info(type)", "Prints information about the current type to output window:"+
                "\n- type=types     : List of all supported types "+
                "\n- type=functions : List of all supported functions" +
                "\n- type=mechanisms: List of all suportetd mechanism" +
                "\n- type=mechanism : Specific mechanism e.g. AES-CBC"
                )
            }}
        };
        public  MyOverloadProvider? GetOverlays(string Text,int Offset)
        {
            string functionName = "";           
            foreach (var f in OperationList.Operations)
            {
                if (Offset > f.Length)
                {
                    var textBeforeCaret = Text.Substring(Offset - (f.Length+1), f.Length);
                    if (textBeforeCaret.Contains(f))
                    {
                        functionName = f;
                        break;
                    }
                }                   
            }
            if(functionName == "")
                return null;
            var overloads = new List<(string header, string description)>();
            if (functionOverloads.TryGetValue(functionName, out overloads))
            {
                var Provider = new MyOverloadProvider(overloads);
                return Provider;
            }
            else
                return null;
        }
    }
    
}
