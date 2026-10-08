using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace CryptoScript.Model;

public sealed record MechanismRegistryEntry
{
    public MechanismRegistryEntry(
        string canonicalName,
        string description,
        string documentationFileName,
        IEnumerable<CryptoScriptFunction> supportedFunctions,
        IEnumerable<MechanismFunctionMetadata>? functionMetadata = null)
    {
        CanonicalName = canonicalName;
        Description = description;
        DocumentationFileName = documentationFileName;
        SupportedFunctions = supportedFunctions.ToFrozenSet();

        MechanismFunctionMetadata[] copiedFunctionMetadata =
            (functionMetadata ?? Array.Empty<MechanismFunctionMetadata>()).ToArray();
        if (copiedFunctionMetadata.Any(metadata =>
                !SupportedFunctions.Contains(metadata.Function)))
        {
            throw new ArgumentException(
                "Function metadata may only describe supported functions.",
                nameof(functionMetadata));
        }

        FunctionMetadata = copiedFunctionMetadata
            .ToFrozenDictionary(metadata => metadata.Function);
    }

    public string CanonicalName { get; }
    public string Description { get; }
    public string DocumentationFileName { get; }
    public IReadOnlySet<CryptoScriptFunction> SupportedFunctions { get; }
    public IReadOnlyDictionary<CryptoScriptFunction, MechanismFunctionMetadata> FunctionMetadata { get; }

    public bool Supports(CryptoScriptFunction function) => SupportedFunctions.Contains(function);
}

public enum MechanismParameterKind
{
    PositionalArgument,
    NamedParameter
}

public enum MechanismParameterDirection
{
    // The operation consumes the parameter. IsRequired describes an input precondition.
    Input,
    // The operation produces or overwrites the parameter; it is not an input precondition.
    Output,
    // The operation consumes the parameter and may change it. IsRequired describes an input precondition.
    InOut
}

public enum MechanismParameterDataType
{
    Mechanism,
    ParameterSet,
    Key,
    Data,
    BinaryData,
    Integer,
    HexString,
    Padding
}

public enum MechanismParameterInputForm
{
    HexLiteral,
    Base64Literal,
    StringLiteral,
    VariableReference
}

public enum AdditionalNamedParameterHandling
{
    None,
    StoreGloballyKnown,
    IgnoreStored
}

public enum MechanismParameterDefaultKind
{
    None,
    Literal,
    Generated
}

public sealed record MechanismParameterMetadata
{
    public MechanismParameterMetadata(
        string name,
        MechanismParameterKind kind,
        MechanismParameterDirection direction,
        bool isRequired,
        IEnumerable<MechanismParameterDataType> resultingDataTypes,
        string description,
        string valueConstraint,
        MechanismParameterDefaultKind defaultKind = MechanismParameterDefaultKind.None,
        string? defaultValue = null,
        string? combinationConstraint = null,
        IEnumerable<MechanismParameterInputForm>? acceptedInputForms = null)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction), direction, "The parameter direction is not defined.");
        }

        if (direction == MechanismParameterDirection.Output && isRequired)
        {
            throw new ArgumentException(
                "An output parameter cannot be a required input.",
                nameof(isRequired));
        }

        if (direction == MechanismParameterDirection.Output &&
            defaultKind != MechanismParameterDefaultKind.None)
        {
            throw new ArgumentException(
                "An output parameter cannot have an input default.",
                nameof(defaultKind));
        }

        if (defaultKind == MechanismParameterDefaultKind.None && defaultValue is not null)
        {
            throw new ArgumentException(
                "A parameter without a default kind cannot have a default value.",
                nameof(defaultValue));
        }

        if (defaultKind != MechanismParameterDefaultKind.None && defaultValue is null)
        {
            throw new ArgumentException(
                "A literal or generated default requires a default value.",
                nameof(defaultValue));
        }

        MechanismParameterInputForm[] copiedInputForms = (acceptedInputForms ??
            Array.Empty<MechanismParameterInputForm>()).ToArray();
        if (copiedInputForms.Any(inputForm => !Enum.IsDefined(inputForm)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(acceptedInputForms),
                "Accepted input forms must contain only defined values.");
        }

        if (direction == MechanismParameterDirection.Output && copiedInputForms.Length != 0)
        {
            throw new ArgumentException(
                "An output parameter cannot have accepted input forms.",
                nameof(acceptedInputForms));
        }

        Name = name;
        Kind = kind;
        Direction = direction;
        IsRequired = isRequired;
        ResultingDataTypes = resultingDataTypes.ToFrozenSet();
        AcceptedInputForms = copiedInputForms.ToFrozenSet();
        Description = description;
        ValueConstraint = valueConstraint;
        DefaultKind = defaultKind;
        DefaultValue = defaultValue;
        CombinationConstraint = combinationConstraint;
    }

    public string Name { get; }
    public MechanismParameterKind Kind { get; }
    public MechanismParameterDirection Direction { get; }
    public bool IsRequired { get; }
    public IReadOnlySet<MechanismParameterDataType> ResultingDataTypes { get; }
    public IReadOnlySet<MechanismParameterInputForm> AcceptedInputForms { get; }
    public string Description { get; }
    public string ValueConstraint { get; }
    public MechanismParameterDefaultKind DefaultKind { get; }
    public string? DefaultValue { get; }
    public string? CombinationConstraint { get; }
}

