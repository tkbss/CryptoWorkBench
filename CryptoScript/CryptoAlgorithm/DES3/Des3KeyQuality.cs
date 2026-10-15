using System.Numerics;
using System.Security.Cryptography;
using CryptoScript.ErrorListner;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.CryptoAlgorithm.DES3;

/// <summary>Metadata-free DES quality inspection; never changes the borrowed key bytes.</summary>
internal static class Des3KeyQuality
{
    internal static void Report(byte[] key, string? identifier, string function, string mechanism,
        ISemanticDiagnosticSink? sink)
    {
        DES3.ValidateKeyLength(key);
        if (sink is null) return;
        var parity = new List<string>();
        var weaknesses = new List<string>();
        byte[] normalized = (byte[])key.Clone();
        try
        {
            DesParameters.SetOddParity(normalized);
            for (int offset = 0; offset < key.Length; offset += 8)
            {
                string component = "K" + (offset / 8 + 1);
                bool invalidParity = false;
                for (int i = offset; i < offset + 8; i++)
                    invalidParity |= (BitOperations.PopCount((uint)key[i]) & 1) == 0;
                if (invalidParity)
                    parity.Add(component);
                byte[] part = normalized.AsSpan(offset, 8).ToArray();
                try
                {
                    if (DES.IsWeakKey(part)) weaknesses.Add($"known weak DES component {component}");
                    else if (DES.IsSemiWeakKey(part)) weaknesses.Add($"known semi-weak DES component {component}");
                }
                finally { CryptographicOperations.ZeroMemory(part); }
            }
            if (normalized.AsSpan(0, 8).SequenceEqual(normalized.AsSpan(8, 8)))
                weaknesses.Add("effective K1 = K2: TDEA degenerates to single DES");
            if (key.Length == 24 && normalized.AsSpan(8, 8).SequenceEqual(normalized.AsSpan(16, 8)))
                weaknesses.Add("effective K2 = K3: TDEA degenerates to single DES");
            if (parity.Count > 0)
                ReportWarning(SemanticDiagnosticCode.DesParityViolation,
                    "Invalid odd DES parity in " + string.Join(", ", parity) + ". Original key bytes are retained.");
            if (weaknesses.Count > 0)
                ReportWarning(SemanticDiagnosticCode.WeakDes3Key, string.Join("; ", weaknesses) + ".");
        }
        finally { CryptographicOperations.ZeroMemory(normalized); }

        void ReportWarning(SemanticDiagnosticCode code, string message) => sink.Report(new SemanticError
        {
            Severity = SemanticDiagnosticSeverity.Warning, DiagnosticCode = code,
            Type = "KeyQuality", FunctionName = function, Identifier = identifier ?? string.Empty,
            Location = new DiagnosticLocation(FunctionName: function, Mechanism: mechanism, Identifier: identifier),
            Message = message
        });
    }
}
