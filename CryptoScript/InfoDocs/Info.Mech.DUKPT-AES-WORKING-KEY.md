# MECHANISM DUKPT-AES-WORKING-KEY

DUKPT-AES-WORKING-KEY performs the host-side, stateless derivation of a directly usable Working Key from an AES DUKPT Initial Key and a complete Key Serial Number (KSN) according to ANSI X9.24-3-2017.

The mechanism processes the 32-bit Transaction Counter contained in the KSN and derives the required Intermediate Derivation Keys internally. It then derives the final Working Key for the requested usage and key type. Intermediate Derivation Keys are internal values and are not exposed as a separate CryptoScript mechanism.

---

## Key Features

- **Stateless Host Derivation**: Derives a Working Key from the Initial Key and complete KSN without maintaining terminal key registers or transaction state.
- **Explicit Usage**: `#USAGE` selects the cryptographic purpose encoded in the X9.24 derivation data and the restricted CryptoScript `KeyUsagePolicy` of the result.
- **Explicit Working-Key Type**: `#KEYTYPE` selects the algorithm family and output length of the Working Key.
- **AES DUKPT KDF**: Intermediate and final keys are derived using the AES-based X9.24-3 KDF, including when the requested output is TDEA or HMAC keying material.
- **Typed Result**: The returned `KEY` contains `KeyType`, `KeySizeInBits`, `DerivationMechanism` and restricted usage metadata.

## Functions

| Function | Arguments | Input | Output |
|----------|-----------|-------|--------|
| Parameters | mechanism, `#USAGE`, `#KEYTYPE` | `DUKPT-AES-WORKING-KEY` and required selectors | PARAM variable |
| Derive | PARAM, INITIAL_KEY, KSN | parameters, AES DUKPT Initial Key, complete 96-bit KSN | Directly usable Working Key |

`Derive(PARAM, INITIAL_KEY, KSN)` uses the following arguments:

| Argument | Meaning |
|----------|---------|
| PARAM | Parameter block containing `#MECH:DUKPT-AES-WORKING-KEY`, `#USAGE` and `#KEYTYPE` |
| INITIAL_KEY | AES DUKPT Initial Key with 128, 192 or 256 bits |
| KSN | Complete 12-byte / 96-bit Key Serial Number |
| Return value | Working Key selected by `#USAGE` and `#KEYTYPE` |

## Parameters

All three parameters are required:

- **#MECH**: `DUKPT-AES-WORKING-KEY`
- **#USAGE**: Purpose for which the Working Key is derived.
- **#KEYTYPE**: Algorithm family and length of the Working Key.

The parameter syntax is:

```text
PARAM working = Parameters(
    DUKPT-AES-WORKING-KEY,
    #USAGE:DATA-ENCRYPT,
    #KEYTYPE:AES-128)
```

## Initial Key and KSN

The Initial Key must be typed as an AES secret key and contain exactly 128, 192 or 256 bits. Its actual key length determines the AES algorithm indicator and length used while deriving Intermediate Derivation Keys.

The KSN must decode to exactly 12 bytes / 96 bits:

| KSN component | Size |
|---------------|------|
| Initial Key ID | 64 bits / 8 bytes |
| Transaction Counter | 32 bits / 4 bytes |
| Complete KSN | 96 bits / 12 bytes |

The complete counter is part of the derivation context. A shorter legacy TDEA-DUKPT KSN or a standalone 64-bit Initial Key ID is not accepted by this mechanism.

## Working-Key Usage

The supported `#USAGE` values and resulting CryptoScript metadata are:

| `#USAGE` | Meaning | Resulting `KeyUsagePolicy` |
|----------|---------|----------------------------|
| `PIN` | PIN encryption key | `Restricted(PinEncrypt)` |
| `MAC-GENERATE` | Generate message authentication codes | `Restricted(MacGenerate)` |
| `MAC-VERIFY` | Verify message authentication codes | `Restricted(MacVerify)` |
| `MAC-BOTH` | Generate and verify message authentication codes | `Restricted(MacGenerate \| MacVerify)` |
| `DATA-ENCRYPT` | Encrypt general data | `Restricted(Encrypt)` |
| `DATA-DECRYPT` | Decrypt general data | `Restricted(Decrypt)` |
| `DATA-BOTH` | Encrypt and decrypt general data | `Restricted(Encrypt \| Decrypt)` |

