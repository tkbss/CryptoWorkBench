using CryptoScript.Model;
using CryptoScript.Variables;
using static CryptoScript.Serialization.SnapshotJson;

namespace CryptoScript.Serialization;

internal static class ParameterSnapshotCodec
{
    internal static ParameterPayloadDto Encode(ParameterVariableDeclaration p, string? reference) =>
        new(p.Value, p.GetParameters().Select(v => new FieldDto(v.Key, v.Value)).ToList(),
            p.ExplicitParameterNames.ToList(), reference, SourceDto.Capture(p));

    internal static ParameterVariableDeclaration Decode(ParameterMetadataDto meta, ParameterPayloadDto payload,
        BlockHeaderVariableDeclaration? header)
    {
        // Call-only options must not expand the S01 PARAM metadata allowlist.
        var known = AntlrLanguageMetadata.GetParameters().Where(name => name != "#ONCONFLICT")
            .Append("#MACLEN").ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in payload.Parameters)
            Require(known.Contains(p.Name) && names.Add(p.Name), "Unknown or duplicate parameter.");
        Require(payload.ExplicitParameterNames.All(names.Contains), "Invalid explicit parameter provenance.");
        var pairs = payload.Parameters.Select(p => KeyValuePair.Create(p.Name, p.Value)).ToArray();
        Require(meta.Mechanism.Length == 0 || MechanismRegistry.TryGet(meta.Mechanism, out _), "Unknown mechanism.");
        var mech = pairs.FirstOrDefault(p => p.Key == "#MECH");
        Require(mech.Key is null ? meta.Mechanism.Length == 0 : mech.Value == meta.Mechanism,
            "Inconsistent parameter mechanism.");
        Require(header is null || names.Contains("#BLKH") &&
            meta.Mechanism is "WRAP-AES-TR31" or "WRAP-DES3-TR31", "Invalid typed header reference.");
        var result = ParameterVariableDeclaration.FromSnapshot(meta.Mechanism, pairs,
            payload.ExplicitParameterNames, payload.Text, header);
        // Validate the Parameters contract without invoking Parameters() or assuming
        // that operation-time inputs (Encrypt, Wrap, etc.) are already present.
        if (meta.Mechanism.Length > 0)
        {
            MechanismRegistry.TryGet(meta.Mechanism, out var mechanism);
            if (mechanism!.Supports(CryptoScriptFunction.Parameters))
                MechanismParameterContractValidator.Validate(meta.Mechanism, CryptoScriptFunction.Parameters, result);
            else
            {
                // TR-31 supports literal PARAM declarations but no Parameters() function.
                MechanismParameterContractValidator.ValidatePsnSupport(meta.Mechanism, result);
                MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(meta.Mechanism, result);
            }
        }
        return result;
    }
}
