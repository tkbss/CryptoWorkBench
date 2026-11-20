# G2-04-I3-CI-F1 · Cross-platform test fixture corrections

**CORRECTION INCOMPLETE** · 20 November 2026 · Local Windows x64

Both authorized fixture changes are implemented and uncommitted. The historical-copy cases and clock controls pass. The corrected dock wrapper now propagates exceptions from five existing test bodies. Those cases prevent a successful local regression result and independent correction acceptance.

## Baseline and implementation

The feature worktree was on `feature/gui-g2-04` at `d6c212c264dfab12835b6bf21c103559c34da34d`, with a clean index and no tracked changes. Local master was `32017a08c4771e73c12a50f6e1548476b9bb0a54`. The preflight and D1 reports were already untracked. Their original hashes are in the baseline inventory. The four original GUI TRX from run 44717123735 were readable and confirmed the failed identities recorded in D1. [D1 diagnosis](G2-04-I3-CI-D1-failure-analysis.md) · [Baseline and original failure identities](../artifacts/test-results/g2-04-i3-ci-f1/baseline.json)

| Changed file under `CryptoWorkBenchGuiTest/` | SHA-256 before | SHA-256 after |
|---|---|---|
| `PropertyInspectorCorrectionsTests.cs` | `AD085F669A4629C207C72898F8C8FDEF8BD31580A5D5EAAA220E32E577554A98` | `B39BA670DEABA6787CBB3D6F28E5B5C972F40BD4B9609CD05A2B6C9C4E1BC819` |
| `DockVisualCorrectionTests.cs` | `D6C8C8BC212F79701498616B9694D9AE4066DA4BD06713C3D6C38E4936CFB156` | `EB21DAF4DFBA577DA711A60462200E0A54F20E782C9C374F08025FF609B2982B` |

**Lease clock.** The historical-copy fixture initializes its own service with `LeaseMilliseconds = () => 1_000UL`, using the existing B1 host pattern. The lease path still authorizes the caller, acquires against the registered document and revision, and invalidates access to the live value. All four cases verify exact historical text, historical status, stable interpreter revision and zero pending editor work. Each fixture awaits service disposal. The production-clock boundary case independently exercises a default service and explicit lease release on Windows. The two rollback/overflow cases retain their own changing clocks. [Historical-copy tests](../CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs#L25)

**Headless lifecycle.** The dock fixture returns `GuiTestSession.RunAsync` with its existing awaited callback and asynchronous host disposal inside the delegate. The helper uses the asynchronous result-bearing Headless overload, so the isolated application owns the full callback lifetime. Exceptions now reach NUnit. Test bodies, assertions and the nonparallelizable attribute retain their original source. [Dock wrapper](../CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs#L18) · [Exact source diff](../artifacts/test-results/g2-04-i3-ci-f1/implementation.diff)

## Windows validation

The GUI test and Avalonia desktop projects each built with SDK 10.0.401 and locked restore in Debug and Release (four build exits of 0). Each test invocation used a fresh process and separate log, TRX and NUnit work directory. Each configuration ran the 15 focused cases, a bounded repeat of the eight lifecycle/dock cases, and the complete GUI suite. The original run and repeat results were retained independently. [Exact commands, arguments, times and exit codes](../artifacts/test-results/g2-04-i3-ci-f1/commands.jsonl)

| Test group | Debug passed / failed | Release passed / failed |
|---|---:|---:|
| Historical copy, both invalidations × both representations | 4 / 0 | 4 / 0 |
| Default production clock, rollback and overflow | 3 / 0 | 3 / 0 |
| Headless async lifetime controls | 2 / 0 | 2 / 0 |
| Dock visual fixture | 1 / 5 | 1 / 5 |
| **Focused total** | **10 / 5** | **10 / 5** |
| Separate eight-case lifecycle/dock repeat | 3 / 5 | 3 / 5 |
| **Complete GUI** | **1,272 / 5** | **1,272 / 5** |

All six test processes exited 1, each with the same five failures and zero skips. The complete Debug and Release TRX each contain 1,277 results. Sorted TRX test IDs and parameterized names match the respective R5 full-suite inventories exactly, including multiplicities. Execution order, full stacks and outcomes are retained per invocation. [Counts](../artifacts/test-results/g2-04-i3-ci-f1/summary.json) · [Debug TRX](../artifacts/test-results/g2-04-i3-ci-f1/gui-Debug.trx) · [Release TRX](../artifacts/test-results/g2-04-i3-ci-f1/gui-Release.trx)

The gated lifecycle controls exercised both the original nested-task behavior and the corrected helper. The positive control verified incomplete completion while the callback or disposal was held, then observed the original late exception after cleanup. In the dock fixture, the single-tab layout case passed. The failing cases reached the callback and propagated the following lookup failures:

| Failing case in `DockVisualCorrectionTests` | Observed failure in Debug |
|---|---|
| `ShortPrintIsVisibleAndSourceButtonNavigates(1200,760)`, `(911,512)`, `(640,420)` | Line 75 enumerates descendants of the null list returned by the preceding `OutputEntries` lookup. The navigation assertion is later in the body. |
| `LongPrintEntriesRemainCompleteAndScrollInBothDirections` | Line 96 enumerates descendants after the same `OutputEntries` lookup. Scroll assertions are later in the body. |
| `NoSelectionHidesVariableDetailsAndTableUsesAvailableWidth` | Line 115 reads `IsEffectivelyVisible` after the `VariableDetails` lookup returned null. The subsequent grid assertions were not reached. |

The exception is `System.NullReferenceException: Object reference not set to an instance of an object.` The output and variables views create these named controls in C#. The tests use `FindControl` and discard its nullable result with `!`. A native probe already locates `OutputEntries` by type and name in the realized visual tree. These results identify a test lookup problem. They do not establish whether each unreached rendering or navigation assertion will pass once the lookup succeeds. [Output view](../CryptoWorkBenchAvalonia/Shell/DockOutputView.cs#L22) · [Variables view](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L51) · [Existing probe](../CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs#L70)

## Remaining validation and owner decision

The original Windows CI failure occurred during dispatcher initialization before the callback. Its connection to the old wrapper remains probable. Successful lifecycle controls and a different failure inside the callback cannot establish that the intermittent startup defect has been eliminated.

Local WSL reported that the subsystem is uninstalled. Docker and Podman were unavailable, and this Windows host had no local macOS runner. Corrected-source validation remains pending on Linux x64, both macOS architectures and hosted Windows x64. The original R5 results remain historical evidence for the accepted product. F1 changed only the two test fixtures.

**Recommendation.** Independently review the two fixture edits and the five newly observable exceptions. A separately authorized test-body correction should verify the realized controls and repair the named lookups while preserving the rendering, scrolling and navigation assertions. Re-run the focused, repeated and complete suites before publication and the four-platform PR matrix. F1 stops at the authorized fixture changes. [Local evidence directory](../artifacts/test-results/g2-04-i3-ci-f1/)
