# MECHANISM KDF-EMV-MASTER-B

## Purpose and Conformance Status

Host-side EMV ICC Master Key Derivation Option B for personalization and issuer processing.
Derives a card-specific ICC Master Key from an Issuer Master Key (IMK), the complete PAN
including its check digit, and the PAN Sequence Number (PSN). It does not calculate a cryptogram.

The normative implementation review and formula comparison against EMV Contact Book 2 v4.4
were completed on 13 October 2026 for commit `881ec9219ca7f558603e98acb3696b7e5219201e`.
The review covered A1.3.1 (pages 137-138), A1.4.1 (pages 138-139), and A1.4.2 (page 139).
This mechanism's construction matches A1.4.2; no security- or interoperability-relevant
normative deviation was found. This is a normative implementation review, not EMV product certification
or an official EMVCo approval. Published comparison and characterization values are not certification evidence.

The review does not establish complete Application Cryptogram or ARPC processing, including
the eight-byte AES Application Cryptogram integration, or Secure Messaging.
Linux/Unix and FIPS runtime verification remain outside the demonstrated scope;
Windows test results are not cross-platform evidence.
The PAN limit of 1-19 digits is a provisional technical
contract, not a normative conformity assertion.

## Input and Parameter Contract

Use exactly three Derive arguments: PARAM, a genuine KEY reference of exactly
16 bytes, and PAN. KEY literals, non-KEY references and value-based structured KEY lookup
are not allowed. Expected metadata is Secret TDEA with DeriveKey usage; conflicts warn.
DES3 parity and weak/degenerate-key findings are non-blocking warnings. The input key and its parity are not changed.

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

Secret TDEA; KeySizeInBits 128 including parity bits; ValueFormat HEX;
Value contains the derived key; KeyUsage Unspecified; DerivationMechanism KDF-EMV-MASTER-B. The result is directly
usable with KDF-EMV-AC-SESSION. No extra derived-key repair or rejection policy is applied.

Errors omit key, PAN and PSN values. Owned snapshots, hash buffers and temporary ECB results
are cleared in finally blocks. Immutable CryptoScript KEY strings cannot reliably be zeroized.
Fault injection after the first successful ECB block remains a documented test limitation;
no additional fault-injection architecture is introduced.

## References

EMV Contact Book 2 v4.4, Annex A1.4.2 Option B, page 139 (formula review completed 13 October 2026).
V1/V2 are published PyEMV comparison values, not official EMV conformance vectors.
V3 and the three ATC-0001 session keys are independent characterization values.

### Semantic diagnostics

Expected KeyType metadata: Tdea. Expected KeyUsage: `DeriveKey`.
Declarative conflicts emit non-blocking `KeyTypeConflict` then `KeyUsageConflict` warnings where a fixed expected algorithm exists. Correct metadata emits no conflict warning; Unknown KeyType and Unspecified usage impose no diagnostic restriction. These checks do not mutate KEY or PARAM and work without a diagnostic sink. The selected Option A/B/C determines the algorithm; conflicting KEY declarations never select another option. PAN, PSN, genuine-reference and known-size consistency checks remain binding. DES parity/weakness is diagnosed without blocking technically valid TDEA test keys. The explicitly enabled analysis backend uses the supplied effective key bits.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.
### TDEA test and analysis keys

`DesParityViolation` (10) and `WeakDes3Key` (11) are non-blocking Warning diagnostics. Weak or semi-weak DES components and effective K1=K2 or K2=K3 are reported independently of parity; K1=K3 alone is not single-DES degeneration. Each quality category is reported at most once per operation on the original KEY, after any KeyTypeConflict and KeyUsageConflict metadata warnings. No KEY bytes or parity bits are changed. Technically valid calls also work without a diagnostic sink.

This support is for test and analysis and makes no suitability claim for production payment systems. Invalid key lengths, malformed data and protocol errors remain blocking. ARQC verification and TR-31 authentication remain mandatory; failed verification stops execution without a successful result. AES behavior and the approved algorithm selectors remain unchanged.
