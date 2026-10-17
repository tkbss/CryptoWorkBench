# MECHANISM MAC-EMV-ARPC-METHOD-2

## Purpose and EMV Scope

Issuer-host computation of the four-byte ARPC, Method 2, optionally returning the complete
Issuer Authentication Data (IAD). Uses an already derived Application Cryptogram Session KEY,
an eight-byte ARQC, four-byte CSU and zero to eight proprietary authentication bytes.
Supports Secret TDEA with 16 bytes and Secret AES with 16, 24 or 32 bytes.
No hidden ARQC verification, automatic derivation or global transaction state.

EMV Contact Book 2 v4.4, section 8.2.2, page 89; A1.2.1/2, pages 134-136;
A1.3.1, pages 137-138. The TDEA core selects Algorithm 3 with ISO-9797-M2 and a null initial
value; AES uses CMAC without extra external padding. This is not EMVCo product certification
or a complete EMV/CCD protocol. Fixed values are independent characterization values.

## Strict Contract

Exactly `Mac(PARAM, KEY, ARQC)`. Genuine PARAM variable reference, original kind Variable,
no reconstruction from VAR or direct MECH selector. Parameters(...) and direct PARAM declarations
followed by references work. Genuine Secret AES/TDEA KEY reference with matching known size
metadata; no KEY literals or value-based lookup. Imported session keys are allowed without
mandatory DerivationMechanism or a new global KeyUsage policy.
ARQC must be exactly eight bytes from a direct HEX literal or HEX VAR. Strings, integers,
Base64, KEY/PARAM data and nested function results are rejected. No implicit conversion.

Only MECH, required CSU, optional ADATA and optional VARIANT are allowed:

- CSU: exactly four bytes as a resolved HEX value, for example `#CSU:0x(00000000)`.
  CSU is opaque profile data; no interpretation of card-state bits.
- ADATA: one to eight proprietary bytes as a resolved HEX value. Omit it for zero bytes.
  The grammar cannot represent `0x()`; no new empty-HEX syntax is introduced.
- VARIANT: only the explicit normal string `#VARIANT:"IAD"`. Without VARIANT, returns ARPC.
  No explicit ARPC variant or alternative spelling. Other mechanisms retain their existing VARIANT contracts.
- MECH must consistently select MAC-EMV-ARPC-METHOD-2 and agree with Parameter.Mechanism.

Named binary parameters are validated by resolved value and length, not by an unavailable
original-variable provenance guarantee. Duplicate (even identical), contradictory, unknown and
unsupported additions are rejected, including ARC, PAD, IV and MACLEN. New ARPC mechanisms
require structured calls; existing legacy MAC contracts are unchanged.

## Core Processing and Output

EmvApplicationCryptogram.ComputeArpcMethod2 alone builds ARQC || CSU || Proprietary Data,
computes the MAC and returns its leftmost four bytes. The wrapper performs no parallel MAC,
padding, truncation or data concatenation.
For IAD, EmvApplicationCryptogram.BuildIssuerAuthenticationData returns:
ARPC || CSU || Proprietary Authentication Data, exactly 8-16 bytes.
This is the value of tag 91, not BER-TLV: no Tag-91 prefix or length field is added.
Both output forms are HEX VARs.

## Executable ARPC Example

```text
KEY sk = GenerateKey(AES-ECB, 0x(89F7B697A028A93345BE7A409665B9A4))
PARAM p = Parameters(MAC-EMV-ARPC-METHOD-2, #CSU:0x(00000000))
VAR arpc = Mac(p, sk, 0x(D3A78E1517C22F70))
```

Expected: `0x(B7E6B69E)`.

## Executable IAD Example

```text
KEY sk = GenerateKey(AES-ECB, 0x(89F7B697A028A93345BE7A409665B9A4))
PARAM p = Parameters(MAC-EMV-ARPC-METHOD-2, #CSU:0x(00000000), #VARIANT:"IAD")
VAR iad = Mac(p, sk, 0x(D3A78E1517C22F70))
```

Expected: `0x(B7E6B69E00000000)`.
With ADATA 01020304 and CSU 00000000, AES-128 IAD is `0x(20A190D60000000001020304)`.
Equivalent direct parameters:
`PARAM p = #MECH:MAC-EMV-ARPC-METHOD-2 #CSU:0x(00000000) #VARIANT:"IAD"`.

## Verification Policy and Security

EMV footnote 36 recommends returning ARPC only after successful ARQC verification.
Use [MAC-EMV-AC-VERIFY](cryptoscript-info://mechanism/MAC-EMV-AC-VERIFY) first, with the same
transaction bytes and AC Session Key. Its mismatch raises SemanticErrorException and stops
the public runner before any following ARPC statement or result variable. Earlier statements
are not rolled back. If responding despite verification failure, do not calculate ARPC from
the network-received ARQC. An isolated Method-2 call does not automatically enforce prior
verification or bind an account, session, transaction profile or ATC; analysis use remains possible.
See [Method 1](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-1) for an executable verification-first hierarchy.

All input validation precedes cryptography. Owned decoded key, ARQC, CSU, proprietary data,
ARPC and IAD buffers are zeroized in finally blocks, including exceptions. Caller values remain
unchanged. Technical primitive checks remain binding; TDEA key-quality findings are non-blocking warnings. Errors do not echo inputs.
Immutable strings and provider-internal copies cannot be reliably erased by the wrappers.
No CDOL/TLV assembly, APDU processing or Secure Messaging. Linux/Unix/FIPS runtime evidence
is not supplied by Windows tests. No claim of complete transaction validation or certification.

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
