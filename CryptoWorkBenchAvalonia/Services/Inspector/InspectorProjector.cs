using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using Newtonsoft.Json;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

internal enum StepKind { Property, Entry, Parameters }
internal sealed record InspectorStep(StepKind Kind, string Name, int Index = 0, bool MapEntry = false)
{
    internal string DisplayName => MapEntry ? Name.Length <= 80 ? $"[{Name}]" :
        string.Concat("[", Name.AsSpan(0,char.IsHighSurrogate(Name[79]) && char.IsLowSurrogate(Name[80]) ? 79 : 80), "…]") :
        Kind == StepKind.Entry ? $"[{Index}]" : Name.Split('/')[^1];
    // Ordinal position is unique inside the map at the validated capture stamp.
    internal string PathKey => MapEntry ? $"map:{Index};" : $"{(int)Kind}:{Name.Length}:{Name}:{Index};";
}
internal sealed record PropertyRequest(ImmutableArray<InspectorStep> Path, int Page = 0, bool Calculate = false);
internal sealed record InspectorRow(string Name, string Type, string State, InspectorStep Step, string? Preview = null, string? ExactScalar = null, ImmutableArray<InspectorStep>? Reference = null, Type? ScalarType = null);
internal sealed record InspectorPage(string RuntimeType, string Preview, ImmutableArray<InspectorRow> Rows, bool More, string? ExactScalar = null, int? StringLength = null);

