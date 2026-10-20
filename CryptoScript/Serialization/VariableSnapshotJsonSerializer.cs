using System.Globalization;
using System.Text.RegularExpressions;
using CryptoScript.Model;
using CryptoScript.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static CryptoScript.Serialization.SnapshotJson;

namespace CryptoScript.Serialization;

/// <summary>
/// Version 1 snapshots of detached variables. Does not execute scripts, access files,
/// generate material, or read/publish the interpreter's variable dictionary.
/// </summary>
public sealed class VariableSnapshotJsonSerializer
{
    public string Serialize(IEnumerable<VariableDeclaration> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return Guard(() =>
        {
            var roots = variables.ToArray();
            var ids = new Dictionary<VariableDeclaration, string>(ReferenceEqualityComparer.Instance);
            foreach (var variable in roots)
            {
                Require(variable is not null && !ids.ContainsKey(variable), "Duplicate or null root object.");
                ids.Add(variable!, ObjectId(ids.Count));
            }
            var dependencies = new List<VariableDeclaration>();
            foreach (var p in roots.OfType<ParameterVariableDeclaration>())
                if (p.HeaderReference is { } header && !ids.ContainsKey(header))
                {
                    ids.Add(header, ObjectId(ids.Count));
                    dependencies.Add(header);
                }
            var dto = new SnapshotDto("CryptoScriptVariables", 1,
                roots.Select(v => Encode(v, ids)).ToList(), dependencies.Select(v => Encode(v, ids)).ToList());
            // Apply the same validation in both directions, on detached objects only.
            var token = Write(dto);
            _ = Decode(Read<SnapshotDto>(token));
            return token.ToString(Formatting.Indented);
        });
    }

