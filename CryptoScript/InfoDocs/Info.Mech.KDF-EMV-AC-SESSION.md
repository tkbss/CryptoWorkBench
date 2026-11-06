# MECHANISM KDF-EMV-AC-SESSION

KDF-EMV-AC-SESSION derives an ATC-specific session key from an Application Cryptogram Master Key. The result is intended for host-side Application Cryptogram and ARPC processing; this mechanism derives the key only and does not calculate an Application Cryptogram or ARPC.

---

## Purpose

The mechanism implements the EMV Common Session Key Derivation construction for an exactly two-byte Application Transaction Counter (ATC). It supports double-length TDEA and AES master keys without exposing algorithm-selection parameters to the script.

## Conformance Status

The normative implementation review and formula comparison against EMV Contact Book 2 v4.4 were completed on 13 October 2026 for commit `881ec9219ca7f558603e98acb3696b7e5219201e`. The review covered A1.3.1 (Common Session Key Derivation, pages 137-138), A1.4.1 (Master Key Option A, pages 138-139), and A1.4.2 (Master Key Option B, page 139). This mechanism's TDEA and AES constructions match A1.3.1; no security- or interoperability-relevant normative deviation was found.

This is a normative implementation review, not EMV product certification or an official EMVCo approval. The fixed reference values are interoperability checks, not certification evidence; their reported Book E provenance was not separately authenticated by the Book 2 review.

The review does not establish complete Application Cryptogram or ARPC processing, including the eight-byte AES Application Cryptogram integration, or Secure Messaging. Linux/Unix and FIPS runtime verification remain outside the demonstrated scope; Windows test results are not cross-platform evidence. Transaction freshness, correct master-key assignment, and the fixed AES key length of a deployed application profile remain the caller's responsibility.

## Supported Master Keys

| Master-key algorithm | Master-key length | Session-key length |
|----------------------|-------------------|--------------------|
| TDEA | 16 bytes / 128 bits | 16 bytes / 128 bits |
| AES | 16 bytes / 128 bits | 16 bytes / 128 bits |
| AES | 24 bytes / 192 bits | 24 bytes / 192 bits |
| AES | 32 bytes / 256 bits | 32 bytes / 256 bits |

The second `Derive` argument must be a real `KEY` variable. Raw literals, `VAR` values and `PARAM` values are not accepted as master keys. TDEA keys of 24 bytes and all other algorithms are rejected.

## ATC Input

The third `Derive` argument is the ATC as exactly two binary bytes. It may be written directly as a hexadecimal literal such as `0x(0001)` or supplied through a referenced `VAR` whose stored value format is hexadecimal. Strings, integers, Base64 values, keys and parameter variables are not interpreted as an ATC. A nested function result is rejected even when the function returns exactly two hexadecimal bytes; assign such a result to a hexadecimal `VAR` first if it is intended to be used as the ATC.

The two bytes are used in their given order. There is no text, decimal or integer conversion.

## Parameter Contract

`Parameters(KDF-EMV-AC-SESSION)` stores only `#MECH:KDF-EMV-AC-SESSION`. The equivalent direct declaration is `PARAM p = #MECH:KDF-EMV-AC-SESSION`. No additional named parameters are supported, including `#HASH`, `#IV`, `#KEYTYPE`, `#OUTLEN`, `#SALT` or `#VARIANT`. Duplicate `#MECH` declarations are rejected even when their values are identical; a stored `#MECH` value that contradicts the selected mechanism is also rejected.

## Derivation Algorithms

For TDEA-128, the ATC is followed by `F0` and five zero bytes for the first 8-byte ECB input, and by `0F` and five zero bytes for the second. The two TDEA-ECB results are concatenated. No padding or IV is used, and the derived bytes are not changed to enforce odd DES parity.

For AES-128, the ATC is followed by fourteen zero bytes and the single AES-ECB result is returned.

For AES-192 and AES-256, two 16-byte inputs are formed from the ATC, respectively followed by `F0` or `0F` and thirteen zero bytes. The two AES-ECB results are concatenated conceptually and the leftmost 192 or 256 bits are returned. No padding or IV is used.

## Example Usage

