# MECHANISM MAC-EMV-AC-VERIFY

## Purpose and Scope

Fail-closed verification of an eight-byte EMV Application Cryptogram using an already derived
session KEY and already assembled binary transaction data. Supports Secret TDEA-128 and
AES-128/192/256. Uses the unchanged EmvApplicationCryptogram.VerifyAc core and its fixed-time
comparison; no separate MAC, padding or comparison implementation in the wrapper.

EMV Contact Book 2 v4.4: 8.1.1/2 (pages 86-87), A1.2.1/2 (pages 134-136), A1.3.1 (pages 137-138).
TDEA selects the Algorithm 3 profile with method-2 padding; AES uses CMAC without extra external
padding. This is not EMV product certification or official EMVCo approval. Fixed examples are
independently calculated characterization values, not official certification vectors.

## Strict Contract and Failure Behavior

Exactly `Mac(PARAM, KEY, binaryData)` with a genuine PARAM reference, genuine Secret AES/TDEA
KEY reference, and a direct HEX literal or HEX VAR for data. Known KEY size metadata must agree.
No value-based KEY lookup or reconstruction of PARAM from VAR. Direct MECH selectors, KEY
literals, strings, integers, Base64 data, KEY/PARAM data and nested function results are rejected.
Imported session keys are allowed; DerivationMechanism is not a mandatory provenance proof.
New mechanisms require structured calls; existing legacy MAC contracts are unchanged.

Only MECH and required EXPECTED parameters are allowed. EXPECTED must resolve to a HEX value
with exactly 16 hex digits (eight binary bytes). No implicit ASCII, integer or Base64 conversion.
The existing Named-Parameter transport proves the resolved value, not its original variable or
AST provenance; no stronger origin claim is made for EXPECTED. MECH must consistently select
MAC-EMV-AC-VERIFY. Missing, malformed, duplicate (even identical) or contradictory parameters
are rejected. IV, PAD, MACLEN and unsupported additions are forbidden. Both Parameters(...)
and direct PARAM declarations followed by references are supported.

On success, returns HEX VAR `0x(01)` and continues execution.
On mismatch, the existing runtime-error path raises SemanticErrorException and actually stops
the public runner's statement loop. It is not merely a non-blocking Semantic Diagnostic.
No successful verified VAR is created and subsequent statements are not executed.
The error message contains no key, data or cryptogram values. Previously executed statements
are not rolled back; the caller must not catch the exception and resume protected actions.

The grammar cannot represent `0x()`; empty HEX data is not exposed. The core's technical
empty-data contract is unchanged. See [AC computation](cryptoscript-info://mechanism/MAC-EMV-AC).

## Executable Example

```text
KEY mk = GenerateKey(AES-ECB, 0x(2EF6E07ECBA86BCF3C3CFF7BBEBE6F38))
PARAM derivation = Parameters(KDF-EMV-AC-SESSION)
KEY sk = Derive(derivation, mk, 0x(0001))
PARAM verify = Parameters(MAC-EMV-AC-VERIFY, #EXPECTED:0x(D3A78E1517C22F70))
VAR transactionData = 0x(000102030405060708090A0B0C0D0E0F)
VAR verified = Mac(verify, sk, transactionData)
VAR after = 0x(AABB)
```

Result: verified is `0x(01)` and after is created. Changing the first EXPECTED byte to 53
instead of D3 raises the verification error; neither verified nor after is created.
Equivalent direct parameters:
`PARAM verify = #MECH:MAC-EMV-AC-VERIFY #EXPECTED:0x(D3A78E1517C22F70)`.

## Security and Application Responsibilities

No new global KeyUsage policy is introduced; this operation is MAC verification.
Owned decoded-key, transaction-data and expected-cryptogram buffers are zeroized in finally
blocks on success and exceptions. The unchanged core snapshots inputs, retains primitive
validation and clears computed MAC buffers. Immutable CryptoScript strings remain a limit.

Verification alone does not establish correct transaction/profile data selection, ordering,
account/session association, ATC freshness or replay prevention. Callers own those bindings.
This verification operation does not compute ARPC. After successful verification, CryptoScript
provides [ARPC Method 1](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-1) and
[ARPC Method 2](cryptoscript-info://mechanism/MAC-EMV-ARPC-METHOD-2).
A failed AC verification stops script execution with SemanticErrorException. An isolated
ARPC invocation does not enforce prior verification; callers own that policy.
No CDOL/TLV assembly, APDU handling, automatic key derivation, Secure Messaging or complete
EMV transaction processing is provided by these mechanisms. Linux/Unix and FIPS provider runtime tests remain
outside the demonstrated scope; platform-independent code is not a runtime certification.
