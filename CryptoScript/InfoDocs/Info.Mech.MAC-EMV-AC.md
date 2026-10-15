# MECHANISM MAC-EMV-AC

## Purpose and Scope

Computes an eight-byte EMV Application Cryptogram (AC) using an already derived session KEY
and already assembled binary transaction data. Supports Secret TDEA-128 (16 bytes) and
Secret AES-128/192/256 (16/24/32 bytes). Known KEY size metadata must match the actual bytes.
Imported session keys are allowed; DerivationMechanism is not a mandatory provenance proof.

Uses the independently reviewed EmvApplicationCryptogram.ComputeAc core unchanged:
TDEA ISO/IEC 9797-1 Algorithm 3 with mandatory method-2 padding, or AES-CMAC with no extra
external padding; in both cases the result is exactly eight bytes. No configurable padding,
IV or MAC length. The wrapper implements no MAC, padding, or truncation of its own.

EMV Contact Book 2 v4.4: 8.1.1 (pages 86-87), 8.1.2 (page 87), A1.2.1/2 (pages 134-136),
and A1.3.1 (pages 137-138). The TDEA mechanism selects the Algorithm-3 profile, not Algorithm 1.
This is not EMV product certification or official EMVCo approval. The examples use independently
calculated characterization values, not official certification vectors.

## Arguments and Parameters

Exactly `Mac(PARAM, KEY, binaryData)` under the existing Mac function.

- PARAM must be a genuine referenced PARAM variable with original argument kind Variable.
  Both Parameters(...) and direct PARAM declarations followed by references work.
  Direct MECH selectors, nested Parameters(...) results and reconstructed PARAMs from VARs are rejected.
- KEY must be a genuine Secret AES or TDEA KEY reference. No KEY literals or value-based lookup.
  Identical KEY values retain the selected instance's metadata. VAR/PARAM are not KEYs.
- Data must be a direct HEX literal or a HEX VAR reference. Strings, integers, Base64,
  KEY/PARAM references and nested function results are rejected. No implicit conversion.
- Only MECH is allowed, consistently MAC-EMV-AC. Duplicate or contradictory parameters
  and all additions including EXPECTED, IV, PAD and MACLEN are rejected.

The grammar cannot represent `0x()`; empty HEX data is therefore not exposed by this mechanism.
The core's technical empty-data boundary remains unchanged; an empty EMV transaction is not validated.
New mechanisms require structured calls. Existing explicit legacy MAC calls retain their contracts.

## Executable Example

```text
KEY mk = GenerateKey(AES-ECB, 0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38))
PARAM derivation = Parameters(KDF-EMV-AC-SESSION)
KEY sk = Derive(derivation, mk, 0x(0001))
PARAM p = Parameters(MAC-EMV-AC)
VAR transactionData = 0x(000102030405060708090A0B0C0D0E0F)
VAR ac = Mac(p, sk, transactionData)
```

Result: HEX VAR `0x(D3A78E1517C22F70)`, exactly eight bytes.
Equivalent direct parameters: `PARAM p = #MECH:MAC-EMV-AC`.
See [AC verification](cryptoscript-info://mechanism/MAC-EMV-AC-VERIFY).

## Security and Application Responsibilities

No new global KeyUsage policy is introduced; AC computation is MAC generation.
Owned decoded-key, data and output buffers are zeroized in finally blocks. The unchanged core
provides input snapshots and primitive validation, including the existing TDEA weak-key policy.
Immutable CryptoScript strings cannot be reliably zeroized. Errors do not echo input material.

Callers must select, order and assemble the correct transaction/profile data and bind it to the
correct account, session key and ATC. This mechanism does not perform CDOL/TLV preparation,
APDU processing, automatic derivation, replay prevention or full EMV transaction validation.
This AC operation does not compute ARPC. After successful verification with
[MAC-EMV-AC-VERIFY](cryptoscript-info://mechanism/MAC-EMV-AC-VERIFY), CryptoScript provides
[ARPC Method 1](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-1) and
[ARPC Method 2](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-2).
A failed AC verification stops script execution with SemanticErrorException. An isolated
ARPC invocation does not enforce prior verification; callers own that policy.
Secure Messaging and complete EMV transaction processing remain outside these mechanisms.
Linux/Unix and FIPS provider
execution remain verification limits; Windows tests alone do not prove cross-platform behavior.
