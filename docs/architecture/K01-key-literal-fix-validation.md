# K01 — KEY literal declaration validation

Date: 2026-10-21. Repository: tkbss/CryptoWorkBench, branch master.
Baseline: 8b3716d93f762b6631e72027e34ecaabe4ea95e5.
Local validation: Windows x64, .NET SDK 10.0.401.

## Cause and correction

Expression.BuildVariable previously created StringVariableDeclaration objects
for HEX and Base64 expressions regardless of declared type. A KEY literal was
therefore stored as a VAR runtime class carrying CryptoTypeKey. Typed crypto
argument resolution rejected that object, as did S01's class/type consistency
check.

The correction is local to declaration construction:

- When a KEY is initialized from an expression, BuildVariable constructs a
  KeyVariableDeclaration before adding it to VariableDictionary.
- Only nonempty whole-byte HEX and valid Base64 binary literals are accepted.
  The complete contents inside the literal delimiters are decoded using
  Convert.FromHexString / Convert.FromBase64String. Original spelling, leading
  zeros, material and format are preserved.
- Decoded byte count determines KeySizeInBits; the existing model setter keeps
  KeySize consistent. Temporary decoded bytes are cleared in a finally block.
- Existing defaults remain Secret(Unknown), Unspecified usage, empty derivation
  mechanism and empty attributes. There is no algorithm inference from length.
  Kcv, Kcvc and Kcvk consequently remain null.
- Malformed or unsuitable literals are translated by VariableDeclarationEvaluator
  into a sanitized InvalidDeclaration semantic diagnostic. No source literal,
  decoder exception payload or inner exception is returned. A missing KEY
  initializer is also rejected. Validation finishes before dictionary insertion,
  so failed declarations preserve any existing variable with the same identifier.
- Function-result declarations retain their existing path. VAR, GenerateKey,
  AlgorithmArgumentResolver, CryptoType, KeyVariableDeclaration and
  KeyMetadataValidator are unchanged.

An Unknown key is usable when an existing mechanism supplies the algorithm and
the bytes satisfy its technical requirements. The validator already treats
Unknown metadata as unspecified; it was not relaxed. AES still rejects byte
lengths other than 16, 24 and 32. AES and HMAC operations do not change the key's
metadata or infer its algorithm.

## Persistence compatibility

S01 and S02 implementation and test files are unchanged. Literal KEYs now meet
their existing contracts and can be exported through the actual Save function.
The existing version 1 JSON format is unchanged: the stored metadata type is
KEY, Unknown is retained, material/format/bit size are preserved, and all three
check values remain null. A detached key read through S02 can be explicitly
registered by the host and used for AES through the public interpreter.
No Load, SecureSave or implicit publication was added.

The former negative S03 test is replaced by a positive Save roundtrip. The
limitation in Info.Func.Save.md is removed and Info.Types.md documents literal
KEY metadata and mechanism-selected use. The S03 validation report remains the
historical record of its original baseline; its KEY-literal limitation is
superseded by this correction.

## Changed files

Production:

- CryptoScript/Expressions/Expression.cs
- CryptoScript/Model/VariableDeclarationEvaluator.cs

Tests:

- CryptoScriptUnitTest/KeyLiteralDeclarationTests.cs (new)
- CryptoScriptUnitTest/SaveFunctionTests.cs (previous negative literal test)
- CryptoScriptUnitTest/SymmetricAlgoGrammarTests.cs (two obsolete class assertions)

Documentation:

- CryptoScript/InfoDocs/Info.Types.md
- CryptoScript/InfoDocs/Info.Func.Save.md
- docs/architecture/K01-key-literal-fix-validation.md (new)

No grammar/generated-code, cryptographic algorithm, S01/S02, Avalonia or
configuration changes. No KeyValue property was introduced.

## Tests and results

All results below are actual local executions.

| Check | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| K01 + Save targeted suites | 99 | 0 | 0 | 99 |
| Complete Core suite | 7,320 | 0 | 4 | 7,324 |
| Complete GUI suite | 225 | 0 | 0 | 225 |

