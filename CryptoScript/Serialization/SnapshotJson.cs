using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CryptoScript.Serialization;

internal static class SnapshotJson
{
    // Create, not CreateDefault: host JsonConvert.DefaultSettings must not affect this format.
    private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Error,
        DateParseHandling = DateParseHandling.None,
        Culture = CultureInfo.InvariantCulture,
        MaxDepth = 64
    });

    internal static JToken Write(object dto) => JToken.FromObject(dto, Serializer());

    internal static T Read<T>(JToken token)
    {
        var serializer = Serializer();
        Validate(token, typeof(T), serializer.ContractResolver);
        return token.ToObject<T>(serializer) ?? throw Error("Null DTO.");
    }

    // Newtonsoft normally coerces numbers to strings, ignores case and accepts omitted
    // constructor arguments. Check the complete, exact DTO shape before binding.
    private static void Validate(JToken token, Type type, IContractResolver resolver)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(JToken)) return;
        if (type == typeof(JObject))
        {
            Require(token.Type == JTokenType.Object, "Expected an object.");
            return;
        }
        if (type == typeof(string))
        {
            Require(token.Type == JTokenType.String, "Expected a string.");
            return;
        }
        if (type == typeof(int) || type == typeof(ulong))
        {
            Require(token.Type == JTokenType.Integer, "Expected an integer.");
            // Range is also checked by the DTO binding.
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            Require(token is JArray, "Expected an array.");
            foreach (var item in (JArray)token) Validate(item, type.GenericTypeArguments[0], resolver);
            return;
        }
        Require(token is JObject, "Expected a DTO object.");
        var obj = (JObject)token;
        var contract = (JsonObjectContract)resolver.ResolveContract(type);
        var properties = contract.Properties.Where(p => !p.Ignored).ToArray();
        Require(obj.Count == properties.Length, "Missing or unknown JSON field.");
        foreach (var property in properties)
        {
            Require(obj.TryGetValue(property.PropertyName!, StringComparison.Ordinal, out var value),
                "Missing or incorrectly cased JSON field.");
            if (value!.Type == JTokenType.Null && property.Required == Required.AllowNull) continue;
            Validate(value, property.PropertyType!, resolver);
        }
    }

    internal static JToken Parse(string json)
    {
        // System.Text.Json supplies strict JSON syntax (no comments, single quotes,
        // NaN, constructors or trailing commas); inspect duplicate names at every depth.
        using var document = System.Text.Json.JsonDocument.Parse(json,
            new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 });
        CheckDuplicates(document.RootElement);
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }

    private static void CheckDuplicates(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), "Duplicate JSON field.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw Error(message);
    }
    internal static JsonSerializationException Error(string message) => new(message);
}
