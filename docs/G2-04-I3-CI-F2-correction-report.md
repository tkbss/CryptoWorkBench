# G2-04-I3-CI-F2 · Dock visual test corrections

**CORRECTION INCOMPLETE** · 20 November 2026 · Windows x64

All five F1 dock failures are corrected. All focused cases and bounded repeats passed in Debug and Release. A separate pending-copy status assertion failed in the complete Release run, so the package misses the full-suite acceptance criterion.

## Source baseline and scope

The feature worktree was on `feature/gui-g2-04` at `d6c212c264dfab12835b6bf21c103559c34da34d`. The index was clean. Both existing tracked edits matched the F1 closing SHA-256 values. The preflight, D1 and F1 reports were untracked. F1's original Debug and Release complete-suite logs and TRX each recorded 1,272 passes and the same five null-reference failures, with zero skips. F2 changed only `CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs`. The two-file package remains uncommitted. [Baseline inventory](../artifacts/test-results/g2-04-i3-ci-f2/baseline.json) · [D1 diagnosis](G2-04-I3-CI-D1-failure-analysis.md) · [F1 report](G2-04-I3-CI-F1-correction-report.md)

| File under `CryptoWorkBenchGuiTest/` | SHA-256 entering F2 | SHA-256 after F2 |
|---|---|---|
| DockVisualCorrectionTests.cs | `EB21DAF4DFBA577DA711A60462200E0A54F20E782C9C374F08025FF609B2982B` | `BE07BE67A789FA12D432BF42ECE8C84B8B248AE652BAA1029EFD0EB8C88D5A5A` |
| PropertyInspectorCorrectionsTests.cs | `B39BA670DEABA6787CBB3D6F28E5B5C972F40BD4B9609CD05A2B6C9C4E1BC819` | `B39BA670DEABA6787CBB3D6F28E5B5C972F40BD4B9609CD05A2B6C9C4E1BC819` |

The F1 dock wrapper still uses `GuiTestSession.RunAsync` with an awaited callback and asynchronous host disposal. The historical-copy fixture retains its deterministic lease clock, real authorization, acquisition and revision checks. The exact F2 diff compares the incoming F1 file with the final file. A second diff covers the two-file package relative to HEAD. [F2-only diff](../artifacts/test-results/g2-04-i3-ci-f2/final/f2-only.diff) · [Combined package diff](../artifacts/test-results/g2-04-i3-ci-f2/final/package.diff)

## Control lookup and assertion coverage

The test host activates the pane and pumps layout, dispatcher jobs and layout again. The dock presenter uses the same Output or Variables instance exposed by the host. The original `FindControl` calls assumed namescope registration for controls named and assembled in C#. Starting at the active view was correct. F2 searches the typed descendants of that view and identifies the expected control and owner in uniqueness assertions. [Presentation](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs) · [Test host](../CryptoWorkBenchGuiTest/DockToolTests.cs) · [Native probe precedent](../CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs#L70)

- **ShortPrint and LongPrint.** The output view places `ListBox#OutputEntries` inside its content DockPanel. The list template owns the scrolling surface, and its realized item template contains `PrintText` and `OutputSource`. The tests now require a unique named ListBox within the active Output view. They retain all original checks for exact text, viewport bounds at each window size, source-button click and caret position, entry counts, complete long entries, both scroll extents, offsets and realized entry text. [Output view](../CryptoWorkBenchAvalonia/Shell/DockOutputView.cs)
- **NoSelection.** The Variables view places `VariablesGrid` and a details scroll viewer in separate rows. The scroll viewer owns `ContentControl#VariableDetails` and hides itself when selection is null. At startup the scroll viewer is in the visual tree, but its content can remain unattached. F2 identifies exactly one scroll viewer in that view by the type and name of its actual content. It verifies null selection, the hidden owner, and that the named content is never both attached and effectively visible. This expresses the original screen-visibility requirement for both realized and unrealized content. The original grid assertions still require all columns to use star sizing and grid height to exceed 80% of view height. [Variables view](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs) · [Assertion comparison against F1](../artifacts/test-results/g2-04-i3-ci-f2/final/assertion-comparison.txt)
- **Single-tab layout.** The existing layout test, header checks and test parameters are untouched. No time delays, mock views, retries inside tests or changes to runner settings were introduced.

## Intermediate evidence

