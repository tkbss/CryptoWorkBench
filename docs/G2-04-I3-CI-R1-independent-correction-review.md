# G2-04-I3-CI-R1 independent correction review

**PASS – READY FOR OWNER REVIEW**

The cumulative F1–F3 package is suitable for owner acceptance. The decisive product evidence is the admitted-close trace. At the confirmed model close, Variables is already absent, its view is still attached, and its generation has advanced. A delayed clipboard write then delivers the full identifier without reporting obsolete success. The rejected-close control retains membership, generation and valid feedback. The Windows full-suite results preserve every accepted R5 case.

## Source and Git baseline

Reviewed the working tree against accepted HEAD `d6c212c264dfab12835b6bf21c103559c34da34d` on `feature/gui-g2-04`. Local master is `32017a08c4771e73c12a50f6e1548476b9bb0a54`. The index is clean. The complete diff has SHA-256 `5D4DBD7346C6B1763D9F9A970617DEBE4FBF5E75B42EF8231FC50DB9DD74D946`, identical to F3's package.diff. These are the five tracked modifications, with independently computed file hashes matched to F2 final and F3 after.json. [Recomputed diff](../artifacts/test-results/g2-04-i3-ci-r1/verified.diff)

| File relative to worktree | Lines added / removed | SHA-256 |
|---|---:|---|
| CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs | 13 / 1 | B1BA227992992EDBBCCD61755018F2F51B32A283142A5F029C777B4AF45C241C |
| CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs | 8 / 1 | A7A3999A75D86306DD9E05D7F7D50F444817A5810C7B7D17E9D6F1AFB79C0D1F |
| CryptoWorkBenchGuiTest/DockInspectorTests.cs | 59 / 6 | 20C1CCBB6A5602112018B829F3E6E3B222AEADF29F901AD1D81EED8B5790A084 |
| CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs | 26 / 9 | BE07BE67A789FA12D432BF42ECE8C84B8B248AE652BAA1029EFD0EB8C88D5A5A |
| CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs | 1 / 1 | B39BA670DEABA6787CBB3D6F28E5B5C972F40BD4B9609CD05A2B6C9C4E1BC819 |

The six pre-existing untracked documents are the I2 integration preflight and the D1, F1, F2, D2 and F3 reports. R1 adds this untracked report and ignored supporting evidence. I read the five CI reports and consulted the R5 acceptance record, then tested their claims against the diff, source, original results and event logs. The 15 other registered worktrees retained their starting HEAD and Git status throughout the review, including pre-existing dirty entries. [Original evidence inventory](../artifacts/test-results/g2-04-i3-ci-r1/original-evidence-inventory.csv) · [R5 acceptance record](G2-04-02-I3-R5-final-verification.md)

## F1 lease clock

All three Unix hosted GUI TRX files fail the two lease-enabled ExactHistoricalCoverage cases at the fixture's successful-acquisition assertion with TimerUnavailable. The other two variants pass. Following the original acquisition path explains the platform split. The default clock uses Windows GetTickCount64, the unsupported-platform path fails the time read, and lease admission rejects before starting its dispatcher timer. The fixture now supplies 1,000 milliseconds on its private service instance. This is a valid stable observation for a test that acquires a lease to invalidate a captured view and does not exercise expiry.

The fixture still activates MCP, issues a credential with editor-control permission and calls AcquireEditorAsync with the registered editor and revision. All four lease/draft and page/row combinations retain exact UTF-16 equality, including isolated surrogates, escaping and NUL, historical status, unchanged interpreter revision and zero pending work. Disposal still releases the model and service and clears the dictionary. The default production clock, rollback/overflow admission tests and platform-boundary controls remain intact. F1 adds no platform skip. [Fixture](../CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs) · [Original CI evidence](../artifacts/test-results/g2-04-i3-ci-d1/)

## F1/F2 Headless lifetime and visual assertions

