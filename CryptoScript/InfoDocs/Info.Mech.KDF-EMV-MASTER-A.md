# MECHANISM KDF-EMV-MASTER-A

## Purpose

Host-side EMV ICC Master Key Derivation Option A, for personalization and issuer processing.
Derives a card-specific ICC Master Key from an Issuer Master Key (IMK), the complete PAN
including its check digit, and the PAN Sequence Number (PSN). This does not calculate a cryptogram.

## Conformance Status

The normative implementation review and formula comparison against EMV Contact Book 2 v4.4
were completed on 13 October 2026 for commit `881ec9219ca7f558603e98acb3696b7e5219201e`.
The review covered A1.3.1 (pages 137-138), A1.4.1 (pages 138-139), and A1.4.2 (page 139).
This mechanism's construction matches A1.4.1; no security- or interoperability-relevant
normative deviation was found. This is a normative implementation review, not EMV product certification
or an official EMVCo approval. Fixed comparison and characterization values are not certification evidence.

The review does not establish complete Application Cryptogram or ARPC processing, including
the eight-byte AES Application Cryptogram integration, or Secure Messaging.
Linux/Unix and FIPS runtime verification remain outside the demonstrated scope;
Windows test results are not cross-platform evidence.
The provisional PAN limit of 1-19 digits is a technical decision,
not a normative conformity assertion. Option B is available separately as KDF-EMV-MASTER-B;
it is never selected automatically by Option A.

## Input Contract

The IMK must be a genuine KEY reference containing exactly 16 bytes (K1 || K2); expected metadata is Secret TDEA.
DES3 parity and weak/degenerate-key findings are non-blocking warnings; conflicting type or DeriveKey usage also emits a warning.
The IMK is not adjusted for parity or modified. Key literals and non-KEY variables are rejected.

PAN is the third Derive argument: a direct normal string literal or a referenced normal string
VAR (not a HEX/Base64 VAR). It contains 1-19 ASCII digits, retaining leading zeros.
Nested function results, integers, HEX/Base64 literals, KEY/PARAM references and implicit
conversions are rejected. No trimming, normalization, Luhn check or check-digit removal occurs.

## Parameter Contract

Parameters contain exactly one consistent MECH and optionally one PSN; no other named parameters.
PSN is supplied only as a normal string literal containing exactly two ASCII digits, e.g.
`#PSN:"45"`. Omission defaults to `"00"`; empty, one-digit, integer, variable, HEX and Base64
values are rejected. Duplicate PSN and MECH declarations are rejected even if identical;
stored MECH must agree with the selected mechanism. These rules also apply to direct PARAM
declarations and serialized legacy parameter sets.
Serialized segments without a colon or value are rejected before legacy parsing; a malformed
PSN never selects the default. PSN is forbidden for mechanisms whose registry contract does
not explicitly support it, including AES-CBC, DES3-ECB and KDF-EMV-AC-SESSION.

## Derivation Algorithm

1. X = PAN || PSN as decimal digits.
2. D = the rightmost 16 digits of X, or left-pad with zero digits to 16 digits.
3. Pack D into eight bytes (two decimal nibbles per byte); F1 = this block.
4. F2 = F1 XOR FF FF FF FF FF FF FF FF, the full bitwise complement.
5. L = TDEA-ECB(IMK, F1); R = TDEA-ECB(IMK, F2). No padding or IV is used.
6. ICC_RAW = L || R, exactly 16 bytes.
7. Set DES Odd Parity on ICC_RAW, changing only bit 0 of each byte.

Unlike this master-key derivation, KDF-EMV-AC-SESSION does not adjust its output parity.
No result repair, retry, extra transformation or automatic Option B fallback is performed.

## Example Usage

```text
KEY IMK = GenerateKey(DES3-ECB, 0x(0123456789ABCDEFFEDCBA9876543210))
PARAM p = Parameters(KDF-EMV-MASTER-A, #PSN:"45")
KEY ICC = Derive(p, IMK, "99012345678901234")
PARAM sessionParameters = Parameters(KDF-EMV-AC-SESSION)
KEY sessionKey = Derive(sessionParameters, ICC, 0x(0001))
```

ICC = 67F8292358083E5EA7AB7FDA58D53B6B.
Session key = 68533955B11DEC088A36B4871DEAF9CF (no session-key parity adjustment).

An equivalent direct declaration is `PARAM p = #MECH:KDF-EMV-MASTER-A #PSN:"45"`.
For the default PSN, use `PARAM p = Parameters(KDF-EMV-MASTER-A)`.
PAN may also be declared with `VAR pan = "99012345678901234"` and passed as `Derive(p, IMK, pan)`.

## Result KEY Metadata

Algorithm TDEA; Secret material; KeySizeInBits 128 including parity bits; ValueFormat HEX;
KeyValue and Value contain the same derived key; DerivationMechanism KDF-EMV-MASTER-A;
KeyUsage Unspecified. The result can be passed directly to KDF-EMV-AC-SESSION.

## Security and Errors

Invalid key lengths, contradictory size metadata, malformed inputs, duplicate/extra
parameters and forbidden argument origins are rejected. Core errors do not include key,
PAN or PSN values. Temporary key and ciphertext buffers are zeroized in finally blocks;
the returned key remains available to the caller. Immutable CryptoScript KEY strings cannot
be reliably zeroized. Fault-injection observation of provider exceptions and private buffer
cleanup remains a test limitation; no special crypto abstraction was added for that test.

## Reference

EMV Contact Book 2 v4.4, Annex A1.4.1 Option A, pages 138-139 (formula review completed 13 October 2026).
Published comparison values: PyEMV 1.3.0 and PyEMV kd.py (EMV 4.3 reference).
V3 in the core tests is an independent characterization, not a normative published vector.

### Semantic diagnostics

Expected KeyType metadata: Tdea. Expected KeyUsage: `DeriveKey`.
Declarative conflicts emit non-blocking `KeyTypeConflict` then `KeyUsageConflict` warnings where a fixed expected algorithm exists. Correct metadata emits no conflict warning; Unknown KeyType and Unspecified usage impose no diagnostic restriction. These checks do not mutate KEY or PARAM and work without a diagnostic sink. The selected Option A/B/C determines the algorithm; conflicting KEY declarations never select another option. PAN, PSN, genuine-reference and known-size consistency checks remain binding. DES parity/weakness is diagnosed without blocking technically valid TDEA test keys. The explicitly enabled analysis backend uses the supplied effective key bits.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.
### TDEA test and analysis keys

`DesParityViolation` (10) and `WeakDes3Key` (11) are non-blocking Warning diagnostics. Weak or semi-weak DES components and effective K1=K2 or K2=K3 are reported independently of parity; K1=K3 alone is not single-DES degeneration. Each quality category is reported at most once per operation on the original KEY, after any KeyTypeConflict and KeyUsageConflict metadata warnings. No KEY bytes or parity bits are changed. Technically valid calls also work without a diagnostic sink.

This support is for test and analysis and makes no suitability claim for production payment systems. Invalid key lengths, malformed data and protocol errors remain blocking. ARQC verification and TR-31 authentication remain mandatory; failed verification stops execution without a successful result. AES behavior and the approved algorithm selectors remain unchanged.
