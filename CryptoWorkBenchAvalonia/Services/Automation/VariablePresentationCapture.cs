using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
namespace CryptoWorkBenchAvalonia.Services.Automation;

internal static class VariablePresentationCapture
{
    internal static IReadOnlyList<VariableModel> Capture()
    {
        InterpreterExecution.VerifyAccess();
        var snapshot = new List<VariableModel>();
        foreach (var v in VariableDictionary.Instance().GetVariables())
        {
            var dv = new VariableModel()
            {
                Type = v.Type?.Name ?? string.Empty,
                Identifier = v.Id,
                Value = v.Value,
                ValueFormat = v.ValueFormat
            };
            switch (v)
            {
                case KeyVariableDeclaration a:
                    dv.IsKey = true;
                    dv.Algorithm = FormatAlgorithm(a.KeyType.Algorithm);
                    dv.Material = a.KeyType.MaterialKind.ToString();
                    dv.HasKeySize = a.KeySizeInBits.IsKnown;
                    dv.KeySize = a.KeySizeInBits.IsKnown
                        ? a.KeySizeInBits.Bits.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;
                    dv.Derivation = a.DerivationMechanism;
                    dv.HasDerivation = !string.IsNullOrWhiteSpace(a.DerivationMechanism);
                    break;
                case StringVariableDeclaration b:
                    dv.GMAC = b.GMAC;
                    break;
                case ParameterVariableDeclaration c:
                    dv.IsParameter = true;
                    dv.Mechanism = c.Mechanism;
                    var parameters = c.GetParameters();
                    foreach (var param in parameters)
                    {

                        dv.Parameters.Add(new ParameterModel
                        {
                            Parameter = param.Key,
                            Value = param.Value
                        });
                    }
                    break;
            }
            snapshot.Add(dv);
        }
        return snapshot;
    }
    private static string FormatAlgorithm(KeyAlgorithm algorithm) => algorithm switch
    {
        KeyAlgorithm.Aes => "AES",
        KeyAlgorithm.Tdea => "TDEA",
        KeyAlgorithm.Hmac => "HMAC",
        KeyAlgorithm.Rsa => "RSA",
        KeyAlgorithm.Ec => "EC",
        _ => "Unknown"
    };
}
