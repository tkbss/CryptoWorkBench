using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CryptoWorkBench.Automation.Abstractions;

/// <summary>The boundary serializer. Both incoming data and outgoing host snapshots are validated.</summary>
public static class AutomationJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.Converters.Add(new ExactEnumConverterFactory());
        options.Converters.Add(new UtcConverter());
        options.Converters.Add(new UuidConverter());
        options.MakeReadOnly();
        return options;
    }

    public static T Deserialize<T>(string json) where T : class
    {
        using var document = JsonDocument.Parse(json);
        CheckInput(document.RootElement);
        var value = JsonSerializer.Deserialize<T>(json, Options) ?? throw Invalid();
        Validate(value);
        return value;
    }
    public static string Serialize<T>(T value) where T : class
    {
        Validate(value);
        return JsonSerializer.Serialize(value, Options);
    }
    private static void CheckInput(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw Invalid();
                if (p.Name is "offset" or "timeoutMs" or "outputPolicy" && p.Value.ValueKind == JsonValueKind.Null) throw Invalid();
                CheckInput(p.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) CheckInput(item);
    }
    internal static JsonException Invalid() => new("Invalid automation contract.");
    public static void Validate(object value)
    {
        if (value is null) throw Invalid();
        var type = value.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Reply<>))
        {
            var ok = (bool)type.GetProperty("Ok")!.GetValue(value)!;
            var data = type.GetProperty("Data")!.GetValue(value);
            var error = type.GetProperty("Error")!.GetValue(value);
            if ((string?)type.GetProperty("SchemaVersion")!.GetValue(value) != "1" ||
                (ok ? data is null || error is not null : data is not null || error is null)) throw Invalid();
        }
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var item = property.GetValue(value);
            if (item is null)
            {
                if (new NullabilityInfoContext().Create(property).ReadState == NullabilityState.NotNull) throw Invalid();
                continue;
            }
            if (property.Name is "Revision" or "ExpectedRevision" or "PreviousRevision" or "SourceRevision" or "StateVersion" or "ExpectedStateVersion")
                if (item is not string revision || revision.Length == 0 || revision.Any(c => c < '0' || c > '9')) throw Invalid();
            if (property.Name == "Text" && value is EditorEdit or EditorSnapshot or TextExecution && ((string)item).Length > AutomationContracts.MaxTextLength) throw Invalid();
            if (property.Name == "Length" && (int)item < 0) throw Invalid();
            if (property.Name == "TimeoutMs" && ((int)item < AutomationContracts.MinTimeoutMs || (int)item > AutomationContracts.MaxTimeoutMs)) throw Invalid();
            if (item is DateTimeOffset timestamp && timestamp.Offset != TimeSpan.Zero) throw Invalid();
            if (item is Enum && !Enum.IsDefined(item.GetType(), item)) throw Invalid();
            if (item is IEnumerable sequence && item is not string)
            {
                // A default ImmutableArray has no backing array and cannot be enumerated.
                if (item.GetType().IsGenericType && item.GetType().GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>)
                    && (bool)item.GetType().GetProperty("IsDefault")!.GetValue(item)!) throw Invalid();
                var count = 0;
                foreach (var entry in sequence)
                {
                    if (++count > AutomationContracts.MaxCollectionEntries || entry is null) throw Invalid();
                    if (entry.GetType().Namespace == typeof(AutomationJson).Namespace && !entry.GetType().IsEnum) Validate(entry);
                }
            }
            else if (item.GetType().Namespace == typeof(AutomationJson).Namespace && !item.GetType().IsEnum) Validate(item);
        }
        switch (value)
        {
            case EditorEdit edit:
                if ((edit.Mode == EditorEditMode.Insert) != edit.Offset.HasValue || edit.Offset < 0) throw Invalid();
                break;
            case EditorSnapshot editor when editor.Length != editor.Text.Length: throw Invalid();
            case VariableInfo variable when variable.ValueIncluded || variable.KeySizeBits < 0: throw Invalid();
            case Output output when output.Sequence < 1 || (output.Redacted && output.Text is not null): throw Invalid();
            case Diagnostic diagnostic when diagnostic.Line < 1 || diagnostic.Column < 0 || diagnostic.StatementIndex < 1: throw Invalid();
            case ExecutionResult result:
                if (result.DeadlineAt < result.AcceptedAt ||
                    (result.State == ExecutionState.Completed && result.Error is not null) ||
                    result.Outputs.Sum(o => o.Text is null ? 0 : Encoding.UTF8.GetByteCount(o.Text)) > AutomationContracts.MaxOutputTextBytes) throw Invalid();
                break;
            case AutomationError error:
                var catalog = AutomationErrors.Create(error.Code, error.ExecutionId);
                if (error != catalog) throw Invalid();
                break;
        }
    }

    private sealed class ExactEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsEnum;
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(ExactEnumConverter<>).MakeGenericType(type))!;
    }
    private sealed class ExactEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        private static string Name(T value) => typeof(T) == typeof(AutomationErrorCode) ? value.ToString() : JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw Invalid();
            foreach (var value in Enum.GetValues<T>()) if (Name(value) == reader.GetString()) return value;
            throw Invalid();
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (!Enum.IsDefined(value)) throw Invalid();
            writer.WriteStringValue(Name(value));
        }
    }
    private sealed class UtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var value) || value.Offset != TimeSpan.Zero ||
                !(reader.GetString()!.EndsWith('Z') || reader.GetString()!.EndsWith("+00:00", StringComparison.Ordinal))) throw Invalid();
            return value;
        }
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            if (value.Offset != TimeSpan.Zero) throw Invalid();
            writer.WriteStringValue(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        }
    }
    private sealed class UuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String && Guid.TryParseExact(reader.GetString(), "D", out var id) ? id : throw Invalid();
        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("D"));
    }
}
