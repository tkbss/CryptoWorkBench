# Temporary official Prism integration (A12-05, corrected by A12-05-F2)

This adapter is only for CryptoWorkBench until the official Prism release with Avalonia 12 support. The owner's reported support exception ends at that release, announced for November 2026. The subsequent switch to the official compatible NuGet pair is mandatory.

## Prepare and build

Prerequisites: Git, PowerShell 7 (`pwsh`) on PATH, and .NET SDK `10.0.401`, as required by `global.json` with `rollForward: disable` and `allowPrerelease: false`. The Prism CI wrapper verifies the exact SDK version. SDK updates must be coordinated with `global.json`, the workflow SDK installation and the wrapper verification. From the repository root:

```powershell
pwsh -NoProfile ./eng/prism/Build.ps1 -Configuration Release
pwsh -NoProfile ./eng/prism/Build.ps1 -Configuration Debug
```

Both solutions are restored in locked mode and built. The root solution includes the full Core tests; the Avalonia solution includes GUI, desktop, MCP and the four upstream Prism projects. The application remains net10.0 / Avalonia 12.1.4. Prism's original frameworks and its own Avalonia 12.1.1 compilation dependencies are unchanged.

`Prepare-Prism.ps1` fetches official `https://github.com/PrismLibrary/Prism.git` commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` into ignored `.deps/prism`. Existing checkouts are never reset, patched or overwritten. Every preparation checks HEAD, origin, clean Git status and all pinned file contents independently of Git index flags and configurable clean filters. Only CRLF-to-LF normalization is accepted in addition to exact blob bytes, to accommodate the official text attributes.

Ignored extra files are rejected except inside the four actual Prism project roots' `bin/` and `obj/` output directories. For example, `src/Prism.Events/Debug/Extra.cs` is rejected despite the upstream Git ignore rule. Symlinks/junctions in the checkout are rejected. Before each non-design-time Prism `CoreCompile`, the external target repeats verification and validates the actual Compile list: only pinned source files and the SDK-declared AssemblyInfo, GlobalUsings and target-framework attributes files under the current project's `obj/` are accepted. A stray `obj/Extra.cs` can remain an excluded output, but explicitly compiling it fails. No official Prism file is edited.

This is a source/build-input consistency check, not a sandbox for hostile SDKs, NuGet packages or concurrent modification by another process. Standard generated NuGet imports and SDK outputs remain part of the trusted build environment. Keep source and build processes under the owner's control.

## Scoped CLI and IDE environment

Use a new PowerShell process, particularly after upgrading from the original A12-05 hooks:

```powershell
. ./eng/prism/Enter-Prism.ps1
try {
    dotnet restore ./CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --locked-mode
    dotnet build ./CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln --no-restore
} finally {
    . ./eng/prism/Leave-Prism.ps1
}
```

Without an explicit Configuration, the intended Debug locks are selected. `-c Release` / `-p:Configuration=Release` select only Release locks. Both outer and inner multi-target evaluations use these paths. Missing locks fail with an explicit message; inconsistent/corrupt locks fail in NuGet's locked restore. No lock is implicitly created to replace a missing reviewed lock. Only Debug and Release are supported.

Enter sets exactly two process-local hook variables: `CustomBeforeDirectoryBuildProps` imports `Lock.props`; `CustomAfterDirectoryBuildProps` imports `PrismSecurity.targets`. The early hook sets scoped versioning policy before upstream props add package references. The after-props hook disables automatic packing in both outer and inner builds. `ProjectScope.props` matches normalized full project identity against the nine intended CryptoWorkBench projects and four precise Prism project paths. Filename collisions and unrelated projects are excluded. The Security pin is restricted to those four Prism paths: `Microsoft.Build.Tasks.Git [10.0.303]`, PrivateAssets=all. NuGetAudit remains true/all for the intended projects.

Enter does not set global process values for versioning, packing or audit, and does not rewrite `NUGET_PACKAGES`. A caller's explicit cache path is normalized only as a scoped MSBuild `NuGetPackageRoot`. Existing conflicting definitions of either required hook cause an error before any environment change; use a separate clean shell instead of overwriting them. Other hook definitions are retained. Legacy A12-05 hook variables are rejected with instructions to start a new shell. Enter is idempotent; Leave restores the previous values and preserves later deliberate caller changes. Build.ps1 also restores its environment on success or failure when it prepared the session itself. Environment changes are never persisted to the user or machine profile.

For Visual Studio, launch a **new IDE process** from Developer PowerShell after Enter:

```powershell
. ./eng/prism/Enter-Prism.ps1
devenv ./CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln
# After closing the IDE:
. ./eng/prism/Leave-Prism.ps1
```

For Rider, use its installed launcher from the prepared PowerShell session, for example `& /path/to/rider.sh ./CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln`. An already running IDE does not acquire this environment; close its existing instance for this solution first. Leave affects the shell and future children, not an already running IDE. Rider must use the installed .NET 10 SDK. Unprepared application restores/builds fail with preparation instructions. Opening the temporary Prism checkout separately outside this environment is unsupported.

## Regression checks, locks and future CI

Run the focused F01/F02/F03 checks in a new process with a new evidence directory:

```powershell
pwsh -NoProfile ./eng/prism/Test-Integration.ps1 -EvidenceDirectory artifacts/prism-regression-new
```

The script creates an isolated fixture and an official fresh checkout. It checks default/Debug/Release locks, outer/inner evaluation, missing/corrupt locks, foreign projects and name collisions, preserved hooks/environment, missing hooks, cleanup on build failure, rejected ignored source and allowed output directories. It never modifies the real product locks or existing official source files. The evidence directory must not already exist.

Keep `eng/prism/locks/Debug/*.lock.json` and `Release/*.lock.json` with the integration. Their existing package hashes and upstream frameworks are unchanged by F2. The Release Bootstrap.lock.json is historical validation provenance, not a product project in the scope allowlist. New validation probes use their own local lockfiles. NuGet.Config selects nuget.org only.

For an intentional dependency update, retain an existing lock at the expected path, restore with explicit Configuration and `-p:RestoreLockedMode=false --force-evaluate`, review the complete diff, then repeat locked validation. Adding a project requires an explicit allowlist entry and a reviewed lock prepared separately; the normal build does not bootstrap missing locks. Do not bypass locked mode in CI.

A future authorized Actions matrix can install Git/PowerShell/.NET on Windows, Linux and macOS and run Build.ps1 plus the required suites/platform smokes. Cache keys must include OS, SDK, Prism SHA and both lock sets. No workflow is installed or changed here. Linux/macOS and Rider execution for this new candidate remain OPEN; historical A12-03 Linux evidence used different Prism packages.

## Remove after official release

1. Verify official matching Prism.Avalonia and Prism.DryIoc.Avalonia versions, provenance and Avalonia-12/.NET-10 compatibility with the owner.
2. Replace the two source ProjectReferences with PackageReferences and remove the four TemporaryPrism solution entries/folder.
3. Remove the temporary root guard and eng/prism adapter, including scope, preparation, integrity and environment hooks. Leave/restart prepared shells and IDEs. Remove `.deps/prism` only after checking its contents.
4. Replace this lock setup with permanent application lock management; regenerate and review both configurations, Core/Events/Container consistency, runtime graph, hashes and audit. Retain NuGetAudit.
5. Repeat full Debug/Release builds, GUI/Core/MCP and Coordinator/F1/C1 suites, region navigation, IDialogService lifecycle checks and native platform smokes. Record the official versions and retain all historical A12 evidence.
