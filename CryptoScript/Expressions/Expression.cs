using CryptoScript.Variables;
using System;
using System.Collections.Generic;
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
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            return stmt; 
        }
        static public Expression Create(string expr)
        {
            string PathPattern = @"^[a-zA-Z]:\\(?:[^\\]*\\?)*$";
            

            if (Regex.IsMatch(expr, PathPattern))
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
