# TR-31 preparation baseline and reference provenance

Preparation date: 2026-09-14. No production changes, binding implementation,
mechanism registration, grammar changes or header validation belong to this step.

## Baseline

Initial `git status --short`: empty (clean working tree).
`dotnet test CryptoWorkBench.sln --verbosity quiet`: 342 passed, 0 failed,
0 skipped, 342 total. The repository contains one test project,
`CryptoScriptUnitTest`. No existing test was modified.

## AES/D compatibility coverage

`WrapperTests` cover the published ANSI X9.143-2022 section 8.1 AES-128/D
wrap output and full-wire unwrap. A separate full-wire test preserves acceptance
of the historical CryptoWorkBench D0112 block, which is not an ANSI vector.

`Tr31AesCharacterizationTests` adds:

- Three deterministic fixtures exercising 128-, 192- and 256-bit KBPKs and
  contained keys. Each has 48 confidential bytes and a D0144 header. The tests
  assert key-length-obfuscation and block-padding regions, ciphertext, MAC,
  exact composite serialization, both unwrap representations and key metadata.
- Six omitted/wrong-length `#RND` cases. The existing implementation silently
  generates filler of the expected length. Assert structure and recovered key,
  never random byte values or uniqueness.
- Wrapped-key algorithm separation: equal-length AES and TDEA keys receive their
  respective 256-bit and 192-bit key-length-obfuscation targets.
- Wrap rejects a declared total length that differs from the generated block;
  a valid header containing an Optional Block is accepted at its actual length.
- Trailing full-wire characters beyond the declared length are currently ignored.

The structural fixtures use ANSI X9.143-2022 key-length obfuscation: AES keys are
padded to 32 key bytes before the two-byte length plus key field is padded to the
16-byte cipher boundary. This gives 48 confidential bytes for all three supported
AES key lengths. Unwrap remains compatible with historical CryptoWorkBench D0112
blocks that omitted the AES-128/192 obfuscation region.

AES fixture inputs: KBPK bytes starting at 00, key bytes starting at 20, each of
the indicated length; filler bytes starting at 01 (30, 22, 14 bytes respectively).
Expected KBEK, CMAC and ciphertext were independently calculated with Python
`cryptography` AES-CMAC and AES-CBC, then matched against current production wrap.
Tests decrypt actual ciphertext with the independently fixed KBEK using .NET AES
to inspect confidential data without accessing private production methods.

## TDEA fixture categories

`Tr31ReferenceVectors.cs` freezes complete blocks as literals as well as individual
components. `Tr31ReferenceDataTests` guards their transcription and categorization;
it deliberately does not implement a key derivation or variant binding algorithm.
These tests do not establish future production A/B/C support.

| Fixture | Category | Source |
| --- | --- | --- |
| B without optional blocks | `AnsiVector` | ANSI X9.143-2022 8.3.2.2, Table 33, printed pp. 74-80 |
| B with KS | `AnsiVector` | 8.4.2, Table 36, printed pp. 83-91 |
| C with KS | `CorrectedAnsiReferenceVector` | 8.4.1, printed pp. 80-83 |
| A without optional blocks | `DerivedReferenceVector` | Inputs from 8.3.2.1, printed pp. 72-73; new output |

`AnsiVector` means vector bytes copied from ANSI and independently confirmed;
`CorrectedAnsiReferenceVector` records an explicit correction to an ANSI example;
`DerivedReferenceVector` is a separately calculated result using ANSI inputs.
None of these fixtures is a triple-length example. Later triple-length references
must have their own documented provenance.

B fixtures include S, K1 and K2 for CMAC under the KBPK. They are not the subkeys
under KBAK. For additional future assertions, Figure 18 gives B/no-optional-block
KBAK subkeys KM1=50CAF914C079A4CC and KM2=A195F22980F34998, with
S=28657C8A603CD266. Do not confuse these with Table 33 values.

The two B fixtures' KBEK, KBAK, authentication value and ciphertext were confirmed
independently with Python `cryptography` TDEA-CMAC/CBC in the preceding analysis.
The 8-byte derivation messages for their 16-byte KBPKs are
0100000000000080, 0200000000000080 (KBEK) and
0100010000000080, 0200010000000080 (KBAK).
Header plus confidential bytes are MAC input; the resulting binary MAC is CBC IV.
The B/KS explanatory paragraph still mentions 48 encoded ciphertext characters
and total 104; the actual vector has 64 ciphertext characters and total 120.
No vector bytes were corrected for either B fixture.

### C correction

Use `C0112B0TX12S0100KS1800604B120F9292800000`, as in 8.4.1.3.
There are 40 header characters + 64 ciphertext characters + 8 MAC characters = 112.
The final block printed in 8.4.1.6 starts `C0096...`, inconsistently with its own
claimed length and preceding header. The encryption IV is ASCII `C0112B0T`
(4330313132423054), not the printed 41303039364B3054. Table 34 also has stale
hex columns (version 0x41 versus C, length 0096 versus 0112, mode 0x44 versus X).
With the consistent header, independent TDEA-CBC and CBC-MAC calculation confirms
the printed ciphertext 42B758A2...1F976A42 and MAC 9EB139E5 unchanged.

### A contradictions and derived result

8.3.2.1 specifies 32 confidential bytes but header `A0072P0TE00E0000`.
Correct wire length is 16 + 64 + 8 = 88, not 72. The final alleged character
string also hex-encodes the ASCII header rather than leaving it as ASCII.
Given its stated key and padding, even using the printed A0072 header does not
reproduce the published ciphertext A8974C06...5F49A542 or MAC D61A8A8B:
independent computation gives F5161ED9...1DFE22F3 and 03FEF86B instead.
Thus this is not merely a corrected length field in an otherwise valid vector.

The derived fixture uses `A0088P0TE00E0000`, preserves the ANSI key and padding,
and recomputes ciphertext 7DD4DD95...51EB4CA and MAC 6E3552DB. These outputs are
NOT published ANSI expected values. For A and C, independent verification uses
KBPK XOR 45 per byte for KBEK, XOR 4D per byte for KBAK, header's first eight
ASCII bytes as encryption IV, and leftmost four bytes of the final TDEA-CBC block
over header plus binary ciphertext with zero IV. No extra primitive padding.

## Unresolved header-table conflicts (no policy implemented)

- Table 2 allows A for B0 with mode X, and for E0-E6/F0-F6 with mode X.
  Table 4 excludes mode X for A. Do not infer historical exceptions or silently
  select the intersection as a resolved normative policy. Resolve with a reliable
  clarification before implementing these combinations.
- Table 4 labels mode T but prints hex 0x55 (ASCII U). Preserve this discrepancy
  in the audit rather than treating the hex column as a new supported mode.
- A and C share cryptography but not attribute permissions. Table 3 allows D/T
  under A, adds E/H/R/S under C; algorithm A is D-only. Table 2 restricts P2 to
  B/D, not C. Future validation must represent these independent restrictions.
- Version alone is not an indicator of supported optional-block types (Table 1).
- Current AES property allowlists are bypassed by raw `#BLKH`; installing them
  in the active path would change behavior (for example P0 is missing there).

No existing or new strict validation was introduced. These notes document
unresolved requirements; they do not grant permission to reject existing D inputs.
