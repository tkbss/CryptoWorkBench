using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Variables;
using static CryptoScript.Serialization.SnapshotJson;

namespace CryptoScript.Serialization;

internal static class HeaderSnapshotCodec
{
    internal static HeaderPayloadDto Encode(BlockHeaderVariableDeclaration header)
    {
        var d = header.Definition;
        var source = SourceDto.Capture(header);
        return d is null ? new(null, header.Value, source) : new(new(
            d.Attributes.Select(p => new FieldDto(p.Key, p.Value)).ToList(),
            d.OptionalBlocks.Select(b => new AttributeDto(b.ID, b.Length, b.Data)).ToList(), d.ExpectedLength), null, source);
    }

    internal static BlockHeaderVariableDeclaration Decode(HeaderPayloadDto payload)
    {
        if (payload.Definition is null)
        {
            Require(payload.LegacyValue is not null, "Missing legacy header value.");
            // Legacy runtime headers are retained verbatim; operation-time validation
            // remains with the legacy TR-31 reader.
            return new() { Value = payload.LegacyValue! };
        }
        Require(payload.LegacyValue is null, "Conflicting header representations.");
        var dto = payload.Definition;
        var fields = dto.Attributes.Select(p => KeyValuePair.Create(p.Name, p.Value)).ToList();
        Require(fields.All(p => p.Key is not ("OPTID" or "OPTBD")), "Optional blocks belong in optionalBlocks.");
        foreach (var block in dto.OptionalBlocks)
        {
            Require(block.Id is not null && block.Data is not null, "Incomplete optional block.");
            fields.Add(KeyValuePair.Create("OPTID", block.Id!));
            fields.Add(KeyValuePair.Create("OPTBD", Write(block.Data!).ToString(Newtonsoft.Json.Formatting.None)));
        }
        var definition = Tr31HeaderDefinition.Create(fields);
        // Create validates and normalizes. A snapshot must already contain that full
        // state: reject data which would introduce defaults, padding or normalization.
        Require(definition.ExpectedLength == dto.ExpectedLength &&
            definition.Attributes.SequenceEqual(dto.Attributes.Select(p => KeyValuePair.Create(p.Name, p.Value))) &&
            definition.OptionalBlocks.Select(b => new AttributeDto(b.ID, b.Length, b.Data)).SequenceEqual(dto.OptionalBlocks),
            "Header snapshot is incomplete or inconsistent.");
        return new() { Definition = definition };
    }
}