    public IReadOnlyList<VariableDeclaration> Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Guard(() => Decode(Read<SnapshotDto>(Parse(json))));
    }

    private static string ObjectId(int index) => "o" + (index + 1).ToString(CultureInfo.InvariantCulture);

    private static VariableDto Encode(VariableDeclaration variable,
        IReadOnlyDictionary<VariableDeclaration, string> ids)
    {
        string type = variable switch
        {
            StringVariableDeclaration => "VAR",
            KeyVariableDeclaration => "KEY",
            ParameterVariableDeclaration => "PARAM",
            PathVariableDeclaration => "PATH",
            BlockHeaderVariableDeclaration => "TR31H",
            SecretVariableDeclaration => "SECRET",
            _ => throw Error("Unsupported variable class.")
        };
        Type expectedClass = type switch
        {
            "VAR" => typeof(StringVariableDeclaration), "KEY" => typeof(KeyVariableDeclaration),
            "PARAM" => typeof(ParameterVariableDeclaration), "PATH" => typeof(PathVariableDeclaration),
            "TR31H" => typeof(BlockHeaderVariableDeclaration), _ => typeof(SecretVariableDeclaration)
        };
        Require(variable.GetType() == expectedClass, "Custom variable subclasses are not supported.");
        Require(variable.Type is null || variable.Type.GetType() == CryptoType.Parse(type).GetType(),
            "Variable class and declared type conflict.");
        var source = SourceDto.Capture(variable);
        bool scalar = (type is "VAR" or "PATH") && source.Text == "" &&
            (variable.Type is null || source.TypeText == "");
        string kind = scalar ? "scalar" : "structured";
        MetadataDto meta = new(type, variable.ValueFormat, kind, variable.Type?.Id);
        object payload;
        switch (variable)
        {
            case StringVariableDeclaration value:
                meta = new ScalarMetadataDto(type, value.ValueFormat, kind, value.Type?.Id, value.GMAC);
                payload = scalar ? value.Value : new ScalarPayloadDto(value.Value, source);
                break;
            case KeyVariableDeclaration key:
                (var keyMeta, var keyPayload) = KeySnapshotCodec.Encode(key);
                meta = keyMeta;
                payload = keyPayload;
                break;
            case ParameterVariableDeclaration parameter:
                meta = new ParameterMetadataDto(type, parameter.ValueFormat, "structured", parameter.Type?.Id, parameter.Mechanism);
                payload = ParameterSnapshotCodec.Encode(parameter,
                    parameter.HeaderReference is null ? null : ids[parameter.HeaderReference]);
                break;
            case BlockHeaderVariableDeclaration header:
                payload = HeaderSnapshotCodec.Encode(header);
                break;
            case SecretVariableDeclaration secret:
                payload = SecretSnapshotCodec.Encode(secret);
                break;
            default:
                payload = scalar ? variable.Value : new ScalarPayloadDto(variable.Value, source);
                break;
        }
        return new(ids[variable], variable.Id, (JObject)Write(meta), Write(payload));
    }

    private static IReadOnlyList<VariableDeclaration> Decode(SnapshotDto snapshot)
    {
        Require(snapshot.Format == "CryptoScriptVariables" && snapshot.Version == 1, "Unsupported snapshot format or version.");
        var all = snapshot.Variables.Concat(snapshot.Dependencies).ToArray();
        var entries = new Dictionary<string, VariableDto>(StringComparer.Ordinal);
        var roots = new HashSet<VariableDto>(snapshot.Variables, ReferenceEqualityComparer.Instance);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in all)
        {
            Require(Regex.IsMatch(entry.ObjectId, @"\Ao[1-9][0-9]*\z", RegexOptions.CultureInvariant) &&
                entries.TryAdd(entry.ObjectId, entry), "Invalid or duplicate object ID.");
            // Dependencies can be anonymous or share a name with a different root;
            // identity is always objectId, never identifier.
            bool root = roots.Contains(entry);
            Require(!root && entry.Identifier.Length == 0 || ValidIdentifier(entry.Identifier), "Invalid variable identifier.");
            if (root) Require(names.Add(entry.Identifier), "Duplicate root identifier.");
        }
        var objects = new Dictionary<string, VariableDeclaration>(StringComparer.Ordinal);
        var metadata = new Dictionary<string, MetadataDto>(StringComparer.Ordinal);
        foreach (var entry in all)
        {
            string type = entry.Metadata["type"]?.Type == JTokenType.String
                ? entry.Metadata["type"]!.Value<string>()! : throw Error("Missing variable type.");
            MetadataDto meta = type switch
            {
                "VAR" => Read<ScalarMetadataDto>(entry.Metadata),
                "KEY" => Read<KeyMetadataDto>(entry.Metadata),
                "PARAM" => Read<ParameterMetadataDto>(entry.Metadata),
                "PATH" or "TR31H" or "SECRET" => Read<MetadataDto>(entry.Metadata),
                _ => throw Error("Unsupported variable type.")
            };
            Require((meta.PayloadKind == "structured" || meta.PayloadKind == "scalar" && (type is "VAR" or "PATH")) &&
                entry.Value.Type == (meta.PayloadKind == "scalar" ? JTokenType.String : JTokenType.Object),
                "Invalid payload kind or shape.");
            Require(KnownFormat(meta.Format), "Unsupported value format.");
            metadata.Add(entry.ObjectId, meta);
            if (type == "TR31H")
                objects.Add(entry.ObjectId, HeaderSnapshotCodec.Decode(Read<HeaderPayloadDto>(entry.Value)));
        }
        foreach (var dependency in snapshot.Dependencies)
            Require(metadata[dependency.ObjectId].Type == "TR31H", "Only headers may be embedded dependencies.");

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in all)
        {
            var meta = metadata[entry.ObjectId];
            var source = meta.PayloadKind == "scalar"
                ? new SourceDto("", meta.RuntimeTypeId is null ? null : "")
                : Read<SourceDto>(entry.Value["source"] ?? throw Error("Missing source state."));
            Require((source.TypeText is null) == (meta.RuntimeTypeId is null), "Inconsistent runtime type source state.");
            VariableDeclaration variable;
            switch (meta.Type)
            {
                case "TR31H": variable = objects[entry.ObjectId]; break;
                case "VAR":
                    variable = new StringVariableDeclaration { Value = ScalarValue(entry, meta), GMAC = ((ScalarMetadataDto)meta).Gmac };
                    break;
                case "PATH": variable = new PathVariableDeclaration { Value = ScalarValue(entry, meta) }; break;
                case "KEY": variable = KeySnapshotCodec.Decode((KeyMetadataDto)meta, Read<KeyPayloadDto>(entry.Value)); break;
                case "SECRET": variable = SecretSnapshotCodec.Decode(Read<SecretPayloadDto>(entry.Value)); break;
                case "PARAM":
                    var payload = Read<ParameterPayloadDto>(entry.Value);
                    BlockHeaderVariableDeclaration? header = null;
                    if (payload.HeaderReference is { } reference)
                    {
                        Require(objects.TryGetValue(reference, out var target) && target is BlockHeaderVariableDeclaration,
                            "Missing or non-header reference target.");
                        header = (BlockHeaderVariableDeclaration)target!;
                        referenced.Add(reference);
                    }
                    variable = ParameterSnapshotCodec.Decode((ParameterMetadataDto)meta, payload, header);
                    break;
                default: throw Error("Unsupported type.");
            }
            variable.Id = entry.Identifier;
            variable.ValueFormat = meta.Format;
            variable.Text = source.Text;
            variable.Type = meta.RuntimeTypeId is null ? null : CryptoType.Parse(meta.Type);
            if (variable.Type is not null)
            {
                variable.Type.Id = meta.RuntimeTypeId!;
                variable.Type.Text = source.TypeText!;
            }
            objects[entry.ObjectId] = variable;
        }
        Require(snapshot.Dependencies.All(d => referenced.Contains(d.ObjectId)), "Unreferenced dependency.");
        return Array.AsReadOnly(snapshot.Variables.Select(v => objects[v.ObjectId]).ToArray());
    }

    private static string ScalarValue(VariableDto entry, MetadataDto meta) => meta.PayloadKind == "scalar"
        ? entry.Value.Value<string>()! : Read<ScalarPayloadDto>(entry.Value).Content;

    private static bool ValidIdentifier(string value)
    {
        if (!Regex.IsMatch(value, @"\A[A-Za-z][A-Za-z0-9]*\z", RegexOptions.CultureInvariant)) return false;
        var lexer = new CryptoScriptLexer(new Antlr4.Runtime.AntlrInputStream(value));
        lexer.RemoveErrorListeners();
        return lexer.NextToken().Type == CryptoScriptLexer.ID;
    }

    private static bool KnownFormat(string value) => value == "" || value == FormatConversions.HEX ||
        value == FormatConversions.B64 || value == FormatConversions.STR || value == FormatConversions.JSO ||
        value == FormatConversions.PAR || value == FormatConversions.PATH || value == FormatConversions.TR31;

    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        // Do not include JSON values or cryptographic material in diagnostics.
        catch (Exception error) when (error is JsonException or System.Text.Json.JsonException or
            ArgumentException or InvalidOperationException or OverflowException or FormatException or System.IO.IOException or
            FunctionContractException or Org.BouncyCastle.Security.SecurityUtilityException)
        {
            throw new JsonSerializationException("Invalid CryptoScript variable snapshot.");
        }
    }
}
