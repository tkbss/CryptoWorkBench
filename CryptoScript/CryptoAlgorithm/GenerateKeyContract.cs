using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm;

// Registry-owned permission and algorithm mapping, shared by operation dispatch
// and the concrete implementations so direct C# calls obey the same contract.
internal static class GenerateKeyContract
{
    internal static MechanismRegistryEntry RequireMechanism(string mechanism, KeyAlgorithm? implementation = null)
    {
        ArgumentNullException.ThrowIfNull(mechanism);
        string canonical = (mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
            ? mechanism[6..] : mechanism).ToUpperInvariant();
        if (!MechanismRegistry.TryGet(canonical, out var entry))
            throw new ArgumentException($"Unknown GenerateKey mechanism: {mechanism}.", nameof(mechanism));
        if (!entry!.Supports(CryptoScriptFunction.GenerateKey))
            throw new FunctionContractException(FunctionContractError.UnsupportedFunction,
                entry.CanonicalName, CryptoScriptFunction.GenerateKey);
        if (entry.GeneratedKeyAlgorithm is null)
            throw new InvalidOperationException($"Mechanism {entry.CanonicalName} has no GenerateKey algorithm.");
        if (implementation.HasValue && implementation != entry.GeneratedKeyAlgorithm)
            throw new ArgumentException(
                $"Mechanism {entry.CanonicalName} generates {entry.GeneratedKeyAlgorithm} keys and cannot be used by the {implementation} implementation.",
                nameof(mechanism));
        return entry;
    }

    internal static byte[] DecodeImport(string value)
    {
        // Validate the complete literal, including its closing delimiter. The shared
        // legacy decoder accepts raw hex and must not be relaxed or changed globally.
        if (!value.StartsWith("0x(", StringComparison.Ordinal) || !value.EndsWith(')') ||
            (value.Length - 4) % 2 != 0 ||
            value.AsSpan(3, value.Length - 4).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            throw new ArgumentException("GenerateKey import requires a complete hexadecimal 0x(...) value.", nameof(value));
        return Convert.FromHexString(value.AsSpan(3, value.Length - 4));
    }
}
