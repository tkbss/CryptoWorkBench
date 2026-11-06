# MCP-B1-F2 architectural corrections

## Review baseline

The incoming F1-R1 candidate was on `feature/mcp-ap3` at `dfea2055992254462b937477a8697754fe39a4d6` in `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`. The index was empty. The working tree contained 32 tracked modifications and six untracked reports and test sources. F1-R1 recorded 662 passing GUI tests and 89 passing MCP tests per configuration. The B1 control, the independent reports and the three previous evidence directories were retained. [Incoming Git inventory](../../artifacts/b1-f2/incoming-status.txt) · [Evidence manifest](../../artifacts/b1-f2/preserved.csv) · [Independent F1-R1 findings](MCP-B1-F1-R1-independent-rereview.md)

## Authority and publication

**Workspace transitions.** The workspace service owns one bridge and coordinator across its open documents. The bridge checks the outgoing native group and the incoming editor before retiring the selected registration. Retirement notifies observers synchronously. The bridge therefore checks the live incoming document for uniqueness and the coordinator checks stopping state, ownership, pending work and native groups again immediately before publication. The bridge verifies that the published registration survived its notification before returning it. When the second check rejects a replacement, the old selection has already retired and retained documents can be explicitly activated. Disposal retires all registrations. Each native history remains attached to its original document.

Document coercion checks the original document, both update states, identity and mutation locks, coordinator and buffer ownership around the workspace notification. A drain that encounters an open group subscribes to that document before notifying the workspace. Its one-shot certificate also waits for coordinator tickets and synchronous observers. The document subscription is released on completion or fault. The coordinator never synthesizes an UpdateFinished notification.

**Editing authority.** Replace All accepts the editor's original unrestricted provider or a provider with a trustworthy monotonic permission revision. A custom provider without that contract cannot authorize Replace All. Version reads and permission checks run under controlled observation. An accepted effective batch applies all original matches in descending order. An entirely identical batch returns before opening a history group. Replace Next keeps its existing progress rule.

Clipboard planning copies each authorized segment to a value type, freezes normalized replacement text, virtual padding and selection geometry, and validates the original ticket and input after the clipboard and protection callbacks. The commit writes the captured edits directly to the original document in a single native update. The publicly replaceable provider is never used to execute that plan. The simple and rectangular cases use the pinned editor's deletion order, virtual columns and caret rules. A policy change during UpdateStarted applies to subsequent operations. [Pinned selection behavior](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/SimpleSelection.cs) · [Rectangular behavior](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/RectangleSelection.cs)

**Shared interpreter.** Controlled observation has a synchronous thread-local depth at the process-wide host queue. Fresh reservations consult the same depth across services. Read authorization, execution-ID ownership and idempotent replay remain service-specific. GUI drafts use a shared interpreter revision advanced under the existing reservation on mutation and on effectful job finalization. Declaration identity and the shared revision are both validated while reserved. An unrelated write conservatively requires a refresh. Public service state versions keep their prior meaning.

Return carries the originating editor's intent check through pruning, reservation and registry initialization. For that path the job remains private through injectable clock, timer and admission callbacks. The service validates the intent again before publishing the execution ID and starting the worker. Failure releases initialized resources and the reservation. Ordinary submissions retain the existing registry publication and cancellation order. Asynchronous producer and execution-output handoff remain assigned to P7/P8 and AP5.

**Native history.** AvaloniaEdit pushes an optional caret snapshot at UpdateStarted and immediately clears redo, even if a later subscriber invalidates the completion. When redo exists, completion opens an outer group on the same native stack and sets its public recording limit to zero for native update startup. AvaloniaEdit defers size enforcement while that group is open. The original limit is restored before the final session check and before either group ends. A successful admission records the original caret/selection as an optional action and writes the completion. A rejected admission retains the original redo entries. This implementation depends on pinned grouping and size-limit behavior and should be retested on editor upgrades. [Native undo implementation](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/UndoStack.cs) · [Native caret history](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs)