The targeted run contains 45 new K01 cases and 54 Save cases. All new cases
execute declarations through the public parser, AST mapping and CryptoScriptRunner.
Coverage includes:

- HEX lengths 8, 64, 128, 192, 256 and 512 bits, mixed-case material and leading zeros.
- Base64 lengths 1, 2, 3, 16, 24 and 32 decoded bytes, including padded encodings.
- Runtime class, declared type, both size properties, format, material, defaults,
  source statement, dictionary identity and unavailable KCV values.
- Odd HEX, malformed Base64, strings, numeric/path/composite TR31 literals and
  missing initializers. Each case checks both a new identifier and an existing
  identifier, unchanged dictionary membership/source state and sanitized diagnostics.
- Unchanged VAR literals and GenerateKey import/random-generation behavior.
- AES-CBC encrypt/decrypt for all three valid AES lengths with both HEX and
  Base64 inputs; complete metadata snapshots remain unchanged.
- AES-128 FIPS 197 known-answer test, independently asserting both encryption
  and decryption against fixed ciphertext/plaintext, not only mutual roundtrip.
  Key: 000102030405060708090A0B0C0D0E0F.
  Plaintext: 00112233445566778899AABBCCDDEEFF.
  Ciphertext: 69C4E0D86A7B0430D8CDB78070B4C55A.
  The same reference vector is used by the existing KeyMetadataDiagnosticTransportTests.
- AES rejection of 1, 8, 15, 17, 31 and 33-byte keys for both Encrypt and Decrypt,
  without publishing a result or changing KEY metadata.
- Existing HMAC-SHA256 behavior with an Unknown key, using RFC 4231 case 1:
  twenty 0b bytes, message "Hi There", expected MAC
  b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7.
- Save/S01/S02 roundtrips for HEX and Base64, stored KEY type, exact source
  preservation and successful AES reuse of the restored object.

File tests use exclusively newly created, uniquely named temporary directories,
removed in teardown. Tests restore the original variable dictionary. The
path-literal rejection test performs no filesystem access at its example path.

The first full Core run exposed two old assertions in SymmetricAlgoGrammarTests
that explicitly expected the incorrect StringVariableDeclaration runtime class.
They now require KeyVariableDeclaration and additionally assert Unknown and the
correct 16-bit size for their original 0x(1234) fixture. The entire Core suite was
then rebuilt and rerun successfully.

The four unchanged local skips are:

- MacOsInheritedExtendedAclIsRemovedBeforePublication: macOS-only.
- NativePublicationDoesNotFollowLateSymlink: Windows symlink privilege unavailable.
- TargetSymlinksIncludingDanglingLinksAreRejected(False): same privilege.
- TargetSymlinksIncludingDanglingLinksAreRejected(True): same privilege.

Validation commands, all successful in their final execution:

    dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --filter "FullyQualifiedName~KeyLiteralDeclarationTests|FullyQualifiedName~SaveFunctionTests" --logger "trx;LogFileName=K01-targeted.trx" -v minimal
    dotnet test CryptoScriptUnitTest/CryptoScriptUnitTest.csproj --no-restore --logger "trx;LogFileName=K01-core.trx" -v minimal
    dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --no-restore --logger "trx;LogFileName=K01-gui.trx" -v minimal
    dotnet build CryptoWorkBench.sln --no-restore -m:1 -v minimal
    dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore -m:1 -v minimal
    git diff --check

Both solution builds succeeded with zero errors and zero warnings in their final
incremental output. The GUI test rebuild emitted the nine existing nullable
warnings in unchanged Avalonia files; no warnings were suppressed. Local logs
are in ignored artifacts/K01-*.log and TRX files in the test projects' ignored
TestResults directories.

## Independent review

Read-only independent review by k01_review covered the implementation, regression
tests, InfoDocs and this report, including verification of the result logs.
Result: no findings. No functional changes were required after review.
The final whitespace check found one trailing space in a changed grammar-test
assertion; it was removed and git diff --check then passed.

## Delivery state

Local validation only: no commit, staging, push or CI run. HEAD remains the stated
baseline. The pre-existing untracked .codex directory is untouched; its
config.toml was neither read nor modified.
