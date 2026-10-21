using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class Expression : Statement
    {
        
        
        public virtual string Value() 
        {
            return string.Empty;
        }
        public static Statement BuildVariable(Statement stmt,string Id, CryptoType type) 
        {
            if (type is CryptoTypeKey && stmt is Expression keyExpression)
            {
                var key = BuildLiteralKey(keyExpression, Id, type);
                VariableDictionary.Instance().Add(key);
                return key;
            }
            var newVariable = new StringVariableDeclaration();
            if (stmt is ExpressionString expressionString)
            {

                newVariable.Id = Id;
                newVariable.Value = expressionString.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionString.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionTR31String expressionTR31)
            {
                
                newVariable.Id = Id;
                newVariable.Value = expressionTR31.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionTR31.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionHex expressionHex)
            {
                
                newVariable.Id = Id;
                newVariable.Value = expressionHex.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionHex.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionNumber expressionNu)
            {
                newVariable.Id = Id;
                newVariable.Value = expressionNu.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionNu.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionBase64 expressionB64)
            {
                newVariable.Id = Id;
                newVariable.Value = expressionB64.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionB64.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionJSON expressionJS)
            {
                newVariable.Id = Id;
                newVariable.Value = expressionJS.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionJS.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionPath expressionPath)
            {
                var pathVariable = new PathVariableDeclaration();
                pathVariable.Id = Id;
                pathVariable.Value = expressionPath.Value();
                pathVariable.Type = type;
                pathVariable.ValueFormat = FormatConversions.ParseString(expressionPath.Value());
                VariableDictionary.Instance().Add(pathVariable);
                return pathVariable;
            }
            return stmt; 
        }
        private static KeyVariableDeclaration BuildLiteralKey(Expression expression, string id, CryptoType type)
        {
            string literal = expression.Value();
            string format;
            byte[] material;
            if (expression is ExpressionHex && literal.StartsWith("0x(", StringComparison.Ordinal) && literal.EndsWith(')'))
            {
                format = FormatConversions.HEX;
                material = Convert.FromHexString(literal[3..^1]);
            }
            else if (expression is ExpressionBase64 && literal.StartsWith("b64(", StringComparison.Ordinal) && literal.EndsWith(')'))
            {
                format = FormatConversions.B64;
                material = Convert.FromBase64String(literal[4..^1]);
            }
            else throw new ArgumentException("KEY requires a nonempty HEX or Base64 literal.");

            try
            {
                if (material.Length == 0)
                    throw new ArgumentException("KEY requires nonempty binary material.");
                return new KeyVariableDeclaration
                {
                    Id = id, Type = type, Value = literal, ValueFormat = format,
                    KeySizeInBits = new KeySize(checked((ulong)material.Length * 8))
                    // Defaults remain Secret(Unknown), Unspecified usage, no derivation or attributes.
                };
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(material); }
        }

        static public Expression Create(string expr)
        {           

            if (FormatConversions.IsPathRegex(expr))            
            {
                return new ExpressionPath() { PathValue = expr };
            }
                        
            if (expr.StartsWith("0x("))
                return new ExpressionHex() { HexValue = expr };
            else if (expr.StartsWith("b64("))
                return new ExpressionBase64() { Base64Value = expr };
            else if ((expr.StartsWith("{") && expr.EndsWith("}")) || //For object
                (expr.StartsWith("[") && expr.EndsWith("]"))) //For array
                return new ExpressionJSON() { JSONValue = expr };
            else if (int.TryParse(expr, out _))
                return new ExpressionNumber() { IntegerValue = expr };
            else if(expr.StartsWith("\""))
                return new ExpressionString() { StringValue=expr };
            else if(FormatConversions.ParseString(expr)==FormatConversions.TR31)
                return new ExpressionTR31String() { TR31StringValue = expr };
            return new ExpressionString() { StringValue = "\"\"" };
        }      
        
        
        
        
        
    }
    
    
}
