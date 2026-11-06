# Test-only Avalonia.Headless 11.3 backport

This source project replaces only the GUI test project's `Avalonia.Headless`
NuGet reference. No production project references it. It is not a published NuGet
package and contains no prebuilt DLL. Normal project-reference restore/build is
the local dependency integration; CI already restores and builds the GUI test graph.

## Origin and exact functional patch

- Repository: https://github.com/AvaloniaUI/Avalonia
- Official tag: `11.3.20`
- Exact commit: `984c3b833f29806eb159fb7f12a6132e936b75df`
- Original directory: `src/Headless/Avalonia.Headless/`
- Fix: https://github.com/AvaloniaUI/Avalonia/pull/22222
- Upstream merge: `52bbf7571f291a3515a7cc9f2cbd6ae865b37e30`
- License: MIT, verbatim upstream notice in `LICENSE.md`.

All nine upstream C# files are vendored. Only `HeadlessUnitTestSession.cs` differs:
`backport.patch` is the complete diff against the official source. It creates the
worker task before starting it on `TaskScheduler.Default`, retaining
`DenyChildAttach`, and moves queue completion after worker completion in synchronous
`Dispose`. The 11.3 class has no `DisposeAsync`; none is added. No other upstream
changes or new public APIs are included.

`upstream-files.json` records original Git blob IDs, original SHA256 and patched
SHA256 for every source file. All original blobs were verified against GitHub's
commit-pinned directory metadata at acquisition. `VerifiedInputs.props` pins build
input SHA256 values; MSBuild verifies every compiled source, license, patch,
provenance manifest and public-key file before compilation. A mismatch fails the
build. Source selection is explicit, not an uncontrolled wildcard.

The patch can be audited offline by copying `src/` to an empty scratch directory
and applying `git apply --reverse backport.patch` there. The result must reproduce
the recorded original Git blobs. Do not reverse the patch in the working source.
The scoped `.gitattributes` keeps LF bytes and preserves inherited upstream
whitespace and required unified-patch context spaces, rather than silently
reformatting the audited source or patch.

## Artifact identity and compatibility

| Property | Value |
|---|---|
| Local project/dependency ID | `Avalonia.Headless.Backport` |
| Version | `11.3.20-cwb.1` |
| Assembly name/version | `Avalonia.Headless`, `11.3.20.0` |
| Public key token | `c8d484a7012f9a8b` |
| File version | `11.3.20.1` |
| Informational version | `11.3.20-cwb.1+upstream.984c3b833f29806eb159fb7f12a6132e936b75df.pr22222` |
| Build target | `net10.0`, matching this repository's GUI testhost |

Avalonia's friend assembly declarations require its assembly name and public key
for access to internal APIs. `Avalonia.public.snk` contains only the public key
from upstream `build/AvaloniaPublicKey.props`; there is no private signing key.
`PublicSign` preserves that identity for .NET compatibility. This is **not** an
upstream publisher signature or authenticity claim: provenance is the reviewed,
vendored source, the documented patch and locked dependencies.

Avalonia's NuGet `ref/` facade deliberately prevents external implementations of
some internal contracts. Therefore this friend source project compiles against
the official package's unchanged `lib/net8.0/Avalonia.Base.dll` and
`Avalonia.Controls.dll`. Its exact `[11.3.20]` package reference excludes only
compile assets and supplies those two explicit compile references. Runtime assets
remain the ordinary Avalonia 11.3.20 NuGet assets. No assembly loader hooks,
binding redirects, runtime patches or second Headless implementation are used.

The repository uses .NET 10 for the testhost; targeting the source build at
`net10.0` avoids a new targeting-pack dependency. The vendored sources have no
framework-conditional functional changes. Local metadata comparison against the
official net8 assembly found the same 48 exported type/member entries. Production
Avalonia references and runtime package versions are unchanged.

The explicit project/package ID matters even though this project is not packed:
it gives NuGet and the deps generator one unambiguous project entry while retaining
the required assembly name. `HeadlessSessionBackportTests` asserts that the testhost
loads exactly one Headless assembly, from its output directory, with this identity
and informational version, and with the SHA256 produced by this build. It also
asserts the one `project` entry in `CryptoWorkBenchGuiTest.deps.json`.

## Build, dependency locking and reproducibility

```powershell
dotnet restore CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj
dotnet build CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --configuration Release --no-restore
dotnet test CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj --configuration Release --no-build --no-restore
```

The source build never downloads source, executes a remote script or installs a
binary patch. Initial NuGet restore uses the existing package infrastructure.
The backport dependency graph is locked, including package content hashes, in
`packages.lock.json`; locked mode is on by default. Changing that lock requires
an intentional restore override and review. All four packages already occur in
the existing Avalonia graph.

Use the repository's `global.json`. Deterministic compilation and an explicit
project-local `PathMap` remove checkout-path differences. Ambient Git/SourceLink
queries are disabled only for this project; upstream provenance is explicit in
the assembly metadata. The generated `Avalonia.Headless.Backport.sha256` is copied
to the test output with the MIT notice. It verifies which locally built bytes
are loaded; it is not a cryptographic attestation by a separate publisher.

Two builds from different paths, with SDK 10.0.401, identical sources and locked
packages, produced identical bytes locally. The second restore used an empty
offline source and the populated NuGet cache. Byte equality across different SDK
versions or OS/compiler environments has not been established; each CI runner
checks its own build/load hash. SDK roll-forward policy is the repository's
existing policy and has not been changed.

## Regression and retirement

The new test fixture verifies 10,000 immediate `StartNew`/`Dispose` lifetimes with
no sleeps/retries. An independently synchronized fault-injection test verifies
that a worker exception during shutdown is propagated by `Dispose`; no genuine
shutdown exceptions are swallowed. Reflection in that test accesses pinned private
queue/task/cancellation fields solely to inject the fault, not to implement the fix.

Remove this project and restore the official test PackageReference once an
appropriate compatible official release contains both fixes and the same stress,
matrix and complete GUI regressions pass. Do not change the production GUI's
dependency versions merely to retire this test-only backport.
