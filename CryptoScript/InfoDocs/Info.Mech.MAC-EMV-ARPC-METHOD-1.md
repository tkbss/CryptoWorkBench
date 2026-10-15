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
modified. Existing TDEA weak-key validation remains in force. Error messages do not echo inputs.
Immutable strings and provider-internal copies cannot be reliably erased by these wrappers.
No CDOL/TLV or APDU processing and no Secure Messaging. Linux/Unix/FIPS runtime verification
remains outside Windows test evidence. See [Method 2](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-2)
for CSU, proprietary data and complete Issuer Authentication Data output.
