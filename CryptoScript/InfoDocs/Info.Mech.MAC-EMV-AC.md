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
provides input snapshots and technical validation, with non-blocking TDEA key-quality warnings.
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

### Algorithm selection contract

KEY.KeyType.Algorithm is the binding algorithm selector: Aes selects AES and Tdea selects TDEA. This is the approved architecture decision, not an outstanding migration. An explicit declaration can select another cryptographic procedure, so this selector is not converted into a KeyTypeConflict warning. Unknown or unsupported algorithms fail; the key length is never used to guess the algorithm. No additional #ALGORITHM parameter is introduced.

GenerateKey(AES-ECB, ...) produces Secret(Aes), and GenerateKey(DES3-ECB, ...) produces Secret(Tdea), for both random keys and hexadecimal imports. The original mode does not restrict later compatible use. Derive, Unwrap, JSON deserialization and legacy imports are additional KEY sources and can still produce Unknown; those inputs remain invalid for EMV algorithm selection until their existing metadata identifies Aes or Tdea. KeyUsage conflicts remain non-blocking warnings. Cryptographic results, length/ATC/data checks and ARQC fail-closed verification are unchanged. This mechanism does not support GenerateKey itself.

### Semantic diagnostics

Expected KeyType metadata: Secret Aes or Tdea, still required to select the algorithm. Expected KeyUsage: `MacGenerate`.
Usage conflicts emit a non-blocking `KeyUsageConflict` warning; correct or Unspecified usage emits none. This mechanism retains KeyType as a binding algorithm selector, so Unknown remains a selection error and is not silently accepted or reduced to a `KeyTypeConflict` warning. These checks do not mutate KEY or PARAM and work without a diagnostic sink. Only the usage metadata warning applies here. KeyAlgorithm still selects AES/TDEA; Unknown and unsupported types remain blocking under the approved selector contract. Technically valid weak or degenerate TDEA test keys remain executable with quality warnings. AC verification failure remains blocking and stops subsequent script execution. ARPC computation does not itself verify the received ARQC.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.
### TDEA test and analysis keys

`DesParityViolation` (10) and `WeakDes3Key` (11) are non-blocking Warning diagnostics. Weak or semi-weak DES components and effective K1=K2 or K2=K3 are reported independently of parity; K1=K3 alone is not single-DES degeneration. Each quality category is reported at most once per operation on the original KEY, after any KeyTypeConflict and KeyUsageConflict metadata warnings. No KEY bytes or parity bits are changed. Technically valid calls also work without a diagnostic sink.

This support is for test and analysis and makes no suitability claim for production payment systems. Invalid key lengths, malformed data and protocol errors remain blocking. ARQC verification and TR-31 authentication remain mandatory; failed verification stops execution without a successful result. AES behavior and the approved algorithm selectors remain unchanged.
