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
            { "MacPack", new List<(string, string)> {
                ("MacPack(parameters, key, source)", "Returns PATH to a new CWM container with a streaming file MAC and HKDF/HMAC-SHA256 authenticated JCS metadata. Requires PARAM, KEY, PATH references.")
            }},
            { "MacVerify", new List<(string, string)> {
                ("MacVerify(parameters, key, container)", "Returns VAL true/false after metadata and file authentication. Requires PARAM, KEY, PATH references; metadata is authenticated before opening the file.")
            }},
            { "CreateHeader", new List<(string, string)> {
                ("CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)",
                 "Returns TR31H. Optional KEYVN:00, KEYCTX:0 and repeated OPTID/OPTBD pairs. Wrap computes KBLEN. See Info(tr31attributes).")
            }},

            { "Share", new List<(string, string)> {
                ("Share(parameters, key)", "Creates a SECRET containing all XOR or Shamir shares and independent key metadata. Requires PARAM and KEY references.")
            }},
            { "Secret", new List<(string, string)> {
                ("Secret(parameters, shares...)", "Creates a SECRET from repeated HEX literals (X-Y or Y-X), or decimal index/HEX pairs (SEPARATE-X). Incomplete sets are allowed.")
            }},
            { "Restore", new List<(string, string)> {
                ("Restore(secret)", "Reconstructs a KEY from the SECRET descriptor and enough consistent shares. This does not authenticate the key.")
            }},
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
                ("GenerateKey(mechanism,length)", "Generates a random symmetric key for a given algorithm of the specified length:" +
                "\n- mechanism: The encryption algorithm (e.g., AES-CBC)" +
                "\n- length   : Key length in bits"),
                ("GenerateKey(mechanism,length)", "Generates a predined symmetric key for a given algorithm from the supplied key block:" +
                "\n- mechanism: The encryption algorithm (e.g., AES-CBC)" +
                "\n- key   : predefined key")
            }},
            { "Parameters", new List<(string, string)> {
                ("Parameters(mechanism)", "Generates default random parameters for the given mechanism:" +
                "\n- mechanism: The algorithm to generate default parameters (e.g., AES-CBC)"),
                ("Parameters(param-1,..,param-N)", "Generates default random parameters for the given mechanism except for the parameters supplied in function call" +
                "\n- param-1 : The mechanism as #MECH:ALOG (e.g., AES-CBC)" +
                "\n- param-2,..,param-N: Predefined parameters needed by  #MECH in the form of #PARAM:VALUE (e.g. #PAD:PKCS-7).")
            }},
            { "PinBlockTranslate", new List<(string, string)> {
                ("PinBlockTranslate(sourceParams, sourceKey, encryptedPinBlock, targetParams, targetKey)",
                 "Translates an encrypted PIN block. Requires PARAM, KEY, VAR, PARAM, KEY references and distinct PARAMs. See Info(pintranslations) for PCI format and PAN rules.")
            }},
            { "Check", new List<(string, string)> {
                ("Check(value, method)", "Finds one visible KEY by a full HEX check value or byte prefix. Optional method: KCV, KCVC, KCVK. No match or ambiguity reports an error without declaring a KEY.")
            }},
            { "Print", new List<(string, string)> {
                ("Print(secret)", "Prints all full share values, indices, reconstruction metadata and copyable Secret statements. This discloses the shared material."),
                ("Print(key)", "Prints the complete key and its metadata, including full KCV, KCVC and KCVK values."),
                ("Print(data)", "Prints the data to the output window:" +
                "\n- data: The data to print")
            }},
            { "Info", new List<(string, string)> {
                ("Info(topic)", "Prints information about a topic to the output window:"+
                "\n- Info(types)      : List of all supported types "+
                "\n- Info(functions)  : List of all supported functions" +
                "\n- Info(mechanisms) : List of all supported mechanisms" +
                "\n- Info(parameters) : List of all supported parameters" +
                "\n- Info(AES-CBC)    : Information about a specific mechanism"
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
