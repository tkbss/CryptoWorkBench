# MECHANISM KDF-SP800-108-COUNTER

KDF-SP800-108-COUNTER derives keying material with the Counter Mode KDF specified in NIST SP 800-108 Rev. 1 Update 1. CryptoScript uses a fixed input layout consisting of a counter, an optional label, a separator byte, the context supplied as `DATA`, and the requested output length.

This mechanism is a general SP 800-108 Counter Mode KDF. It is not an AES DUKPT derivation.

---

## Key Features

- **Selectable PRF**: Uses one of the supported HMAC mechanisms or AES-CMAC.
- **Bit-Based Output Length**: `#OUTLEN` is specified as an unsigned 32-bit number of bits and must be a positive multiple of eight. The largest byte-aligned value representable by this field is 4,294,967,288 bits, subject to the lower PRF/counter limit described below.
- **Selectable Counter Width**: `#COUNTER` accepts 8, 16, 24 or 32 bits and defaults to 32 bits.
- **Binary Label and Context**: Label and context are processed as decoded binary data, without implicit hexadecimal-text conversion.
- **Return Value**: `Derive` returns a generic `KEY` in hexadecimal `0x(...)` form. `Mechanism` is empty and `DerivationMechanism` is `KDF-SP800-108-COUNTER`.

## Functions

| Function | Arguments | Input | Output |
|----------|-----------|-------|--------|
| Parameters | mechanism and KDF parameters | `KDF-SP800-108-COUNTER` | PARAM variable |
| Derive | PARAM, KEY, DATA | parameters, KIN, Context | Generic KEY variable |

`Derive(PARAM, KEY, DATA)` uses the following arguments:

| Argument | Meaning |
|----------|---------|
| PARAM | Parameter block selecting the PRF, output length, counter width and optional Label |
| KEY | Input key `KIN` for the selected PRF |
| DATA | Binary `Context`; an empty value is permitted |
| Return value | The leftmost `L` bits of the concatenated PRF blocks, as a `KEY` |

## Parameters

| Parameter | Required | Meaning |
|-----------|----------|---------|
| `#MECH` | Yes | Must be `KDF-SP800-108-COUNTER` |
| `#PRF` | Yes | Selects one supported HMAC PRF or `AES-CMAC` |
| `#OUTLEN` | Yes | Requested output length `L` in bits; 1 through 4,294,967,288, divisible by 8, and subject to the PRF/counter block limit |
| `#COUNTER` | No | Counter length `r` in bits: 8, 16, 24 or 32; default 32 |
| `#LABEL` | No | Optional binary Label; omission means an empty Label |

Example parameter block:

```text
PARAM kdf = Parameters(KDF-SP800-108-COUNTER, #PRF:HMAC-SHA256, #OUTLEN:256, #COUNTER:32, #LABEL:"label")
```

## Supported PRFs

The selected PRF determines the size `h` of each generated block:

| `#PRF` | `h` |
|--------|-----|
| `HMAC-SHA1` | 160 bits |
| `HMAC-SHA224` | 224 bits |
| `HMAC-SHA256` | 256 bits |
| `HMAC-SHA384` | 384 bits |
| `HMAC-SHA512` | 512 bits |
| `HMAC-SHA512-224` | 224 bits |
| `HMAC-SHA512-256` | 256 bits |
| `HMAC-SHA3-224` | 224 bits |
| `HMAC-SHA3-256` | 256 bits |
| `HMAC-SHA3-384` | 384 bits |
| `HMAC-SHA3-512` | 512 bits |
| `AES-CMAC` | 128 bits |

No other PRF name is accepted. In particular, `DES3-CMAC` and KMAC mechanisms are not supported by this CryptoScript mechanism.

## KIN Requirements

For HMAC PRFs, `KIN` must contain at least one byte. CryptoScript imposes no additional mechanism-specific HMAC key-length restriction.

For `AES-CMAC`, `KIN` must be a valid AES key:

