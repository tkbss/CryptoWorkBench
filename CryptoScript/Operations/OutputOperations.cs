using CryptoScript.CryptoAlgorithm;
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
        public static event Action<string>? InfoEvent;
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
            
            string output=string.Empty;
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
                case "parameters":
                    output += ParameterList();
                    break;
                case "AES-CBC":
                    output += AES_CBC();
                    break;
                    case "keymap":
                        output += KeyMap();
                        break;
                    case "paddings":
                        output += PaddingList();
                        break;
                default:
                    Console.WriteLine("No info available");
                    break;
            }
            
            if (InfoEvent != null)
                InfoEvent?.Invoke(output);
            Console.WriteLine(output);
            return new VariableDeclaration();
        }
        private string AES_CBC()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mech.AES-CBC.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;
        }
        private string FunctionList()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Functions.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;         
        }
        private string KeyMap() 
        { 
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.KeyMap.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;
        }
        private string ParameterList()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Parameters.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;
        }
        private string MechanismList() 
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Mechanisms.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;
            

        }
        private string TypeList()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Types.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;            
        }
        private string PaddingList()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", "Info.Paddings.md");
            var InfoText = File.ReadAllText(path);
            return InfoText;
        }
    }
}