public sealed record MechanismFunctionMetadata
{
    public MechanismFunctionMetadata(
        CryptoScriptFunction function,
        IEnumerable<MechanismParameterMetadata> parameters,
        AdditionalNamedParameterHandling additionalNamedParameterHandling =
            AdditionalNamedParameterHandling.None)
    {
        if (!Enum.IsDefined(additionalNamedParameterHandling))
        {
            throw new ArgumentOutOfRangeException(
                nameof(additionalNamedParameterHandling),
                "Additional named parameter handling must be a defined value.");
        }

        MechanismParameterMetadata[] copiedParameters = parameters.ToArray();
        if (copiedParameters.Select(parameter => parameter.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != copiedParameters.Length)
        {
            throw new ArgumentException(
                "Parameter names must be unique within function metadata.",
                nameof(parameters));
        }

        Function = function;
        Parameters = Array.AsReadOnly(copiedParameters);
        AdditionalNamedParameterHandling = additionalNamedParameterHandling;
    }

    public CryptoScriptFunction Function { get; }
    public IReadOnlyList<MechanismParameterMetadata> Parameters { get; }
    public AdditionalNamedParameterHandling AdditionalNamedParameterHandling { get; }
}

public enum CryptoScriptFunction
{
    Parameters,
    GenerateKey,
    Encrypt,
    Decrypt,
    Mac,
    Hash,
    Derive,
    Wrap,
    Unwrap,
    BlockHeader
}

public static class MechanismRegistry
{
    private static MechanismParameterMetadata Argument(
        string name,
        bool required,
        MechanismParameterDirection direction,
        MechanismParameterDataType[] types,
        string description,
        string constraint,
        string? combination = null) =>
        new(name, MechanismParameterKind.PositionalArgument, direction, required, types,
            description, constraint, combinationConstraint: combination);

    private static MechanismParameterMetadata NamedParameter(
        string name,
        bool required,
        MechanismParameterDirection direction,
        MechanismParameterDataType type,
        string description,
        string constraint,
        MechanismParameterDefaultKind defaultKind = MechanismParameterDefaultKind.None,
        string? defaultValue = null,
        string? combination = null,
        MechanismParameterInputForm[]? inputForms = null) =>
        new(name, MechanismParameterKind.NamedParameter, direction, required, new[] { type },
            description, constraint, defaultKind, defaultValue, combination, inputForms);

    private static readonly MechanismParameterInputForm[] AesCbcIvInputForms =
    {
        MechanismParameterInputForm.HexLiteral,
        MechanismParameterInputForm.Base64Literal,
        MechanismParameterInputForm.VariableReference
    };

