# MECHANISM MAC-EMV-ARPC-METHOD-1

## Purpose and EMV Scope

Issuer-host computation of the eight-byte Authorisation Response Cryptogram (ARPC), Method 1.
Uses an already derived Application Cryptogram Session KEY and the received eight-byte ARQC.
Secret TDEA with exactly 16 bytes or Secret AES with 16, 24 or 32 bytes is supported.
No automatic derivation, ARQC verification, transaction-state machine or transaction binding.
Imported session keys are allowed; no mandatory DerivationMechanism or new global KeyUsage policy.

EMV Contact Book 2 v4.4, section 8.2.1, page 88; A1.3.1, pages 137-138 for session keys.
The previously reviewed core is reused unchanged. This is not EMVCo product certification or
a complete EMV/CCD transaction protocol. Fixed values are independent characterization values.

## Strict Arguments and Parameters

Exactly `Mac(PARAM, KEY, ARQC)`. PARAM must be a genuine variable reference created by
Parameters(...) or a direct PARAM declaration, not a direct MECH selector, reconstructed VAR
or nested function result. KEY must be a genuine Secret AES/TDEA reference, never a literal
or value-based lookup. Known key-size metadata must match the actual key bytes.
ARQC must contain exactly eight bytes from a direct HEX literal or HEX VAR. Strings, integer,
Base64, KEY/PARAM data and nested function results are rejected; no implicit conversions.

Only MECH and required ARC are allowed. ARC must resolve to exactly two binary HEX bytes,
for example `#ARC:0x(3030)`. No implicit ASCII conversion of `"00"` or integer interpretation.
Binary ARC 3030 and 0000 are different inputs. Named binary values have a resolved-value
contract, not a claim of original AST/variable provenance. Duplicate (even identical), unknown,
contradictory and unsupported parameters are rejected, including PAD, IV, MACLEN, CSU,
ADATA and VARIANT. MECH must agree with Parameter.Mechanism.
New ARPC mechanisms require structured calls; existing legacy MAC contracts are unchanged.

## Algorithm and Result

Only EmvApplicationCryptogram.ComputeArpcMethod1 implements the cryptography:
X = ARC || six zero bytes; Y = ARQC XOR X.
TDEA: encrypt the eight-byte Y in ECB with the AC Session Key.
AES: encrypt Y || eight zero bytes in ECB and return the leftmost eight bytes.
No IV, external padding, parity adjustment or configurable output length.
The wrapper implements no XOR, ECB or truncation itself. Result is an eight-byte HEX VAR.

## Executable Standalone Example

```text
KEY sk = GenerateKey(AES-ECB, 0x(89F7B697A028A93345BE7A409665B9A4))
PARAM p = Parameters(MAC-EMV-ARPC-METHOD-1, #ARC:0x(3030))
VAR arpc = Mac(p, sk, 0x(D3A78E1517C22F70))
```

Expected: `0x(FC16C28C668DDC7C)`.
Equivalent direct parameters: `PARAM p = #MECH:MAC-EMV-ARPC-METHOD-1 #ARC:0x(3030)`.

## Recommended Verification-First Flow

EMV Book 2 footnote 35 recommends returning ARPC only after successful ARQC verification.
If an issuer nevertheless responds after verification failure, the ARPC should not be computed
from the ARQC received from the network. This mechanism does not automatically enforce that policy.
Use [MAC-EMV-AC-VERIFY](cryptoscript-info://mechanism/MAC-EMV-AC-VERIFY) before generating a reply:

```text
KEY mk = GenerateKey(AES-ECB, 0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38))
PARAM s = Parameters(KDF-EMV-AC-SESSION)
KEY sk = Derive(s, mk, 0x(0001))
VAR data = 0x(000102030405060708090A0B0C0D0E0F)
VAR received = 0x(D3A78E1517C22F70)
PARAM verify = Parameters(MAC-EMV-AC-VERIFY, #EXPECTED:received)
PARAM reply = Parameters(MAC-EMV-ARPC-METHOD-1, #ARC:0x(3030))
VAR verified = Mac(verify, sk, data)
VAR arpc = Mac(reply, sk, received)
```

Expected verified = `0x(01)`, arpc = `0x(FC16C28C668DDC7C)`.
A mismatching received ARQC raises SemanticErrorException and stops the runner before the ARPC
statement; neither successful verified nor arpc is created. Earlier statements are not rolled back.
An isolated ARPC invocation remains independently usable for cryptographic analysis and does not
prove prior verification, correct data selection, account association or ATC freshness.

## Security and Limits

Argument, parameter and length validation precedes cryptography. Owned decoded key, ARQC, ARC
and output buffers are zeroized in finally blocks, including exceptions. Caller values are not
modified. TDEA key quality is reported as non-blocking warnings. Error messages do not echo inputs.
Immutable strings and provider-internal copies cannot be reliably erased by these wrappers.
No CDOL/TLV or APDU processing and no Secure Messaging. Linux/Unix/FIPS runtime verification
remains outside Windows test evidence. See [Method 2](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-2)
for CSU, proprietary data and complete Issuer Authentication Data output.

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
