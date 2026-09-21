# MECHANISM DUKPT-AES-INITIAL-KEY

DUKPT-AES-INITIAL-KEY derives the Initial Key of an AES DUKPT device from a Base Derivation Key (BDK) and an Initial Key ID (IKID) according to ANSI X9.24-3-2017.

The BDK is the parent secret derivation key held on the host or key-management side. Combining it with an IKID produces an Initial Key for that Initial-Key context. The IKID identifies the context and is not itself a secret key. The resulting Initial Key is the cryptographic starting point for subsequent AES DUKPT key derivations.

This CryptoScript mechanism performs only BDK to Initial Key derivation. It does not derive Transaction Keys or Working Keys and does not manage DUKPT transaction state.

---

## Key Features

- **X9.24 Initial-Key Derivation**: Uses the fixed 16-byte derivation-data structure defined for an AES DUKPT Initial Key.
- **BDK-Selected Key Type**: The BDK length fixes the AES type and output size. There is no separate output-length or key-type selection.
- **AES-ECB Primitive**: Each derivation block is encrypted under the BDK with AES-ECB, without an IV and without padding.
- **Return Value**: `Derive` returns a generic `KEY` in hexadecimal `0x(...)` form. `Mechanism` is empty and `DerivationMechanism` is `DUKPT-AES-INITIAL-KEY`.

## DUKPT Context

```text
                 Host / Key Management

                       BDK
                        +
                       IKID
                        |
                        v
             DUKPT-AES-INITIAL-KEY
                        |
                        v
                   Initial Key
                        |
            -------------------------
                        |
            subsequent AES DUKPT
               key derivations
```

- **BDK**: Long-term parent AES key from which Initial Keys are derived for DUKPT devices or Initial-Key contexts.
- **IKID**: 64-bit identifier which, together with the BDK, determines the Initial Key.
- **Initial Key**: Secret AES key derived from the BDK and IKID, and the starting point of the subsequent DUKPT key hierarchy.

The BDK and Initial Key are not the same key. Different IKIDs allow different Initial Keys to be derived from one BDK without using the BDK itself as the Initial Key.

## Functions

| Function | Arguments | Input | Output | Key Length |
|----------|-----------|-------|--------|------------|
| Parameters | mechanism | `DUKPT-AES-INITIAL-KEY` | PARAM variable | N/A |
| Derive | PARAM, KEY, DATA | parameters, AES BDK, 64-bit IKID | Generic KEY variable | Same as BDK |

`Derive(PARAM, KEY, DATA)` uses the following arguments:

| Argument | Meaning |
|----------|---------|
| PARAM | Parameter block containing `#MECH:DUKPT-AES-INITIAL-KEY` |
| KEY | AES Base Derivation Key (BDK) |
| DATA | 64-bit Initial Key ID (IKID) |
| Return value | Derived AES Initial Key as a `KEY` |

## Parameters

The only required and permitted parameter is:

- **#MECH**: `DUKPT-AES-INITIAL-KEY`

The mechanism has no additional mechanism-specific parameters. In particular, `#OUTLEN`, `#PRF`, `#LABEL` and `#COUNTER` are not used and are rejected. Output length and AES key type are determined exclusively by the BDK.

The actual CryptoScript parameter syntax is:

```text
PARAM dukpt = Parameters(DUKPT-AES-INITIAL-KEY)
```

## BDK Requirements

The second `Derive` argument is an existing `KEY` variable containing the Base Derivation Key. Its stored key value must use CryptoScript's hexadecimal representation.

| BDK | Length | Derived Initial Key |
|-----|--------|---------------------|
| AES-128 | 16 bytes / 128 bits | AES-128 / 128 bits |
| AES-192 | 24 bytes / 192 bits | AES-192 / 192 bits |
| AES-256 | 32 bytes / 256 bits | AES-256 / 256 bits |

The Initial Key always has the same AES type and size as the BDK. An AES-256 BDK therefore cannot produce a selectable AES-128 or AES-192 Initial Key through this mechanism. BDK lengths other than 16, 24 or 32 bytes are rejected.

## IKID Requirements and Formats

The third `Derive` argument is the Initial Key ID. ANSI X9.24-3-2017 defines it as:

```text
IKID = BDK ID || Derivation ID
```

| Component | Size |
|-----------|------|
| BDK ID | 32 bits / 4 bytes |
| Derivation ID | 32 bits / 4 bytes |
| IKID | 64 bits / 8 bytes |

