# MECHANISM DUKPT-TDEA-WORKING-KEY

DUKPT-TDEA-WORKING-KEY performs the host-side, stateless derivation of a directly usable double-length TDEA Working Key from a TDEA DUKPT Initial Key and a complete Key Serial Number (KSN) according to ANSI X9.24-3:2017 Annex C.

The mechanism reconstructs the Current Key internally with the classic TDEA DUKPT Non-Reversible Key Generation Process (NRKGP), then applies the variant selected by `#USAGE`. The Current Key is an internal intermediate value and is not exposed as a CryptoScript result or separate mechanism.

---

## Key Features

- **Stateless Host Derivation**: Derives a Working Key from the Initial Key and complete KSN without maintaining terminal Future Key Registers or transaction state.
- **Double-Length TDEA**: The Initial Key and returned Working Key are each exactly 16 bytes / 128 bits.
- **Explicit Usage**: `#USAGE` selects the Annex C key variant and the restricted CryptoScript `KeyUsagePolicy` stored on the result.
- **Classic TDEA DUKPT**: The Current Key is reconstructed by processing the 21-bit Transaction Counter through the NRKGP.
- **Data-Key Isolation**: Data variants additionally pass through the Annex C One-Way Function (OWF).

## Functions

| Function | Arguments | Input | Output |
|----------|-----------|-------|--------|
| Parameters | mechanism, `#USAGE` | `DUKPT-TDEA-WORKING-KEY` and required usage | PARAM variable |
| Derive | PARAM, INITIAL_KEY, KSN | parameters, double-length TDEA Initial Key, complete 80-bit KSN | 16-byte double-length TDEA Working Key |

`Derive(PARAM, INITIAL_KEY, KSN)` uses:

| Argument | Meaning |
|----------|---------|
| PARAM | Parameter block containing `#MECH:DUKPT-TDEA-WORKING-KEY` and `#USAGE` |
| INITIAL_KEY | Double-length / 2-key TDEA DUKPT Initial Key, exactly 16 bytes |
| KSN | Complete 10-byte / 80-bit TDEA DUKPT Key Serial Number |
| Return value | Working Key selected by `#USAGE`, always 16-byte double-length TDEA |

## Parameters

The required parameters are:

- **#MECH**: `DUKPT-TDEA-WORKING-KEY`
- **#USAGE**: Purpose and DUKPT derivation direction of the Working Key.

The mechanism does not accept `#KEYTYPE`, `#OUTLEN` or `#COUNTER`. The algorithm and output length are fixed by the TDEA DUKPT procedure, and the Transaction Counter is read from the complete KSN.

```text
PARAM working = Parameters(DUKPT-TDEA-WORKING-KEY, #USAGE:MAC-REQUEST)
```

## Initial Key, KSN and Transaction Counter

The Initial Key must contain exactly 16 bytes / 128 bits of double-length, 2-key TDEA key material.

The KSN must decode to exactly 10 bytes / 80 bits. Its 21 least-significant bits are the Transaction Counter. CryptoScript clears those bits in the internal KSN register and processes the set counter bits from the most significant bit to the least significant bit. Each step applies the NRKGP to the preceding derivation key and the progressively constructed KSN register.

The resulting Current Key exists only during the derivation. It is not returned, stored as a CryptoScript variable or exposed as another CryptoScript mechanism.

Counter `0` is invalid for this Working-Key mechanism. As a stateless host reconstruction function, the mechanism accepts any nonzero value in the 21-bit counter field, including values with more than ten set bits. Annex C's rule that avoids counter values with more than ten set bits describes terminal-side counter advancement; it is not an additional input restriction on this host function.

## Working-Key Usage and Variants

The supported `#USAGE` values and resulting metadata are:

| `#USAGE` | DUKPT variant | Resulting `KeyUsagePolicy` |
|----------|---------------|----------------------------|
| `PIN` | PIN encryption | `Restricted(PinEncrypt)` |
| `MAC-REQUEST` | Message authentication, request | `Restricted(MacGenerate \| MacVerify)` |
| `MAC-RESPONSE` | Message authentication, response | `Restricted(MacGenerate \| MacVerify)` |
| `MAC-BOTH` | Same DUKPT variant as `MAC-REQUEST` | `Restricted(MacGenerate \| MacVerify)` |
| `DATA-REQUEST` | Data encryption, request | `Restricted(Encrypt \| Decrypt)` |
| `DATA-RESPONSE` | Data encryption, response | `Restricted(Encrypt \| Decrypt)` |
| `DATA-BOTH` | Same DUKPT variant as `DATA-REQUEST` | `Restricted(Encrypt \| Decrypt)` |

Request and response identify the TDEA DUKPT derivation direction and select different Annex C variants. They do not mean that a request key only generates a MAC or encrypts data while a response key only verifies a MAC or decrypts data. Consequently, all MAC variants carry both `MacGenerate` and `MacVerify`, and all data variants carry both `Encrypt` and `Decrypt`.

`MAC-BOTH` uses exactly the `MAC-REQUEST` DUKPT variant. `DATA-BOTH` uses exactly the `DATA-REQUEST` DUKPT variant.

The policy is stored as typed metadata on the result. CryptoScript does not currently enforce this `KeyUsagePolicy` generally across all cryptographic operations.

## PIN, MAC and Data-Key Derivation

PIN and MAC Working Keys are produced by XORing the Current Key with the corresponding Annex C variant constant.

Data Working Keys use an additional isolation step:

1. XOR the Current Key with the request/both or response data variant.
2. Do not adjust DES parity on that variant value.
3. Apply the Annex C One-Way Function by encrypting both 8-byte halves with the complete double-length TDEA variant key.
4. Concatenate the two encrypted halves to form the 16-byte Data Working Key.

Data derivation is therefore not equivalent to merely XORing the Current Key with a variant constant.

## Result Metadata

The returned `KEY` contains:

| Metadata | Value |
|----------|-------|
| Algorithm | `Tdea` |
| Material | `Secret` |
| Key Size | `128` bits |
| Derivation | `DUKPT-TDEA-WORKING-KEY` |
| Usage | Restricted policy selected by `#USAGE` |

No Key Check Value (KCV) is calculated or stored by this mechanism.

## Example Usage

```text
KEY initialKey = GenerateKey(DES3-ECB, 0x(6AC292FAA1315B4D858AB3A3D7D5933A))
PARAM workingParameters = Parameters(DUKPT-TDEA-WORKING-KEY, #USAGE:DATA-REQUEST)
KEY wk = Derive(workingParameters, initialKey, 0x(FFFF9876543210E00001))
```

The result is the Annex C Data Request Working Key `0x(448D3F076D8304036A55A3D7E0055A78)` with TDEA secret-key metadata and restricted `Encrypt | Decrypt` usage.

## Invalid Inputs

The mechanism rejects:

- Initial Keys that are neither TDEA keys nor untyped legacy keys compatible with the existing transition policy.
- Initial Keys shorter or longer than exactly 16 bytes.
- KSN values that do not decode to exactly 10 bytes.
- A Transaction Counter of zero.
- Missing or unsupported `#USAGE` values.
- `#KEYTYPE`, `#OUTLEN`, `#COUNTER` and all other additional parameters.

The mechanism does not implement terminal Future Key Registers, terminal state management, general usage enforcement or KCV calculation.

## Reference

ANSI X9.24-3:2017, *Retail Financial Services Symmetric Key Management, Part 3: Derived Unique Key Per Transaction*, Annex C.

---
