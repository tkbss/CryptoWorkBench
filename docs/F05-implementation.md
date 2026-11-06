# F05 implementation and acceptance report

Date: 2026-10-24. Working directory: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench`.
Starting branch: `master`; HEAD: `1ee2e25feeae6e4c1776e9b6b84a625c0e92708a`.
The pre-existing untracked `.codex/` directory was left untouched. No commits,
pushes or merges were performed. This report concerns the uncommitted working tree.

## Implemented contract

| Mechanism | Key bits | File padding |
|---|---|---|
| AES-CBC | 128/192/256 | All nine |
| AES-ECB | 128/192/256 | NONE |
| AES-CTR | 128/192/256 | None |
| AES-GCM | 128/192/256 | None; 16-byte authentication tag |
| AES-CCM | 128/192/256 | None; 16-byte authentication tag |
| DES3-CBC | 128/192 | All nine |
| DES3-ECB | 128/192 | All nine |

The nine paddings are NONE, PKCS-7, ANSI-X923, ISO-10126, ISO-7816,
ISO-9797-M1, ISO-9797-M2, ISO-9797-M3 and TLS-CBC. The exact schema, field order,
binary encodings, length representations, AAD construction, mode limits and
padding semantics are specified in [F01-container-format.md](F01-container-format.md).

## Architecture and streaming

F05.1: the codec validates mechanism-dependent metadata, preserving magic and 1.0
framing. Seven common fields remain mandatory; optional fields are allowed only
where specified. M1 length is a decimal string. The raw input frame is retained.

F05.2: FileCipherParameters resolves existing key diagnostics and parameter
provenance. FileCipherStream processes block modes with Bouncy Castle AES/TDEA
and CBC primitives, reusing existing CryptoScript suffix-padding classes.
Input/output arrays are 64 KiB. Encryption retains one partial block; decryption
retains up to 256 bytes for the full existing TLS unpadding contract. M3's length
block is processed separately. M1/M3 validate the expected data-region length
and all removed zeros. DES quality warnings and weak-key analysis behavior remain.

F05.3: CTR uses platform AES ECB, a continuous four-byte big-endian counter, and
a fixed nonce. The number of required blocks is checked before processing; no
wrapped counter is used, including on the last partial block.

F05.4: GCM uses Bouncy Castle's streaming API, with a 2^36-32-byte plaintext
limit. Its original complete frame is passed as AAD. FileCcmStream uses platform
AES ECB for length-aware CCM composition: B0, encoded AAD, CBC-MAC of plaintext,
CTR blocks S1 onward and tag masking with S0. Encryption/decryption need one pass
because the source handle exposes length up front; no plaintext reread or packet
array is needed. CCM q=15-nonceLength is checked before reads. Counter and MAC
state are fixed-size, and the final tag is compared in constant time.

The existing [Bouncy Castle CCM implementation](https://github.com/bcgit/bc-csharp/blob/master/crypto/src/crypto/modes/CcmBlockCipher.cs)
is a packet implementation whose ProcessBytes stores data in MemoryStream; it is
not used for PATH CCM. The existing VAR CCM implementation remains unchanged.
The bounded CCM composition is checked against independent OpenSSL fixtures and
platform AesCcm, including every valid nonce length.

F05.5: reproducible references and matrix, fault, tampering and memory tests are
included. See [F05-reference/README.md](F05-reference/README.md) for reproduction.
No Core dependency on Avalonia was added; no F03/F04 or CWM production code changed.

## Parameters and compatibility

PARAM values and explicit-name lists are borrowed, never changed by file operations.
Automatic IVs/nonces are refreshed per encryption. Explicit values are preserved.
Decrypt compares explicit PAD/IV/NONCE/COUNTER/ADATA with stored values; automatic
defaults do not conflict. Unsupported/duplicate parameters fail.

Legacy AES parameter factories normalize explicit ECB/GCM padding. A private
OriginalExplicitPadding provenance value records the requested padding before
normalization so PATH rejects invalid requests while VAR behavior stays intact.
The snapshot format is unchanged: restored snapshots describe stored effective
parameters, not a discarded original declaration. Empty legacy default IV and
NONE placeholders are accepted where the factories emit them.

Intentional differences from VAR: PATH M1 restores the original length; AEAD AAD
is the CWE frame containing #ADATA; file CTR rejects wrap. ISO-10126 encryption
uses fresh random filler. AES-ECB accepts empty input; CBC/DES3 NONE rejects it.
All deterministic fixture ciphertexts match VAR with the same effective inputs.

## Security and publication

Both coordinators retain the handle-bound FilePublication path. They create
private temporary files, flush completed output and atomically publish without
replacement. GCM/CCM never publish until tag verification succeeds. On failures,
temporary output is removed through the existing cleanup path and no result PATH
is returned or registered. Cancellation is checked across chunks/short reads and
before publication; a completed publication wins late cancellation.

Controlled input/output/tail buffers and decoded key copies are cleared in finally
blocks. Platform cipher handles are disposed. Bouncy Castle's managed expanded-key
state has no disposable zeroization API, so no complete process-memory erasure is
claimed. Temporary-file deletion does not securely erase storage, swap or caches.

ECB, CBC, CTR and DES3 are unauthenticated reference/legacy modes, not preferred
modern file encryption. AEAD still requires appropriate key lifecycle and nonce
uniqueness; explicit IV/nonce reuse is the caller's responsibility. Metadata is
public. The JSON bound also limits #ADATA/nonce size; oversized values fail.

Windows holds source handles denying write/delete sharing. Unix may allow
same-length concurrent writes, which cannot reliably be detected. Observed
truncation/growth fails. Linux publication requires procfs/hard links; macOS
requires a trusted target directory under the existing F01/S02 boundary. Secure
erasure, hostile equal-privilege writers and universal crash durability are not
claimed. See the container specification for full platform detail.

## Independent review

A separate reviewer inspected the working-tree implementation, schema, padding,
parameter handling, AEAD, streaming and tests. One Medium finding (legacy factory
overwriting invalid explicit AES-ECB padding) was fixed, with script-level tests
for ECB and GCM. Re-review reported no remaining confirmed Critical/High/Medium
findings and independently passed 45 validation and two script regression tests.
This is an independent code review, not formal cryptographic certification.

## Validation and platform status

Local validation was executed on Windows x64 with .NET SDK 10.0.401.
Both Solutions build in Release using `-m:1 -nr:false`; the local parallel MSBuild
attempt failed without a compiler diagnostic, while serial builds succeeded.
Existing nullable warnings remain. NU1900 reports unavailable NuGet vulnerability
metadata; dependency versions were not changed.

The existing GitHub Actions matrix already covers Windows x64, Linux x64, macOS
x64 and macOS ARM64 and executes complete suites. No remote run can validate this
uncommitted tree. The user's no-push instruction therefore leaves all four remote
platform results pending. Local Windows success must not be read as cross-platform
approval. F05 is not fully accepted until those required remote checks succeed.

### Local results

| Check | Result |
|---|---|
| CryptoWorkBench.sln, Release | Passed |
| CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln, Release | Passed |
| Complete Core suite, including F01–F04 | 11,399 passed, 24 skipped, 0 failed |
| Complete Avalonia GUI suite | 248 passed, 0 skipped, 0 failed |
| New F05 cases (included in Core) | 701 passed: 478 independent fixtures, 177 file tests, 46 validation tests |
| File matrix within those tests | 150 mechanism/key/padding/default configurations, each checks nine lengths |
| Independent reference regeneration | Identical SHA-256 after regeneration |
| Git whitespace/diff validation | Passed |

The 24 Core skips cover unavailable symbolic-link privilege, macOS-only native
operations and an optional hardware measurement; they are not claimed as tested.
The adapter's TRX total can include an extra undispatched/explicit test; the console
summary above reports executed/discovered run counts. Local result files are
`artifacts/f05/F05-Core.trx` and `artifacts/f05/F05-Gui.trx` (ignored build artifacts).

Measured managed allocations for one 64 MiB streaming encryption after warm-up:
CTR 66,952 bytes, GCM 134,040 bytes, CCM 67,080 bytes. The existing 64 MiB CBC
decryption test measured 217,840 bytes. These are allocation measurements for the
controlled managed path, not OS working-set, disk-cache or native-memory guarantees.

Generated `vectors.json` (UTF-8/LF) SHA-256:
`3d5d912669c17dd611bb33eff1b1ef096dd48d56b5f40e5cb7d4d878785d3f54`.

Reproduce local validation:

```powershell
dotnet build CryptoWorkBench.sln -c Release --no-restore -m:1 -nr:false
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Release --no-restore -m:1 -nr:false
dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj -c Release --no-build --no-restore
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj -c Release --no-build --no-restore
```

### Changed and new files

- `CryptoScript/CryptoAlgorithm/AES/AES.cs`
- `CryptoScript/FileEncryption/EncryptedFileCodec.cs`
- `CryptoScript/FileEncryption/FileCcmStream.cs`
- `CryptoScript/FileEncryption/FileCipherParameters.cs`
- `CryptoScript/FileEncryption/FileCipherStream.cs`
- `CryptoScript/FileEncryption/FileDecryptCoordinator.cs`
- `CryptoScript/FileEncryption/FileEncryptCoordinator.cs`
- `CryptoScript/InfoDocs/Info.Func.Decrypt.md`
- `CryptoScript/InfoDocs/Info.Func.Encrypt.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-CBC.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-CCM.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-CTR.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-ECB.md`
- `CryptoScript/InfoDocs/Info.Mech.AES-GCM.md`
- `CryptoScript/InfoDocs/Info.Mech.DES3-CBC.md`
- `CryptoScript/InfoDocs/Info.Mech.DES3-ECB.md`
- `CryptoScript/InfoDocs/Info.Padding.ANSI-X923.md`
- `CryptoScript/InfoDocs/Info.Padding.ISO-10126.md`
- `CryptoScript/InfoDocs/Info.Padding.ISO-7816.md`
- `CryptoScript/InfoDocs/Info.Padding.ISO-9797-M1.md`
- `CryptoScript/InfoDocs/Info.Padding.ISO-9797-M2.md`
- `CryptoScript/InfoDocs/Info.Padding.ISO-9797-M3.md`
- `CryptoScript/InfoDocs/Info.Padding.NONE.md`
- `CryptoScript/InfoDocs/Info.Padding.PKCS-7.md`
- `CryptoScript/InfoDocs/Info.Padding.TLS-CBC.md`
- `CryptoScript/Variables/ParameterVariableDeclaration.cs`
- `CryptoScriptUnitTest/CryptoScriptUnitTest.csproj`
- `CryptoScriptUnitTest/F05FileTests.cs`
- `CryptoScriptUnitTest/F05ReferenceTests.cs`
- `CryptoScriptUnitTest/F05ValidationTests.cs`
- `CryptoScriptUnitTest/FileEncryptionTests.cs`
- `docs/F01-container-format.md`
- `docs/F05-implementation.md`
- `docs/F05-reference/README.md`
- `docs/F05-reference/generate.py`
- `docs/F05-reference/vectors.json`