    private static readonly MechanismParameterMetadata[] AesCbcConsumedParameters =
    {
        NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
            "Selects AES-CBC for parameter creation and algorithm dispatch.",
            "Exactly AES-CBC.", combination: "Use either AES-CBC or #MECH:AES-CBC as the first Parameters argument."),
        NamedParameter("#IV", true, MechanismParameterDirection.Input, MechanismParameterDataType.BinaryData,
            "Initialization vector consumed by AES-CBC encryption and decryption.",
            "Hexadecimal or Base64 literal, or variable resolving to either; must decode to exactly 16 bytes.",
            inputForms: AesCbcIvInputForms),
        NamedParameter("#PAD", true, MechanismParameterDirection.Input, MechanismParameterDataType.Padding,
            "Padding mode consumed by AES-CBC encryption and decryption.",
            "PKCS-7, ISO-10126, ISO-7816, ISO-9797-M1, ISO-9797-M2, ISO-9797-M3, ANSI-X923, TLS-CBC, or NONE.")
    };

    private static readonly MechanismFunctionMetadata[] AesCbcFunctionMetadata =
    {
        new(CryptoScriptFunction.Parameters, new MechanismParameterMetadata[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                "Selects the parameter generator.", "AES-CBC or #MECH:AES-CBC.",
                "This is the first argument; the named form is an alternative, not an additional mechanism."),
            NamedParameter("#IV", false, MechanismParameterDirection.Input, MechanismParameterDataType.BinaryData,
                "Initialization vector.",
                "Hexadecimal or Base64 literal, or variable resolving to either; must decode to exactly 16 bytes.",
                MechanismParameterDefaultKind.Generated, "A random 16-byte value",
                inputForms: AesCbcIvInputForms),
            NamedParameter("#PAD", false, MechanismParameterDirection.Input, MechanismParameterDataType.Padding,
                "Padding mode.",
                "PKCS-7, ISO-10126, ISO-7816, ISO-9797-M1, ISO-9797-M2, ISO-9797-M3, ANSI-X923, TLS-CBC, or NONE.",
                MechanismParameterDefaultKind.Literal,
                "PKCS-7")
        }, AdditionalNamedParameterHandling.StoreGloballyKnown),
        new(CryptoScriptFunction.GenerateKey, new[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                "Selects AES key generation or import.", "Exactly AES-CBC."),
            Argument("keySizeOrValue", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Integer, MechanismParameterDataType.HexString },
                "Generates a key of the requested size or imports the supplied key bytes.",
                "Integer 128, 192, or 256; or a hexadecimal value of exactly 16, 24, or 32 bytes.")
        }),
        new(CryptoScriptFunction.Encrypt, new[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.ParameterSet },
                "AES-CBC parameter variable or serialized parameter value.", "Must contain processed #MECH, #IV, and #PAD values."),
            Argument("key", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key, MechanismParameterDataType.HexString },
                "AES key variable or raw key value.", "16, 24, or 32 bytes when processed."),
            Argument("data", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Data },
                "Plaintext variable or literal.", "With #PAD:NONE, length must be a non-zero multiple of 16 bytes."),
        }.Concat(AesCbcConsumedParameters),
            AdditionalNamedParameterHandling.IgnoreStored),
        new(CryptoScriptFunction.Decrypt, new[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.ParameterSet },
                "AES-CBC parameter variable or serialized parameter value.", "Must contain processed #MECH, #IV, and #PAD values."),
            Argument("key", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key, MechanismParameterDataType.HexString },
                "AES key variable or raw key value.", "16, 24, or 32 bytes when processed."),
            Argument("data", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Data },
                "Ciphertext variable or literal.", "With #PAD:NONE, length must be a non-zero multiple of 16 bytes; otherwise length must be a multiple of 16 bytes."),
        }.Concat(AesCbcConsumedParameters),
            AdditionalNamedParameterHandling.IgnoreStored)
    };

    private static readonly MechanismParameterMetadata[] AesCbcMacConsumedParameters =
    {
        NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
            "Selects AES-CBC-MAC for parameter creation and algorithm dispatch.",
            "Exactly AES-CBC-MAC."),
        NamedParameter("#PAD", true, MechanismParameterDirection.Input, MechanismParameterDataType.Padding,
            "Padding applied before CBC-MAC calculation.",
            "NONE, PKCS-7, ANSI-X923, ISO-7816, ISO-9797-M1, ISO-9797-M2, ISO-9797-M3, or TLS-CBC."),
        NamedParameter("#MACLEN", true, MechanismParameterDirection.Input, MechanismParameterDataType.Integer,
            "Number of leftmost MAC bytes returned.", "String integer from 8 through 16 bytes.")
    };

    private static readonly MechanismFunctionMetadata[] AesCbcMacFunctionMetadata =
    {
        new(CryptoScriptFunction.Parameters, new MechanismParameterMetadata[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                "Selects the parameter generator.", "AES-CBC-MAC or #MECH:AES-CBC-MAC."),
            NamedParameter("#PAD", false, MechanismParameterDirection.Input, MechanismParameterDataType.Padding,
                "Padding applied before CBC-MAC calculation.",
                "NONE, PKCS-7, ANSI-X923, ISO-7816, ISO-9797-M1, ISO-9797-M2, ISO-9797-M3, or TLS-CBC.",
                MechanismParameterDefaultKind.Literal, "PKCS-7"),
            NamedParameter("#MACLEN", false, MechanismParameterDirection.Input, MechanismParameterDataType.Integer,
                "Number of leftmost MAC bytes returned.", "String integer from 8 through 16 bytes.",
                MechanismParameterDefaultKind.Literal, "16")
        }),
        new(CryptoScriptFunction.GenerateKey, new[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                "Selects AES key generation or import.", "Exactly AES-CBC-MAC."),
            Argument("keySizeOrValue", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Integer, MechanismParameterDataType.HexString },
                "Generates a key of the requested size or imports the supplied key bytes.",
                "Integer 128, 192, or 256; or a hexadecimal value of exactly 16, 24, or 32 bytes.")
        }),
        new(CryptoScriptFunction.Mac, new[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.ParameterSet },
                "AES-CBC-MAC parameter variable or serialized parameter value.",
                "Must contain processed #MECH, #PAD, and #MACLEN values."),
            Argument("key", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key, MechanismParameterDataType.HexString },
                "AES key variable or raw key value.", "16, 24, or 32 bytes when processed."),
            Argument("data", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Data },
                "Message variable or literal.",
                "With #PAD:NONE, length must be a non-zero multiple of 16 bytes.")
        }.Concat(AesCbcMacConsumedParameters))
    };

    private static readonly CryptoScriptFunction[] CipherFunctions =
    {
        CryptoScriptFunction.Parameters,
        CryptoScriptFunction.GenerateKey,
        CryptoScriptFunction.Encrypt,
        CryptoScriptFunction.Decrypt
    };

    private static readonly CryptoScriptFunction[] MacFunctions =
    {
        CryptoScriptFunction.Parameters,
        CryptoScriptFunction.GenerateKey,
        CryptoScriptFunction.Mac
    };

    private static readonly CryptoScriptFunction[] Des3CbcFunctions =
    {
        CryptoScriptFunction.Parameters,
        CryptoScriptFunction.GenerateKey,
        CryptoScriptFunction.Encrypt,
        CryptoScriptFunction.Decrypt,
        CryptoScriptFunction.Mac
    };

    private static readonly CryptoScriptFunction[] DerivationFunctions =
    {
        CryptoScriptFunction.Parameters,
        CryptoScriptFunction.Derive
    };

    private static readonly CryptoScriptFunction[] HashFunctions =
    {
        CryptoScriptFunction.Parameters,
        CryptoScriptFunction.Hash
    };

    private static readonly CryptoScriptFunction[] WrapFunctions =
    {
        CryptoScriptFunction.Wrap,
        CryptoScriptFunction.Unwrap
    };

    private static readonly CryptoScriptFunction[] AesTr31WrapFunctions =
    {
        CryptoScriptFunction.Wrap,
        CryptoScriptFunction.Unwrap,
        CryptoScriptFunction.BlockHeader
    };

    private static MechanismFunctionMetadata PinBlockFunction(
        string mechanism,
        CryptoScriptFunction function,
        bool mutatesParameters,
        params MechanismParameterMetadata[] namedParameters)
    {
        if (function is not (CryptoScriptFunction.Wrap or CryptoScriptFunction.Unwrap))
            throw new ArgumentOutOfRangeException(nameof(function));

        string dataName = function == CryptoScriptFunction.Wrap ? "pin" : "pinBlock";
        MechanismParameterDataType dataType = function == CryptoScriptFunction.Wrap
            ? MechanismParameterDataType.Data
            : MechanismParameterDataType.BinaryData;
        string dataDescription = function == CryptoScriptFunction.Wrap
            ? "Clear PIN consumed when constructing the PIN block."
            : "Encrypted PIN block consumed when recovering the clear PIN.";

        var parameters = new List<MechanismParameterMetadata>
        {
            Argument("parameters", true,
                mutatesParameters ? MechanismParameterDirection.InOut : MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet },
                "PIN-block parameter variable.",
                "Must contain the named parameters required by the selected ISO 9564 format."),
            Argument("key", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key, MechanismParameterDataType.HexString },
                "PIN encryption key.",
                mechanism.StartsWith("WRAP-AES-", StringComparison.Ordinal)
                    ? "AES key suitable for ISO 9564 format 4."
                    : "TDEA key suitable for the selected ISO 9564 format."),
            Argument(dataName, true, MechanismParameterDirection.Input, new[] { dataType },
                dataDescription,
                function == CryptoScriptFunction.Wrap
                    ? "A PIN value accepted by the future ISO 9564 implementation."
                    : "A PIN-block value accepted by the future ISO 9564 implementation."),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input,
                MechanismParameterDataType.Mechanism,
                "Selects the ISO 9564 PIN-block mechanism.",
                $"Exactly {mechanism}.")
        };
        parameters.AddRange(namedParameters);
        return new MechanismFunctionMetadata(
            function, parameters, AdditionalNamedParameterHandling.None);
    }

    private static MechanismParameterMetadata PanParameter() =>
        NamedParameter("#PAN", true, MechanismParameterDirection.Input,
            MechanismParameterDataType.HexString,
            "Complete primary account number used by the PIN-block format.",
            "Complete decimal PAN encoded as a hexadecimal string.");

    private static MechanismParameterMetadata VariablePinField(
        string name,
        MechanismParameterDirection direction,
        string description,
        string constraint,
        bool securelyGeneratedWhenAbsent = false) =>
        NamedParameter(name, false, direction, MechanismParameterDataType.HexString,
            description, constraint,
            securelyGeneratedWhenAbsent
                ? MechanismParameterDefaultKind.Generated
                : MechanismParameterDefaultKind.None,
            securelyGeneratedWhenAbsent ? "Cryptographically secure random value" : null);

    private static readonly MechanismFunctionMetadata[] PinBlockFormat0FunctionMetadata =
    {
        PinBlockFunction("WRAP-DES3-PINBLOCK-0", CryptoScriptFunction.Wrap, false,
            PanParameter()),
        PinBlockFunction("WRAP-DES3-PINBLOCK-0", CryptoScriptFunction.Unwrap, false,
            PanParameter())
    };

    private static readonly MechanismFunctionMetadata[] PinBlockFormat1FunctionMetadata =
    {
        PinBlockFunction("WRAP-DES3-PINBLOCK-1", CryptoScriptFunction.Wrap, true,
            VariablePinField("#TRANSACTION", MechanismParameterDirection.InOut,
                "Transaction field optionally supplied for wrapping and later stored with the used value.",
                "Exactly 14 minus PIN length hexadecimal nibbles.",
                securelyGeneratedWhenAbsent: true)),
        PinBlockFunction("WRAP-DES3-PINBLOCK-1", CryptoScriptFunction.Unwrap, true,
            VariablePinField("#TRANSACTION", MechanismParameterDirection.Output,
                "Transaction field extracted during unwrapping.",
                "Exactly 14 minus PIN length hexadecimal nibbles."))
    };

    private static readonly MechanismFunctionMetadata[] PinBlockFormat2FunctionMetadata =
    {
        PinBlockFunction("WRAP-DES3-PINBLOCK-2", CryptoScriptFunction.Wrap, false),
        PinBlockFunction("WRAP-DES3-PINBLOCK-2", CryptoScriptFunction.Unwrap, false)
    };

    private static readonly MechanismFunctionMetadata[] PinBlockFormat3FunctionMetadata =
    {
        PinBlockFunction("WRAP-DES3-PINBLOCK-3", CryptoScriptFunction.Wrap, true,
            PanParameter(),
            VariablePinField("#FILL", MechanismParameterDirection.InOut,
                "Fill field optionally supplied for wrapping and later stored with the used value.",
                "Exactly 14 minus PIN length nibbles, each A through F.",
                securelyGeneratedWhenAbsent: true)),
        PinBlockFunction("WRAP-DES3-PINBLOCK-3", CryptoScriptFunction.Unwrap, true,
            PanParameter(),
            VariablePinField("#FILL", MechanismParameterDirection.Output,
                "Fill field extracted during unwrapping.",
                "Exactly 14 minus PIN length nibbles, each A through F."))
    };

    private static readonly MechanismFunctionMetadata[] PinBlockFormat4FunctionMetadata =
    {
        PinBlockFunction("WRAP-AES-PINBLOCK-4", CryptoScriptFunction.Wrap, true,
            PanParameter(),
            VariablePinField("#RANDOM", MechanismParameterDirection.InOut,
                "Random field optionally supplied for wrapping and later stored with the used value.",
                "Exactly 16 hexadecimal nibbles.",
                securelyGeneratedWhenAbsent: true)),
        PinBlockFunction("WRAP-AES-PINBLOCK-4", CryptoScriptFunction.Unwrap, true,
            PanParameter(),
            VariablePinField("#RANDOM", MechanismParameterDirection.Output,
                "Random field extracted during unwrapping.",
                "Exactly 16 hexadecimal nibbles."))
    };

    private static readonly ReadOnlyCollection<MechanismRegistryEntry> RegistryEntries =
        Array.AsReadOnly(new MechanismRegistryEntry[]
        {
            new("AES-CBC", "Symmetric Advanced Encryption Standard in Cipher Block Chaining mode.", "Info.Mech.AES-CBC.md", CipherFunctions, AesCbcFunctionMetadata),
            new("AES-CBC-MAC", "AES Cipher Block Chaining Message Authentication Code with selectable deterministic padding and left truncation.", "Info.Mech.AES-CBC-MAC.md", MacFunctions, AesCbcMacFunctionMetadata),
            new("AES-CCM", "Symmetric Advanced Encryption Standard in Counter with CBC-MAC mode.", "Info.Mech.AES-CCM.md", CipherFunctions),
            new("AES-CMAC", "Symmetric Advanced Encryption Standard in Cipher-based Message Authentication Code mode.", "Info.Mech.AES-CMAC.md", MacFunctions),
            new("AES-CTR", "Symmetric Advanced Encryption Standard in Counter mode.", "Info.Mech.AES-CTR.md", CipherFunctions),
            new("AES-ECB", "Symmetric Advanced Encryption Standard in Electronic Codebook mode.", "Info.Mech.AES-ECB.md", CipherFunctions),
            new("AES-GCM", "Symmetric Advanced Encryption Standard in Galois/Counter mode.", "Info.Mech.AES-GCM.md", CipherFunctions),
            new("AES-GMAC", "Symmetric Advanced Encryption Standard in Galois/Counter mode.", "Info.Mech.AES-GMAC.md", MacFunctions),
            new("DES3-CBC", "Symmetric Triple Data Encryption Standard in Cipher Block Chaining mode.", "Info.Mech.DES3-CBC.md", Des3CbcFunctions),
            new("DES3-CMAC", "Symmetric Triple Data Encryption Standard in Cipher-based Message Authentication Code mode.", "Info.Mech.DES3-CMAC.md", MacFunctions),
            new("DES3-ECB", "Symmetric Triple Data Encryption Standard in Electronic Codebook mode.", "Info.Mech.DES3-ECB.md", CipherFunctions),
            new("DES3-RETAIL", "Symmetric Triple Data Encryption Standard in Retail mode.", "Info.Mech.DES3-RETAIL.md", MacFunctions),
            new("DUKPT-AES-INITIAL-KEY", "ANSI X9.24-3-2017 derivation of an AES DUKPT Initial Key from an AES BDK and 64-bit IKID.", "Info.Mech.DUKPT-AES-INITIAL-KEY.md", DerivationFunctions),
            new("DUKPT-AES-WORKING-KEY", "ANSI X9.24-3-2017 stateless host derivation of a directly usable Working Key from an AES DUKPT Initial Key and complete 96-bit KSN.", "Info.Mech.DUKPT-AES-WORKING-KEY.md", DerivationFunctions),
            new("DUKPT-TDEA-INITIAL-KEY", "Legacy ANSI X9.24 double-length TDEA DUKPT Initial Key derivation from a 16-byte BDK and complete 80-bit KSN.", "Info.Mech.DUKPT-TDEA-INITIAL-KEY.md", DerivationFunctions),
            new("DUKPT-TDEA-WORKING-KEY", "ANSI X9.24-3 Annex C stateless host derivation of a double-length TDEA Working Key from a TDEA Initial Key and complete 80-bit KSN.", "Info.Mech.DUKPT-TDEA-WORKING-KEY.md", DerivationFunctions),
            new("HASH-SHA1", "Unkeyed message digest using SHA-1.", "Info.Mech.HASH-SHA1.md", HashFunctions),
            new("HASH-SHA224", "Unkeyed message digest using SHA-224.", "Info.Mech.HASH-SHA224.md", HashFunctions),
            new("HASH-SHA256", "Unkeyed message digest using SHA-256.", "Info.Mech.HASH-SHA256.md", HashFunctions),
            new("HASH-SHA3-224", "Unkeyed message digest using SHA3-224.", "Info.Mech.HASH-SHA3-224.md", HashFunctions),
            new("HASH-SHA3-256", "Unkeyed message digest using SHA3-256.", "Info.Mech.HASH-SHA3-256.md", HashFunctions),
            new("HASH-SHA3-384", "Unkeyed message digest using SHA3-384.", "Info.Mech.HASH-SHA3-384.md", HashFunctions),
            new("HASH-SHA3-512", "Unkeyed message digest using SHA3-512.", "Info.Mech.HASH-SHA3-512.md", HashFunctions),
            new("HASH-SHA384", "Unkeyed message digest using SHA-384.", "Info.Mech.HASH-SHA384.md", HashFunctions),
            new("HASH-SHA512", "Unkeyed message digest using SHA-512.", "Info.Mech.HASH-SHA512.md", HashFunctions),
            new("HASH-SHA512-224", "Unkeyed message digest using SHA-512/224.", "Info.Mech.HASH-SHA512-224.md", HashFunctions),
            new("HASH-SHA512-256", "Unkeyed message digest using SHA-512/256.", "Info.Mech.HASH-SHA512-256.md", HashFunctions),
            new("HKDF-EXPAND", "RFC 5869 HKDF Expand operation only; derives output keying material from an existing PRK.", "Info.Mech.HKDF-EXPAND.md", DerivationFunctions),
            new("HKDF-EXTRACT", "RFC 5869 HKDF Extract operation only; returns the pseudorandom key (PRK).", "Info.Mech.HKDF-EXTRACT.md", DerivationFunctions),
            new("HMAC-SHA1", "Keyed-Hash Message Authentication Code using SHA-1.", "Info.Mech.HMAC-SHA1.md", MacFunctions),
            new("HMAC-SHA224", "Keyed-Hash Message Authentication Code using SHA-224.", "Info.Mech.HMAC-SHA224.md", MacFunctions),
            new("HMAC-SHA256", "Keyed-Hash Message Authentication Code using SHA-256.", "Info.Mech.HMAC-SHA256.md", MacFunctions),
            new("HMAC-SHA3-224", "Keyed-Hash Message Authentication Code using SHA3-224.", "Info.Mech.HMAC-SHA3-224.md", MacFunctions),
            new("HMAC-SHA3-256", "Keyed-Hash Message Authentication Code using SHA3-256.", "Info.Mech.HMAC-SHA3-256.md", MacFunctions),
            new("HMAC-SHA3-384", "Keyed-Hash Message Authentication Code using SHA3-384.", "Info.Mech.HMAC-SHA3-384.md", MacFunctions),
            new("HMAC-SHA3-512", "Keyed-Hash Message Authentication Code using SHA3-512.", "Info.Mech.HMAC-SHA3-512.md", MacFunctions),
            new("HMAC-SHA384", "Keyed-Hash Message Authentication Code using SHA-384.", "Info.Mech.HMAC-SHA384.md", MacFunctions),
            new("HMAC-SHA512", "Keyed-Hash Message Authentication Code using SHA-512.", "Info.Mech.HMAC-SHA512.md", MacFunctions),
            new("HMAC-SHA512-224", "Keyed-Hash Message Authentication Code using SHA-512/224.", "Info.Mech.HMAC-SHA512-224.md", MacFunctions),
            new("HMAC-SHA512-256", "Keyed-Hash Message Authentication Code using SHA-512/256.", "Info.Mech.HMAC-SHA512-256.md", MacFunctions),
            new("KDF-EP2-PAN-RECEIPT-TRM", "ep2 8.13 Extract-and-Expand using SHA-256(Terminal Properties) as info and returning the leftmost 16 of 32 bytes.", "Info.Mech.KDF-EP2-PAN-RECEIPT-TRM.md", DerivationFunctions),
            new("KDF-EP2-PAN-RECEIPT-TRX", "ep2 8.12 direct Expand using SHA-256(DOL) as info and returning the leftmost 16 of 32 bytes.", "Info.Mech.KDF-EP2-PAN-RECEIPT-TRX.md", DerivationFunctions),
            new("KDF-EP2-PAN-SURROGATE-TRX", "ep2 8.14 direct Expand using raw DOL as info and returning all 32 bytes.", "Info.Mech.KDF-EP2-PAN-SURROGATE-TRX.md", DerivationFunctions),
            new("KDF-EP2-SESSION", "ep2 8.11 Extract-and-Expand derivation of a selected Session Key Variant.", "Info.Mech.KDF-EP2-SESSION.md", DerivationFunctions),
            new("KDF-HKDF", "HMAC-based Extract-and-Expand Key Derivation Function specified in RFC 5869.", "Info.Mech.KDF-HKDF.md", DerivationFunctions),
            new("KDF-SP800-108-COUNTER", "NIST SP 800-108 Rev. 1 Update 1 Counter Mode KDF using a supported HMAC PRF or AES-CMAC.", "Info.Mech.KDF-SP800-108-COUNTER.md", DerivationFunctions),
            // BlockHeader is supported only through CryptoOperations' internal
            // BLOCKHEADER-WRAP-AES-TR31 dispatch and its populated one-argument overload.
            new("WRAP-AES-PINBLOCK-4", "ISO 9564 format 4 PIN-block wrapping with AES; cryptographic processing is not yet implemented.", "Info.Mech.WRAP-AES-PINBLOCK-4.md", WrapFunctions, PinBlockFormat4FunctionMetadata),
            new("WRAP-AES-TR31", "TR-31 Version D key wrapping with AES Key Derivation Binding.", "Info.Mech.WRAP-AES-TR31.md", AesTr31WrapFunctions),
            new("WRAP-DES3-PINBLOCK-0", "ISO 9564 format 0 PIN-block wrapping and unwrapping with TDEA.", "Info.Mech.WRAP-DES3-PINBLOCK-0.md", WrapFunctions, PinBlockFormat0FunctionMetadata),
            new("WRAP-DES3-PINBLOCK-1", "ISO 9564 format 1 PIN-block wrapping and unwrapping with TDEA.", "Info.Mech.WRAP-DES3-PINBLOCK-1.md", WrapFunctions, PinBlockFormat1FunctionMetadata),
            new("WRAP-DES3-PINBLOCK-2", "ISO 9564 format 2 PIN-block wrapping and unwrapping with TDEA for EMV offline PIN verification.", "Info.Mech.WRAP-DES3-PINBLOCK-2.md", WrapFunctions, PinBlockFormat2FunctionMetadata),
            new("WRAP-DES3-PINBLOCK-3", "ISO 9564 format 3 PIN-block wrapping and unwrapping with TDEA.", "Info.Mech.WRAP-DES3-PINBLOCK-3.md", WrapFunctions, PinBlockFormat3FunctionMetadata),
            new("WRAP-DES3-TR31", "TR-31 Version A/B/C key wrapping with TDEA Variant or Derivation Binding.", "Info.Mech.WRAP-DES3-TR31.md", WrapFunctions)
        });

    private static readonly FrozenDictionary<string, MechanismRegistryEntry> EntriesByName =
        RegistryEntries.ToFrozenDictionary(entry => entry.CanonicalName, StringComparer.Ordinal);

    public static IReadOnlyList<MechanismRegistryEntry> Entries => RegistryEntries;

    public static bool TryGet(string? canonicalName, out MechanismRegistryEntry? entry)
    {
        if (canonicalName is null)
        {
            entry = null;
            return false;
        }

        return EntriesByName.TryGetValue(canonicalName, out entry);
    }
}