## Correction and regression matrix

The red column records the independent F1-R1 candidate. The test names identify assertions against production objects in the permanent F2 fixture. [Independent red output](../../artifacts/b1-f1-r1/counterchecks.log) · [Permanent tests](../../CryptoWorkBenchGuiTest/B1F2CorrectionTests.cs)

| R1 | F1-R1 effect | F2 result and permanent test |
|---|---|---|
| 01 High | Replacement published an alias and the next keystroke faulted the coordinator. | Final uniqueness check rejects C. A can be activated and A and C later undo/redo independently. `ReplacementUsesIncomingLiveDocumentAfterRetirement` |
| 02 High | Checking offset 0 revoked offset 4, but both Xs became Ys. | Unversioned policy rejects before evaluation. A versioned policy changing during final evaluation rejects before the group. Snapshot, revision and executable redo survive. `MultiMatchRequiresCoherentProtection` |
| 03 High | A successor expanded deletion to include LOCK, cleared redo on narrowing or faulted on exception. | All three successors leave LOCKP and stay installed. An originally denied edit retains executable redo. `PasteCommitsOnlyOriginalSegments` |
| 04 High | Replacing A's document hid its open group and B accepted input. | Replacement, activation and attach reject during the original group. Late drain/stop also veto a document swap. `NativeFinishAloneWakesOriginalDrain`, `DocumentSwapRechecksLifecycleAfterWorkspaceNotification` |
| 05 High | Ending the original group left the drain waiting for an unrelated call. | Retained and closed variants complete on real native finish. B subsequently accepts input and the certificate cannot resume twice. `NativeFinishAloneWakesOriginalDrain` |
| 06 High | Service B executed a fresh job from A's synchronous publication. | Both property and collection callbacks receive Busy. Lookup returns NotFound, state and versions stay unchanged, and submission and replay work afterward. `ForeignObserverCannotAllocateExecution` |
| 07 Medium | An outer attach retained a new registration after disposal completed. | Disposal wins during both retirement and new-registration notification. Native input is denied until a fresh service binds the control. `DisposalWinsDuringReplacement` |
| 08 Medium | A clock callback revoked Return before reservation but a new job ran. | Revocation during pruning or at the worker checkpoint preserves the seed ID, state version and source, with no declaration or newline. A later Return executes once. `ReturnChecksOriginalIntentAtRegistryAdmission` |
| 09 Medium | A visual selection with zero document length ignored paste. | Forward and reverse virtual selections match native text, padding, caret and one-step history. A rectangle differential also checks selection geometry and single/multiline replacements. `VirtualPasteMatchesPinnedSelection`, `RectangularClipboardMatchesNativeGeometryAndHistory` |
| 10 Medium | An invalidated completion skipped insertion but lost redo. | Disposal and caret movement during UpdateStarted reject insertion and the original redo inserts the seeded exclamation mark. Accepted insertion remains undoable and redoable. `CompletionPreservesExecutableHistory` |
| 11 Medium | A stale draft silently overwrote a second service's in-place edit. | In-place, replacement and unrelated writes invalidate the draft. Explicit recapture permits a fresh save. `DraftRevisionCoversForeignStateWriters` |
| 12 Medium | A retained Refresh command replaced B's unsaved row. | The VM issues fresh commands per view attachment and retires the old lifetime. Prior commands preserve B's row, value, selection, freshness and version. B's commands work. `PreviousViewCommandsCannotDiscardCurrentUnsavedDraft` |
| 13 Medium | Skipping an identical match preserved an anchor the pinned editor deleted. | Whole-batch no-op keeps redo. Mixed replacement now agrees on normal and surviving anchors, offsets, selection, caret and grouped history. `MixedBatchMatchesPinnedAnchorAndHistorySemantics` |
| 14 Low | An expired weak origin prevented mutation but kept the row visibly fresh. | Real collection of the origin marks the displayed draft stale on edit. Explicit refresh removes the obsolete row without advancing interpreter state. `ExpiredOriginExposesRefreshWithoutWriting` |

