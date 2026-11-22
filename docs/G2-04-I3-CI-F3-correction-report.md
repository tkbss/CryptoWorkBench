# G2-04-I3-CI-F3 · Pending copy lifecycle correction

**READY FOR INDEPENDENT CORRECTION REVIEW** · 20 November 2026 · Local Windows x64

The Variables view invalidates pending clipboard feedback synchronously when Dock confirms that its pane has closed. The underlying transfer finishes with its original text. A denied close leaves the active view eligible to report the result. The close regression now verifies actual model removal before releasing its delayed write.

## Baseline and source scope

The branch was `feature/gui-g2-04`, HEAD `d6c212c264dfab12835b6bf21c103559c34da34d` and local master `32017a08c4771e73c12a50f6e1548476b9bb0a54`. The index was clean. The only incoming tracked changes were the two F1/F2 test files. Their SHA-256 values matched the F2 closing inventory. The preflight, D1, F1, F2 and D2 reports were untracked. Their entry hashes were recorded alongside the source inventory. [Baseline](../artifacts/test-results/g2-04-i3-ci-f3/baseline.json) · [F2 report](G2-04-I3-CI-F2-correction-report.md) · [D2 diagnosis](G2-04-I3-CI-D2-pending-copy-analysis.md)

| F3 source file | SHA-256 before | SHA-256 after |
|---|---|---|
| CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs | `0A216FFECAB8C2377797D676AE6EEC1D99DDEF1472868B1869AA409B4736593C` | `B1BA227992992EDBBCCD61755018F2F51B32A283142A5F029C777B4AF45C241C` |
| CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs | `E5F30A95C2E5AE0E5C1A6CF8D46760CAF0D2DFED99545DB2222EF19826625635` | `A7A3999A75D86306DD9E05D7F7D50F444817A5810C7B7D17E9D6F1AFB79C0D1F` |
| CryptoWorkBenchGuiTest/DockInspectorTests.cs | `6D0DDD74E3B38400BE1DAF3FC315BCDEC89A345B32BEFFF33A07BEF1597540F9` | `20C1CCBB6A5602112018B829F3E6E3B222AEADF29F901AD1D81EED8B5790A084` |

The complete local package includes the prior `DockVisualCorrectionTests.cs` and `PropertyInspectorCorrectionsTests.cs` changes. F3 edited only the three files above. [F3-only diff](../artifacts/test-results/g2-04-i3-ci-f3/f3.diff) · [Combined package diff](../artifacts/test-results/g2-04-i3-ci-f3/package.diff)

## Product behavior and subscription ownership

D2 found two distinct causes. An active Inspector read could deny relocation while the test assumed closure. After an admitted close, Dock could remove the pane while the view remained attached at the generation captured by the pending copy. The saved event trace showed the obsolete success reaching the shell during that interval. [Original evidence](../artifacts/test-results/g2-04-i3-ci-d2/probe/event-close-window.log)

`DockShellPresentation.StartTools` subscribes to the layout factory's `DockableClosed` event under the existing single-start guard. The handler compares the event's dockable by reference to the Variables pane in the owned layout. It advances that view's existing copy generation on the UI thread. Closing another pane or declining a request has no such effect. Presentation disposal removes the subscription before clearing the layout and disposing the view. Repeated pane closes/reopens do not install more subscriptions, and a repeated disposal has no active subscription to remove.

The view uses the same invalidation method for visual detach. The success and error continuations retain their original generation checks, the original clipboard transfer continues, and explicit disposal still clears the feedback callback. A reopened view may issue a fresh copy with a fresh generation. No new timer, cancellation or asynchronous ordering state was introduced. [Presentation](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs) · [Variables view](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs)

## Fixture and regression coverage

The close variant starts a real pending copy and waits on the bounded existing UI predicate for an available Inspector page, completed Inspector read and admitted relocation. It requests close once, asserts that the original pane is absent from the dock model, and releases the transfer without an intervening layout pump. Both original variants retain their status and exact clipboard-content assertions. The explicit-disposal variant still disposes the view directly.

