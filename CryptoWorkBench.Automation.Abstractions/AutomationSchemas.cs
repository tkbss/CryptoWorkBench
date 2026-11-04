using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CryptoWorkBench.Automation.Abstractions;

/// <summary>JSON Schema 2020-12 for the closed, host-neutral contract types.</summary>
public static class AutomationSchemas
{
    public static JsonElement For<T>()
    {
        var schema = Build(typeof(T), typeof(IContractV2).IsAssignableFrom(typeof(T)));
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        return JsonSerializer.SerializeToElement(schema);
    }
    private static JsonObject Build(Type type, bool v2)
    {
        if (Nullable.GetUnderlyingType(type) is Type underlying) return NullableSchema(Build(underlying, v2));
        if (type == typeof(string)) return new() { ["type"] = "string" };
        if (type == typeof(bool)) return new() { ["type"] = "boolean" };
        if (type == typeof(int) || type == typeof(long)) return new() { ["type"] = "integer" };
        if (type == typeof(Guid)) return new() { ["type"] = "string", ["format"] = "uuid" };
        if (type == typeof(DateTimeOffset)) return new() { ["type"] = "string", ["format"] = "date-time" };
        if (type.IsEnum) return new()
        {
            ["type"] = "string",
            ["enum"] = new JsonArray(Enum.GetNames(type).Select(n => (JsonNode?)JsonValue.Create(type == typeof(AutomationErrorCode) ? n : JsonNamingPolicy.CamelCase.ConvertName(n))).ToArray())
        };
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            return new() { ["type"] = "array", ["items"] = Build(type.GetGenericArguments()[0], v2), ["maxItems"] = AutomationContracts.MaxCollectionEntries };
        if (type.Namespace != typeof(AutomationSchemas).Namespace) throw new NotSupportedException(type.FullName);
        var properties = new JsonObject();
        var required = new JsonArray();
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        foreach (var parameter in constructor.GetParameters())
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!);
            var nullable = new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.Nullable;
            var property = Build(parameter.HasDefaultValue ? Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType : parameter.ParameterType, v2);
            if (nullable && !parameter.ParameterType.IsValueType && !parameter.HasDefaultValue) property = NullableSchema(property);
            if (name is "revision" or "previousRevision" or "expectedRevision" or "sourceRevision" or "stateVersion" or "expectedStateVersion") property["pattern"] = "^[0-9]+$";
            if (name == "text" && type == typeof(EditorEdit) || name == "text" && type == typeof(EditorSnapshot) || name == "text" && type == typeof(TextExecution)) property["maxLength"] = AutomationContracts.MaxTextLength;
            if (name == "timeoutMs") { property["minimum"] = AutomationContracts.MinTimeoutMs; property["maximum"] = AutomationContracts.MaxTimeoutMs; }
            if (name is "offset" or "length" or "column" or "keySizeBits") property["minimum"] = 0;
            if (name is "sequence" or "line" or "statementIndex") property["minimum"] = 1;
            if (name == "valueIncluded") property["const"] = false;
            if (name == "schemaVersion") property["const"] = "1";
            if (typeof(IRequestV2).IsAssignableFrom(type) && parameter.ParameterType == typeof(Guid))
                property["not"] = new JsonObject { ["const"] = Guid.Empty.ToString() };
            if (name == "contractVersion") property["const"] = "2";
            if (v2 && name is "expectedRevision" or "revision" or "previousRevision" or "sourceRevision" or
                "stateVersion" or "expectedStateVersion" or "observedRevision") property["pattern"] = "^(0|[1-9][0-9]{0,19})$";
            if (name is "expectedLeaseVersion" or "inputLeaseVersion" or "leaseVersion") property["pattern"] = "^[1-9][0-9]{0,19}$";
            if (type == typeof(EditorEditV2) && name == "text") property["maxLength"] = AutomationContracts.MaxTextLength;
            if (type == typeof(EditorEditV2) && name == "offset") property["maximum"] = AutomationContracts.MaxTextLength;
            if (type == typeof(EditorMutationReceiptV2) && name == "retryPolicy") property["const"] = "noAutomaticRetry";
            if (type == typeof(SafeError) && name == "messageKey") property["pattern"] = "^mcp[.][a-zA-Z0-9.]+$";
            properties[name] = property;
            if (!parameter.HasDefaultValue) required.Add(name);
        }
        var result = new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties, ["required"] = required };
        if (type == typeof(EditorEdit) || type == typeof(EditorEditV2)) result["allOf"] = JsonNode.Parse("""
            [{"if":{"properties":{"mode":{"const":"insert"}}},"then":{"required":["offset"]},"else":{"not":{"required":["offset"]}}}]
            """);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Reply<>)) result["oneOf"] = JsonNode.Parse("""
            [{"properties":{"ok":{"const":true},"data":{"type":"object"},"error":{"type":"null"}}},
             {"properties":{"ok":{"const":false},"data":{"type":"null"},"error":{"type":"object"}}}]
            """);
        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(ResultV2<>) || type.GetGenericTypeDefinition() == typeof(LeaseReplyV2<>)))
            result["oneOf"] = JsonNode.Parse("""[{"properties":{"result":{"type":"object"},"error":{"type":"null"}}},{"properties":{"result":{"type":"null"},"error":{"type":"object"}}}]""");
        if (type == typeof(EditorMutationReceiptV2)) result["allOf"] = JsonNode.Parse("""
            [{"if":{"properties":{"historyEffect":{"const":"indeterminate"}}},"then":{"properties":{"finalization":{"const":"faulted"}}}},
             {"if":{"properties":{"finalization":{"const":"faulted"}}},"then":{"properties":{"error":{"type":"object"},"requiresStateRefresh":{"const":true}}}},
             {"if":{"properties":{"error":{"type":"null"}}},"then":{"properties":{"requiresStateRefresh":{"const":false}},"oneOf":[{"properties":{"textEffect":{"const":"none"},"historyEffect":{"const":"none"}}},{"properties":{"textEffect":{"const":"applied"},"historyEffect":{"const":"applied"},"observedRevision":{"type":"string"}}}]},"else":{"properties":{"requiresStateRefresh":{"const":true}}}},
             {"if":{"properties":{"error":{"type":"object","properties":{"code":{"enum":["Busy","RevisionConflict"]}}}}},"then":{"properties":{"textEffect":{"const":"none"},"historyEffect":{"const":"none"},"finalization":{"const":"complete"}}}}]
            """);
        return result;
    }
    private static JsonObject NullableSchema(JsonObject schema)
    {
        if (schema["type"] is JsonValue type) schema["type"] = new JsonArray(type.GetValue<string>(), "null");
        if (schema["enum"] is JsonArray values) values.Add((JsonNode?)null);
        return schema;
    }
}