## Source and test scope

The workspace changes are in `EditorBridge`, `EditorCoordinator` and `WorkbenchTextEditor`. The search and clipboard changes are in the workbench editor and text area. `InfoCompletionController` owns the completion history boundary. The shared admission/revision changes are in `InterpreterHostQueue` and the two automation service/registry partials. `CryptoScriptEditViewModel` carries the Return validator. `VariableViewModel` owns draft freshness and command lifetimes.

Two existing fixtures required contract updates. The F1 search fixture supplies a monotonic revision for its custom positive policy and still runs its option, pattern, replacement, policy and native-update callbacks. The earlier-change-subscriber test still verifies the during-change revision, unavailable editor and replacement identity. It now asserts that a null swap from inside Changed is denied and makes the successful null swap once the group closes. The previous independent probe sources and outputs remain available with their original expectations.

The AP3-01 inventory still routes editor writes through the registered host. AP3-02 History retains its original, owner-bound single-use capability. The accepted Cut/Paste event omission, early drag/IME controls, multi-stage CJK limitation and raw third-party control boundary remain in scope as previously documented. AP4 shutdown and provider drains, AP5 output finalization including IR-N1-01, AP6 transport, B3 public document addressing, and N1-F02 remain separate work. [AP3-01 inventory](MCP-AP3-01-integration-assessment.md) · [AP3-02 acceptance](MCP-AP3-02-final-acceptance.md)

## Automated verification

SDK 10.0.401 built the desktop solution under the repository Prism hooks. The source check verified all 1,180 pinned files at official Prism commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Both configurations completed locked restore with `NuGet.Config`, followed by a single-node build with `--no-restore`. Each final build completed with seven warnings and zero errors. [Debug restore](../../artifacts/b1-f2/restore-Debug.log) · [Release restore](../../artifacts/b1-f2/restore-Release.log) · [Debug build](../../artifacts/b1-f2/final-build-Debug.log) · [Release build](../../artifacts/b1-f2/final-build-Release.log)

| Test group | Passed | Failed | Unexecuted |
|---|---:|---:|---:|
| [Full Debug GUI](../../artifacts/b1-f2/Debug-GUI.trx) | 694 | 0 | 0 |
| [Full Release GUI](../../artifacts/b1-f2/Release-GUI.trx) | 694 | 0 | 0 |
| [Full Debug MCP](../../artifacts/b1-f2/Debug-MCP.trx) | 89 | 0 | 1 |
| [Full Release MCP](../../artifacts/b1-f2/Release-MCP.trx) | 89 | 0 | 1 |
| [Debug B1 fixture](../../artifacts/b1-f2/Debug-B1.trx) | 35 | 0 | 0 |
| [Debug F1 fixture](../../artifacts/b1-f2/Debug-F1.trx) | 38 | 0 | 0 |
| [Debug F2 fixture](../../artifacts/b1-f2/Debug-F2.trx) | 32 | 0 | 0 |
| [Debug History/Enter filter](../../artifacts/b1-f2/Debug-History-Enter.trx) | 130 | 0 | 0 |

The four filtered results overlap the full GUI suite. All 662 incoming GUI cases and all 122 incoming History/Enter cases were retained by name. Eight new F2 cases also match the historical History/Enter filter. Each MCP TRX contains 90 cases, including the explicit `InstalledCodexDiscoversAndCallsStatus` case recorded as NotExecuted. [Coverage comparison](../../artifacts/b1-f2/coverage.txt)

The first full candidate run exposed two retention regressions when a flag was added to the timer callback state. The correction keeps the unpublished Return job in the registry's private initialization state until admission finishes. The timer still holds only the service and two IDs. Both retention tests pass on the final candidate. [First full run](../../artifacts/b1-f2/first-full-run/Debug-GUI.trx)

