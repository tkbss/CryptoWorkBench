# Temporary local Prism NuGet packages (P1)

CryptoWorkBench consumes four **local-only** packages built from unchanged official Prism commit
`358118cd640d9a22ff8cf21c8ad197fa038b7990`. This temporary integration relies on the owner's reported
license exception. It grants no new license and must be replaced by owner-approved official
Avalonia-12-compatible packages. Never publish these packages to public or external feeds.

## One-time bootstrap

Requirements: Git, PowerShell 7.4+, exact .NET SDK 10.0.401. From the repository root, in a clean process:

```powershell
pwsh -NoProfile -File ./eng/prism/Bootstrap-Packages.ps1
```

The script creates a **new** ignored `.deps/prism-packaging` checkout if absent, verifies its origin, exact
commit, 1,180 pinned files, extra inputs and links, restores existing reviewed upstream locks, and
compiles only net10.0 Release outputs. Existing checkouts are never reset or patched. External
bootstrap-only MSBuild policy retains the Microsoft.Build.Tasks.Git 10.0.303 security pin and validates
the actual Compile list. No official Prism source/project is modified.

Four canonical ZIP packages are written to `.packages/prism` only after they match
`package-digests.json`. The p2 recipe fixes ZIP metadata, checkout line endings and generated resource text. It disables debug symbols and Source Link for the packaged DLLs. Each CI runner checks the same archive and assembly hashes before installing its local feed. Each package contains the original DLL, unchanged upstream LICENSE and provenance.
PDB/source packages and build-only upstream dependencies are not distributed.

Repeated bootstrap validates the feed and returns without rebuilding Prism. An incomplete or
altered existing feed fails. After inspection, explicitly use `-Rebuild`, or generate into a new
`-FeedDirectory`. `-CandidateManifest <path>` is for deliberate package-recipe maintenance only:
it produces candidate evidence instead of silently updating reviewed digests. Review all package
hashes, MSBuild digest inputs and application locks before adopting a changed recipe/version.

## Ordinary development and Visual Studio 2026

After bootstrap, open `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` directly from Explorer
or Visual Studio. Enable the IDE's normal automatic NuGet restore, select
`CryptoWorkBenchAvalonia.Desktop` as startup project and Debug/Any CPU, then F5.
No prepared shell, Enter-Prism.ps1, Build.ps1, Prism source project, global MSBuild hook or global
NuGet configuration is required. The local feed must exist before the first solution restore. On Windows, use a short checkout path if Visual Studio reports a missing file deep inside the NuGet cache.

```powershell
dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --locked-mode
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Debug --no-restore
dotnet restore CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --locked-mode -p:Configuration=Release
dotnet build CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln -c Release --no-restore
dotnet run --project CryptoWorkBenchAvalonia.Desktop -c Debug --no-build
```

The Core solution `CryptoWorkBench.sln` works with the same ordinary commands. Application locks
remain configuration/RID-specific under `eng/prism/locks`. NuGetAudit stays enabled in all mode.
NuGet.Config selects the repository-local cache `.packages/cache`, maps only the temporary package
prefix to the local feed and uses nuget.org for other packages and auditing. It does not modify the
user's NuGet.Config. Source mapping is supplemented by checked package SHA-256, locked restore and
resolved Prism DLL hash validation, including a warm cache.

The four temporary packages use version `1.0.0-prism.358118cd640d.p2`:

| Package ID suffix after CryptoWorkBench.Temporary.Prism. | Assembly |
|---|---|
| Events | Prism.Events.dll |
| Core | Prism.dll |
| Avalonia | Prism.Avalonia.dll |
| DryIoc.Avalonia | Prism.DryIoc.Avalonia.dll |

Assembly identities and namespaces are unchanged. Prism.Container.Abstractions and
Prism.Container.DryIoc remain official 9.0.114 packages; they do not pull official counterparts of
these four packages. See [dependency graph](../../docs/prism/P1-dependency-graph.json).

## Validation and CI

```powershell
pwsh -NoProfile -File ./eng/prism/Test-LocalPackages.ps1
pwsh -NoProfile -File ./eng/prism/Test-Regression.ps1 -Configuration Debug
pwsh -NoProfile -File ./eng/prism/Test-Regression.ps1 -Configuration Release
```

Run package fault probes without another build or IDE restore in progress: they temporarily alter
one local feed package and one resolved cache DLL, restoring both in finally blocks. Do not terminate
the probe process during those checks. The script rejects stale backup files instead of deleting them.

The existing four-platform CI now bootstraps local packages before normal builds and runs the
package integrity probes after the Debug build, before the Release build.
Native macOS shader/shim and publish gates remain intact. See [integration PR #10](https://github.com/tkbss/CryptoWorkBench/pull/10) for current CI and acceptance results.
The original Windows-only prototype results are archived in [the P1 report](../../docs/prism/P1-temporary-local-nuget.md). B4's historical CI success is not P1 CI evidence.

For an intentional dependency update only, `Refresh-ApplicationLocks.ps1` regenerates Debug,
Release and four RID lock sets. Review every diff, then repeat locked builds and regressions.
It is never part of normal restore, bootstrap or CI.

## Security boundaries and replacement

These checks bind the recipe and packages to reviewed inputs; they do not sandbox an adversarial
SDK, compiler, package cache writer, MSBuild customization or simultaneous source mutation.
Protect the repository and build environment. NuGet audit does not assign official Prism advisories
to renamed local package IDs; the pinned upstream source, official dependencies and subsequent
official replacement must still be reviewed.

Keep upstream LICENSE unchanged; the reported exception covers compilation and assembly use with
CryptoWorkBench, not public redistribution of unofficial NuGet packages. No private certificates,
credentials or signing keys are packed.

Rollback is an explicitly reviewed return to the baseline source integration, including its matching
locks and process policy; do not mix the old ProjectReferences with local package references.
When official compatible packages are available and approved, replace the two top-level temporary
PackageReferences, remove the local source mapping/feed and packaging/hash guards, regenerate and
review all locks, and repeat Windows/CI/native acceptance. Do not delete unrelated existing checkouts
or shared caches. Historical evidence remains unchanged.
