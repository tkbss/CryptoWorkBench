# MECHANISM KDF-EMV-MASTER-C

## Purpose and Conformance Status

Host-side EMV ICC Master Key Derivation Option C for personalization and issuer processing.
Derives a card-specific ICC Master Key from an AES Issuer Master Key (IMK), the complete
PAN including its check digit, and the PAN Sequence Number (PSN). It does not calculate
an Application Cryptogram or ARPC.

The core formula was directly reviewed on 13 October 2026 against EMV Contact Book 2 v4.4,
Annex A1.4.3, page 140, and Specification Bulletin 162 (AES Key Derivation Erratum).
The erratum's full 16-byte inversion is already incorporated into v4.4.
This is a normative implementation review, not EMV product certification or official EMVCo
approval. The fixed values below are independently calculated characterization values,
not official EMVCo conformance vectors. The new language integration remains subject to
the concluding AP5 security and integration review.

The PAN limit of 1-19 digits is a provisional CryptoScript input contract, not an explicit
normative limit from A1.4.3. The review does not establish complete Application Cryptogram,
ARPC or Secure Messaging processing. Linux/Unix and FIPS runtime verification remain
outside the demonstrated scope; Windows tests are not cross-platform evidence.

## Input and Parameter Contract

Use exactly three Derive arguments: PARAM, a genuine KEY reference, and PAN. Expected metadata is Secret AES.
IMK lengths of 16, 24 and 32 bytes produce AES-128, AES-192 and AES-256 ICC Master Keys,
respectively. Known KeySizeInBits metadata must match the actual key data.
Conflicting declared key algorithms warn; Unknown emits no type warning. KEY literals, VAR/PARAM in the
KEY position and value-based structured KEY lookup are not allowed. Identical values in
different KEY variables do not override the referenced KEY's metadata.

PAN must be a direct normal string literal or a referenced normal string VAR containing
1-19 ASCII decimal digits. Leading zeros and the check digit are retained. HEX/Base64,
integer, KEY/PARAM and nested function results are rejected. No implicit conversion,
trimming, normalization, Luhn validation or check-digit removal takes place.

Only MECH and optional PSN parameters are permitted. MECH must consistently select
KDF-EMV-MASTER-C. PSN must be a direct normal string literal containing exactly two ASCII
digits, for example `#PSN:"45"`; omission defaults to `"00"`. Empty, integer, variable,
HEX/Base64 and malformed PSN values are rejected. Duplicate PSN or MECH declarations
are rejected even if identical. A stored MECH contradicting Parameter.Mechanism is rejected.
These rules apply to Parameters(...), direct PARAM declarations and serialized legacy PARAMs.
Malformed serialized segments are rejected before lossy legacy parsing. The explicit legacy
Derive(string[]) adapter retains its registered-key lookup; structured calls never fall back
to this lookup. Other mechanisms retain their existing parameter contracts.

## Derivation Algorithm

Concatenate PAN || PSN and encode Packed Decimal, left-padded with zero nibbles to exactly
16 bytes (32 digits), to form Y. No PAN truncation, SHA-1, ASCII encryption or F fill nibble.

- AES-128: ICC = AES-ECB(IMK, Y), exactly one block.
- AES-192: ICC = AES-ECB(IMK, Y) || leftmost8(AES-ECB(IMK, Y*)).
- AES-256: ICC = AES-ECB(IMK, Y) || AES-ECB(IMK, Y*).

Y* is the full bitwise complement of all 16 bytes of Y, including the left padding.
ECB operations use no IV and no padding. No DES Odd Parity correction is applied.
The ICC key length equals the IMK length. Option A/B use TDEA and their own preparation
rules; Option C is never selected as an automatic fallback from either mechanism.

## Executable Example and Key Hierarchy

```text
KEY IMK = GenerateKey(AES-ECB, 0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38))
PARAM p = Parameters(KDF-EMV-MASTER-C, #PSN:"45")
KEY ICC = Derive(p, IMK, "99012345678901234")
PARAM sessionParameters = Parameters(KDF-EMV-AC-SESSION)
KEY sessionKey = Derive(sessionParameters, ICC, 0x(0001))
```

Y = 00000000000009901234567890123445.
ICC = 35C006583E2B417207AE5B166C0EA6EF.
Session key = 1E955EB21AFFA73F01DBED4D41BD25F4.

Equivalent direct parameters: `PARAM p = #MECH:KDF-EMV-MASTER-C #PSN:"45"`.
Default PSN: `PARAM p = Parameters(KDF-EMV-MASTER-C)`.
PAN can be declared as `VAR pan = "99012345678901234"` and passed as `Derive(p, IMK, pan)`.
The ICC KEY is directly usable with KDF-EMV-AC-SESSION without manual conversion.

## Result Metadata and Security

Secret AES; KeySizeInBits 128, 192 or 256; ValueFormat HEX; Value contains the derived key;
KeyUsage Unspecified; DerivationMechanism KDF-EMV-MASTER-C. The subsequent session KEY
retains the AES size and reports KDF-EMV-AC-SESSION. No extra key-usage or AES weak-key
policy is introduced.

Errors do not echo key, PAN or PSN values. The core uses one IMK snapshot, then only that
snapshot for validation and AES. Owned decoded-key, snapshot, diversification and ECB
buffers are zeroized in finally blocks, including results that are not returned on failure.
The wrapper also clears the derived byte array after creating the CryptoScript KEY value.
Immutable CryptoScript KEY/PAN/PSN strings cannot reliably be zeroized.
Callers must prevent concurrent mutation while an input-key snapshot is being copied;
the copy is not guaranteed to be atomic against external writers.
Dynamic provider-fault injection and observation of all private buffers after exceptions
remain test limitations; no production fault-injection abstraction is introduced.

## References

EMV Contact Book 2 v4.4, Annex A1.4.3 Option C, page 140; AES variants in A1.4, page 138.
EMVCo Specification Bulletin 162, April 2015, pages 1-2 (corrects the earlier 8-byte mask).
ICC and ATC-0001 session values were independently checked with Python cryptography / OpenSSL,
without invoking the CryptoScript cores; they are not product-certification evidence.

### Semantic diagnostics

Expected KeyType metadata: Aes. Expected KeyUsage: `DeriveKey`.
Declarative conflicts emit non-blocking `KeyTypeConflict` then `KeyUsageConflict` warnings where a fixed expected algorithm exists. Correct metadata emits no conflict warning; Unknown KeyType and Unspecified usage impose no diagnostic restriction. These checks do not mutate KEY or PARAM and work without a diagnostic sink. The selected Option A/B/C determines the algorithm; conflicting KEY declarations never select another option. PAN, PSN, genuine-reference and known-size consistency checks remain binding.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.
