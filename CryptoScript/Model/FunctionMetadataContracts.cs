using System.Collections;
using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace CryptoScript.Model;

public sealed record CallVariantId
{
    public CallVariantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ArgumentPosition
{
    public ArgumentPosition(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }
}

public enum ArgumentCardinality
{
    Required,
    Optional,
    Variadic
}

public enum FunctionArgumentKind
{
    Value,
    ParameterSet
}

public sealed record ParameterSetContract
{
    public ParameterSetContract(string id, IEnumerable<string> namedParameterNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(namedParameterNames);

        string[] names = namedParameterNames.ToArray();
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Named parameter names must not be null, empty, or whitespace.",
                nameof(namedParameterNames));
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
        {
            throw new ArgumentException(
                "Named parameter names must be unique.",
                nameof(namedParameterNames));
        }

        Id = id;
        NamedParameterNames = names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public string Id { get; }
    public IReadOnlySet<string> NamedParameterNames { get; }
}

public sealed record FunctionArgument
{
    public FunctionArgument(
        ArgumentPosition position,
        string name,
        ArgumentCardinality cardinality,
        FunctionArgumentKind kind = FunctionArgumentKind.Value,
        ParameterSetContract? parameterSet = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(cardinality))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cardinality), cardinality, "The argument cardinality is not defined.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "The function argument kind is not defined.");
        }

        if (kind == FunctionArgumentKind.ParameterSet && parameterSet is null)
        {
            throw new ArgumentException(
                "A parameter-set argument requires a parameter-set contract.",
                nameof(parameterSet));
        }

        if (kind == FunctionArgumentKind.Value && parameterSet is not null)
        {
            throw new ArgumentException(
                "A value argument cannot contain a parameter-set contract.",
                nameof(parameterSet));
        }

        Position = position;
        Name = name;
        Cardinality = cardinality;
        Kind = kind;
        ParameterSet = parameterSet;
    }

    public ArgumentPosition Position { get; }
    public string Name { get; }
    public ArgumentCardinality Cardinality { get; }
    public FunctionArgumentKind Kind { get; }
    public ParameterSetContract? ParameterSet { get; }
}

public sealed record CallVariant
{
    public CallVariant(
        CallVariantId id,
        IEnumerable<FunctionArgument> arguments,
        ParameterSetContract? producesParameterSet = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(arguments);

        FunctionArgument[] copiedArguments = arguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
        {
            throw new ArgumentException(
                "Function arguments must not contain null entries.",
                nameof(arguments));
        }

        bool optionalArgumentSeen = false;
        for (int index = 0; index < copiedArguments.Length; index++)
        {
            if (copiedArguments[index].Position.Value != index)
            {
                throw new ArgumentException(
                    "Function argument positions must be unique, ordered, and contiguous from zero.",
                    nameof(arguments));
            }

            if (copiedArguments[index].Cardinality == ArgumentCardinality.Variadic &&
                index != copiedArguments.Length - 1)
            {
                throw new ArgumentException(
                    "A variadic function argument must be the final argument.",
                    nameof(arguments));
            }

            if (copiedArguments[index].Cardinality == ArgumentCardinality.Required &&
                optionalArgumentSeen)
            {
                throw new ArgumentException(
                    "A required function argument cannot follow an optional argument.",
                    nameof(arguments));
            }

            optionalArgumentSeen |=
                copiedArguments[index].Cardinality == ArgumentCardinality.Optional;
        }

        if (copiedArguments.Select(argument => argument.Name)
            .Distinct(StringComparer.Ordinal).Count() != copiedArguments.Length)
        {
            throw new ArgumentException(
                "Function argument names must be unique within a call variant.",
                nameof(arguments));
        }

        Id = id;
        Arguments = Array.AsReadOnly(copiedArguments);
        ProducesParameterSet = producesParameterSet;
    }

    public CallVariantId Id { get; }
    public IReadOnlyList<FunctionArgument> Arguments { get; }
    public ParameterSetContract? ProducesParameterSet { get; }
}

public sealed class CallVariantCollection : IReadOnlyList<CallVariant>
{
    private readonly ReadOnlyCollection<CallVariant> variants;
    private readonly FrozenDictionary<CallVariantId, CallVariant> variantsById;

    public CallVariantCollection(IEnumerable<CallVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);

        CallVariant[] copiedVariants = variants.ToArray();
        if (copiedVariants.Any(variant => variant is null))
        {
            throw new ArgumentException(
                "Call variants must not contain null entries.",
                nameof(variants));
        }

        if (copiedVariants.Select(variant => variant.Id).Distinct().Count() !=
            copiedVariants.Length)
        {
            throw new ArgumentException(
                "Call variant IDs must be unique within a function.",
                nameof(variants));
        }

        this.variants = Array.AsReadOnly(copiedVariants);
        variantsById = copiedVariants.ToFrozenDictionary(variant => variant.Id);
    }

    public int Count => variants.Count;
    public CallVariant this[int index] => variants[index];
    public CallVariant this[CallVariantId id] => variantsById[id];

    public bool TryGet(CallVariantId id, out CallVariant? variant)
    {
        ArgumentNullException.ThrowIfNull(id);
        return variantsById.TryGetValue(id, out variant);
    }

    public IEnumerator<CallVariant> GetEnumerator() => variants.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