The first Debug attempt found the output list but could not find the hidden details content among visual descendants. The next Release attempt found the real content via its scroll viewer. Reading the detached child's `IsEffectivelyVisible` alone yielded true even though the child was absent from the displayed subtree. Each attempt yielded 14/15 focused, 7/8 on the repeat and 1,276/1,277 complete. The final test explicitly checks both the hosting surface and whether its content is attached and visible. Original logs, dated test results and source snapshots for these attempts remain at the evidence root. Final-build artifacts are in its separate `final` directory. [Intermediate results](../artifacts/test-results/g2-04-i3-ci-f2/matrix.json)

## Final local validation

| Run | Debug | Release |
|---|---:|---:|
| GUI test project and desktop host, locked builds | 2 succeeded | 2 succeeded |
| Focused | 15/15 passed | 15/15 passed |
| Bounded fresh-process repeat | 8/8 passed | 8/8 passed |
| Complete GUI | 1,277 passed, 0 failed | 1,276 passed, 1 failed |
| Skipped across final runs | 0 | 0 |

All 15 focused identities passed in **each configuration**:

| Group | Passing cases |
|---|---|
| Dock visual (6) | `ShortPrintIsVisibleAndSourceButtonNavigates` at (1200,760), (911,512) and (640,420), `LongPrintEntriesRemainCompleteAndScrollInBothDirections`, `NoSelectionHidesVariableDetailsAndTableUsesAvailableWidth`, `SingleTabRowsSurviveSwitchCloseResetRestoreAndStartup` |
| Headless lifetime (2) | `RawTaskDispatchCompletesWhileInterpreterOwnerIsStillActive`, `AsyncDispatchWaitsForBodyAndAsyncDisposalAndPropagatesLateFailure` |
| Historical copy (4) | `ExactHistoricalCoverage` with (False,False), (False,True), (True,False) and (True,True) |
| Lease clock (3) | `ProductionLeaseClockRetainsPlatformBoundaryAndGuiAdmission`, `ClockRollbackOrOverflowClosesAdmission` with False and True |

**Remaining failure.** The complete Release process exited 1. In `DockInspectorTests.InactiveVariablesViewDoesNotReportPendingCopy(False)`, the pane-close case captured a null shell status, completed the pending clipboard write, and then observed `Copied 256 characters.` Line 299 expected the captured status to persist. The same case passed in the final Debug suite. The failure occurred in a different fixture from the F2 dock visual tests. Its cause and any relationship to F2 are undetermined. All five F1 failures passed in both final full suites. The prior dispatcher startup exception did not recur. [Failing assertion](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L299) · [Release log and stack](../artifacts/test-results/g2-04-i3-ci-f2/final/gui-Release.log)

Both complete inventories matched all 1,277 R5 and F1 case identities, with zero differences. All final focused and repeat inventories also matched F1.

Each build used SDK 10.0.401 with locked restore and the repository's pinned Prism setup. Each test command started a fresh process. The bounded repeat ran all six dock visual and both gated lifetime cases together in each configuration. The lifetime controls cover the original nested-task hazard, callback completion, asynchronous disposal and propagation of a late exception. Test IDs, fully qualified class and parameterized display names were compared as sorted multisets against the matching F1 run and, for the complete suites, the matching R5 run. [Commands and exit codes](../artifacts/test-results/g2-04-i3-ci-f2/final/commands.jsonl) · [Counts and identity differences](../artifacts/test-results/g2-04-i3-ci-f2/final/matrix.json) · [All focused Debug identities](../artifacts/test-results/g2-04-i3-ci-f2/final/focused-Debug-cases.json) · [All focused Release identities](../artifacts/test-results/g2-04-i3-ci-f2/final/focused-Release-cases.json)

Generated logs, six final TRX, source snapshots, diffs and identity inventories are isolated local evidence. The index and accepted feature commit were preserved. [Debug complete TRX](../artifacts/test-results/g2-04-i3-ci-f2/final/gui-Debug.trx) · [Release complete TRX](../artifacts/test-results/g2-04-i3-ci-f2/final/gui-Release.trx)

## Independent review and remaining validation

Review the saved Release TRX and authorize a separate, targeted investigation of the pane-close clipboard failure before accepting the package. F2 stopped at its test-only scope. Hosted Windows x64, Linux x64 and both macOS architectures still require execution against a separately authorized correction commit. PR #9 received no update or CI rerun in F2. Local observations cannot establish permanent elimination of the historical intermittent dispatcher failure.