// Paths originate at a verified declaration. ReadOnly wrappers are reached only through
// exact SecretSet, metadata and header constructors which own their backing collections.
internal sealed class InspectorProjector(VariableDeclaration root, CancellationToken token)
{
    internal const int PageSize = 100;
    // Resolve each audited name to exactly one public signature on its concrete BCL/core type.
    // An added overload is ambiguous and cannot silently inherit approval.
    private static readonly Dictionary<Type, HashSet<(Module,int)>> schema = new();
    private static readonly Type dictionary = typeof(Dictionary<string, string>);
    private static readonly Type readOnlyDictionary = typeof(ReadOnlyDictionary<string, string>);
    static InspectorProjector()
    {
        void Register(Type type, string names)
        {
            var slots = new HashSet<(Module,int)>();
            foreach (string name in names.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property?.GetMethod is { IsPublic: true } getter)
                    slots.Add((getter.Module,getter.MetadataToken));
            }
            schema.Add(type,slots);
        }
        const string common = "Id Type Value ValueFormat Text";
        Register(typeof(VariableDeclaration), common);
        Register(typeof(StringVariableDeclaration), common + " GMAC");
        Register(typeof(PathVariableDeclaration), common);
        Register(typeof(ValVariableDeclaration), common);
        Register(typeof(ParameterVariableDeclaration), common + " Mechanism HeaderReference");
        Register(typeof(KeyVariableDeclaration), common + " DerivationMechanism KeyType Usage KeySizeInBits KeySize KeyAttributes Kcv Kcvc Kcvk");
        Register(typeof(SecretVariableDeclaration), common + " SecretSet");
        Register(typeof(BlockHeaderVariableDeclaration), common + " Definition");
        foreach (var t in new[] { typeof(CryptoType), typeof(CryptoTypeKey), typeof(CryptoTypeVar), typeof(CryptoTypeVal),
            typeof(CryptoTypeParameters), typeof(CryptoTypePath), typeof(CryptoTypeTR31Header), typeof(CryptoTypeSecret) })
            Register(t, "Id Name Text");
        Register(typeof(Statement), "Text");
        Register(typeof(KeyType), "Algorithm MaterialKind");
        Register(typeof(KeySize), "Bits IsKnown");
        Register(typeof(KeyUsagePolicy), "Mode AllowedUsages");
        Register(typeof(OptionalBlock), "ID Length Data");
        Register(typeof(SecretSet), "Descriptor Shares PreferredShareFormat OriginalKeyMetadata");
        Register(typeof(SecretDescriptor), "Mechanism Parts MinParts KeySizeBits Profile Field ReductionPolynomial SecretPosition");
        Register(typeof(SecretShare), "X Value");
        Register(typeof(SecretKeyMetadata), "KeyType Usage DerivationMechanism Attributes");
        Register(typeof(SecretKeyAttribute), "Id Length Data");
        Register(typeof(Tr31HeaderDefinition), "Attributes OptionalBlocks ExpectedLength IsLengthResolved Version Algorithm HeaderLength");
        Register(typeof(string), "Length Chars");
        Register(typeof(byte[]), "Length LongLength Rank SyncRoot IsReadOnly IsFixedSize IsSynchronized");
        Register(typeof(List<OptionalBlock>), "Capacity Count Item");
        foreach (var t in new[] { typeof(ReadOnlyCollection<SecretShare>), typeof(ReadOnlyCollection<SecretKeyAttribute>),
            typeof(ReadOnlyCollection<OptionalBlock>) }) Register(t, "Count Item");
        Register(dictionary, "Comparer Count Capacity Keys Values Item");
        Register(readOnlyDictionary, "Keys Values Item Count");
        foreach (var t in new[] { typeof(Dictionary<string,string>.KeyCollection), typeof(Dictionary<string,string>.ValueCollection),
            typeof(ReadOnlyDictionary<string,string>.KeyCollection), typeof(ReadOnlyDictionary<string,string>.ValueCollection) })
            Register(t, "Count");
        Register(typeof(KeyValuePair<string,string>), "Key Value");
        Register(StringComparer.Ordinal.GetType(), "");
        Register(EqualityComparer<string>.Default.GetType(), "");
    }

    internal static IEnumerable<PropertyInfo> Inventory(Type type)
    {
        var slots = new HashSet<MethodInfo>();
        for (var t = type; t != null; t = t.BaseType)
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (slots.Add((p.GetMethod ?? p.SetMethod!).GetBaseDefinition())) yield return p;
    }
    private static bool Registered(Type type, PropertyInfo p) =>
        p.GetMethod is { IsPublic: true } getter && schema.TryGetValue(type, out var slots) && slots.Contains((getter.Module,getter.MetadataToken));
    private static bool Computed(string name) => name is "Kcv" or "Kcvc" or "Kcvk";
    private sealed record ShareBytes(SecretShare Share, int Length);
    private static Type Runtime(object value) => value is ShareBytes ? typeof(byte[]) : value.GetType();
    private static string Slot(PropertyInfo p)
    {
        var accessor = (p.GetMethod ?? p.SetMethod!).GetBaseDefinition();
        return $"{p.DeclaringType!.AssemblyQualifiedName}:{p.MetadataToken}:{accessor.Module.ModuleVersionId}:{accessor.MetadataToken}/{p.Name}";
    }
    private static int InheritanceDepth(Type? type) => type == null ? 0 : 1 + InheritanceDepth(type.BaseType);
    internal static IEnumerable<PropertyInfo> TreeInventory(Type type) => Inventory(type)
        .OrderBy(p => InheritanceDepth(p.DeclaringType)).ThenBy(p => p.Name,StringComparer.Ordinal)
        .ThenBy(p => p.ToString(),StringComparer.Ordinal);
    private object? Read(object owner, PropertyInfo p, bool calculate)
    {
        token.ThrowIfCancellationRequested();
        if (!Registered(Runtime(owner), p) || p.GetIndexParameters().Length != 0)
            throw new NotSupportedException("UnsupportedAccessor");
        if (owner is KeyVariableDeclaration key && Computed(p.Name))
        {
            if (!calculate) throw new InvalidOperationException("NotCalculated");
            return Calculate(key, p);
        }
        if (owner is BlockHeaderVariableDeclaration && p.Name == "Value" && JsonConvert.DefaultSettings != null)
            throw new NotSupportedException("UnsupportedFormatter");
        if (owner is SecretShare share && p.Name == "Value")
            return new ShareBytes(share, ((SecretVariableDeclaration)root).SecretSet.Descriptor.KeySizeBits / 8);
        if (owner is ShareBytes bytes)
            return p.Name switch { "Length" => bytes.Length, "LongLength" => (long)bytes.Length, "Rank" => 1,
                "IsReadOnly" or "IsSynchronized" => false, "IsFixedSize" => true, "SyncRoot" => owner,
                _ => throw new NotSupportedException() };
        return p.GetValue(owner);
    }

    internal int CalculatedGetterCalls { get; private set; }
    private object? Calculate(KeyVariableDeclaration key, PropertyInfo p)
    {
        if (key.KeyType.MaterialKind != KeyMaterialKind.Secret || key.KeyType.Algorithm is not (KeyAlgorithm.Aes or KeyAlgorithm.Tdea)) return null;
        var value = key.Value;
        if (value == null || value.Length <= 128)
        {
            CalculatedGetterCalls++;
            return p.GetValue(key);
        }
        // Only base64 permits arbitrarily much whitespace for a supported key size.
        bool tagged = value.StartsWith("b64(", StringComparison.Ordinal);
        if (key.ValueFormat != FormatConversions.B64 && !(tagged && string.IsNullOrEmpty(key.ValueFormat))) return null;
        var span = value.AsSpan();
        if (tagged && span.EndsWith(")")) span = span[4..^1];
        Span<char> compact = stackalloc char[44];
        int count = 0;
        for (int i = 0; i < span.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            if (span[i] is ' ' or '\r' or '\n' or '\t') continue;
            if (count == compact.Length) return null;
            compact[count++] = span[i];
        }
        CalculatedGetterCalls++;
        return p.GetValue(new KeyVariableDeclaration { KeyType = key.KeyType, ValueFormat = FormatConversions.B64, Value = new(compact[..count]) });
    }

    private bool HasEntries(object value) => value is ShareBytes || schema.ContainsKey(Runtime(value)) && value is IEnumerable;
    private object? Entry(object owner, int index)
    {
        if (!HasEntries(owner) || index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (owner is ShareBytes bytes) { Span<byte> b = stackalloc byte[1]; bytes.Share.CopyValueRange(index, b); return b[0]; }
        if (owner is string s) return s[index];
        if (owner is IList list) return list[index];
        return ((IEnumerable)owner).Cast<object?>().ElementAt(index);
    }
    private IEnumerable<string> Resolve(PropertyRequest request, Action<object?> resolved)
    {
        object? value = root;
        foreach (var step in request.Path)
        {
            if (value == null) throw new InvalidOperationException("Null property path.");
            if (step.Kind == StepKind.Parameters && value.GetType() == typeof(ParameterVariableDeclaration))
                value = ((ParameterVariableDeclaration)value).GetParameters();
            else if (step.Kind == StepKind.Entry)
            {
                if (value is IReadOnlyDictionary<string,string> map && HasEntries(value))
                    {
                    if (step.Name.Length <= 32)
                        value = new KeyValuePair<string,string>(step.Name,map[step.Name]);
                    else
                    {
                        bool found = false;
                        int visited = 0;
                        foreach (var item in map)
                        {
                            bool matches = ReferenceEquals(item.Key,step.Name);
                            if (!matches && item.Key.Length == step.Name.Length)
                            {
                                for (int offset=0; offset<item.Key.Length;)
                                {
                                    token.ThrowIfCancellationRequested();
                                    int count=Math.Min(8192,item.Key.Length-offset);
                                    bool equal=item.Key.AsSpan(offset,count).SequenceEqual(step.Name.AsSpan(offset,count));
                                    yield return "";
                                    if (!equal) break;
                                    offset+=count;
                                    matches=offset==item.Key.Length;
                                }
                            }
                            if (matches) { value=item; found=true; break; }
                            if (++visited % 64 == 0) { token.ThrowIfCancellationRequested(); yield return ""; }
                        }
                        if (!found) throw new InvalidOperationException("Stale dictionary entry.");
                    }
                }
                else if (value is IEnumerable sequence && value is not (IList or string) && HasEntries(value))
                {
                    bool found = false;
                    int index = 0;
                    foreach (var item in sequence)
                    {
                        token.ThrowIfCancellationRequested();
                        if (index == step.Index) { value = item; found = true; break; }
                        if (++index % 64 == 0) yield return "";
                    }
                    if (!found) throw new ArgumentOutOfRangeException(nameof(request));
                }
                else value = Entry(value, step.Index);
            }
            else value = Read(value, Inventory(Runtime(value)).Single(p => Slot(p) == step.Name), request.Calculate);
        }
        resolved(value);
    }

    internal InspectorPage Describe(PropertyRequest request)
    {
        InspectorPage? result = null;
        foreach (var _ in DescribeSteps(request, page => result = page)) { }
        return result!;
    }

    internal IEnumerable<string> DescribeSteps(PropertyRequest request, Action<InspectorPage> publish)
    {
        object? value = null;
        foreach (var checkpoint in Resolve(request, resolved => value = resolved)) yield return checkpoint;
        foreach (var checkpoint in DescribeValue(value,request,publish)) yield return checkpoint;
    }

    internal static InspectorPage DetachedPage(object? value, PropertyRequest request)
    {
        InspectorPage? page = null;
        foreach (var _ in new InspectorProjector(null!,default).DescribeValue(value,request,result => page = result)) { }
        return page!;
    }

    internal static InspectorPage IndexedPage(InspectorValueSource source, PropertyRequest request)
    {
        var type = source.Slice!.Type!;
        var rows = ImmutableArray.CreateBuilder<InspectorRow>();
        foreach (var property in TreeInventory(type))
        {
            var step = new InspectorStep(StepKind.Property,Slot(property));
            bool captured = source.Parts[0].Index!.ContainsKey(Key(request.Path.Add(step)));
            string state = property.GetIndexParameters().Length != 0 ? "Indexer · see entries" :
                captured || Registered(type,property) ? "NotLoaded" : "UnsupportedAccessor";
            object? known = property.Name switch
            {
                "Count" when typeof(ICollection).IsAssignableFrom(type) => source.Slice.Entries,
                "Length" when type==typeof(byte[]) => source.Slice.Entries,
                "LongLength" when type==typeof(byte[]) => (long)source.Slice.Entries,
                "Rank" when type==typeof(byte[]) => 1,
                "IsFixedSize" when type==typeof(byte[]) => true,
                "IsReadOnly" or "IsSynchronized" when type==typeof(byte[]) => false,
                _ => null
            };
            var reference = type==typeof(byte[]) && property.Name=="SyncRoot";
            var text = known == null ? null : Scalar(known);
            rows.Add(new(property.Name,property.PropertyType.Name,reference ? "Reference · self" : state,step,
                text,text,reference ? request.Path : null,known?.GetType()));
        }
        var container = source.Slice;
        int start = checked(request.Page*PageSize);
        for (int i=start; i<container.Entries && i-start<PageSize; i++)
        {
            var step = container.Map == null ? new InspectorStep(StepKind.Entry,"",i) :
                new InspectorStep(StepKind.Entry,container.Map[i].Key,i,true);
            if (Lookup(source.Parts[0],request.Path.Add(step)) is { } entry)
                rows.Add(new(step.DisplayName,entry.Type?.Name ?? "null","NotLoaded",step));
        }
        return new(type.FullName!,type.Name,rows.ToImmutable(),container.Entries-start>PageSize);
    }

    private IEnumerable<string> DescribeValue(object? value, PropertyRequest request, Action<InspectorPage> publish)
    {
        var rows = ImmutableArray.CreateBuilder<InspectorRow>();
        if (value == null) { publish(new("null", Computed(request.Path.LastOrDefault()?.Name.Split('/')[^1] ?? "") ? "NotApplicable · null" : "null", [], false, "null")); yield break; }
        var type = Runtime(value);
        foreach (var p in TreeInventory(type))
        {
            string state = p.GetMethod?.IsPublic != true ? "GetterNotPublic" : !Registered(type,p) ? "UnsupportedAccessor" :
                p.GetIndexParameters().Length != 0 ? "Indexer · see entries" : Computed(p.Name) ? "NotCalculated" :
                value is ShareBytes && p.Name == "SyncRoot" ? "Reference · self" : "NotLoaded";
            Type? scalarType = null;
            string? display = null;
            string? exactChild = null;
            if (state == "NotLoaded" && (p.PropertyType == typeof(string) || p.PropertyType.IsPrimitive || p.PropertyType.IsEnum) &&
                !(value is BlockHeaderVariableDeclaration && p.Name == "Value"))
            {
                try
                {
                    var child = Read(value,p,false);
                    scalarType = child?.GetType();
                    var raw = child == null ? "null" : Scalar(child);
                    if (raw is { Length: <= 120 }) exactChild = raw;
                    display = raw is { Length: > 120 } ? DisplayTextPreview.WithEllipsis(raw, 120) : raw == "" ? "(empty)" : raw;
                }
                catch (Exception error) { state = "GetterError"; display = error.GetBaseException().Message; }
            }
            string label = !schema.ContainsKey(type) && Inventory(type).Count(other => other.Name == p.Name) > 1 ? p.DeclaringType!.Name + "." + p.Name : p.Name;
            string valueType = p.PropertyType.Name;
            if (p.GetIndexParameters().Length != 0)
                valueType += "[" + string.Join(", ",p.GetIndexParameters().Select(arg => arg.ParameterType.Name)) + "]";
            rows.Add(new(label, valueType, state, new(StepKind.Property,Slot(p)),display,exactChild,state == "Reference · self" ? request.Path : null,scalarType));
        }
        if (type == typeof(ParameterVariableDeclaration))
            rows.Add(new("GetParameters()", "Dictionary", "Method adapter", new(StepKind.Parameters,"GetParameters")));
        int count = value switch { ShareBytes bytes => bytes.Length, string s => s.Length, ICollection c when HasEntries(value) => c.Count, _ => 0 };
        int start = checked(request.Page * PageSize);
        // Only remember identities for the visible window. Aliases on a later page
        // still point to their first index without retaining the entire collection.
        var firstIndices = new Dictionary<object,int>(ReferenceEqualityComparer.Instance);
        if (value is IList list && HasEntries(value))
        {
            for (int i=start; i<count && i-start<PageSize; i++)
                if (list[i] is { } item && item is not string && !item.GetType().IsValueType) firstIndices.TryAdd(item,i);
            if (firstIndices.Count != 0)
                for (int i=0; i<Math.Min(start,count); i++)
                {
                    if ((i & 63)==0) { token.ThrowIfCancellationRequested(); yield return ""; }
                    if (list[i] is { } item && firstIndices.TryGetValue(item,out int first) && i<first)
                        firstIndices[item]=i;
                }
        }
        List<object?>? window = null;
        if (value is IReadOnlyDictionary<string,string> map && HasEntries(value))
        {
            window = new(PageSize);
            if (start < count)
            {
                bool reverse = start > count/2;
                int skip = reverse ? Math.Max(0,count-start-PageSize) : start;
                foreach (var entry in OrderedEntries(map,reverse))
                {
                    if (entry == null) { yield return ""; continue; }
                    if (skip-- > 0) continue;
                    TraversalWork++;
                    window.Add(entry.Value);
                    if (window.Count == Math.Min(PageSize,count-start)) break;
                }
                if (reverse) window.Reverse();
            }
        }
        else if (value is IEnumerable sequence && HasEntries(value) && value is not (IList or string))
        {
            window = new(PageSize);
            int index = 0;
            foreach (var element in sequence)
            {
                if (index >= start) window.Add(element);
                if (window.Count == PageSize) break;
                if (++index % 64 == 0) { token.ThrowIfCancellationRequested(); yield return ""; }
            }
        }
        for (int i = start; i < count && i - start < PageSize; i++)
        {
            if (value is IReadOnlyDictionary<string,string>)
            {
                if ((i-start)%64 == 0) { token.ThrowIfCancellationRequested(); yield return ""; }
                TraversalWork++;
            }
            var item = window == null ? Entry(value,i) : window[i-start];
            var slot = new InspectorStep(StepKind.Entry,item is KeyValuePair<string,string> pair ? pair.Key : "",i,item is KeyValuePair<string,string>);
            string label = slot.DisplayName;
            var inline = item == null ? "null" : Scalar(item);
            int first = item != null && firstIndices.TryGetValue(item,out int target) ? target : i;
            rows.Add(new(label,item?.GetType().Name ?? "null",first == i ? "NotLoaded" : "Reference",slot,
                inline is {Length: > 120} ? DisplayTextPreview.WithEllipsis(inline, 120) : inline,inline is {Length: <= 120} ? inline : null,
                first == i ? null : request.Path.Add(slot with { Index = first }),item?.GetType()));
        }
        var preview = value is string text ? (text.Length == 0 ? "(empty)" : text.Length > 160 ? DisplayTextPreview.WithEllipsis(text, 160) : text) :
            Scalar(value) ?? (HasEntries(value) ? $"{count} entries" : type.Name);
        publish(new(type.FullName!, preview, rows.ToImmutable(), count - start > PageSize, Scalar(value) is { Length: <= 160 } exact ? exact : null,(value as string)?.Length));
    }

    private static string? Scalar(object v) => v switch
    {
        string s => s, char c => c.ToString(), bool b => b ? "true" : "false",
        byte or int or long or ulong => Convert.ToString(v, CultureInfo.InvariantCulture),
        KeyUsage or KeyAlgorithm or KeyMaterialKind or KeyUsageMode => v.ToString(),
        _ => null
    };

    internal IEnumerable<string> EncodeScalar(object? scalar)
    {
        if (scalar != null && Scalar(scalar) == null && scalar is not byte[] &&
            !(scalar.GetType() == dictionary && ((Dictionary<string,string>)scalar).Comparer == EqualityComparer<string>.Default))
            throw new NotSupportedException("Unsupported standalone value.");
        return Tokens(scalar, false, false, new(ReferenceEqualityComparer.Instance));
    }

    // One invocation is one AccessState step. Empty tokens mark bounded traversal work.
    internal int ReadChunk(IEnumerator<string> tokens,byte[] encoded,ref string remainder,out bool finished)
    {
        finished = false;
        int n = 0;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < 256 && n < encoded.Length; i++)
        {
            if (i > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds >= 10) break;
            if (remainder.Length == 0)
            {
                if (!tokens.MoveNext()) { finished = true; break; }
                remainder = tokens.Current;
                if (remainder.Length == 0) break; // Cooperative traversal checkpoint.
            }
            int chars = Math.Min(remainder.Length, (encoded.Length - n) / 2);
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(remainder.AsSpan(0, chars)).CopyTo(encoded.AsSpan(n));
            n += chars * 2;
            remainder = remainder[chars..];
        }
        return n;
    }

    internal IEnumerable<string> Encode(PropertyRequest request)
    {
        object? value = null;
        foreach (var checkpoint in Resolve(request, resolved => value = resolved)) yield return checkpoint;
        foreach (var chunk in IndexedTokens(value,false,request.Calculate,new(ReferenceEqualityComparer.Instance),request.Path))
        {
            position += chunk.Length;
            yield return chunk;
        }
    }

    private static readonly InspectorStep PairKey = new(StepKind.Property,Slot(typeof(KeyValuePair<string,string>).GetProperty("Key")!));
    private static readonly InspectorStep PairValue = new(StepKind.Property,Slot(typeof(KeyValuePair<string,string>).GetProperty("Value")!));
    internal static CapturedSlice? Lookup(SnapshotPart part, ImmutableArray<InspectorStep> path)
    {
        var index = part.Index;
        if (index == null) return null;
        if (index.TryGetValue(Key(path),out var direct)) return direct;
        int i = path.Length-1;
        if (i >= 0 && path[i].Kind == StepKind.Property) i--;
        if (i < 0 || path[i].Kind != StepKind.Entry ||
            !index.TryGetValue(Key(path[..i]),out var container)) return null;
        if (container.Type==typeof(byte[]) && i==path.Length-1 && path[i].Index>=0 && path[i].Index<container.Entries)
            return new(container.Offset+(container.Quoted ? 4 : 3)+2L*path[i].Index,2,false,typeof(byte),path,null,HexByte:true);
        if (container.Map is not { } map) return null;
        var name = path[i].Name;
        int ordinal = path[i].Index;
        if (!path[i].MapEntry || (uint)ordinal >= (uint)map.Count) return null;
        var entry = map[ordinal];
        if (!ReferenceEquals(entry.Key,name) && !string.Equals(entry.Key,name,StringComparison.Ordinal)) return null;
        if (i==path.Length-1)
            return new(entry.Offset,entry.KeyCount+1+entry.ValueCount,false,typeof(KeyValuePair<string,string>),
                path,null,PairColon:entry.KeyCount);
        if (path[^1]==PairKey)
            return new(entry.Offset,entry.KeyCount,true,typeof(string),path,name.Length);
        if (path[^1]==PairValue)
            return new(entry.Offset+entry.KeyCount+1,entry.ValueCount,entry.ValueLength.HasValue,
                entry.ValueLength.HasValue ? typeof(string) : null,path,entry.ValueLength);
        return null;
    }

    private long position;
    internal ImmutableDictionary<string, CapturedSlice>.Builder? Index { get; init; }
    internal static string Key(ImmutableArray<InspectorStep> path) => string.Concat(path.Select(s=>s.PathKey));
    private IEnumerable<string> IndexedTokens(object? value, bool json, bool calculate, HashSet<object> ancestors, ImmutableArray<InspectorStep> path)
    {
        long start = position;
        var keys = Index != null && value is IReadOnlyDictionary<string,string> ? new CapturedMapIndex.Builder() : null;
        foreach (var chunk in Tokens(value,json,calculate,ancestors,path,keys)) yield return chunk;
        if (Index != null)
            Index[Key(path)] = new(start,position-start,json && value is (string or char or Enum or ShareBytes or byte[]),
                value == null ? null : Runtime(value),path, value is string str ? str.Length : value is char ? 1 : null,
                value is ShareBytes bytes ? bytes.Length : value is ICollection collection ? collection.Count : 0, keys?.Finish());
    }

    private IEnumerable<string> Tokens(object? value, bool json, bool calculate, HashSet<object> ancestors, ImmutableArray<InspectorStep> path = default, CapturedMapIndex.Builder? keys = null)
    {
        token.ThrowIfCancellationRequested();
        if (value == null) { yield return "null"; yield break; }
        if (value is byte[] array) { yield return (json ? "\"0x(" : "0x(") + Convert.ToHexString(array) + (json ? ")\"" : ")"); yield break; }
        if (value is ShareBytes bytes)
        {
            yield return json ? "\"0x(" : "0x(";
            byte[] buffer = new byte[Math.Min(8192, bytes.Length)];
            for (int offset = 0; offset < bytes.Length;)
            {
                int n = Math.Min(bytes.Length-offset, buffer.Length);
                bytes.Share.CopyValueRange(offset, buffer.AsSpan(0,n));
                yield return Convert.ToHexString(buffer,0,n);
                offset += n;
            }
            yield return json ? ")\"" : ")";
            yield break;
        }
        if (Scalar(value) is string scalar)
        {
            if (json && value is string or char or Enum)
                foreach (var chunk in Quoted(scalar)) yield return chunk;
            else for (int i = 0; i < scalar.Length;) { int n = Math.Min(8192, scalar.Length-i); yield return scalar.Substring(i,n); i += n; }
            yield break;
        }
        var type = Runtime(value);
        if (!schema.ContainsKey(type)) throw new NotSupportedException("UnsupportedAccessor: " + type.Name);
        if (!ancestors.Add(value)) throw new InvalidOperationException("CopyUnavailable(Cycle)");
        try
        {
            if (value is IReadOnlyDictionary<string,string> map)
            {
                yield return "{";
                bool first = true;
                foreach (var entry in OrderedEntries(map,false))
                {
                    if (entry == null) { yield return ""; continue; }
                    var key = entry.Value.Key;
                    if (!first) yield return ",";
                    first = false;
                    long start = position;
                    foreach (var chunk in Quoted(key)) yield return chunk;
                    long keyCount = position-start;
                    yield return ":";
                    TraversalWork++;
                    string? mapValue = entry.Value.Value;
                    foreach (var chunk in Tokens(mapValue,true,calculate,ancestors)) yield return chunk;
                    keys?.Add(new(key,start,keyCount,position-start-keyCount-1,mapValue?.Length));
                }
                yield return "}";
            }
            else if (HasEntries(value))
            {
                yield return "[";
                bool first = true;
                int index = 0;
                foreach (var item in (IEnumerable)value)
                {
                    if (!first) yield return ",";
                    first = false;
                    foreach (var chunk in IndexedTokens(item,true,calculate,ancestors, Index == null ? default : path.Add(new(StepKind.Entry,"",index)))) yield return chunk;
                    index++;
                }
                yield return "]";
            }
            else
            {
                yield return "{";
                bool first = true;
                foreach (var p in Inventory(type).OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first) yield return ",";
                    first = false;
                    yield return "\"" + p.Name + "\":";
                    foreach (var chunk in IndexedTokens(Read(value,p,calculate),true,calculate,ancestors,Index == null ? default : path.Add(new(StepKind.Property,Slot(p))))) yield return chunk;
                }
                yield return "}";
            }
        }
        finally { ancestors.Remove(value); }
    }

    // Select the next bounded run of ordinal keys. Only references to existing keys
    // are retained. A checkpoint ends the current host slice, including while skipping.
    internal long TraversalWork { get; private set; }
    // A short-key heap can use framework comparisons. Longer keys need resumable
    // comparisons; carry both members so looking up the value never rehashes a long key.
    private IEnumerable<KeyValuePair<string,string>?> OrderedEntries(IReadOnlyDictionary<string,string> map,bool reverse)
    {
        int visited=0;
        foreach (var pair in map)
        {
            token.ThrowIfCancellationRequested();
            TraversalWork++;
            if (pair.Key.Length>32)
            {
                yield return null;
                foreach (var item in OrderLongEntries(map,reverse)) yield return item;
                yield break;
            }
            if (++visited%64==0) yield return null;
        }
        yield return null;
        foreach (string? key in OrderedKeys(map,reverse))
            yield return key==null ? null : new KeyValuePair<string,string>(key,map[key]);
    }

    private IEnumerable<KeyValuePair<string,string>?> OrderLongEntries(IReadOnlyDictionary<string,string> map,bool reverse)
    {
        // Merge-sort bounded batches, retaining only the first run after the cursor.
        // All arrays hold references to immutable strings belonging to the input.
        const int run=4096;
        int capacity=Math.Min(run,map.Count);
        var buffer=new KeyValuePair<string,string>[capacity*2];
        var scratch=new KeyValuePair<string,string>[buffer.Length];
        int order=0, work=0, units=0;
        IEnumerable<int> Compare(string a,string b)
        {
            order=0;
            for (int at=0, limit=Math.Min(a.Length,b.Length); at<limit;)
            {
                int n=Math.Min(4096,limit-at);
                if (units+n>16384)
                {
                    units=0;
                    token.ThrowIfCancellationRequested();
                    yield return 0;
                }
                units+=n;
                order=a.AsSpan(at,n).SequenceCompareTo(b.AsSpan(at,n));
                if (order!=0) break;
                at+=n;
            }
            if (order==0) order=a.Length.CompareTo(b.Length);
            if (reverse) order=-Math.Sign(order);
        }
        IEnumerable<int> Sort(int count)
        {
            for (int width=1;width<count;width*=2)
            {
                for (int start=0;start<count;start+=2*width)
                {
                    int mid=Math.Min(start+width,count),end=Math.Min(mid+width,count);
                    int left=start,right=mid;
                    for (int dest=start;dest<end;dest++)
                    {
                        if (++work%32==0) { token.ThrowIfCancellationRequested(); yield return 0; }
                        TraversalWork++;
                        if (left<mid && right<end)
                            foreach (var checkpoint in Compare(buffer[left].Key,buffer[right].Key)) yield return checkpoint;
                        scratch[dest]=right==end || left<mid && order<=0 ? buffer[left++] : buffer[right++];
                    }
                }
                (buffer,scratch)=(scratch,buffer);
            }
        }
        string? after=null;
        while (true)
        {
            int count=0;
            foreach (var item in map)
            {
                if (++work%32==0) { token.ThrowIfCancellationRequested(); yield return null; }
                TraversalWork++;
                if (after!=null)
                {
                    foreach (var _ in Compare(item.Key,after)) yield return null;
                    if (order<=0) continue;
                }
                buffer[count++]=item;
                if (count==buffer.Length)
                {
                    foreach (var _ in Sort(count)) yield return null;
                    count=capacity;
                }
            }
            foreach (var _ in Sort(count)) yield return null;
            int emit=Math.Min(count,capacity);
            for (int i=0;i<emit;i++)
            {
                if (++work%32==0) { token.ThrowIfCancellationRequested(); yield return null; }
                TraversalWork++;
                yield return buffer[i];
            }
            if (emit<capacity || capacity==map.Count) yield break;
            after=buffer[emit-1].Key;
        }
    }

    private IEnumerable<string?> OrderedKeys(IReadOnlyDictionary<string,string> map, bool reverse = false)
    {
        const int capacity = 4096;
        int Compare(string a,string b) => reverse ? StringComparer.Ordinal.Compare(b,a) : StringComparer.Ordinal.Compare(a,b);
        var heap = new PriorityQueue<string,string>(Math.Min(capacity,map.Count),
            Comparer<string>.Create((a,b) => Compare(b,a)));
        var buffer = new string[Math.Min(capacity,map.Count)];
        string? after = null;
        do
        {
            int steps = 0;
            foreach (string key in map.Keys)
            {
                token.ThrowIfCancellationRequested();
                TraversalWork++;
                if (after == null || Compare(after,key)<0)
                {
                    if (heap.Count < capacity) heap.Enqueue(key,key);
                    else if (Compare(key,heap.Peek())<0) heap.DequeueEnqueue(key,key);
                }
                if (++steps % 64 == 0) yield return null;
            }
            int count = heap.Count;
            for (int i=count-1; i>=0; i--)
            {
                TraversalWork++;
                buffer[i] = heap.Dequeue();
                if ((i & 63)==0) yield return null;
            }
            for (int i=0;i<count;i++) { if ((i & 63)==0) yield return null; TraversalWork++; yield return buffer[i]; }
            if (count<capacity) yield break;
            after = buffer[count-1];
        } while (true);
    }

    internal static IEnumerable<string> Quoted(string text)
    {
        yield return "\"";
        var buffer = new System.Text.StringBuilder(Math.Min(text.Length,8192));
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i+1 < text.Length && char.IsLowSurrogate(text[i+1])) buffer.Append(c).Append(text[++i]);
            else if (c is '"' or '\\') buffer.Append('\\').Append(c);
            else if (c is '\b' or '\t' or '\n' or '\f' or '\r')
                buffer.Append('\\').Append(c switch { '\b'=>'b', '\t'=>'t', '\n'=>'n', '\f'=>'f', _=>'r' });
            else if (c < 32 || char.IsSurrogate(c)) buffer.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else buffer.Append(c);
            if (buffer.Length >= 8192) { yield return buffer.ToString(); buffer.Clear(); }
        }
        if (buffer.Length != 0) yield return buffer.ToString();
        yield return "\"";
    }
}