The former wrapper could select the synchronous generic dispatch overload with Task as its result. Awaiting the outer operation could finish before the callback and host cleanup. The replacement calls GuiTestSession.RunAsync. Its async result-bearing delegate awaits the entire callback. Inside that callback, the await-using scope awaits both the test body and host disposal before returning. Exceptions from either flow through the returned dispatch task to NUnit. The shared dispatcher thread, isolated application per dispatch, host ownership and nonparallel fixture are preserved. The lifetime controls and five failures surfaced in both F1 full runs corroborate that body failures are observable. The original hosted Windows stack enters dispatcher verification during Headless initialization. These local runs establish no recurrence in this sample. [Dispatch wrapper](../CryptoWorkBenchGuiTest/GuiTestSession.cs) · [Original Headless dispatch source](../artifacts/test-results/g2-04-i3-ci-d1/HeadlessUnitTestSession-upstream.cs)

F2 resolves OutputEntries and VariablesGrid by unique type and name beneath the actual active pane. For ShortPrint, the three viewport sizes still require exact output, containment in the scroller and a routed source click that moves the caret from 0 to 2. LongPrint retains complete entries, overflowing extents, actual offset movement on both axes and exact realized text. For an empty selection the test requires exactly one visual scroll host owning VariableDetails, a hidden host, and a child that cannot be both attached and effectively visible. A detached child alone cannot satisfy the independent host check. The actual grid must use star columns and occupy over 80% of pane height. The single-tab test still checks visible tab strips, grips and drag areas through selection, close, maximize/restore, reset and startup reconstruction. [Dock visual tests](../CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs)

## F3 confirmed close and ordering

DockShellLayout.Close passes the actual pane to EditorDockFactory. The factory's change admission can reject the request while an Inspector operation is active. In the installed Dock.Model 12.1.0.13 revision, FactoryBase.CloseDockable checks eligibility and CloseDockableCore checks the cancellable closing notification. The accepted path hides or removes the model and then calls OnDockableClosed. That method invokes the factory event synchronously with the same dockable. Rejection before this point cannot notify the handler. DockShellPresentation compares object identity with this layout's Variables pane, so closing a sibling or container leaves pending Variables feedback valid.

For floating Variables, the application host checks its close veto and delegates accepted closure to the Dock host. The ordinary host-close path calls Factory.CloseWindow, which walks the floating layout's visible children and calls CloseDockable for each. This reaches the same Variables identity. The model membership assertion traverses floating roots as well. The floating regression exercises a real host Window.Close and reopens Variables. The detailed generation trace was collected on the admitted model-close path. The floating event path is established by source and its outcome by the floating test. [Dock factory close implementation](../artifacts/test-results/g2-04-i3-ci-r1/FactoryBase.Dockable.cs.txt) · [Event invocation](../artifacts/test-results/g2-04-i3-ci-r1/FactoryBase.Events.cs.txt) · [Floating host](../artifacts/test-results/g2-04-i3-ci-r1/HostWindow.axaml.cs.txt) · [Floating layout traversal](../artifacts/test-results/g2-04-i3-ci-r1/FactoryBase.Window.cs.txt)

The key handler captures the selection and increments the existing generation before awaiting the clipboard. Both success and exception paths compare that generation immediately before publishing. The confirmed-close handler verifies UI-thread access and increments the same counter inline. In the supported dispatcher model a continuation queued after closure cannot interleave between the synchronous event and this increment. A later copy obtains a newer generation. Closing retires feedback while allowing an accepted transfer to finish with its original text. The scheme introduces no second ordering mechanism. [Presentation](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs) · [Selection copy](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs)

**Observed order.** D2 records confirmed removal with generation 1 and the view attached. Releasing the write produced “Copied 256 characters.” before detachment. The corresponding F3 Debug and Release probes record generation 1 at pending copy, 2 at the confirmed-close observer with membership false and attachment true, then 3 at detach. Status remained null after release and after the late pump, and clipboardExact was true. The natural busy-host controls retain membership and generation 1 and deliver the valid success. These traces reproduce the two distinct mechanisms. They cannot identify the exact interleaving of an earlier uninstrumented CI failure. [D2 admitted close](../artifacts/test-results/g2-04-i3-ci-d2/probe/event-close-window.log) · [F3 admitted close](../artifacts/test-results/g2-04-i3-ci-f3/probe-Debug-close-window.log) · [F3 rejected close](../artifacts/test-results/g2-04-i3-ci-f3/probe-Debug-natural.log)

## Ownership and regression tests