`PIN` is represented by the dedicated `PinEncrypt` usage. It does not grant the generic `Encrypt` usage and must not be interpreted as permission to encrypt arbitrary application data.

The policy is stored as typed metadata on the result. This page does not imply that every CryptoScript cryptographic operation currently enforces that metadata.

## Working-Key Types

The supported `#KEYTYPE` values are:

| `#KEYTYPE` | Resulting `KeyType` | Key size |
|------------|---------------------|----------|
| `TDEA-2` | TDEA / Secret | 128 bits |
| `TDEA-3` | TDEA / Secret | 192 bits |
| `AES-128` | AES / Secret | 128 bits |
| `AES-192` | AES / Secret | 192 bits |
| `AES-256` | AES / Secret | 256 bits |
| `HMAC-128` | HMAC / Secret | 128 bits |
| `HMAC-192` | HMAC / Secret | 192 bits |
| `HMAC-256` | HMAC / Secret | 256 bits |

`HMAC-128`, `HMAC-192` and `HMAC-256` specify the length of the generated HMAC keying material. They do not select SHA-1, SHA-256, SHA-512 or any other particular hash algorithm.

Selecting `TDEA-2`, `TDEA-3` or an HMAC type changes the final derivation-data algorithm indicator and result length. It does not switch to TDEA DUKPT or to an HMAC-based KDF: the complete derivation remains the AES DUKPT KDF.

## Strength Rules

A Working Key must not be stronger than its AES Initial Key. CryptoScript permits these combinations:

| Initial Key | Permitted Working-Key Types |
|-------------|-----------------------------|
| AES-128 | TDEA-2, TDEA-3, AES-128, HMAC-128 |
| AES-192 | TDEA-2, TDEA-3, AES-128, AES-192, HMAC-128, HMAC-192 |
| AES-256 | TDEA-2, TDEA-3, AES-128, AES-192, AES-256, HMAC-128, HMAC-192, HMAC-256 |

Requests outside this matrix are rejected rather than deriving a Working Key whose requested strength exceeds the Initial Key.

## Derivation Process

At a high level, the host derivation performs these steps:

1. Read the 32-bit Transaction Counter from the complete KSN.
2. Process its set bits from the most significant bit to the least significant bit.
3. Successively derive Intermediate Derivation Keys using the AES Initial Key type and the internal Key Derivation usage.
4. Derive the final Working Key from the last Derivation Key using the requested `#USAGE` and `#KEYTYPE`.

Only set counter bits cause an intermediate derivation. The working counter is accumulated as those bits are processed. Intermediate Derivation Keys never appear as CryptoScript variables and are not a public mechanism.

For a 192- or 256-bit result, the X9.24 derivation function produces the required number of AES blocks by incrementing the Key Block Counter, concatenates them and truncates the result to the requested length.

## Result Metadata

The returned `KEY` contains metadata matching the selected `#KEYTYPE`:

- `KeyType`: AES, TDEA or HMAC with `MaterialKind = Secret`.
- `KeySizeInBits`: 128, 192 or 256 as selected by `#KEYTYPE`.
- `DerivationMechanism`: `DUKPT-AES-WORKING-KEY`.
- `KeyUsagePolicy`: restricted to the usage or usage combination selected by `#USAGE`.

## Example Usage

```text
KEY initialKey = GenerateKey(AES-ECB, 0x(1273671EA26AC29AFA4D1084127652A1))
PARAM workingParameters = Parameters(
    DUKPT-AES-WORKING-KEY,
    #USAGE:DATA-ENCRYPT,
    #KEYTYPE:AES-128)
KEY wk = Derive(
    workingParameters,
    initialKey,
    0x(123456789012345600000001))
```

The result is a 128-bit AES secret key with `DerivationMechanism = DUKPT-AES-WORKING-KEY` and a restricted `Encrypt` usage.

## Invalid Inputs

The mechanism rejects:

- Initial Keys that are not typed as AES secret keys.
- Initial Keys other than 128, 192 or 256 bits.
- KSN values that do not decode to exactly 12 bytes.
- Missing or unsupported `#USAGE` values.
- Missing or unsupported `#KEYTYPE` values.
- Working-Key types that violate the strength matrix.
- Additional mechanism-specific parameters.

## Reference

ANSI X9.24-3-2017, *Retail Financial Services Symmetric Key Management, Part 3: Derived Unique Key Per Transaction*. See the host-side AES DUKPT key derivation and Annex B test vectors.

---
