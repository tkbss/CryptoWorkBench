# G2-04-I3-CI-D1 · Independent failure analysis

**READY FOR OWNER REVIEW – CORRECTION SCOPE IDENTIFIED** · 20 November 2026

PR #9 failed its complete GUI suite on all four runners. Six Unix test failures across three jobs have one confirmed cause in a new test fixture. Windows recorded a different, previously documented Headless startup failure. Two test-helper changes have a concrete technical justification. The causal link between the Windows helper and this particular dispatcher violation remains probable. All four CI jobs remain failed.

## Baseline and original evidence

GitHub recorded run **44717123735**, attempt **1**, event `pull_request`, head SHA `d6c212c264dfab12835b6bf21c103559c34da34d`. The checkout logs identify the actual tested PR merge ref as `2b8c9a277bf9be83e2876760fe4bdcab225589ea`. PR metadata confirms one commit and 83 changed files against `32017a08c4771e73c12a50f6e1548476b9bb0a54`. All four jobs succeeded in the earlier push run on that exact parent. [PR #9](https://github.com/tkbss/CryptoWorkBench/pull/9) · [Failed run](https://github.com/tkbss/CryptoWorkBench/actions/runs/44717123735) · [Parent run](https://github.com/tkbss/CryptoWorkBench/actions/runs/43877451136)

| Runtime host | Original job | Diagnostic artifact ID | GUI console passed / failed / skipped | GUI TRX passed / failed / NotExecuted |
|---|---|---:|---:|---:|
| Windows Server 2025 x64 | [136223250614](https://github.com/tkbss/CryptoWorkBench/actions/runs/44717123735/job/136223250614) | 14621260463 | 1,276 / 1 / 0 | 1,276 / 1 / 0 |
| Ubuntu 26.04.1 x64 | [136223250741](https://github.com/tkbss/CryptoWorkBench/actions/runs/44717123735/job/136223250741) | 14620650975 | 1,259 / 2 / 8 | 1,259 / 2 / 16 |
| macOS 26.7.2 arm64 | [136223250326](https://github.com/tkbss/CryptoWorkBench/actions/runs/44717123735/job/136223250326) | 14620906771 | 1,259 / 2 / 8 | 1,259 / 2 / 16 |
| macOS 26.6.1 x64 | [136223250782](https://github.com/tkbss/CryptoWorkBench/actions/runs/44717123735/job/136223250782) | 14620428634 | 1,259 / 2 / 8 | 1,259 / 2 / 16 |

The Unix console totals are 1,269. Each TRX contains 1,277 result rows, including 16 individual `NotExecuted` outcomes. For example, Ubuntu records eight Windows platform restriction messages, five Windows chrome cases, two mandatory file-lock cases and one Windows sharing/native desktop case. Both counting surfaces agree on the identities and number of failures.

**Evidence inventory.** The isolated local evidence directory holds the original run log ZIP and extracted per-job and per-step logs, the four original artifact ZIPs and extracted trees, API responses for the run, jobs, artifacts, PR and parent jobs, plus the version-pinned upstream Headless source. In each `test-results-<rid>/` directory, `test-results/Gui.trx` contains the exact messages and stacks. The other suites' TRX, CI transcripts and native diagnostics are alongside it. The extracted JSON below preserves every failing row's message and full stack, including repeated NUnit frames. These supporting files are local. [Evidence directory](../artifacts/test-results/g2-04-i3-ci-d1/) · [Extracted failures and controls](../artifacts/test-results/g2-04-i3-ci-d1/trx-findings.json)

## Linux and macOS · ROOT CAUSE CONFIRMED

**B. Test fixture defect introduced by G2-04.** All three Unix jobs failed exactly these two NUnit cases in `CryptoWorkBenchGuiTest.PropertyInspectorCorrectionsTests`:

- `ExactHistoricalCoverage(True,False)` exercises the captured parent value.
- `ExactHistoricalCoverage(True,True)` assembles captured pages.

Both returned the same diagnostic at `Fixture.Invalidate(Boolean lease)`, line 48:

```text
Assert.That(result.Error, Is.Null)
Expected: null
But was:  <SafeError { Code = TimerUnavailable, MessageKey = mcp.TimerUnavailable }>
```

The stack goes through `ExactHistoricalCoverage` lines 94 and 98, `GuiTestSession.RunAsync` line 22, Headless dispatch and NUnit. Line 94 is the common lease-invalidation call. The assertion fails before the historical copy, status, revision and pending-work assertions. Both edit-only variants passed on every runner. All four variants passed on Windows. [Failing fixture](../CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs#L42)

1. The fixture constructs a default `WorkbenchAutomationService` at line 25. For the lease cases it activates MCP, issues an editor-control credential and calls `AcquireEditorAsync` with the registered editor and its current revision. The test provides no synthetic lease clock. The page flag changes how the value is captured before the call. Both cases use the same service setup.
2. Acquisition runs on the editor UI path, checks authorization, reserves the acquisition ID, checks editor state/revision, and reserves the interpreter. In `WorkbenchLeaseV2.cs:254` it calls `ReadTime()`. The default `LeaseMilliseconds` at lines 32–33 uses Windows `GetTickCount64` and explicitly throws `PlatformNotSupportedException` elsewhere. `ReadTime` at lines 36–46 catches the exception, latches the clock as broken, and returns null. It also guards against rollback and deadline overflow. This is deterministic on a fresh default service taking this path on either Unix platform.
3. Line 254 rejects the acquisition. The local `Reject` records the slot error, `LeaseResult` calls `SafeError.From`, and the latter constructs the `mcp.TimerUnavailable` key. The second possible rejection at line 263 handles failure to start a one-second dispatcher timer. Unix has already returned at line 254, before timer construction, UTC receipt creation or lease installation. Waiting longer, initializing another dispatcher, or adjusting CI timeouts cannot change this result. [Acquisition](../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs#L224) · [Error construction](../CryptoWorkBench.Automation.Abstractions/AutomationV2.cs#L21)

**Contract and independent control.** I2-F1 requires exact historical copying both after an edit and under an acquired lease, for parent values and captured pages. I3 integrates that behavior into the dock. The existing MCP contract requires a suspend-aware lease clock and safe refusal where unsupported. Production lease acquisition is supported on Windows within this contract. The dedicated `ProductionLeaseClockRetainsPlatformBoundaryAndGuiAdmission` control passed in **all four original GUI TRX**. On Unix it checks the clock exception, safe rejection, absent lease and continued GUI admission. The parent already contained this contract, the control, and a reusable example of a successful synthetic clock. G2-04 added the historical-copy fixture, thereby newly demanding successful production-clock acquisition on Unix. [I2-F1 acceptance evidence](G2-04-02-I2-F1-corrections.md) · [I3 integration](G2-04-02-I3-completion-report.md) · [MCP lease contract](mcp/MCP-B3-mcp-binding-lease-v2.md) · [Platform-boundary control](../CryptoWorkBenchGuiTest/LeaseV2Tests.cs#L24)

**Minimum correction.** In `CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs`, initialize the fixture's service with `LeaseMilliseconds = () => 1_000UL`, following the existing B1 host. A stable reading satisfies the monotonic contract during this short non-expiry test. This substitutes only the time source. Real authorization, acquisition, editor control, historical copy and cleanup still execute, with all four parameterizations and their assertions intact. The separate production-clock control must continue to construct the default service. [Existing fixture precedent](../CryptoWorkBenchGuiTest/B1EditorIntegrationTests.cs#L46)

## Windows · ROOT CAUSE PROBABLE

**Previously known intermittent infrastructure defect.** The sole failure was `CryptoWorkBenchGuiTest.DockVisualCorrectionTests.ShortPrintIsVisibleAndSourceButtonNavigates(911,512)`. Exact exception:

```text
System.InvalidOperationException : The calling thread cannot access this object because a different thread owns it.
```

The stack runs from `Dispatcher.<VerifyAccess>g__ThrowVerifyAccess|17_0` through `DefaultRenderLoop.Add`, `ServerCompositor..ctor`, `Compositor..ctor`, `AvaloniaHeadlessPlatform.Initialize`, `AppBuilder.SetupUnsafe`, `HeadlessUnitTestSession.EnsureIsolatedApplication`, and `DispatchCore` into NUnit's task adapter. The original Windows stack supplies method names without PDB line numbers. In the official 12.1.4 source, dispatch initializes the isolated application at line 103, resets the dispatcher and calls setup at lines 202–203, and first invokes the user delegate at line 118. Thus the 911×512 callback at `DockVisualCorrectionTests.cs:66` never reached window sizing, print, rendering assertions or navigation. The other two dimensions passed on Windows. [Original Windows TRX](../artifacts/test-results/g2-04-i3-ci-d1/test-results-win-x64/test-results/Gui.trx) · [Pinned upstream implementation](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs)

**Confirmed lifetime error in the fixture.** `DockVisualCorrectionTests.Run` at lines 18–19 passes a resultless async lambda to `Session.Dispatch`. The official API has `Action`, `Func<T>` and `Func<Task<T>>` overloads. The lambda binds as `Func<Task>` to the synchronous generic overload with `T = Task`. The resulting `Task<Task>` is returned as plain `Task`. NUnit can observe completion while the inner callback, late assertions or asynchronous host disposal are still outstanding. Headless can dispose the isolated application before that inner work completes. G2-03 already diagnosed this exact overload pattern in a different fixture and introduced `GuiTestSession.RunAsync` with an awaited, result-bearing callback. The official-package identity test passed in every CI job. [Failing fixture wrapper](../CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs#L18) · [Existing helper](../CryptoWorkBenchGuiTest/GuiTestSession.cs#L18) · [Prior gated negative and positive controls](gui/G2-03-I2-P3-arm64-execution-busy.md)

**What ties it to this run.** The fixture is nonparallelizable and uses one lazy assembly session, a dispatch queue and isolated applications per dispatch. NUnit worker selection alone does not explain a wrong owner inside platform setup. Serialization of outer tasks cannot serialize abandoned inner tasks. Windows TRX timestamps put the 1200×760 case immediately before 911×512, with about 24 microseconds between reported end and next start. Both use the same wrapper and asynchronous host. 640×420 then passed. The ordering and lifetime gap support cross-test interference with dispatcher/render-loop state, but the logs do not identify the thread that established ownership, a surviving callback, or which predecessor caused the mismatch. Earlier records include a serial occurrence. Worker-count changes and automatic retries would not establish a fix. [G2-02 acceptance history](gui/G2-02-final-acceptance.md) · [G2-03 recurrence](gui/G2-03-03-theme-implementation.md) · [G2-04 R4 original failure and successful repeats](G2-04-02-I3-R4-targeted-final-review.md)

**Controlled reproduction.** One fresh Windows VSTest process ran the six unchanged Release cases in `DockVisualCorrectionTests`, including all three ShortPrint dimensions. Exit 0, six reported passed, zero failed or skipped. The exception did not reproduce. The existing wrapper limits what those passes establish about late assertions. No source was rebuilt. From the isolated evidence directory, the command was:

```powershell
$dll = (Resolve-Path '../../../CryptoWorkBenchGuiTest/bin/Release/net10.0/CryptoWorkBenchGuiTest.dll').Path
dotnet vstest $dll '--TestCaseFilter:FullyQualifiedName~DockVisualCorrectionTests' '--Logger:trx;LogFileName=shortprint-sequence.trx' '--ResultsDirectory:local-windows'
```

The local TRX lists each identity. The original failed CI outcome and the successful local sequence are retained separately. Unix findings use the original three runner executions. [Local log and exit code](../artifacts/test-results/g2-04-i3-ci-d1/local-windows.log) · [Local sequence TRX](../artifacts/test-results/g2-04-i3-ci-d1/local-windows/shortprint-sequence.trx)

**Minimum correction.** Route only `DockVisualCorrectionTests.Run` through the existing `GuiTestSession.RunAsync`. Keep its awaited host disposal, test body and assertions. Joining only the outer task, or unwrapping the inner task after Headless has already disposed the application, leaves the lifecycle gap. This change corrects a verified wrapper defect. Whether it eliminates this intermittent startup exception requires separate validation.

## Parent versus feature

| Component | Comparison and causal relevance |
|---|---|
| Historical-copy fixture | Added in G2-04. Its default service introduced the incorrect cross-platform assumption. The earlier parent CI did not execute these cases. |
| Production lease and controls | Lease implementation, safe error mapping, B1 clock precedent and platform-boundary control already existed. G2-04 changed inspector/reset and editor ticket code elsewhere, with no change to the default clock or the rejecting branch. |
| ShortPrint and its wrapper | Both predate G2-04, as do the shared session, official package and overload behavior. G2-04 changed shared dock-host wiring to call `StartTools(Layout)`. That host initialization is inside the delegate, beyond the point of failure. Earlier sessions can influence timing. |
| Build and runner infrastructure | The GUI project added an embedded inspector fixture. Package versions, Headless execution contract, workflow and CI suite script are unchanged. Green parent CI and local R5 are consistent with an intermittent startup failure and a Windows-only R5 baseline. |

## CI configuration and gates

The PR workflow uses four independent hosted jobs with matrix fail-fast disabled. It checks SDK 10.0.401, performs locked restore and Debug/Release builds, and runs the full Release GUI project targeting net10.0 via VSTest. The uploaded per-job native-stage VSTest traces identify console 18.7.0 and Microsoft.NETCore.App 10.0.15 on the appropriate architecture. The project pins Microsoft.NET.Test.Sdk 18.9.0, NUnit 4.6.1, adapter 6.3.0 and Avalonia.Headless 12.1.4. The workflow specifies neither a GUI filter nor a worker override. The two affected classes are nonparallelizable. The script scopes and restores Prism MSBuild environment hooks. It sets no lease clock, dispatcher or GUI-specific platform override. GUI execution uses `--no-build --no-restore`, a named TRX and a two-minute blame-hang timeout.

On all four jobs the locked builds, native publication gate, Core, EditorCoordinator, F1, C1, MCP, desktop publish, asset/InfoDocs validation and diagnostic upload succeeded. Only **Test GUI complete** failed. The script propagates its nonzero exit code, while subsequent diagnostic gates can run after a successful Release build. The platform matrix exposed the test assumption. There is no evidence of a workflow configuration defect for either signature. [Workflow](../.github/workflows/dotnet.yml) · [Suite execution](../.github/scripts/Invoke-PrismCi.ps1)

## Owner decision and validation required

Authorize a **test-only correction in two files**: `CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs` for clock injection and `CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs` for the awaited fixture lifecycle. Production contracts, inspector implementation and CI configuration require no change on this evidence.

1. On Windows x64, Linux x64 and both macOS architectures, run all four historical-copy combinations, the production-clock platform-boundary control, and the existing clock rollback/overflow cases. Verify successful lease invalidation and exact historical text with the synthetic clock, continued Unix refusal with the production clock, revision stability and zero pending work.
2. On Windows, run the existing gated `HeadlessAsyncLifetimeTests` and the six dock visual cases in the same process, including consecutive ShortPrint sizes. Check that late exceptions propagate and disposal completes before the next isolated application starts. Preserve any newly exposed assertion or teardown failure. If the startup exception recurs, capture dispatcher-owner and current thread IDs plus per-dispatch setup/callback/disposal boundaries around the preceding and failing cases before expanding the fix.
3. After separately authorized corrections, obtain the four complete PR jobs on the corrected commit and examine their original GUI TRX. A focused pass alone cannot close an intermittent defect. PR #9 remains blocked on its failed CI. D1 performed diagnosis only.