StartTools guards its single subscription with the stored layout. The presentation owns the factory subscription and removes it before clearing the layout and disposing Variables. Repeated close/open reuses the pane identity and view without adding handlers. Actual visual detachment also advances the generation. Multiple advances before reopening are harmless. Explicit view disposal removes its feedback callback and observers, so pending completions cannot reach a disposed shell. The reviewed paths use the UI dispatcher. I found no new retained event subscription or reset that could make an old generation current.

InactiveVariablesViewDoesNotReportPendingCopy retains both close and explicit-dispose variants. For close, the fixture waits for admission, requests the real model operation, verifies absence of the precise pane, captures status and then releases the pending write. It performs no intervening layout pump. Both variants assert unchanged shell status and exact original clipboard text. The rejected-close test holds an actual host reservation, verifies rejection by membership, then requires success feedback and exact text. The two new failed-write cases close a docked pane or its floating window, establish absence, inject a transfer error, and check both unchanged status and the sentinel clipboard. Reopening and copying a different suffix must produce its exact text and fresh length feedback. The interceptor verifies it actually captured a pending transfer. Existing positive and supersession controls cover observable success, errors and newer copies. [Permanent tests and clipboard interceptor](../CryptoWorkBenchGuiTest/DockInspectorTests.cs)

## Original execution evidence

I parsed original TRX result rows and resolved test IDs against each file's test definitions. I compared sorted multisets of ID, class and parameterized name against R5 and F2 in each configuration. The six F3 logs agree with the TRX. The command record contains a single invocation for each of the six runs and four locked builds, each with exit code 0. Each configuration built both the GUI test and desktop projects. The full-suite commands have no filter. All six test commands used the corresponding Debug or Release build. These are first-attempt passes in the retained record. R1 required no new run. [Audit with original file hashes, commands, differences and counts](../artifacts/test-results/g2-04-i3-ci-r1/results-audit.json) · [Original F3 logs and TRX](../artifacts/test-results/g2-04-i3-ci-f3/)

| Windows population | Debug passed / total | Release passed / total | Failed / skipped per run |
|---|---:|---:|---:|
| Focused | 40 / 40 | 40 / 40 | 0 / 0 |
| Fresh-process race repeat | 9 / 9 | 9 / 9 | 0 / 0 |
| Complete GUI | 1,280 / 1,280 | 1,280 / 1,280 | 0 / 0 |

All 15 F1/F2 focused cases are present alongside 25 DockInspector cases. The nine race identities are contained in the focused set, and the focused identities are contained in the complete set. Both full suites retain all 1,277 R5 identities. Each configuration adds the same three identities.

- RejectedVariablesClosePreservesPendingCopyFeedback
- ClosedVariablesSuppressFailedCopyAndReopenAllowsFreshFeedback(False)
- ClosedVariablesSuppressFailedCopyAndReopenAllowsFreshFeedback(True)

The progression is independently visible in the full results. F1 had 1,272 passes and five visual-test failures per configuration. F2 Debug passed all 1,277. F2 Release failed only InactiveVariablesViewDoesNotReportPendingCopy(False), with obsolete success text observed. D2 established both a rejected request and a confirmed-close interval capable of explaining that symptom. [F2 Release original](../artifacts/test-results/g2-04-i3-ci-f2/final/gui-Release.trx) · [Original failed hosted run](https://github.com/tkbss/CryptoWorkBench/actions/runs/44717123735)

## Findings, limits and owner action

| Severity | Review result |
|---|---|
| High | No product or test-contract defect identified. |
| Medium | No lifecycle, reliability or evidence defect requiring correction identified. |
| Low | No actionable documentation or maintainability finding. |

The fixture clock and managed Dock event path introduce no platform-specific dependency. Headless uses synthetic input and a delayed clipboard adapter over the real Headless clipboard. It verifies the supported UI continuation order in those runs. Native clipboard scheduling and hosted dispatcher initialization still require platform evidence. In particular, permanent elimination of the original intermittent Windows dispatcher exception is unproven. Corrected hosted Windows x64, Linux x64, macOS arm64 and macOS x64 results are pending. The accepted manual boundaries remain 100% DPI, 125% DPI, screenreader speech and cross-window mouse Redock.

I recommend owner acceptance of this five-file package for subsequent publication under separate authorization. The review ends with the corrections uncommitted.