### Independent probe replays

Both independent programs were copied into isolated F2 directories and run against the final Debug product. B1 and F1 used the same original probe source. The original probe's PDB comparison matched all reported worktree inputs in Debug and Release, covering 37 GUI-test files and 42 product files in each configuration. The replay verifier checks 35 predicates, including native search comparisons, history recovery and the results of permanent assertion-based tests. [B1 replay](../../artifacts/b1-f2/b1-independent/replay.log) · [F1-R1 replay](../../artifacts/b1-f2/b1-f1-r1/replay.log) · [Verifier](../../artifacts/b1-f2/verify-replays.ps1) · [Checked results](../../artifacts/b1-f2/replay-verification.txt)

The original B1 diagnostic stops three cases on Busy when it tries activation after disposal, a transition during an open group, or duplicate attachment. The R1 diagnostic stops the replacement-attach case when disposal wins. The permanent tests in the matrix assert the expected rejection, resulting registrations, coordinator availability and subsequent authorized behavior. The original B1-10, B1-11, B1-13, B1-14 and B1-15 checks also retained their effects for declaration replacement, parameter identity, retired editor bindings, Replace Next progression and retired Markdown commands.

## Release identity and Windows acceptance

The Release launch target is `CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.exe`. Its managed product DLL matches the copy used by the Release GUI tests byte for byte. [Paths and SHA-256 manifest](../../artifacts/b1-f2/release-binaries.csv)

| Release file | SHA-256 |
|---|---|
| Desktop.exe | `C5E414A79FD2B61D46BBD0D0E44004DF772FB1895D0E779B355D4A28C0E3249C` |
| Desktop.dll | `414A8713967778E498EAACC7B49263C9466591BC21261B57CB07D4DFD3BA9C7C` |
| CryptoWorkBenchAvalonia.dll | `E81FA161C95C9DC7E4CA75A649B08F8E2C369F997855E597E1A3D15E99AFB4A0` |

Direct command and document tests establish synthetic state, editing, selection and history behavior. Avalonia Headless separately exercised routed pointer completion, external focus and Escape. The replay recorded a routed press, one insertion request and insertion of the selected item. Its preliminary hit-test did not identify the row. These results are headless evidence only.

**Native owner acceptance remains open on this exact Release build.** The prior user-initiated Computer Use stop was respected. The native matrix still requires mouse and keyboard completion, genuine external-focus cancellation, Return and KP_Enter, overload navigation and closure, Search/Replace and Find Next, History and Undo/Redo, cross-application Cut/Paste, variable and parameter editing, rectangle selection/paste and drag gestures, and physical Swiss AltGr/dead-key input.

## Final inventory and recommendation

The cumulative worktree has 34 modified tracked paths and eight untracked paths. The complete file-by-file Git status distinguishes inherited B1/F1 work from the two additional tracked paths and two additional untracked paths introduced by F2. The latter four are `Services/InterpreterHostQueue.cs` under the GUI project, `CryptoWorkBenchGuiTest/EditorAutomationTests.cs`, `CryptoWorkBenchGuiTest/B1F2CorrectionTests.cs` and this report. F2 also updates the existing candidate files identified in the source scope above and the F1 test fixture. The isolated evidence directory is Git-ignored. The previous evidence manifest verified 41 files with zero differences. [Incoming inventory](../../artifacts/b1-f2/incoming-status.txt) · [Final inventory](../../artifacts/b1-f2/final-status.txt) · [Preservation check](../../artifacts/b1-f2/preservation.txt)

**Ready for independent F2 rereview.** The final branch and HEAD match the baseline, the index is empty, and `git diff --check` passes. No commit, push, merge or master update occurred. Independent rereview and native Windows owner acceptance remain the next gates.
