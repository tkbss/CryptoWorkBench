# G2-04-I3-CI-D2 · Pending clipboard copy lifecycle analysis

**READY FOR OWNER REVIEW – ROOT CAUSE IDENTIFIED**  
**ROOT CAUSE CONFIRMED · D. Multiple contributing causes** · Windows x64, 20 November 2026

The test can request close while an Inspector read prevents docking changes. In a fresh-process reproduction the pane stayed open, so the copy correctly reported success. A second, independent defect exists in the accepted product: after Dock confirmed a successful Variables close, the pending copy could still publish success before visual detachment. The actual shell status changed to `Copied 256 characters.` after the close event. The clipboard received the correct original text in both cases.

**Owner decision:** authorize a narrowly scoped product and regression-test correction (option D). Waiting for the Inspector alone would make the existing test more reliable but leave the confirmed post-close publication window open. The original F2 TRX contains no pane-membership or generation trace, so its specific failing execution cannot be attributed conclusively to either ordering.

## Baseline and original evidence

Verified `feature/gui-g2-04` at `d6c212c264dfab12835b6bf21c103559c34da34d`, local master at `32017a08c4771e73c12a50f6e1548476b9bb0a54`, and a clean index. Only the following tracked files had working changes, matching F2's final SHA-256 inventory:

| Existing F1/F2 change | SHA-256 |
|---|---|
| CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs | `BE07BE67A789FA12D432BF42ECE8C84B8B248AE652BAA1029EFD0EB8C88D5A5A` |
| CryptoWorkBenchGuiTest/PropertyInspectorCorrectionsTests.cs | `B39BA670DEABA6787CBB3D6F28E5B5C972F40BD4B9609CD05A2B6C9C4E1BC819` |

The preflight, D1, F1 and F2 reports were untracked. Their hashes and the original worktree inventory are retained in [baseline.json](../artifacts/test-results/g2-04-i3-ci-d2/baseline.json). The prior [D1](G2-04-I3-CI-D1-failure-analysis.md), [F1](G2-04-I3-CI-F1-correction-report.md) and [F2](G2-04-I3-CI-F2-correction-report.md) reports and evidence were preserved.

The final F2 Release TRX records `DockInspectorTests.InactiveVariablesViewDoesNotReportPendingCopy(True)` passing immediately before `InactiveVariablesViewDoesNotReportPendingCopy(False)` failed (141.776 ms). At `DockInspectorTests.cs:299` the latter expected the captured null status and read `Copied 256 characters.` The subsequent clipboard assertion at line 300 was not reached in that invocation. The F2 totals were Debug 1,277/1,277 and Release 1,276/1,277 with zero skips and the same 1,277 R5 identities. [Original TRX](../artifacts/test-results/g2-04-i3-ci-f2/final/gui-Release.trx) · [Original stack](../artifacts/test-results/g2-04-i3-ci-f2/final/gui-Release.log)

## Controlled reproduction

Five fresh VSTest processes used the existing F2 binaries. The table gives individual TRX durations. Every isolated failure had the same line, expected null and actual 256-character success message. For passing permanent tests the TRX records the assertion outcome rather than the literal status text. The instrumented controls below captured the actual text separately.

| Configuration and process | Close (False) | Dispose (True) | Exit |
|---|---|---|---:|
| Release, close only 1 | Failed · 1.0103240 s | — | 1 |
| Debug, close only 1 | Failed · 1.0086420 s | — | 1 |
| Release, both | Passed · 0.1510800 s | Passed · 0.9532760 s | 0 |
| Release, close only 2 | Failed · 0.9467900 s | — | 1 |
| Debug, both | Passed · 0.1462510 s | Passed · 0.9311500 s | 0 |

The F2 Debug/Release split reflects one sampling of a scheduling-sensitive test. Both configurations failed in isolation and passed in a paired process. A previous test is unnecessary for failure. The paired results do not establish contamination or a particular JIT cause. No whole GUI suite or GitHub workflow was rerun. Commands include the precise NUnit case filter, configuration-specific DLL, isolated work directory and logger. The evidence directory contains separate logs and TRX with full stacks. [Command arguments and exit codes](../artifacts/test-results/g2-04-i3-ci-d2/commands.jsonl) · [Case results](../artifacts/test-results/g2-04-i3-ci-d2/results.json)

## Contract and implementation

