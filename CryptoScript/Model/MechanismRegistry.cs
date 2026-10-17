using System.Collections.Frozen;
using System.Collections.ObjectModel;
using CryptoScript.Variables;

namespace CryptoScript.Model;

public sealed record MechanismRegistryEntry
{
    public MechanismRegistryEntry(
        string canonicalName,
        string description,
        string documentationFileName,
        IEnumerable<CryptoScriptFunction> supportedFunctions,
        IEnumerable<MechanismFunctionMetadata>? functionMetadata = null,
        KeyAlgorithm? generatedKeyAlgorithm = null)
    {
        CanonicalName = canonicalName;
        Description = description;
        DocumentationFileName = documentationFileName;
        SupportedFunctions = supportedFunctions.ToFrozenSet();
        if (SupportedFunctions.Contains(CryptoScriptFunction.GenerateKey) != generatedKeyAlgorithm.HasValue ||
            generatedKeyAlgorithm.HasValue &&
            generatedKeyAlgorithm is not (KeyAlgorithm.Aes or KeyAlgorithm.Tdea or KeyAlgorithm.Hmac))
        {
            throw new ArgumentException(
                "GenerateKey support requires exactly one defined symmetric key algorithm; unsupported functions must not declare one.",
                nameof(generatedKeyAlgorithm));
        }
        GeneratedKeyAlgorithm = generatedKeyAlgorithm;

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
    public KeyAlgorithm? GeneratedKeyAlgorithm { get; }
    public IReadOnlyDictionary<CryptoScriptFunction, MechanismFunctionMetadata> FunctionMetadata { get; }
    internal bool StrictMechanismParameterContract { get; init; }

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

    private static MechanismFunctionMetadata[] Ep2Tr31FunctionMetadata()
    {
        var mech = NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
            "Consistent mechanism selector.", "Exactly EP2-TR31-KEY-BLOCK.");
        MechanismParameterMetadata Header(bool required) => NamedParameter("#BLKH", required, MechanismParameterDirection.Input,
            MechanismParameterDataType.Data, "Complete EP2 Table 11 header; expected authenticated header for Unwrap.",
            "D011299HX00N0000, D01129AHG00N0000 or D01129BHG00N0000.",
            inputForms: new[] { MechanismParameterInputForm.StringLiteral, MechanismParameterInputForm.VariableReference });
        var random = NamedParameter("#RND", false, MechanismParameterDirection.Input, MechanismParameterDataType.BinaryData,
            "Optional deterministic filler; omitted means cryptographically secure random bytes.", "Exactly 14 HEX bytes; forbidden for Unwrap.",
            inputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference });
        var common = new[] {
            Argument("parameters", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.ParameterSet },
                "Genuine matching PARAM reference.", "Position 0; no nested or reconstructed objects."),
            Argument("kbpk", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key },
                "Genuine AES KBPK KEY reference.", "16 bytes; consistent known size. WrapKey/UnwrapKey metadata conflicts warn.") };
        return new[] {
            new MechanismFunctionMetadata(CryptoScriptFunction.Parameters, new[] {
                Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                    "Selects EP2 TR-31.", "EP2-TR31-KEY-BLOCK."), Header(false), random }),
            new MechanismFunctionMetadata(CryptoScriptFunction.Wrap, common.Concat(new[] {
                Argument("key", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key },
                    "Genuine transported KEY reference.", "Exactly 16 bytes; HMAC classification conflicts warn. Header determines role."),
                mech, Header(true), random })),
            new MechanismFunctionMetadata(CryptoScriptFunction.Unwrap, common.Concat(new[] {
                Argument("block", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Data },
                    "Genuine VAR holding a complete wire or composite block.", "112 ASCII wire characters; authenticated before key release."),
                mech, Header(false) })) };
    }

    private static MechanismFunctionMetadata[] Ep2PanReceiptFunctionMetadata()
    {
        const string mechanism = "EP2-PAN-RECEIPT-ENCRYPTION";
        var arguments = new[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.ParameterSet },
                "Genuine matching PARAM reference at position 0.", "Only MECH; no reconstructed objects or nested results."),
            Argument("key", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key },
                "Genuine Key PAN Receipt Trx KEY reference at position 1, already derived by the caller.",
                "Exactly 16 bytes; known size must agree. AES and Encrypt/Decrypt usage conflicts warn."),
            new MechanismParameterMetadata("data", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input,
                true, new[] { MechanismParameterDataType.BinaryData }, "Already encoded PAN bytes or ciphertext; no PAN conversion.",
                "Direct HEX literal or genuine HEX VAR; mandatory ISO-7816 padding. Decrypt requires positive 16-byte multiples and valid padding.",
                acceptedInputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Consistent mechanism selector.", "Exactly " + mechanism + "; no additional or duplicate parameters.")
        };
        return new[]
        {
            new MechanismFunctionMetadata(CryptoScriptFunction.Parameters, new[] {
                Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                    "Selects EP2 PAN receipt encryption.", mechanism + " or #MECH:" + mechanism + ".") }),
            new MechanismFunctionMetadata(CryptoScriptFunction.Encrypt, arguments),
            new MechanismFunctionMetadata(CryptoScriptFunction.Decrypt, arguments)
        };
    }

    private static MechanismFunctionMetadata[] Ep2EncryptionFunctionMetadata()
    {
        var consumed = new[]
        {
            NamedParameter("#NONCE", true, MechanismParameterDirection.Input, MechanismParameterDataType.BinaryData,
                "Caller-supplied nonce; no automatic generation or session management.", "Exactly 12 HEX bytes.",
                inputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#OFFSET", true, MechanismParameterDirection.Input, MechanismParameterDataType.BinaryData,
                "EP2 Table 10 field offset.", "Exactly one HEX byte: SEND 00–07; RECEIVE 00–01; other values are RFU.",
                inputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#DIRECTION", true, MechanismParameterDirection.Input, MechanismParameterDataType.Data,
                "Message direction relative to the master, independent of Encrypt or Decrypt.",
                "Normal string literal SEND (from master) or RECEIVE (to master).",
                inputForms: new[] { MechanismParameterInputForm.StringLiteral, MechanismParameterInputForm.VariableReference })
        };
        var arguments = new[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.ParameterSet },
                "Genuine PARAM reference at position 0.", "No reconstructed objects or nested results."),
            Argument("key", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Key },
                "Genuine KEY reference at position 1.", "Exactly 16 bytes with consistent known size; AES and Encrypt/Decrypt usage conflicts warn."),
            new MechanismParameterMetadata("data", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input,
                true, new[] { MechanismParameterDataType.BinaryData }, "Byte-exact plaintext or ciphertext.",
                "Direct HEX literal or genuine HEX VAR; at most 2^24 blocks, no padding or nested results.",
                acceptedInputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Consistent mechanism selector.", "Exactly EP2-DATA-ENCRYPTION; no additional or duplicate parameters.")
        };
        return new[]
        {
            new MechanismFunctionMetadata(CryptoScriptFunction.Parameters, new[]
            {
                Argument("mechanism", true, MechanismParameterDirection.Input, new[] { MechanismParameterDataType.Mechanism },
                    "Selects EP2 data encryption.", "EP2-DATA-ENCRYPTION or #MECH:EP2-DATA-ENCRYPTION.")
            }.Concat(consumed)),
            new MechanismFunctionMetadata(CryptoScriptFunction.Encrypt, arguments.Concat(consumed)),
            new MechanismFunctionMetadata(CryptoScriptFunction.Decrypt, arguments.Concat(consumed))
        };
    }

    private static MechanismFunctionMetadata[] Ep2MacFunctionMetadata(string mechanism) => new[]
    {
        new MechanismFunctionMetadata(CryptoScriptFunction.Parameters, new[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism }, "Selects EP2 MAC processing.", mechanism + ".")
        }),
        new MechanismFunctionMetadata(CryptoScriptFunction.Mac, new[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet }, "Genuine PARAM reference at position 0.",
                "No reconstructed objects or nested results; only the selected MECH is allowed."),
            Argument("key", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key }, "Genuine KEY reference at position 1.",
                "Exactly 32 bytes; known size metadata must agree. HMAC/MacGenerate metadata conflicts warn."),
            new MechanismParameterMetadata("data", MechanismParameterKind.PositionalArgument,
                MechanismParameterDirection.Input, true, new[] { MechanismParameterDataType.BinaryData },
                mechanism == "EP2-MESSAGE-MAC" ? "Unchanged bytes after the message MAC field." : "Already prepared DOL value bytes.",
                "Direct HEX literal or genuine HEX VAR; no nested results or implicit conversions. Output is eight bytes.",
                acceptedInputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Consistent mechanism selector.", "Exactly " + mechanism + "; no additional or duplicate parameters.")
        })
    };

    private static MechanismFunctionMetadata[] EmvAcFunctionMetadata(string mechanism, bool verify)
    {
        var generation = new List<MechanismParameterMetadata>
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism }, "Selects EMV AC processing.", mechanism + ".")
        };
        var mac = new List<MechanismParameterMetadata>
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet }, "Genuine PARAM variable reference.",
                "Original Variable argument; no reconstructed PARAM or direct MECH selector."),
            Argument("sessionKey", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key }, "Genuine Secret AES or TDEA KEY reference.",
                "TDEA exactly 16 bytes; AES 16, 24 or 32 bytes; known size metadata must agree."),
            new("transactionData", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input, true,
                new[] { MechanismParameterDataType.BinaryData }, "Already assembled binary transaction data.",
                "Direct HEX literal or HEX VAR reference; no implicit conversion or nested function result.",
                acceptedInputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Consistent mechanism selector.", "Exactly " + mechanism + ".")
        };
        if (verify)
        {
            // Parameter transport establishes the resolved value, not its original AST origin.
            var expected = NamedParameter("#EXPECTED", true, MechanismParameterDirection.Input,
                MechanismParameterDataType.BinaryData, "Expected Application Cryptogram.",
                "HEX value with exactly 16 hex digits (eight bytes); no implicit ASCII or Base64 conversion.");
            generation.Add(expected);
            mac.Add(expected);
        }
        return new[]
        {
            new MechanismFunctionMetadata(CryptoScriptFunction.Parameters, generation),
            new MechanismFunctionMetadata(CryptoScriptFunction.Mac, mac)
        };
    }

    private static MechanismFunctionMetadata[] EmvArpcFunctionMetadata(string mechanism, bool method2)
    {
        var named = new List<MechanismParameterMetadata>
        {
            NamedParameter(method2 ? "#CSU" : "#ARC", true, MechanismParameterDirection.Input,
                MechanismParameterDataType.BinaryData, method2 ? "Binary Card Status Update." : "Binary Authorisation Response Code.",
                method2 ? "Exactly four bytes as a resolved HEX value." : "Exactly two bytes as a resolved HEX value.")
        };
        if (method2)
        {
            named.Add(NamedParameter("#ADATA", false, MechanismParameterDirection.Input,
                MechanismParameterDataType.BinaryData, "Proprietary Authentication Data.",
                "Resolved HEX value of one to eight bytes; omission means empty data."));
            named.Add(NamedParameter("#VARIANT", false, MechanismParameterDirection.Input,
                MechanismParameterDataType.Data, "Selects complete Issuer Authentication Data output.",
                "Only the explicit normal string IAD; omission returns four-byte ARPC.",
                inputForms: new[] { MechanismParameterInputForm.StringLiteral }));
        }
        var generation = new List<MechanismParameterMetadata>
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism }, "Selects the EMV ARPC method.", mechanism + ".")
        };
        generation.AddRange(named);
        var mac = new List<MechanismParameterMetadata>
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet }, "Genuine PARAM variable reference.", "No reconstruction or direct MECH argument."),
            Argument("sessionKey", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key }, "Already derived AC Session KEY.",
                "Genuine Secret TDEA 16-byte or AES 16/24/32-byte KEY reference; known size metadata must match."),
            new("arqc", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input, true,
                new[] { MechanismParameterDataType.BinaryData }, "Received ARQC; not automatically verified.",
                "Exactly eight bytes from a direct HEX literal or HEX VAR reference; no nested function result.",
                acceptedInputForms: new[] { MechanismParameterInputForm.HexLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Consistent mechanism selector.", "Exactly " + mechanism + ".")
        };
        mac.AddRange(named);
        return new[]
        {
            new MechanismFunctionMetadata(CryptoScriptFunction.Parameters, generation),
            new MechanismFunctionMetadata(CryptoScriptFunction.Mac, mac)
        };
    }

    private static readonly MechanismFunctionMetadata[] EmvAcSessionFunctionMetadata =
    {
        new(CryptoScriptFunction.Parameters, new[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism },
                "Selects EMV Application Cryptogram session-key derivation.",
                "KDF-EMV-AC-SESSION or #MECH:KDF-EMV-AC-SESSION.")
        }),
        new(CryptoScriptFunction.Derive, new MechanismParameterMetadata[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet },
                "KDF-EMV-AC-SESSION parameter variable.",
                "Must contain only #MECH:KDF-EMV-AC-SESSION."),
            Argument("masterKey", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key },
                "AES or double-length TDEA Application Cryptogram Master Key.",
                "AES key of 16, 24 or 32 bytes, or TDEA key of exactly 16 bytes."),
            new MechanismParameterMetadata(
                "atc",
                MechanismParameterKind.PositionalArgument,
                MechanismParameterDirection.Input,
                true,
                new[] { MechanismParameterDataType.BinaryData },
                "Two-byte EMV Application Transaction Counter.",
                "Exactly two bytes supplied as a hexadecimal literal or a VAR containing hexadecimal binary data.",
                acceptedInputForms: new[]
                {
                    MechanismParameterInputForm.HexLiteral,
                    MechanismParameterInputForm.VariableReference
                }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input,
                MechanismParameterDataType.Mechanism,
                "Selects EMV Application Cryptogram session-key derivation.",
                "Exactly KDF-EMV-AC-SESSION.")
        })
    };

    private static readonly MechanismFunctionMetadata[] EmvMasterFunctionMetadata =
    {
        new(CryptoScriptFunction.Parameters, new MechanismParameterMetadata[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism }, "Selects EMV ICC Master Key Option A.", "KDF-EMV-MASTER-A."),
            new("#PSN", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
                new[] { MechanismParameterDataType.Data }, "PAN Sequence Number.", "Exactly two ASCII decimal digits in a normal string literal.",
                MechanismParameterDefaultKind.Literal, "\"00\"", acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral })
        }),
        new(CryptoScriptFunction.Derive, new MechanismParameterMetadata[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet }, "Option A parameters.", "Only MECH and optional PSN; no duplicates."),
            Argument("issuerMasterKey", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key }, "Issuer Master KEY.", "Secret TDEA KEY of exactly 16 bytes."),
            new("pan", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input, true,
                new[] { MechanismParameterDataType.Data }, "Complete PAN including check digit.", "1-19 ASCII decimal digits, no normalization.",
                acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Selects Option A.", "Exactly KDF-EMV-MASTER-A."),
            new("#PSN", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
                new[] { MechanismParameterDataType.Data }, "PAN Sequence Number.", "Exactly two ASCII decimal digits in a normal string literal.",
                MechanismParameterDefaultKind.Literal, "\"00\"", acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral })
        })
    };

    private static readonly MechanismFunctionMetadata[] EmvMasterBFunctionMetadata =
    {
        new(CryptoScriptFunction.Parameters, new MechanismParameterMetadata[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism }, "Selects EMV ICC Master Key Option B.", "KDF-EMV-MASTER-B."),
            new("#PSN", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
                new[] { MechanismParameterDataType.Data }, "PAN Sequence Number.", "Exactly two ASCII decimal digits in a normal string literal.",
                MechanismParameterDefaultKind.Literal, "\"00\"", acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral })
        }),
        new(CryptoScriptFunction.Derive, new MechanismParameterMetadata[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet }, "Option B parameters.", "Only MECH and optional PSN; no duplicates."),
            Argument("issuerMasterKey", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key }, "Issuer Master KEY.", "Secret TDEA KEY of exactly 16 bytes."),
            new("pan", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input, true,
                new[] { MechanismParameterDataType.Data }, "Complete PAN including check digit.", "1-19 ASCII decimal digits, no normalization.",
                acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Selects Option B.", "Exactly KDF-EMV-MASTER-B."),
            new("#PSN", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
                new[] { MechanismParameterDataType.Data }, "PAN Sequence Number.", "Exactly two ASCII decimal digits in a normal string literal.",
                MechanismParameterDefaultKind.Literal, "\"00\"", acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral })
        })
    };

    private static readonly MechanismFunctionMetadata[] EmvMasterCFunctionMetadata =
    {
        new(CryptoScriptFunction.Parameters, new MechanismParameterMetadata[]
        {
            Argument("mechanism", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Mechanism }, "Selects EMV ICC Master Key Option C.", "KDF-EMV-MASTER-C."),
            new("#PSN", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
                new[] { MechanismParameterDataType.Data }, "PAN Sequence Number.", "Exactly two ASCII decimal digits in a normal string literal.",
                MechanismParameterDefaultKind.Literal, "\"00\"", acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral })
        }),
        new(CryptoScriptFunction.Derive, new MechanismParameterMetadata[]
        {
            Argument("parameters", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.ParameterSet }, "Option C parameters.", "Only MECH and optional PSN; no duplicates."),
            Argument("issuerMasterKey", true, MechanismParameterDirection.Input,
                new[] { MechanismParameterDataType.Key }, "Issuer Master KEY.", "Secret AES KEY of exactly 16, 24 or 32 bytes; known size metadata must match."),
            new("pan", MechanismParameterKind.PositionalArgument, MechanismParameterDirection.Input, true,
                new[] { MechanismParameterDataType.Data }, "Complete PAN including check digit.", "1-19 ASCII decimal digits, no normalization; provisional CryptoScript contract.",
                acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral, MechanismParameterInputForm.VariableReference }),
            NamedParameter("#MECH", true, MechanismParameterDirection.Input, MechanismParameterDataType.Mechanism,
                "Selects Option C.", "Exactly KDF-EMV-MASTER-C."),
            new("#PSN", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
                new[] { MechanismParameterDataType.Data }, "PAN Sequence Number.", "Exactly two ASCII decimal digits in a normal string literal.",
                MechanismParameterDefaultKind.Literal, "\"00\"", acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral })
        })
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
                    ? "A hexadecimal PIN containing 4 to 12 decimal nibbles."
                    : "An encrypted PIN block with the length required by the selected ISO 9564 format."),
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
            new("AES-CBC", "Symmetric Advanced Encryption Standard in Cipher Block Chaining mode.", "Info.Mech.AES-CBC.md", CipherFunctions, AesCbcFunctionMetadata, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-CBC-MAC", "AES Cipher Block Chaining Message Authentication Code with selectable deterministic padding and left truncation.", "Info.Mech.AES-CBC-MAC.md", MacFunctions, AesCbcMacFunctionMetadata, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-CCM", "Symmetric Advanced Encryption Standard in Counter with CBC-MAC mode.", "Info.Mech.AES-CCM.md", CipherFunctions, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-CMAC", "Symmetric Advanced Encryption Standard in Cipher-based Message Authentication Code mode.", "Info.Mech.AES-CMAC.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-CTR", "Symmetric Advanced Encryption Standard in Counter mode.", "Info.Mech.AES-CTR.md", CipherFunctions, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-ECB", "Symmetric Advanced Encryption Standard in Electronic Codebook mode.", "Info.Mech.AES-ECB.md", CipherFunctions, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-GCM", "Symmetric Advanced Encryption Standard in Galois/Counter mode.", "Info.Mech.AES-GCM.md", CipherFunctions, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("AES-GMAC", "Symmetric Advanced Encryption Standard in Galois/Counter mode.", "Info.Mech.AES-GMAC.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Aes),
            new("DES3-CBC", "Symmetric Triple Data Encryption Standard in Cipher Block Chaining mode.", "Info.Mech.DES3-CBC.md", Des3CbcFunctions, generatedKeyAlgorithm: KeyAlgorithm.Tdea),
            new("DES3-CMAC", "Symmetric Triple Data Encryption Standard in Cipher-based Message Authentication Code mode.", "Info.Mech.DES3-CMAC.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Tdea),
            new("DES3-ECB", "Symmetric Triple Data Encryption Standard in Electronic Codebook mode.", "Info.Mech.DES3-ECB.md", CipherFunctions, generatedKeyAlgorithm: KeyAlgorithm.Tdea),
            new("DES3-RETAIL", "Symmetric Triple Data Encryption Standard in Retail mode.", "Info.Mech.DES3-RETAIL.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Tdea),
            new("DUKPT-AES-INITIAL-KEY", "ANSI X9.24-3-2017 derivation of an AES DUKPT Initial Key from an AES BDK and 64-bit IKID.", "Info.Mech.DUKPT-AES-INITIAL-KEY.md", DerivationFunctions),
            new("DUKPT-AES-WORKING-KEY", "ANSI X9.24-3-2017 stateless host derivation of a directly usable Working Key from an AES DUKPT Initial Key and complete 96-bit KSN.", "Info.Mech.DUKPT-AES-WORKING-KEY.md", DerivationFunctions),
            new("DUKPT-TDEA-INITIAL-KEY", "Legacy ANSI X9.24 double-length TDEA DUKPT Initial Key derivation from a 16-byte BDK and complete 80-bit KSN.", "Info.Mech.DUKPT-TDEA-INITIAL-KEY.md", DerivationFunctions),
            new("DUKPT-TDEA-WORKING-KEY", "ANSI X9.24-3 Annex C stateless host derivation of a double-length TDEA Working Key from a TDEA Initial Key and complete 80-bit KSN.", "Info.Mech.DUKPT-TDEA-WORKING-KEY.md", DerivationFunctions),
            new("EP2-DATA-ENCRYPTION", "EP2 AES-128 CTR encryption with a 12-byte nonce, field offset and 24-bit counter.", "Info.Mech.EP2-DATA-ENCRYPTION.md",
                new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Encrypt, CryptoScriptFunction.Decrypt }, Ep2EncryptionFunctionMetadata())
            { StrictMechanismParameterContract = true },
            new("EP2-MESSAGE-MAC", "EP2 message MAC: leftmost eight bytes of HMAC-SHA256 over SHA-256 of message bytes.", "Info.Mech.EP2-MESSAGE-MAC.md", new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }, Ep2MacFunctionMetadata("EP2-MESSAGE-MAC"))
            {
                StrictMechanismParameterContract = true
            },
            new("EP2-PAN-RECEIPT-ENCRYPTION", "EP2 PAN receipt AES-128 ECB encryption with mandatory ISO-7816 padding and an already derived transaction key.", "Info.Mech.EP2-PAN-RECEIPT-ENCRYPTION.md",
                new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Encrypt, CryptoScriptFunction.Decrypt }, Ep2PanReceiptFunctionMetadata())
            { StrictMechanismParameterContract = true },
            new("EP2-TR31-KEY-BLOCK", "EP2 TR-31 Version D authenticated transport of 128-bit H material with the fixed Table 11 header profile.", "Info.Mech.EP2-TR31-KEY-BLOCK.md",
                new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Wrap, CryptoScriptFunction.Unwrap }, Ep2Tr31FunctionMetadata())
            { StrictMechanismParameterContract = true },
            new("EP2-TRANSACTION-CERTIFICATE", "EP2 transaction certificate: leftmost eight bytes of HMAC-SHA256 over prepared DOL bytes.", "Info.Mech.EP2-TRANSACTION-CERTIFICATE.md", new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }, Ep2MacFunctionMetadata("EP2-TRANSACTION-CERTIFICATE"))
            {
                StrictMechanismParameterContract = true
            },
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
            new("HMAC-SHA1", "Keyed-Hash Message Authentication Code using SHA-1.", "Info.Mech.HMAC-SHA1.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA224", "Keyed-Hash Message Authentication Code using SHA-224.", "Info.Mech.HMAC-SHA224.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA256", "Keyed-Hash Message Authentication Code using SHA-256.", "Info.Mech.HMAC-SHA256.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA3-224", "Keyed-Hash Message Authentication Code using SHA3-224.", "Info.Mech.HMAC-SHA3-224.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA3-256", "Keyed-Hash Message Authentication Code using SHA3-256.", "Info.Mech.HMAC-SHA3-256.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA3-384", "Keyed-Hash Message Authentication Code using SHA3-384.", "Info.Mech.HMAC-SHA3-384.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA3-512", "Keyed-Hash Message Authentication Code using SHA3-512.", "Info.Mech.HMAC-SHA3-512.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA384", "Keyed-Hash Message Authentication Code using SHA-384.", "Info.Mech.HMAC-SHA384.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA512", "Keyed-Hash Message Authentication Code using SHA-512.", "Info.Mech.HMAC-SHA512.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA512-224", "Keyed-Hash Message Authentication Code using SHA-512/224.", "Info.Mech.HMAC-SHA512-224.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("HMAC-SHA512-256", "Keyed-Hash Message Authentication Code using SHA-512/256.", "Info.Mech.HMAC-SHA512-256.md", MacFunctions, generatedKeyAlgorithm: KeyAlgorithm.Hmac),
            new("KDF-EMV-AC-SESSION", "EMV Common Session Key Derivation for ATC-based Application Cryptogram and ARPC processing.", "Info.Mech.KDF-EMV-AC-SESSION.md", DerivationFunctions, EmvAcSessionFunctionMetadata)
            {
                StrictMechanismParameterContract = true
            },
            new("KDF-EMV-MASTER-A", "EMV ICC Master Key Derivation Option A using a TDEA Issuer Master Key, PAN and PSN.", "Info.Mech.KDF-EMV-MASTER-A.md", DerivationFunctions, EmvMasterFunctionMetadata)
            {
                StrictMechanismParameterContract = true
            },
            new("KDF-EMV-MASTER-B", "EMV ICC Master Key Derivation Option B using TDEA and SHA-1 decimalization for long PANs.", "Info.Mech.KDF-EMV-MASTER-B.md", DerivationFunctions, EmvMasterBFunctionMetadata)
            {
                StrictMechanismParameterContract = true
            },
            new("KDF-EMV-MASTER-C", "EMV ICC Master Key Derivation Option C using AES, the complete PAN and PSN.", "Info.Mech.KDF-EMV-MASTER-C.md", DerivationFunctions, EmvMasterCFunctionMetadata)
            {
                StrictMechanismParameterContract = true
            },
            new("KDF-EP2-PAN-RECEIPT-TRM", "ep2 8.13 Extract-and-Expand using SHA-256(Terminal Properties) as info and returning the leftmost 16 of 32 bytes.", "Info.Mech.KDF-EP2-PAN-RECEIPT-TRM.md", DerivationFunctions),
            new("KDF-EP2-PAN-RECEIPT-TRX", "ep2 8.12 direct Expand using SHA-256(DOL) as info and returning the leftmost 16 of 32 bytes.", "Info.Mech.KDF-EP2-PAN-RECEIPT-TRX.md", DerivationFunctions),
            new("KDF-EP2-PAN-SURROGATE-TRX", "ep2 8.14 direct Expand using raw DOL as info and returning all 32 bytes.", "Info.Mech.KDF-EP2-PAN-SURROGATE-TRX.md", DerivationFunctions),
            new("KDF-EP2-SESSION", "ep2 8.11 Extract-and-Expand derivation of a selected Session Key Variant.", "Info.Mech.KDF-EP2-SESSION.md", DerivationFunctions),
            new("KDF-HKDF", "HMAC-based Extract-and-Expand Key Derivation Function specified in RFC 5869.", "Info.Mech.KDF-HKDF.md", DerivationFunctions),
            new("KDF-SP800-108-COUNTER", "NIST SP 800-108 Rev. 1 Update 1 Counter Mode KDF using a supported HMAC PRF or AES-CMAC.", "Info.Mech.KDF-SP800-108-COUNTER.md", DerivationFunctions),
            new("MAC-EMV-AC", "EMV eight-byte Application Cryptogram using TDEA Algorithm 3 or AES-CMAC.", "Info.Mech.MAC-EMV-AC.md", new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }, EmvAcFunctionMetadata("MAC-EMV-AC", false))
            {
                StrictMechanismParameterContract = true
            },
            new("MAC-EMV-AC-VERIFY", "Fail-closed verification of an eight-byte EMV Application Cryptogram.", "Info.Mech.MAC-EMV-AC-VERIFY.md", new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }, EmvAcFunctionMetadata("MAC-EMV-AC-VERIFY", true))
            {
                StrictMechanismParameterContract = true
            },
            new("MAC-EMV-ARPC-METHOD-1", "EMV eight-byte ARPC Method 1 using an AC Session Key, ARQC and binary ARC.", "Info.Mech.MAC-EMV-ARPC-METHOD-1.md", new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }, EmvArpcFunctionMetadata("MAC-EMV-ARPC-METHOD-1", false))
            {
                StrictMechanismParameterContract = true
            },
            new("MAC-EMV-ARPC-METHOD-2", "EMV four-byte ARPC Method 2 with optional complete Issuer Authentication Data output.", "Info.Mech.MAC-EMV-ARPC-METHOD-2.md", new[] { CryptoScriptFunction.Parameters, CryptoScriptFunction.Mac }, EmvArpcFunctionMetadata("MAC-EMV-ARPC-METHOD-2", true))
            {
                StrictMechanismParameterContract = true
            },
            // BlockHeader is supported only through CryptoOperations' internal
            // BLOCKHEADER-WRAP-AES-TR31 dispatch and its populated one-argument overload.
            new("WRAP-AES-PINBLOCK-4", "ISO 9564 format 4 PIN-block wrapping and unwrapping with AES.", "Info.Mech.WRAP-AES-PINBLOCK-4.md", WrapFunctions, PinBlockFormat4FunctionMetadata),
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