Three new cases cover distinct lifecycle outcomes:

- `RejectedVariablesClosePreservesPendingCopyFeedback` holds a real interpreter host reservation, verifies that relocation is denied and the pane remains present, then checks both the success message and the exact original clipboard text.
- `ClosedVariablesSuppressFailedCopyAndReopenAllowsFreshFeedback(False)` closes the docked pane, fails the pending transfer and checks that the shell status and clipboard sentinel survive. It reopens the pane and checks the exact suffix and fresh success feedback.
- `ClosedVariablesSuppressFailedCopyAndReopenAllowsFreshFeedback(True)` exercises the same assertions through an actual floating host window close in Avalonia Headless. The fixture sends Ctrl+C through the window owning the selected field, including after return to the main window.

The focused runs also exercise older-success/newer-error and older-error/newer-copy ordering, ordinary selections, expanded selections, busy-clipboard retry, NUL handling, independent Inspector reads, layout behavior and the original 15 F1/F2 controls. [Regression tests](../CryptoWorkBenchGuiTest/DockInspectorTests.cs) · [Per-case Debug results](../artifacts/test-results/g2-04-i3-ci-f3/focused-Debug-identities.json) · [Per-case Release results](../artifacts/test-results/g2-04-i3-ci-f3/focused-Release-identities.json)

## Event observations and Windows validation

The retained D2 event observer was run in fresh processes against the rebuilt assemblies. At admitted close the model membership was already false, the view was still attached, and generation had advanced from 1 to 2. Releasing the transfer then left shell status null and delivered the exact identifier. Detach advanced generation again to 3. In the Debug busy-host control, close returned with membership true and generation still 1; completion reported the valid 256-character success. These observations exercise the interval missed when a test pumps layout before releasing its transfer. [Debug admitted close](../artifacts/test-results/g2-04-i3-ci-f3/probe-Debug-close-window.log) · [Debug rejected close](../artifacts/test-results/g2-04-i3-ci-f3/probe-Debug-natural.log) · [Release admitted close](../artifacts/test-results/g2-04-i3-ci-f3/probe-Release-close-window.log)

| Run | Debug | Release |
|---|---:|---:|
| GUI test and desktop projects, locked restore | 2 builds passed | 2 builds passed |
| Focused (25 DockInspector cases plus the 15 F1/F2 cases) | 40/40 | 40/40 |
| Fresh-process lifecycle and ordering repeat | 9/9 | 9/9 |
| Complete Windows GUI | 1,280/1,280 | 1,280/1,280 |

Each invocation passed on its first attempt. Both full suites retained all 1,277 original R5 identities and added exactly the three cases listed above. There were zero failures and zero skips.

The focused command selected the complete DockInspectorTests fixture plus the F1/F2 focus set. One additional fresh-process run per configuration exercised the nine pending-copy and ordering cases. Each complete GUI command used the unfiltered project. Population checks compared sorted test IDs and class-qualified parameterized names as multisets. All commands, individual exit codes, logs and TRX were retained separately. [Commands](../artifacts/test-results/g2-04-i3-ci-f3/commands.jsonl) · [Population and results](../artifacts/test-results/g2-04-i3-ci-f3/matrix.json) · [Debug full TRX](../artifacts/test-results/g2-04-i3-ci-f3/gui-Debug.trx) · [Release full TRX](../artifacts/test-results/g2-04-i3-ci-f3/gui-Release.trx)

## Review and remaining verification

Recommend independent review of the event subscription, the admission and model-removal assertions, and the before/after close traces. Hosted Windows x64 must still verify Headless lifecycle behavior. Linux x64 and both macOS architectures must verify the synthetic-clock fixture and applicable GUI tests. The bounded Windows runs establish the tested orderings only. The five source edits remain uncommitted; PR #9 and GitHub Actions received no updates from F3.