KEY masterKey = GenerateKey(AES-ECB, 0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38))
PARAM sessionParameters = Parameters(KDF-EMV-AC-SESSION)
KEY sessionKey = Derive(sessionParameters, masterKey, 0x(0001))

The example returns `0x(89F7B697A028A93345BE7A409665B9A4)`.

## Result KEY Metadata

The result is a secret `KEY` with hexadecimal `Value`. Its algorithm and key size are preserved from the master key, `DerivationMechanism` is `KDF-EMV-AC-SESSION`, and `Usage` is `Unspecified`.

## Invalid Inputs

- A parameter set for another mechanism, any additional or duplicate named parameter, or the wrong argument count.
- A master-key argument that is not a `KEY` variable.
- AES key material other than 16, 24 or 32 bytes, or TDEA key material other than 16 bytes.
- Unsupported key algorithms or inconsistent key-size metadata.
- An ATC that is not exactly two bytes, is supplied in a non-hexadecimal input form, or is produced directly by a nested function call.
- Invalid TDEA key material lengths; weak or degenerate but technically valid test keys produce warnings and remain executable.

Errors describe the violated contract and do not include key material.

## Security and Scope

The implementation reuses the existing AES-ECB and DES3-ECB primitives, validates all inputs before the first cryptographic operation, and clears temporary key-material buffers. Caller-owned input buffers are not modified. The mechanism does not implement issuer-key derivation, Application Cryptogram calculation, ARPC calculation, transaction-data assembly, key storage, or HSM controls.

## Reference

- EMV Contact Chip v4.4, Book 2, Annex A1.3.1, Common Session Key Derivation.
- EMVCo Book E, Cryptography Worked Examples, reference vectors used for interoperability testing.

---

### Algorithm selection contract

KEY.KeyType.Algorithm is the binding algorithm selector: Aes selects AES and Tdea selects TDEA. This is the approved architecture decision, not an outstanding migration. An explicit declaration can select another cryptographic procedure, so this selector is not converted into a KeyTypeConflict warning. Unknown or unsupported algorithms fail; the key length is never used to guess the algorithm. No additional #ALGORITHM parameter is introduced.

GenerateKey(AES-ECB, ...) produces Secret(Aes), and GenerateKey(DES3-ECB, ...) produces Secret(Tdea), for both random keys and hexadecimal imports. The original mode does not restrict later compatible use. Derive, Unwrap, JSON deserialization and legacy imports are additional KEY sources and can still produce Unknown; those inputs remain invalid for EMV algorithm selection until their existing metadata identifies Aes or Tdea. KeyUsage conflicts remain non-blocking warnings. Cryptographic results, length/ATC/data checks and ARQC fail-closed verification are unchanged. This mechanism does not support GenerateKey itself.

### Semantic diagnostics

Expected KeyType metadata: Secret Aes or Tdea, still required to select the algorithm. Expected KeyUsage: `DeriveKey`.
Usage conflicts emit a non-blocking `KeyUsageConflict` warning; correct or Unspecified usage emits none. This mechanism retains KeyType as a binding algorithm selector, so Unknown remains a selection error and is not silently accepted or reduced to a `KeyTypeConflict` warning. These checks do not mutate KEY or PARAM and work without a diagnostic sink. This mechanism emits only the usage metadata warning. KeyAlgorithm selects AES or TDEA; Unknown and unsupported types still fail because no independent selector exists. No algorithm is inferred from key length. DES quality warnings are non-blocking for technically valid TDEA test keys; ATC, size and derivation rules remain binding.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.
### TDEA test and analysis keys

`DesParityViolation` (10) and `WeakDes3Key` (11) are non-blocking Warning diagnostics. Weak or semi-weak DES components and effective K1=K2 or K2=K3 are reported independently of parity; K1=K3 alone is not single-DES degeneration. Each quality category is reported at most once per operation on the original KEY, after any KeyTypeConflict and KeyUsageConflict metadata warnings. No KEY bytes or parity bits are changed. Technically valid calls also work without a diagnostic sink.

This support is for test and analysis and makes no suitability claim for production payment systems. Invalid key lengths, malformed data and protocol errors remain blocking. ARQC verification and TR-31 authentication remain mandatory; failed verification stops execution without a successful result. AES behavior and the approved algorithm selectors remain unchanged.
