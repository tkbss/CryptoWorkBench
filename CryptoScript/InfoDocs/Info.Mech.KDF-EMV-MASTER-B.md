# MECHANISM KDF-EMV-MASTER-B

## Purpose and Conformance Status

Host-side EMV ICC Master Key Derivation Option B for personalization and issuer processing.
Derives a card-specific ICC Master Key from an Issuer Master Key (IMK), the complete PAN
including its check digit, and the PAN Sequence Number (PSN). It does not calculate a cryptogram.

Official normative verification against EMV Contact Book 2 v4.4, Annex A1.4.2 is pending
and remains a release gate. The algorithm is corroborated by EMV-4.3-related implementations
and published comparison values. The PAN limit of 1-19 digits is a provisional technical
contract, not a normative conformity assertion.

## Input and Parameter Contract

Use exactly three Derive arguments: PARAM, a genuine Secret TDEA KEY reference of exactly
16 bytes, and PAN. KEY literals, non-KEY references and value-based structured KEY lookup
are not allowed. Existing DES3 weak/degenerate-key checks apply unchanged; no extra usage
or weak-key policy is added. The input key and its parity are not changed.

PAN must be a direct normal string literal or a referenced normal string VAR containing
1-19 ASCII decimal digits. Leading zeros and the check digit are retained. HEX/Base64,
integer, KEY/PARAM and nested-function results are rejected; there is no implicit conversion,
trimming, normalization, Luhn validation or check-digit removal.

Only MECH and optional PSN parameters are permitted. MECH must consistently select
KDF-EMV-MASTER-B. PSN must be a direct normal string literal with exactly two ASCII digits,
for example `#PSN:"45"`; omission defaults to `"00"`. Integer, variable, HEX/Base64 and malformed
PSN values are rejected. Duplicate PSN or MECH declarations are rejected even if identical.
These rules apply to Parameters(...), direct PARAM declarations and serialized legacy PARAMs.
Malformed serialized segments are rejected before lossy legacy parsing; malformed PSN never
selects the default. PSN remains forbidden for mechanisms not explicitly supporting it.

## Derivation Algorithm

The branch depends on PAN.Length alone, not PAN.Length + PSN.Length.

- PAN with 1-16 digits: exactly Option-A preparation. Take the rightmost 16 digits of
  PAN || PSN, left-pad with zero digits if shorter, then encode eight Packed-Decimal bytes as F1.
- PAN with 17-19 digits: concatenate the full PAN and PSN. For odd PAN length prepend exactly
  one zero digit. Encode Packed Decimal before binary SHA-1; do not hash ASCII/UTF-8 text.
  Scan the resulting 40 nibbles high-nibble first and collect the first 16 nibbles 0-9.
  If fewer than 16 occur, scan again for A-F, convert A-F to 0-5, and append until 16 digits
  exist. Pack these digits into eight bytes as F1. There is no second hash or modulo-10 pass.

For both branches, F2 is the complete bitwise complement of F1. Encrypt F1 and F2 using
TDEA-ECB(IMK), without IV or padding. Concatenate the two ciphertexts in that order and
set DES Odd Parity in-place, changing only bit 0 of each byte. The output is exactly 16 bytes.

Option A always uses its rightmost-digit procedure even for long PANs. Option B selects its
own short/long branch; it is never an automatic fallback from Option A. A short-PAN Option-B
result still reports DerivationMechanism KDF-EMV-MASTER-B.

## Executable Example

```text
KEY IMK = GenerateKey(DES3-ECB, 0x(0123456789ABCDEFFEDCBA9876543210))
PARAM p = Parameters(KDF-EMV-MASTER-B, #PSN:"45")
KEY ICC = Derive(p, IMK, "99012345678901234")
PARAM sessionParameters = Parameters(KDF-EMV-AC-SESSION)
KEY sessionKey = Derive(sessionParameters, ICC, 0x(0001))
```

ICC = 985EC4FD3EDF6162E31AF1C7D0543416.
Session key = 43A79915202AD2C8504F42A91438B152 (no session-key parity adjustment).

Equivalent direct parameters: `PARAM p = #MECH:KDF-EMV-MASTER-B #PSN:"45"`.
Default PSN: `PARAM p = Parameters(KDF-EMV-MASTER-B)`.
PAN may be declared as `VAR pan = "99012345678901234"` and passed as `Derive(p, IMK, pan)`.

## Result Metadata and Security

Secret TDEA; KeySizeInBits 128 including parity bits; ValueFormat HEX; identical Value and
KeyValue; KeyUsage Unspecified; DerivationMechanism KDF-EMV-MASTER-B. The result is directly
usable with KDF-EMV-AC-SESSION. No extra derived-key repair or rejection policy is applied.

Errors omit key, PAN and PSN values. Owned snapshots, hash buffers and temporary ECB results
are cleared in finally blocks. Immutable CryptoScript KEY strings cannot reliably be zeroized.
Fault injection after the first successful ECB block remains a documented test limitation;
no additional fault-injection architecture is introduced.

## References

EMV Contact Book 2, Annex A1.4.2 (official v4.4 verification pending).
V1/V2 are published PyEMV comparison values, not official EMV conformance vectors.
V3 and the three ATC-0001 session keys are independent characterization values.
