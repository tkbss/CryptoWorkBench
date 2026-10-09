# MECHANISM KDF-EMV-AC-SESSION

KDF-EMV-AC-SESSION derives an ATC-specific session key from an Application Cryptogram Master Key. The result is intended for host-side Application Cryptogram and ARPC processing; this mechanism derives the key only and does not calculate an Application Cryptogram or ARPC.

---

## Purpose

The mechanism implements the EMV Common Session Key Derivation construction for an exactly two-byte Application Transaction Counter (ATC). It supports double-length TDEA and AES master keys without exposing algorithm-selection parameters to the script.

## Conformance Status

The construction follows EMV Contact Chip v4.4, Book 2, Annex A1.3.1 as specified for this implementation. The fixed known-answer values below originate from EMVCo Book E Cryptography Worked Examples. Their applicability to the Contact Book 2 construction has been technically cross-checked, but final normative confirmation against the official v4.4 publication remains open.

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

The result is a secret `KEY` with hexadecimal `Value` and `KeyValue`. Its algorithm and key size are preserved from the master key, `DerivationMechanism` is `KDF-EMV-AC-SESSION`, and `Usage` is `Unspecified`.

## Invalid Inputs

- A parameter set for another mechanism, any additional or duplicate named parameter, or the wrong argument count.
- A master-key argument that is not a `KEY` variable.
- AES key material other than 16, 24 or 32 bytes, or TDEA key material other than 16 bytes.
- Unsupported key algorithms or inconsistent key-size metadata.
- An ATC that is not exactly two bytes, is supplied in a non-hexadecimal input form, or is produced directly by a nested function call.
- TDEA keys rejected by the existing DES3 primitive validation, including unusable weak key material.

Errors describe the violated contract and do not include key material.

## Security and Scope

The implementation reuses the existing AES-ECB and DES3-ECB primitives, validates all inputs before the first cryptographic operation, and clears temporary key-material buffers. Caller-owned input buffers are not modified. The mechanism does not implement issuer-key derivation, Application Cryptogram calculation, ARPC calculation, transaction-data assembly, key storage, or HSM controls.

## Reference

- EMV Contact Chip v4.4, Book 2, Annex A1.3.1, Common Session Key Derivation.
- EMVCo Book E, Cryptography Worked Examples, reference vectors used for interoperability testing.

---