| AES key type | KIN length |
|--------------|------------|
| AES-128 | 16 bytes / 128 bits |
| AES-192 | 24 bytes / 192 bits |
| AES-256 | 32 bytes / 256 bits |

Any other `AES-CMAC` KIN length is rejected.

## Binary Label and Context

`#LABEL` and `DATA` are decoded before derivation. Supported CryptoScript representations include:

| Representation | Example | Resulting bytes |
|----------------|---------|-----------------|
| Hex | `0x(010203)` | `01 02 03` |
| Base64 | `b64(AQID)` | `01 02 03` |
| String | `"ctx"` | UTF-8 bytes `63 74 78` |

Omitting `#LABEL` supplies a zero-length Label. An explicitly empty string, `#LABEL:""`, also supplies a zero-length Label. An empty Context can be supplied as `""`. The separator byte is present even when Label or Context is empty, so these cases remain unambiguous.

There is no separate mechanism-specific maximum for Label or Context. Available memory and the general CryptoScript runtime remain practical limits.

## PRF Input Encoding

For every block, CryptoScript constructs exactly:

```text
[i]r || Label || 00 || Context || [L]32
```

- `[i]r` is the positive counter `i`, encoded unsigned big-endian in exactly `r` bits.
- `Label` is the decoded binary value of `#LABEL`.
- `00` is one separator byte.
- `Context` is the decoded binary value supplied as `DATA`.
- `[L]32` is `#OUTLEN` in bits, encoded as an unsigned 32-bit big-endian integer.

The counter starts at `i = 1`. Neither Label nor Context is converted back into hexadecimal or textual characters when the PRF input is assembled.

## Derivation Algorithm

Let `L` be `#OUTLEN`, `h` the PRF output size and `r` the selected counter size:

```text
n = ceil(L / h)

K(i) = PRF(KIN, [i]r || Label || 00 || Context || [L]32)

KOUT = leftmost L bits of K(1) || K(2) || ... || K(n)
```

Because CryptoScript permits only byte-aligned output lengths, final truncation occurs on a byte boundary. Derivation is rejected when `n > 2^r - 1`.

CryptoScript parses `L` as an unsigned 32-bit integer. The representable range is therefore 0 through 4,294,967,295 bits; after applying the requirements that `L > 0` and `L` be divisible by eight, the largest value allowed by the representation alone is 4,294,967,288 bits. This is not necessarily usable with every PRF and counter width. The effective maximum is the lower of:

1. 4,294,967,288 bits, imposed by the unsigned 32-bit representation and byte alignment; and
2. the largest byte-aligned `L` for which `ceil(L / h) <= 2^r - 1` for the selected PRF and counter width.

For example, HMAC-SHA256 has `h = 256`. With `r = 8`, at most 255 blocks can be represented:

| `#OUTLEN` | Blocks | Result |
|-----------|--------|--------|
| 65280 bits | 255 | Allowed |
| 65536 bits | 256 | Rejected |

## HMAC-SHA256 Example

```text
KEY kin = GenerateKey(HMAC-SHA256, 0x(3EDC6B5B8F7AADBD713732B482B8F979286E1EA3B8F8F99C30C884CFE3349B83))
PARAM kdf = Parameters(KDF-SP800-108-COUNTER, #PRF:HMAC-SHA256, #OUTLEN:256, #COUNTER:32, #LABEL:"label")
KEY derivedKey = Derive(kdf, kin, "context")
```

The first and only PRF input is:

```text
00000001 6C6162656C 00 636F6E74657874 00000100
```

The resulting `derivedKey` is:

```text
0x(508BE685D92997294C12712641077442382A77FD41A6F3D0A10CBB805EAEA7A0)
```

This value was independently calculated with HMAC-SHA256. It is an executable CryptoScript example, not an official NIST CAVP or ACVP vector.

## AES-CMAC Example

