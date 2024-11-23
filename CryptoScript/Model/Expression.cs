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
            if (stmt is ExpressionHex expressionHex)
            {
                var newVariable = new StringVariableDeclaration();
                newVariable.Id = Id;
                newVariable.Value = expressionHex.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionHex.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionNumber expressionNu)
            {
                var newVariable = new StringVariableDeclaration();
                newVariable.Id = Id;
                newVariable.Value = expressionNu.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionNu.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionBase64 expressionB64)
            {
                var newVariable = new StringVariableDeclaration();
                newVariable.Id = Id;
                newVariable.Value = expressionB64.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionB64.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionJSON expressionJS)
            {
                var newVariable = new StringVariableDeclaration();
                newVariable.Id = Id;
                newVariable.Value = expressionJS.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionJS.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            if (stmt is ExpressionPath expressionPath)
            {
                var newVariable = new PathVariableDeclaration();
                newVariable.Id = Id;
                newVariable.Value = expressionPath.Value();
                newVariable.Type = type;
                newVariable.ValueFormat = FormatConversions.ParseString(expressionPath.Value());
                VariableDictionary.Instance().Add(newVariable);
                return newVariable;
            }
            return stmt; 
        }
        static public Expression Create(string expr)
        {
            string pattern = @"^[a-zA-Z]:\\(?:[^\\]*\\?)*$";
            

            if (Regex.IsMatch(expr, pattern))
            {
                return new ExpressionPath() { PathValue = expr };
            }
                        
            if (expr.StartsWith("0x("))
                return new ExpressionHex() { HexValue = expr };//value.HexValue = expr;
            else if (expr.StartsWith("b64("))
                return new ExpressionBase64() { Base64Value = expr };//value.Base64Value = expr;
            else if ((expr.StartsWith("{") && expr.EndsWith("}")) || //For object
                (expr.StartsWith("[") && expr.EndsWith("]"))) //For array
                return new ExpressionJSON() { JSONValue = expr };//value.JSONValue = expr;
            else
                return new ExpressionNumber() { IntegerValue=expr };//value.StringValue = expr;           
        }

        
        public class ExpressionHex : Expression
        {
            public string HexValue { get;  set; }=string.Empty;

            public override string Value()
            {
                return HexValue;
            }
        }
        public class ExpressionBase64 : Expression 
        {
            public string Base64Value { get;  set; }=string.Empty;

            public override string Value()
            {
                return Base64Value;
            }
        }
        public class ExpressionJSON : Expression
        {
            public string JSONValue { get; set; } = string.Empty;

            public override string Value()
            {
                return JSONValue;
            }
        }
        public class ExpressionNumber : Expression 
        {
            public string IntegerValue { get; set; } = string.Empty;

            public override string Value()
            {
                return IntegerValue;
            }
        }
        public class ExpressionPath : Expression 
        {
            public string PathValue { get; set; } = string.Empty;
            public override string Value()
            {
                return PathValue;
            }
        }
    }
    
    
}
