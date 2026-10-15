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
unchanged. Existing primitive and TDEA weak-key checks are retained. Errors do not echo inputs.
Immutable strings and provider-internal copies cannot be reliably erased by the wrappers.
No CDOL/TLV assembly, APDU processing or Secure Messaging. Linux/Unix/FIPS runtime evidence
is not supplied by Windows tests. No claim of complete transaction validation or certification.
