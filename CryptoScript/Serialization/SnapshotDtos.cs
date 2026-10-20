using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CryptoScript.Serialization;

// Only these concrete DTOs cross the JSON boundary. Every property is mandatory;
// AllowNull distinguishes a deliberately unavailable value from a missing field.
internal sealed record SnapshotDto(string Format, int Version,
    List<VariableDto> Variables, List<VariableDto> Dependencies);
internal sealed record VariableDto(string ObjectId, string Identifier, JObject Metadata, JToken Value);
internal record MetadataDto(string Type, string Format, string PayloadKind,
    [property: JsonProperty(Required = Required.AllowNull)] string? RuntimeTypeId);
internal sealed record ScalarMetadataDto(string Type, string Format, string PayloadKind,
    string? RuntimeTypeId,
    string Gmac) : MetadataDto(Type, Format, PayloadKind, RuntimeTypeId);
internal sealed record KeyMetadataDto(string Type, string Format, string PayloadKind,
    string? RuntimeTypeId,
    KeyTypeDto KeyType, ulong KeySizeInBits, string KeySize, UsageDto Usage, string DerivationMechanism,
    [property: JsonProperty(Required = Required.AllowNull)] string? Kcv,
    [property: JsonProperty(Required = Required.AllowNull)] string? Kcvc,
    [property: JsonProperty(Required = Required.AllowNull)] string? Kcvk)
    : MetadataDto(Type, Format, PayloadKind, RuntimeTypeId);
internal sealed record ParameterMetadataDto(string Type, string Format, string PayloadKind,
    string? RuntimeTypeId,
    string Mechanism) : MetadataDto(Type, Format, PayloadKind, RuntimeTypeId);
internal sealed record KeyTypeDto(string Algorithm, string MaterialKind);
internal sealed record UsageDto(string Mode, int AllowedUsages);
internal sealed record AttributeDto(
    [property: JsonProperty(Required = Required.AllowNull)] string? Id, int Length,
    [property: JsonProperty(Required = Required.AllowNull)] string? Data);
internal sealed record SourceDto(string Text,
    [property: JsonProperty(Required = Required.AllowNull)] string? TypeText)
{
    internal static SourceDto Capture(Variables.VariableDeclaration variable) =>
        new(variable.Text, variable.Type?.Text);
}
internal sealed record ScalarPayloadDto(string Content, SourceDto Source);
internal sealed record KeyPayloadDto(string Material, List<AttributeDto> Attributes, SourceDto Source);
internal sealed record FieldDto(string Name, string Value);
internal sealed record ParameterPayloadDto(string Text, List<FieldDto> Parameters,
    List<string> ExplicitParameterNames,
    [property: JsonProperty(Required = Required.AllowNull)] string? HeaderReference, SourceDto Source);
internal sealed record HeaderPayloadDto(
    [property: JsonProperty(Required = Required.AllowNull)] HeaderDefinitionDto? Definition,
    [property: JsonProperty(Required = Required.AllowNull)] string? LegacyValue, SourceDto Source);
internal sealed record HeaderDefinitionDto(List<FieldDto> Attributes, List<AttributeDto> OptionalBlocks,
    [property: JsonProperty(Required = Required.AllowNull)] int? ExpectedLength);
internal sealed record SecretPayloadDto(SecretDescriptorDto Descriptor, string PreferredShareFormat,
    List<ShareDto> Shares,
    [property: JsonProperty(Required = Required.AllowNull)] SecretKeyMetadataDto? OriginalKeyMetadata, SourceDto Source);
internal sealed record SecretDescriptorDto(string Mechanism, int Parts, int MinParts, int KeySizeBits,
    [property: JsonProperty(Required = Required.AllowNull)] string? Profile);
internal sealed record ShareDto(int X, string Bytes);
internal sealed record SecretKeyMetadataDto(KeyTypeDto KeyType, UsageDto Usage,
    string DerivationMechanism, List<AttributeDto> Attributes);