I3's product integration uses the shell status for clipboard feedback. R4 documented success/error handling and disposal. F4 explicitly separates closing or disposing the pane from finishing the already submitted clipboard operation. R5's close case records that the post-close status is preserved and the original write succeeds. The documents specify that observable outcome. They describe detachment as the implementation mechanism but do not separately specify dispatcher timing or guarantee a synchronous detach. Treating a denied request as a close is unsupported. Allowing feedback after a confirmed close solely because a presenter is still attached also fails the recorded outcome. [I3](G2-04-02-I3-completion-report.md) · [R4](G2-04-02-I3-R4-targeted-final-review.md) · [F4, case E](G2-04-02-I3-F4-correction-report.md) · [R5, Close/Detach/Dispose](G2-04-02-I3-R5-final-verification.md)

**Model close.** `DockShellLayout.Close` checks `CanChange` and membership in `VisibleTree(Root)`. It returns void and silently leaves the model alone when relocation is denied. `DockEditorSession.CanRelocate` includes service/host busy, editor state and coordinator gates. Selecting the row in `SelectionCopyFields` can launch an Inspector read which reserves the interpreter queue. A dispatcher pump neither waits for that asynchronous read to finish nor retries a declined close. An admitted factory change calls `base.CloseDockable`; the override explicitly synchronizes Inspector visibility only for the Inspector pane. [Layout close](../CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs#L154) · [Relocation gate](../CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs#L99) · [Factory](../CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs#L215) · [Inspector reservation](../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchInspectorRead.cs#L94)

**View lifetime.** The presentation owns one Variables view and keeps its latest inner ContentControl presenter. It subscribes the view to layout feedback once. Closing Variables does not explicitly dispose the view, clear the presenter field, remove its callback or advance its copy generation. Visual detach increments the generation. Each nonempty selection copy also increments it, including ordinary partial copies; an empty selection returns first. Dispose clears the callback and unregisters the view. Dispose does not itself increment the copy generation. Reopening uses the retained view and can replace its presenter, clearing the previous presenter's content. [View](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs#L109) · [Presentation](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs#L57)

**Transfer and feedback.** The fixture creates the production shell, selects the 256-character identifier, focuses its preview and sends Ctrl+C. The async event handler snapshots the selection and increments generation before awaiting the real clipboard API. The proxy intercepts the first `SetDataAsync`, holds it at `await _release.Task`, then forwards it to the original clipboard. `CompleteAsync` requires a non-null pending task, opens the gate, awaits forwarding and awaits one dispatcher callback at Background priority. The default TaskCompletionSource allows inline continuations. The UI handler normally resumes with its captured Avalonia synchronization context. Success and error both compare the captured generation before calling the feedback delegate. The layout forwards feedback synchronously to the shell status text. [Proxy and fixture](../CryptoWorkBenchGuiTest/DockInspectorTests.cs#L289) · [Shell subscription](../CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs#L24)

A Background-priority callback provides one ordered dispatcher checkpoint. It does not join the async-void copy event handler, certify Dock detachment, or drain future work of all priorities. An unchanged status at that checkpoint alone would be weak negative evidence. The failure here is positive evidence of publication. The observer saw the text-change event before the final check.

## Observed event order

Isolated diagnostic executables under the ignored evidence directory used reflection to instantiate the original Release test host, SelectionCopyFields and DelayedSelectionWrite and dispatched through the original GuiTestSession. They sampled actual Dock model membership, visual attachment, generation, callback, inspector activity, presenter identity, context and status. An additional observer subscribed to the original factory's `DockableClosed` event. No replacement product DLL, instrumented project file or modified permanent test was used. The probe sources and logs are retained together. Sequence numbers indicate order within a process, without inferred timestamps. [Probe directory](../artifacts/test-results/g2-04-i3-ci-d2/probe/)

| Probe | T1–T4 · initiate, suspend, request and return | T5–T9 · detach, invalidate, release, resume and publish |
|---|---|---|
| Same close/pump sequence as fixture | After Ctrl+C the actual transfer was pending, generation 1. An active Inspector read held the host busy. Immediately before and after close, CanChange and CanRelocate were false, the pane was a model member and the view was attached. | Pump left membership and generation unchanged. Release was followed by shell success on the UI thread at generation 1. The pane was still open, and the clipboard text exactly matched the original. No detach or invalidation occurred before publication. [Trace](../artifacts/test-results/g2-04-i3-ci-d2/probe/natural.log) |
| Admitted close, then fixture pump | Waited on the existing bounded UI predicate mechanism for Inspector idle, page present and relocation admitted. After close returned the pane was absent from the model, yet attached at generation 1. | During the pump the view detached and advanced to generation 2. The callback remained installed. Release completed the original write; status stayed null. Reopen attached the same view at generation 2 through a different presenter. [Trace](../artifacts/test-results/g2-04-i3-ci-d2/probe/settled.log) |
| Admitted close, release before pump | Same admitted setup. The factory emitted `DockableClosed` for the exact Variables model with membership already false, attachment true and generation 1. Close returned in that state. | Release preceded detach. The shell text changed to `Copied 256 characters.` with membership false, attachment true and generation 1. Then detach advanced generation to 2. After detach and an additional pump the incorrect text remained. The clipboard was exact. The ordering also reproduced without the factory event observer. [Event trace](../artifacts/test-results/g2-04-i3-ci-d2/probe/event-close-window.log) · [Second trace](../artifacts/test-results/g2-04-i3-ci-d2/probe/close-window.log) |
| Explicit dispose | While the transfer was pending, Dispose immediately removed the feedback callback even though the model remained open and the view attached at generation 1. | After release the clipboard was exact and status stayed null. [Trace](../artifacts/test-results/g2-04-i3-ci-d2/probe/dispose.log) |

The T1 input and T2 hold were verified by the original fixture and a pending-task check. Handler resumption immediately before publication follows from the awaited product code. The status event itself was directly observed on thread 4 with AvaloniaSynchronizationContext. The probes sampled the generation around transitions rather than instrumenting individual product instructions. The headless shell demonstrated a user-visible status-property violation after actual close; D2 did not measure the frequency of that interleaving on a native desktop or observe a physical user seeing it.

## Determinations

1. **Close contract:** successful closure retires feedback from an already pending copy. A denied request leaves the still-active view eligible.
2. **Implementation:** generation gating honors newer copies and detachment, and callback removal honors disposal. There is a gap after logical close.
3. **Detach on return:** not guaranteed. The event probe observed a confirmed close followed by a return before detach.
4. **Continued attachment:** observed after model removal. The unchanged presenter reference persisted through close.
5. **Active callback:** observed through logical close and subsequent detach. Explicit disposal cleared it.
6. **Same generation on resumption:** observed at successful publication both after a denied request and after a confirmed close before detach.
7. **Test synchronization:** insufficient to prove its close precondition and to formally join event-handler completion. Its expected status is correct once the pane actually closes.
8. **Prior-test dependency:** none required for reproduction.
9. **Dispose versus close:** disposal directly disables publication. Close can be declined or can complete in the model ahead of visual retirement.
10. **F1/F2 causation:** neither touched this fixture, its shared dispatch helper, docking gates or the copy handler. The behavior reproduced in a fresh process without executing the edited fixtures. The exact influence of full-suite timing is unknown.
11. **Observable product violation:** confirmed for the admitted close/release-before-detach sequence on the real headless shell. For the original F2 execution, close admission remains unrecorded. There is no evidence that Avalonia or NUnit infrastructure alone caused it.
12. **Test-only sufficiency:** a fixture repair addresses denied-close false positives but cannot prevent the separately observed post-close publication.

## Minimum correction and owner decision

**D · Product and test correction.** In `DockShellPresentation.cs`, subscribe during StartTools to the existing factory's confirmed `DockableClosed` event and unsubscribe during presentation disposal. Match the actual Variables pane. In `DockVariablesView.cs`, expose a narrow mechanism to invalidate pending feedback (advance the existing generation) synchronously on that event. Retain detach and dispose safeguards, callback reuse on reopen, and unconditional completion of the original clipboard transfer. The event probe places this hook after model removal and before the demonstrated publication window. A rejected or vetoed close must not retire valid feedback. No change to the dock admission policy or clipboard backend is indicated.

In `CryptoWorkBenchGuiTest/DockInspectorTests.cs`, establish close admission with a bounded observable precondition after selection, assert successful model removal, and release the pending transfer immediately after confirmed close, before a layout pump. Preserve the real write and both final assertions. Add a deterministic completion observation for the handler if the corrected negative-status assertion would otherwise depend solely on one Background turn. Keep a denied-close control, where the pane remains active and feedback is allowed. These three files are the proposed correction scope. Approval is required before implementation.

**Follow-up validation.** Exercise successful and failed delayed writes after a confirmed close and after explicit disposal. Cover close before detach, close followed by reopen before release, and a denied close while the host is busy. Assert that the old completion cannot overwrite status, the successful write contains the exact original selection, a failed write preserves the previous clipboard content, and the reopened view reports its own new copies. Retain newer-copy supersession in both success/failure orders, normal partial selection and explicit retry. Run the targeted cases in fresh Debug and Release processes and then the complete 1,277-case Windows GUI suites. After separate authorization, validate the correction on hosted Windows x64, Linux x64, macOS arm64 and macOS x64 with the existing platform contracts.