```text
KEY cmacKin = GenerateKey(AES-CMAC, 0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM cmacKdf = Parameters(KDF-SP800-108-COUNTER, #PRF:AES-CMAC, #OUTLEN:256, #COUNTER:32, #LABEL:"aes-label")
KEY cmacDerivedKey = Derive(cmacKdf, cmacKin, 0x(0102030405))
```

The two PRF inputs differ only in their counters:

```text
K(1): 00000001 6165732D6C6162656C 00 0102030405 00000100
K(2): 00000002 6165732D6C6162656C 00 0102030405 00000100
```

The resulting `cmacDerivedKey` is:

```text
0x(64EC377C7A14A3D931E7BFD71840A493BC6012B38EC96C68A048053D0D35705B)
```

This value was independently calculated with AES-CMAC. It is an executable CryptoScript example, not an official NIST CAVP or ACVP vector.

## Result Metadata

The returned value follows the existing CryptoScript KDF convention:

- `Value` and `KeyValue` contain identical hexadecimal `0x(...)` output.
- `KeySize` is the requested `#OUTLEN` in bits.
- `Mechanism` is empty because the result is generic derived keying material.
- `DerivationMechanism` is `KDF-SP800-108-COUNTER`.

## Invalid Inputs

Invalid input can be rejected at different layers:

### Grammar and Parser

The CryptoScript grammar represents an unquoted numeric parameter value with decimal digits only. A negative numeric literal such as `#OUTLEN:-8` is therefore not a valid parameter value and is rejected before KDF parameter evaluation. Malformed CryptoScript expressions and unsupported binary-value syntax are likewise syntax errors.

### Parameter Evaluation

Parameter construction rejects:

- A missing or unsupported `#PRF`.
- A missing `#OUTLEN`, zero, a nonnumeric value that reaches parameter evaluation, a value greater than 4,294,967,295, or a value not divisible by eight. Consequently, the largest accepted byte-aligned value is 4,294,967,288 bits before applying the block-count limit.
- A `#COUNTER` other than 8, 16, 24 or 32.
- An output requiring more than `2^r - 1` PRF blocks.
- Parameters other than `#PRF`, `#OUTLEN`, `#COUNTER` and `#LABEL` in addition to `#MECH`.
- A `#LABEL` whose value cannot be decoded as one of the supported binary formats.

### Derivation and Cryptographic Validation

Derivation rejects:

- An empty HMAC KIN.
- An `AES-CMAC` KIN whose decoded length is not 16, 24 or 32 bytes.
- A KIN or Context whose value cannot be decoded as one of the supported binary formats.
- A parameter object whose mechanism or stored KDF parameter values do not satisfy this mechanism's contract.

## Scope and CMAC Note

This implementation provides only Counter Mode with CryptoScript's fixed input layout shown above. Feedback Mode, Double-Pipeline Iteration Mode, KMAC and a general configurable `FixedInputData` layout are outside its scope.

NIST SP 800-108 Rev. 1 describes additional key-control-security considerations and an optional `K(0)` construction for CMAC-based KDFs. This CryptoScript mechanism implements the ordinary Counter Mode expression only; it does not append or otherwise apply the CMAC `K(0)` mitigation. No alternative API or mitigation design is selected here.

## Separation from AES DUKPT

`DUKPT-AES-INITIAL-KEY` is a distinct ANSI X9.24 mechanism. It constructs X9.24 derivation data and encrypts it with AES-ECB. `KDF-SP800-108-COUNTER` instead applies a selected HMAC or AES-CMAC PRF to the SP 800-108 input defined above. The two mechanisms are not interchangeable.

## Reference

NIST Special Publication 800-108 Revision 1 Update 1, *Recommendation for Key Derivation Using Pseudorandom Functions*, August 2022, updated February 2, 2024. See Section 3 for approved PRFs, Section 4.1 for Counter Mode, and the CMAC key-control-security discussion for the optional `K(0)` construction.

---
