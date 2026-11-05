using CryptoScript.ErrorListner;
using CryptoScript.Variables;

namespace CryptoScript.Model;

internal static class PathInitializerEvaluator
{
    internal static ExpressionPath Prepare(string literal, List<SemanticError> semanticErrors)
    {
        string raw = literal;
        // PATH has its own literal mapping; other declared types retain their existing behavior.
        if (raw.StartsWith('"') && raw.EndsWith('"'))
            raw = System.Text.RegularExpressions.Regex.Replace(raw[1..^1], @"\\([btnrf""'\\])",
                match => match.Groups[1].Value[0] switch
                {
                    'b' => "\b", 't' => "\t", 'n' => "\n", 'r' => "\r", 'f' => "\f",
                    var character => character.ToString()
                });
        else if (!FormatConversions.IsPathRegex(raw))
        {
            var error = new SemanticError { Type = "Declaration",
                DiagnosticCode = SemanticDiagnosticCode.InvalidDeclaration,
                Message = "PATH requires a path literal or a quoted string." };
            semanticErrors.Add(error);
            throw new SemanticErrorException { SemanticError = error };
        }
        return new ExpressionPath { PathValue = raw };
    }
}