CryptoScript checks the decoded value, which must contain exactly eight bytes:

- **Hex**: `0x(1234567890123456)` decodes to `12 34 56 78 90 12 34 56`, exactly eight binary bytes. Sixteen hexadecimal digits represent eight bytes, not sixteen ASCII characters.
- **Base64**: `b64(EjRWeJASNFY=)` decodes to the same eight bytes. The decoded length, not the Base64 text length, is decisive.
- **String**: `"12345678"` produces eight UTF-8 bytes. Eight characters do not necessarily mean eight bytes when the string contains multibyte UTF-8 characters.

Examples of invalid IKIDs include `0x(12345678901234)` (7 bytes), `0x(123456789012345678)` (9 bytes), and `""` (0 bytes).

## Derivation Data and Algorithm

CryptoScript constructs the 16-byte X9.24 Initial-Key derivation data as:

```text
01 || Counter || 8001 || Algorithm || KeyLength || IKID
```

| Field | Size | Meaning |
|-------|------|---------|
| Version | 1 byte | `01` |
| Key Block Counter | 1 byte | Starts at `01` |
| Key Usage | 2 bytes | `8001`, Initial-Key derivation |
| Algorithm | 2 bytes | Desired AES key type |
| Key Length | 2 bytes | Key length in bits |
| IKID | 8 bytes | Initial Key ID |

The fixed variants are:

| BDK and Initial Key | Derivation-data prefix |
|---------------------|------------------------|
| AES-128 | `01 || Counter || 8001 || 0002 || 0080 || IKID` |
| AES-192 | `01 || Counter || 8001 || 0003 || 00C0 || IKID` |
| AES-256 | `01 || Counter || 8001 || 0004 || 0100 || IKID` |

For an Initial Key of `L` bits, CryptoScript calculates `n = ceil(L / 128)`. Each derivation block is `AES-ECB(BDK, DerivationData(i))`, with the Key Block Counter set to `i`:

- AES-128 uses one block with Counter `01`.
- AES-192 uses two blocks with Counters `01` and `02`; the concatenated result is truncated to the leftmost 24 bytes.
- AES-256 uses two blocks with Counters `01` and `02`; all 32 bytes are used.

No IV and no padding are used.

## Official Annex B Example

ANSI X9.24-3-2017 Annex B provides this AES-128 example:

| Value | Hexadecimal data |
|-------|------------------|
| BDK | `FEDCBA9876543210F1F1F1F1F1F1F1F1` |
| BDK ID | `12345678` |
| Derivation ID | `90123456` |
| IKID | `1234567890123456` |
| Derivation Data | `01018001000200801234567890123456` |
| Initial Key | `1273671EA26AC29AFA4D1084127652A1` |

The derivation data separates into:

```text
01 | 01 | 8001 | 0002 | 0080 | 1234567890123456
```

This represents Version 1, Key Block Counter 1, Initial-Key derivation usage, AES-128, a 128-bit output, and the complete IKID.

## Example Usage

```text
KEY bdk = GenerateKey(AES-ECB, 0x(FEDCBA9876543210F1F1F1F1F1F1F1F1))
PARAM dukpt = Parameters(DUKPT-AES-INITIAL-KEY)
KEY initialKey = Derive(dukpt, bdk, 0x(1234567890123456))
```

The resulting `initialKey` is:

```text
0x(1273671EA26AC29AFA4D1084127652A1)
```

## Invalid Inputs

The mechanism rejects:

- BDKs shorter than 16 bytes.
- BDKs between the supported AES lengths, such as 20 bytes.
- BDKs longer than 32 bytes.
- IKIDs shorter or longer than eight decoded bytes.
- An empty IKID.
- Additional parameters, including `#OUTLEN`, `#PRF`, `#LABEL` and `#COUNTER`.

## Separation from KDF-SP800-108-COUNTER

Although the X9.24 derivation is structurally based on a counter-oriented KDF concept, `DUKPT-AES-INITIAL-KEY` uses the fixed 16-byte derivation data and AES-ECB specified by X9.24. `KDF-SP800-108-COUNTER` instead exposes CryptoScript's general SP 800-108 Counter Mode interface. The mechanisms are therefore intentionally separate.

## Reference

ANSI X9.24-3-2017, *Retail Financial Services Symmetric Key Management, Part 3: Derived Unique Key Per Transaction*. See the AES DUKPT key-derivation function, the Initial-Key derivation-data definition, and Annex B.

---
