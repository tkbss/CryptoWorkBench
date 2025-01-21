using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class OutputOperations
    {
        public static event Action<string>? PrintEvent;
        public VariableDeclaration Print(string[] args) 
        {
            string output= "out: "+ args[0];
            if(PrintEvent != null)
                PrintEvent?.Invoke(output);
            Console.WriteLine(output);
            return new VariableDeclaration();
        }
        public VariableDeclaration Info(string[] args)
        {
            string output = "out:\n";
            switch (args[0])
            {
                case "functions":
                    output+=FunctionList();
                    break;
                case "mechanisms":
                    output += MechanismList();
                    break;
                    case "types":
                    output += TypeList();
                    break;
                default:
                    Console.WriteLine("No info available");
                    break;
            }
            output+= "end:";
            if (PrintEvent != null)
                PrintEvent?.Invoke(output);
            Console.WriteLine(output);
            return new VariableDeclaration();
        }
        private string FunctionList()
        {
            return "Function List: Upper and lower case letters are mandatory. Parameters are defined in overlay window during typing.\n" +
                "   - VAR s   = Sign(parameters,key,data)        : Sign a message\n" +
                "   - VAR e   = Encrypt(parameters,key,data)     : Encrypt a message\n" +
                "   - VAR d   = Decrypt(parameters,key,data)     : Decrypt a message\n" +
                "   - VAR m   = Mac(parameters,key,data)         : Compute a message authentication code (MAC)\n" +
                "   - KEY k   = GenerateKey(mechanism,length)    : Generate a key\n" +
                "   - PARAM p = Parameters(mechanism,p1,..,pn)   : Generate parameters\n" +
                "   - Print(data)                                : Print variables\n" +
                "   - Info(type)                                 : Crypto Script information\n"+
                "   - VAR   d = Load(VAR,path)                   : Crypto Script information\n"+
                "   - PARAM p = Load(PARAM,path)                 : Crypto Script information\n" +
                "   - KEY   k=  Load(KEY,path)                   : Crypto Script information\n"; 
                
        }
        private string MechanismList() 
        {
            return "Mechanism List: Upper case letters are mandatory. The mechanism can be used only in the defined operations.\n" +
                "   - AES-ECB: Algorithm is AES, Mode is ECB \n" +
                "       \u2022 Operations: Encrypt, Decrypt\n" +
                "   - AES-CBC: Algorithm is AES, Mode is CBC\n" +
                "       \u2022 Operations: Encrypt, Decrypt\n" +
                "   - AES-CTR: Algorithm is AES, Mode is CTR \n" +
                "       \u2022 Operations: Encrypt, Decrypt\n" +
                "   - AES-CMAC: Algorithm is AES, Mode is CMAC\n" +
                "       \u2022 Operations: Mac\n" +
                "   - AES-GCM: Algorithm is AES, Mode is GCM\n" +
                "       \u2022 Operations: Encrypt, Decrypt\n" +
                "   - AES-CCM: Algorithm is AES, Mode is GCM\n" +
                "       \u2022 Operations: Encrypt, Decrypt\n" +
                "   - AES-GMAC: Algorithm is AES, Mode is GMAC\n" +
                "       \u2022 Operations: Mac\n";
                
        }
        private string TypeList()
        {
            return "Types: Upper case letters are mandatory.\n" +
                "   - PATH : file path declaration\n" +
                "       \u2022 PATH p=c:\\dir1\\dir2\\file.txt\n" +
                "   - VAR  : Data declaration\n" +
                "       \u2022 VAR d=Function(p1,..pn)      : Some of the functions in Function List\n" +
                "       \u2022 VAR d=Load(VAR,path)         : Loads data from the file specified through path\n" +
                "       \u2022 VAR d=FORMAT(DATA)\n" +
                "       \u2022 FORMAT(HEX-STRING)           : 0x(HEX-DATA)\n" +
                "       \u2022 FORMAT(BASE64-STRING)        : b64(BASE64-DATA)\n" +
                "   - KEY  : Key declaration\n" +
                "       \u2022 KEY k=GenerateKey(mechanism,key-length)\n" +
                "       \u2022 KEY k=Load(KEY,path)         : Loads key from the file specified through path\n" +
                "   - PARAM: Parameter declaration\n" +
                "       \u2022 VAR p=Parameters(mechanism)  : Generates default parameters for corresponding mechanism\n"+
                "       \u2022 VAR p=Load(PARAM,path)       : Loads parameters from the file specified through path\n" +
                "       \u2022 VAR p=#MECH:AES-CBC #IV:0x(123...) : Declaration string for AES-CBC parameters\n" +
                "       \u2022 VAR p=#MECH:AES-CTR #NONCE:0x(123...) #COUNTER:0x(00): Declaration string for AES-CTR parameters \n" +
                "       \u2022 VAR p=#MECH:AES-GCM #NONCE:0x(123...) #ADATA:\"AUTNETICATION_DATA\": Declaration string for AES-GCM parameters\n" +
                "       \u2022 VAR p=#MECH:AES-CCM #NONCE:0x(123...) #ADATA:\"AUTNETICATION_DATA\": Declaration string for AES-CCM parameters\n" +
                "       \u2022 VAR p=#MECH:AES-GMAC #NONCE:0x(123...): Declaration string for AES-GMAC parameters\n";
        }
    }
}