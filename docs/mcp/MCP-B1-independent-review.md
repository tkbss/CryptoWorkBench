# MCP-B1 independent review

**CHANGES REQUIRED** · 2 November 2026 · `feature/mcp-ap3`

A Return pressed on one line executed a different line after an overload popup closed and the workspace entered Releasing. A synchronous property observer also started an interpreter job through the existing text-execution service. Both jobs reached the real interpreter. Other counterchecks reproduced a protected-range overwrite, a shared buffer with two live registrations, writes after disposal or view detachment, and stale completion and paste commits. All 624 GUI tests still passed in both configurations.

The findings below distinguish failures introduced by the new registration and editing paths from older behavior that P2–P6 undertook to protect. The reproductions exercise the in-process host. They establish no remote exploit, new MCP permission or v2 lease support.

## Baseline and evidence

The worktree root was `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp-ap3`. Branch and HEAD matched the brief. HEAD was `dfea2055992254462b937477a8697754fe39a4d6`. The index was empty. Before this review there were **29 tracked modifications and two untracked candidate files**. The tracked diff contained 701 insertions and 200 deletions. The two untracked files were read separately. No correction was applied.

**Complete entry inventory.** Each filename below is relative to the stated directory. M means modified and ?? means untracked.

| Directory | Status | Files |
|---|---|---|
| `CryptoWorkBenchAvalonia/` | M | `App.axaml.cs` |
| `CryptoWorkBenchAvalonia/Controls/` | M | `WorkbenchTextArea.cs`, `WorkbenchTextEditor.cs` |
| `CryptoWorkBenchAvalonia/Models/` | M | `InfoCompletionReopenPolicy.cs` |
| `CryptoWorkBenchAvalonia/Services/Automation/` | M | `EditorBridge.cs`, `EditorCoordinator.cs`, `WorkbenchAutomationRegistry.cs`, `WorkbenchAutomationService.cs`, `WorkbenchEditorOperations.cs`, `WorkbenchSaveCommit.cs` |
| `CryptoWorkBenchAvalonia/Services/` | M | `InfoCompletionController.cs` |
| `CryptoWorkBenchAvalonia/ViewModels/` | M | `CryptoScriptEditViewModel.cs`, `InfoViewModel.cs`, `KeyMapViewModel.cs`, `MainViewModel.cs`, `SidebarViewModel.cs`, `StatusViewModel.cs`, `VariableViewModel.cs`, `ViewModelBase.cs` |
| `CryptoWorkBenchAvalonia/Views/` | M | `CryptoScriptEditView.axaml.cs`, `InfoView.axaml.cs`, `VariableView.axaml`, `VariableView.axaml.cs` |
| `CryptoWorkBenchGuiTest/` | M | `AsyncPasswordGuiTests.cs`, `CwmInfoTests.cs`, `EditorCoordinatorTests.cs`, `EditorReviewCorrectionTests.cs`, `NativeEnterContinuationTests.cs`, `WorkbenchReviewRegressionTests.cs` |
| `CryptoWorkBenchGuiTest/` | ?? | `B1EditorIntegrationTests.cs` |
| `docs/mcp/` | ?? | `MCP-B1-editor-integration.md` |

I used the accepted single-writer, original-intent, observer and effect contracts as the standard of review. I compared the production and test diff with the AP3-01 packages, traced the surrounding service and native library calls, then ran the binaries and independent counterexamples. The B1 report supplied the claims and candidate evidence to check. [Architecture decision](MCP-00-R2-consolidated-architecture-decision.md) · [AP0 contracts, §§3–4 and 7–8](MCP-00-AP0-contract-finalization.md) · [AP3-01 inventory and packages](MCP-AP3-01-integration-assessment.md) · [AP3-02 acceptance](MCP-AP3-02-final-acceptance.md) · [B1 implementation report](MCP-B1-editor-integration.md)

## Findings

Locations in this section are relative to `CryptoWorkBenchAvalonia/` in the reviewed worktree. Probe labels correspond to the local evidence log described below. Severity takes account of the entry point and whether a custom host callback is required.

### B1-01 · High · Return can execute a different line after release begins

`Views/CryptoScriptEditView.axaml.cs:103–125`, `Controls/WorkbenchTextArea.cs:169–180`. P3/P6 require popup observers to have no power to redirect the admitted native continuation. The view hides the insight window synchronously, **then** reads the current caret line and starts execution. The final original-ticket check in the text area runs after that launch.

**Reproduced — `return-after-release`.** In a real edit view, put `VAR reviewAfterRelease=0x(12)` on line 1 and type the opening parenthesis after `Hash` on line 2. In the overload window's Closed callback, begin a drain and move the caret to line 1. Press Return through the native text-input entry. The newline was suppressed, the phase was Releasing, and a fresh execution ID was published. The interpreter successfully declared the variable from line 1. The job had not been submitted at the time of the Closed callback. The intended outcome is no job for the redirected or invalidated input. This is a synchronous P6 handoff defect. IR-N1-01 separately covers an **already accepted** job outliving cancellation of the newline. The old view also read the caret after popup handling. B1's new delegation still leaves that gap.

**Correction and regression.** Capture the original line, document and intent before popup callbacks. Validate the single-use continuation immediately before handing that line to execution, including any intervening presentation callbacks. Exercise real overload closure with caret movement alone, drain, detach and a control case that executes the original line exactly once. Assert interpreter state and job registration as well as editor text.

### B1-02 · High · A property observer can start a fresh text execution

`Services/Automation/WorkbenchAutomationRegistry.cs:266–298,315–326`. The new observation check covers `RunGuiAsync`. The public GUI-authorized `ExecuteTextAsync` path reserves the same interpreter queue and calls `Accept` separately. It has no corresponding synchronous observer check.

**Reproduced — `observer-execution`.** A service-bound `StatusViewModel.PropertyChanged` subscriber submitted a new execution ID using the authorized local GUI caller and current state version. Observer depth was 1. The reply was successful, the job completed, and `reviewObserver` existed. A fresh request must be rejected before reservation or interpreter effects. This is a missing P6 guard on an existing entry point. External MCP execution remains forbidden.

**Correction and regression.** Enforce the same observer rule at fresh GUI execution admission across both paths. Preserve the existing authorization, lookup and replay behavior. Invoke both entry points from property and collection observers with a free interpreter queue. Check Busy, unchanged state version, no job and no variable creation. The permanent test named `PropertyObserverCannotEditOrRunAndErrorReleasesDepth` never calls an execution service.

### B1-03 · High · Document replacement can register the same buffer twice and fault the workspace

`Services/Automation/EditorBridge.cs:78–98,147–163`, `Controls/WorkbenchTextEditor.cs:120–128,161–171`. Attach compares the proposed buffer with each registration's cached `RawDocument`. During a valid editor `DocumentChanged` notification an earlier subscriber can already see the new document before the bridge refreshes that cache.

**Reproduced — `duplicate-buffer`.** Prepare editor B with document D. Register A with a different document and with an editor-level DocumentChanged subscriber installed before the bridge subscriber. Set A.Document to D. In that subscriber, attach B with retention. Both attached registrations subsequently referred to D. Typing through selected B hit inactive A's document guard, left the text unchanged and faulted the global coordinator. The second attachment should have failed without altering either registration or availability. This is a B1 multi-document regression. It requires no foreign handler installed ahead of the workbench's raw document update guards.

**Correction and regression.** Check live editor-document references or serialize the identity transition before publishing an attach. Keep the original selection intact on failure. Repeat this ordering inside DocumentChanged, then verify one identity, normal typing, one undo history and a usable coordinator.

### B1-04 · High · Replace All writes matches that did not pass the protection check

`Controls/WorkbenchTextEditor.cs:81–101`. The adapter preflights one result set, retains a Boolean, then calls the native panel, which takes a new set of matches from its renderer and writes directly to the document. The final availability and ticket checks do not bind the search options, replacement or actual segments.

**Reproduced — `protected-search`.** Start with `open LOCK`. Allow changes to `open` and protect `LOCK`. During the read-only provider's first check of the allowed match, change the panel search pattern from `open` to `LOCK`. The result was `open done`. The provider never authorized editing the protected match. It changed only presentation state during preflight. This defeats the new P2 protection with a reentrant host provider. [Pinned SearchPanel implementation](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Search/SearchPanel.cs#L379-L392)

**Correction and regression.** Bind the checked search state and segments to the actual write, or abort if the native result set or permissions change before use. Keep grouped undo. Test a callback that changes the pattern, a replaced protection provider, and a successful native grouped replace. Rejection must preserve protected text, revision and redo.

### B1-05 · High · A retained registration accepts input after its service was disposed

`Services/Automation/EditorBridge.cs:56–60,164–170`, `Services/Automation/WorkbenchAutomationRegistry.cs:587–594`, `Services/Automation/WorkbenchEditorOperations.cs:26–37`. Disposal uses the presence of a **selected** registration to decide whether to detach **all open** registrations. Activation of an existing registration does not check the service's stopping state.

**Reproduced — `disposed-service`.** Retain A, open B, close selected B without selecting A, then await service disposal. Activate A using its original registration and type `!`. A remained attached, the text changed from `A` to `A!`, and `CanStartGui` was true. Closing the service must retire its retained registrations even when no view is selected. This is local B1 bridge cleanup. No asynchronous provider or AP4 shutdown protocol is required to reproduce it.

**Correction and regression.** Enumerate the open set on the UI thread during disposal regardless of selection and fence activation once stopping begins. Test disposal with zero, one and several retained views, including no selection. Verify rejection of old registrations and native input.

### B1-06 · High · Variable publication revives a draft after its view detached

`ViewModels/VariableViewModel.cs:104–108,117–145`, `Views/VariableView.axaml.cs:11–32`. The publisher clears its draft map and calls observable collection subscribers. A subscriber can detach the view, which invalidates the draft. When publication resumes it unconditionally installs the prepared map and clears the stale flag.

**Reproduced — `detached-draft`.** In a real VariableView, clear its DataContext and remove it from the window on the first Add during Refresh. After layout, the view had no TopLevel, but its former model reported a fresh snapshot. Selecting the published row and executing its retained parameter command wrote `LATE` to the interpreter. Document revision stayed 0. Observation blocked writes *inside* the callback but did not preserve the detach after it returned. This is a B1 publication/lifetime regression.

**Correction and regression.** Stamp publication with the original binding lifetime and revalidate it before making the draft committable. A detached generation cannot be refreshed into authority by an old view command. Test detach/rebind during Clear, Add and selection notification, including commits attempted after the outer publication returns.

### B1-07 · High · Opening another editor bypasses the update-group switch guard

`Services/Automation/EditorBridge.cs:85–98`, `Services/Automation/EditorCoordinator.cs:91–114`. Selecting an existing view rejects an open update on the outgoing document. Attaching and selecting a new retained view checks coordinator tickets, observers and phase but omits that outgoing-document check.

**Reproduced — `attach-during-update`.** With A inside a native `RunUpdate` and no active coordinator ticket, attach B using `retainCurrent: true` and type through B. A's group remained open while B changed from `B` to `!B`. This is a test of the supported registration API with an open native group. A normal active GUI ticket was already rejected in the permanent suite. A switch must fail before publishing B under either condition.

**Correction and regression.** Share transition preconditions between attach-and-select and activate. Check the outgoing group before changing bridge state and assert both success after closure and zero B effects while A's group is open.

### B1-08 · Medium · Completion commits after its controller was disposed during preflight

`Services/InfoCompletionController.cs:170–191`. The insertion request checks disposal, session, caret, anchor and version before invoking the read-only provider. The coordinator's final check knows about the editor ticket but knows nothing about the completion session.

**Reproduced — `disposed-completion`.** Open the real popup for `Info(AES`. Have the provider dispose the controller during `CanInsert` and return true. Request insertion. The disposed session inserted `AES-ECB`, leaving `Info(AES-ECB`. It also called the acceptance bookkeeping after disposal, which can subscribe its reopen policy again. The session should lose the right to insert or rearm. Existing close-dispose tests cancel earlier in the invocation list. [Pinned native insertion order](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/CodeCompletion/CompletionWindow.cs#L133-L139)

**Correction and regression.** Revalidate the original session, disposal, accepted range, writability and caret context after extensible preflight and before Replace. Fence the post-replace callback against disposal as well. Cover disposal, successor popup and caret movement during preflight. Assert no stale replace, reopen or extra undo entry.

### B1-09 · Medium · Paste can follow a different caret from the admitted intent

`Controls/WorkbenchTextArea.cs:209–215,227–246`. The saved integer offset omits the visual column. After the last selection/caret comparison, the new host `CanInsert` check can also move the caret. An empty native selection resolves insertion against the live caret.

**Reproduced — `virtual-paste`, `stale-paste`.** Delay a real platform clipboard read, move the virtual caret from visual column 6 to 10 on the same line at the same document offset, then release it. The selection object was unchanged and the result was `line      X`. In the second variant a provider moved the caret from offset 5 to 0 during the final permission query. Paste changed `12345` to `X12345`. Both intents should expire without a text or history effect. The offset-only limitation predates B1. The extra post-comparison callback is in B1's P4 change.

**Correction and regression.** Bind the full caret position and selection geometry and validate after callbacks that can move them, immediately before writing. Exercise virtual space, an empty selection, a rectangular selection and provider reentry. Seed a redo entry and verify it survives rejection.

### B1-10 · Medium · A same-name replacement can receive a stale variable edit

`ViewModels/VariableViewModel.cs:17–19,32–35,51–62,120–126`, `Services/Automation/WorkbenchAutomationService.cs:83–92`. The draft identifies the underlying declaration by identifier and type name. The version check is service-local, while the interpreter dictionary is process-wide.

**Reproduced — `replaced-variable`.** Capture a parameter row through service A. Using service B's ordinary serialized Mutate path, replace the declaration with a different object of the same name and type. Commit A's old row. The replacement's value changed to `STALE` and A reported a fresh snapshot. The ordinary single-service version-conflict test passes. This counterexample requires a second service or another legitimate state writer outside A's version domain. The shipped DI container registers one shared service. It is a gap in the new P5 identity contract, not evidence of two owners in the shipped window.

**Correction and regression.** Bind an immutable identity for the original declaration and verify it under the interpreter reservation. Align the draft's version domain with all permitted writers. Replace a same-name object through a separate service and ensure the old draft cannot write to it.

### B1-11 · Medium · Editing a parameter key redirects an existing row's Modify command

`ViewModels/VariableViewModel.cs:49–56,123–125`, `Views/VariableView.axaml:115,129–130`. The membership snapshot retains a parameter model reference but omits the key it represented. The Parameters grid lets the user edit that key. The commit uses the current row string.

**Reproduced — `retarget-parameter`.** On a published `#PAD` row, change the name to the existing `#IV`, enter `new`, and invoke Modify with that same row. The original PAD stayed `NONE` and the separate IV value was overwritten. Selecting a foreign parameter object is already rejected. A local row also needs a stable target. The editable key existed previously. B1's new membership check does not close this P5 gap.

**Correction and regression.** Bind the original parameter key and use it for value edits. If key renaming is supported it needs an explicit collision-checked rename contract. Exercise the editable grid and the command with an existing sibling key and assert that the sibling is preserved.

### B1-12 · Medium · A completed drain on an empty coordinator has no exit

`Services/Automation/EditorCoordinator.cs:91–99,295–305,307–326`. Before the first registration, BeginDrain returns a completed certificate. Resume recognizes a detached registration but omits the null-registration case. B1 also rejects Attach while Releasing.

**Reproduced — `empty-drain`.** On a fresh service, begin a drain, then resume with its own completed certificate. Resume returned false, phase stayed Releasing, and first attachment threw `ExecutionBusyException`. There is no ticket to finish or view to detach that would clear the condition. This is a B1 liveness regression. A completed empty transition must return to Unavailable or be declined before entering Releasing.

**Correction and regression.** Handle the empty slot explicitly while preserving certificate identity. Cover never-attached, selected-then-closed, foreign-certificate and Faulted cases. Verify subsequent valid first attachment.

### B1-13 · Medium · A closed selected editor can still invalidate a successor's intents

`Services/Automation/EditorBridge.cs:128–138`, `Services/Automation/EditorCoordinator.cs:78–87,97–99,102–114,122–129`. Closing a selected registration deliberately retains the coordinator association for drain. Attach can unlink the retired editor. The new select-existing path replaces the registration without that unlink.

**Reproduced — `old-binding`.** Keep A and B open, select and close A, then activate retained B. Trying to attach A to a different service threw Busy although A was detached. Changing A's orphan document advanced B's coordinator generation from 5 to 6. This is enough to invalidate B's pending input. A completed close must leave no control over an unrelated selection.

**Correction and regression.** Retire the original editor's association once its own obligations permit, on both successor paths. Verify A can be rebound after drain and that focus/document changes on A cannot cancel a pending B paste.

### B1-14 · Medium · Replace Next stalls at an already-correct match

`Controls/WorkbenchTextEditor.cs:83–93`. When the selected match equals the replacement, the adapter skips the entire native operation, including its navigation to the next match. This affects normal use without a custom callback.

**Reproduced — `replace-progress`.** With `cat CAT`, case-insensitive search for `cat`, replacement `cat`, select the first match and invoke Replace Next three times. A plain pinned TextEditor reached `cat cat` at caret 7. The workbench stayed at `cat CAT` at caret 3, with the original selection. The no-op test checks version and redo but never tries to reach a differing subsequent match.

**Correction and regression.** Keep the no-write/no-history behavior of an identical match and preserve forward search progress. Use the mixed-case example, repeated replacement, wraparound and a pre-existing redo stack on both command routes.

### B1-15 · Low · An already detached Markdown view can install a live link command

`Views/InfoView.axaml.cs:19–36,58–70`. Detach invalidates the current command token. A later DataContextChanged event unconditionally creates a fresh one even when the view remains detached.

**Reproduced — `markdown-detached`.** Attach an InfoView, remove it, then change its DataContext. The former command was correctly retired. The newly installed command still opened internal documentation on the replacement model and enabled Back while the view had no TopLevel. The demonstrated effect is presentation/navigation only.

**Correction and regression.** Track the detached interval and install a new live token on reattachment. Preserve the documented initial construction behavior. Test old and newly obtained commands after detach/context change, then verify that links work on reattachment.

## Assessment of R1–R8

| Area | What holds | What remains |
|---|---|---|
| R1 · Global coordinator | The application registers one shared service. Its bridge keeps per-document registration, GUID, version and revision, with one UI-affine coordinator and active slot. Normal A/B activation retains separate history stacks. Target-aware commit, generation and original identity reject replay after a switch, closing/reopening or switching away and back. A nonselected close uses registration equality. | B1-03, 05, 07, 12 and 13. Opening, selecting, replacing and closing do not yet obey one transition contract. Keep one global writer. Independent tab locks would not fix these bugs. |
| R2 · Commands and search | The text area obtains the installed editing bindings, adds undo, redo and copy, and consumes the native delegation before dispatching the actual binding. Active/observing/releasing paths reject a new write. The panel and text-area replace routes, stale retemplate route and invalid-regex guard were exercised. Native Find and panel text-field commands retain their own routes. | B1-04 and 14. Enumeration of commands in the blocked state is weaker than demonstrating successful editing, caret movement, options and undo for each. The drain test compares document snapshots, which do not include overstrike mode, selection or redo. |
| R3 · Completion and overload | The native request hides the popup before looking up its selected item. The host captures its original item/range on Closed, removes the handler, and receives the in-flight invocation. Retained workbench completion items cannot call Replace. Normal Tab/Enter insertion, disposal before the request, close-time swap and original-document/generation rejection pass. Reopen uses document anchors and the post-accept generation. | B1-01 and 08. Popup identity checks on Closed and overload provider notification do not validate the subsequent caller's intention. The acceptance callback can rearm after disposal. Desktop mouse acceptance and overload navigation are open. |
| R4 · Clipboard | Native Copy can run during drain under observation and raises TextCopied synchronously. Native Copy initiates platform export without waiting for acknowledgement. Strict Cut/Paste tickets bind registration, document and revision across the await. Successful and canceled delayed exports were independently exercised. Ordinary changed selections and switched documents reject without an undo group. | B1-09. Rectangle cut and normalized ordinary paste are separate verified cases. The permanent test named for both clears the rectangle before pasting. No independent desktop rectangular paste/drag or cross-application result. The approved event limitation stands. |
| R5 · Variables and parameters | Within one service, GUI ticket admission precedes interpreter reservation. The expected version is checked under reservation before mutation. Capture uses detached models. Busy refresh marks the previous display stale. The reservation is released even when presentation throws. KeyMap and the already-admitted book clear use the injected service. | B1-06, 10 and 11. Detached object membership alone proves neither live declaration identity nor the parameter key. A publication stamp taken before observable callbacks is insufficient for a view that detaches during those callbacks. State-only commits kept the editor revision unchanged, including the bad late commit. |
| R6 · Observers and local lifetimes | Host input continuation is consumed before TextEntering subscribers. TextEntered, editor TextChanged, bindable properties, diagnostics and variable publication use observation. The paired guard handles synthetic finish for a rejected outer start. Input/caret handlers are removed during view detach, and normal completion disposal removes subscriptions from the original objects. | B1-01, 02, 06, 08 and 15. A scope ends at the callback return. It cannot validate the continuation that follows. The Markdown token retires the old command but can be renewed while detached. No claim is made for arbitrary preinstalled raw document handlers. |
| R7 · Existing services | Editor writes check GUI collision/drain initially and again after confirmation. The original identity lease and one-change guard still protect the v1 MCP edit. Replay and caller restrictions remain in the service. Save capture, commit and synchronous save reject a controlled presentation observer. Save commit reserves the queue, binds the original snapshot, checks again after read-only publication and releases in finally. | B1-02 and 05 expose alternate admission and disposal routes. B1-01 is an editor continuation entering GUI execution after local invalidation. No public DTO, transport, external execution permission or lease was added. Generation-bound picker/confirmation and full job finalization retain their original package assignments. |
| R8 · Coverage | Every numbered AP3-01 item appears below. The implementation report distinguishes many later-package obligations correctly. | Its grouped rows overstate coverage for search, caret semantics, completion disposal, variable target/lifetime and some local transitions. Passing one route or testing rejection of every binding does not prove every positive case. |

### Multi-document and AP3-02 regression assessment

A per-registration document lifetime plus one coordinator is a suitable foundation for multiple tabs. A ticket captures the original registration, GUID, document, version, revision and input generation. The bridge's current registration is separately validated at commit. Workspace generation is useful for state drafts because transferring focus from the editor to the variable grid is a normal edit. The failures concentrate at boundaries where code conflates selected with open, trusts cached identity during notification, or treats first attach differently from activation. Fix these internal transitions before exposing more callers. No tab UI or public document addressing is needed to exercise or correct them.

AP3-02's owner-bound history capability, interpreter reservation and two-step anchored line replacement remain in place. The original 589 GUI case names, including multiplicities, occur in the new suite. Some setups and assertions changed as described below. The independent `paired-finish` probe used the actual history capability, undo and redo. Across three legitimate groups it saw three starts, three finishes and three rejected recursive insertions. The history transition was `a → b → a → b`. The final update flag was false, pending count zero and fresh admission available. In the pinned library, outer BeginUpdate starts the undo group before notifying, and outer EndUpdate unwinds the group before UpdateFinished. The host's early handler balances the synthetic start, and its paired handler stops synthetic finish before later host listeners. A nested BeginUpdate inside an already active group has different native notification semantics. This evidence establishes the host-installed ordering only. [Accepted history and Enter behavior](MCP-AP3-02-final-acceptance.md) · [Native TextDocument update ordering](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextDocument.cs#L418-L459)

The ordinary calibrated Enter/Unicode/history tests passed. B1-01 begins a fresh job for a different line after a popup callback. B1-03 introduces a new way to fault the entire history workspace. Neither result invalidates the historical AP3-02 native acceptance. They do prevent that acceptance from being carried forward to this candidate.

## AP3-01 coverage audit

The number in the middle column is the classification for the indicated work. **1** means implemented and verified in B1 for the stated extent. **2** means a previously accepted mechanism. **3** means explicitly deferred. **4** means incomplete protection or insufficient evidence. For mixed items, the first number describes the immediate B1 assessment and the text identifies the accepted or deferred portion. Native checks common to several rows are listed in the acceptance section.

### Mutation paths · 51 of 51

| AP3-01 item | Class | Independent disposition |
|---|---|---|
| M01 | 1 | Ordinary native text input and direct PerformTextInput use the one-event original ticket. TextEntering/TextEntered rejection cases passed. Return is separated below. |
| M02 | 2 / 4 | Accepted Unicode input core survives the automated suite. The physical Swiss input matrix on final B1 is unverified. |
| M03 | 2 | Early IME disable is unchanged in effect. The agreed multi-stage CJK residual risk remains. |
| M04, M05 | 4 / 3 | Ordinary Return, KP_Enter and Shift+Enter regressions pass. The popup-before-execution case B1-01 fails. Asynchronous job/output ownership is P8/AP5. |
| M06, M07 | 1 | Installed backspace/delete families and routed Delete hit the guarded command path. The tests demonstrate blocked routing. They do not enumerate every successful movement/selection combination. |
| M08 | 4 | Tab/Shift+Tab have an explicit focused-key gate. The blocked Shift+Tab and completion Tab cases do not establish the full admitted multiline/rectangle/strategy matrix. |
| M09 | 2 | Native Undo/Redo bindings and prior history receipts remain. A/B separation and the independent undo/redo probe support ordinary operation. B1-03 breaks the prerequisite of distinct buffers. |
| M10, M11 | 1 | Delayed Cut export, selection invalidation, provider failure, whole-line partial protection and rectangle cut covered. A completed external export survives stale deletion rejection. |
| M12, M13 | 4 | Normalized newline/tab paste, null/failure and stale-document rejection have evidence. Full caret identity and post-provider validation fail B1-09. Rectangle paste equivalence is not demonstrated by the rectangle-cut test. |
| M14, M15 | 4 | Both panel and text-area replace commands reach admission. B1-04 and 14 invalidate the stronger protected-range and native progression claims. |
| M16 | 1 | Template reinstallation removes the old panel and blocks its routed replacement fallback. Document replacement closes the old session. |
| M17, M18 | 4 | Live native completion and retained-item refusal pass. Disposal during writability checking can still insert and rearm. Overload closure can redirect Return. B1-01 and 08. |
| M19 | 2 | Accepted AP3-02 owner-bound capability, history index admission and grouped replacement. Independent undo/redo control passed. |
| M20, M21, M22, M23 | 2 / 4 | Early drag disable, option enforcement and template configuration remain the accepted mechanisms. Final Windows inbound, source and move gestures were not verified. |
| M24, M25 | 4 | DeleteLine and whitespace commands occur in the guarded editing set. Snapshot equality during drain is insufficient evidence for authorized selection, composite grouping and callback failure in these command families. |
| M26 | 1 | Native uppercase and undo were exercised after admission. Lower/invert share the registered wrapper. Their positive Unicode behavior was not separately measured here. |
| M27 | 2 / 4 | The native TitleCase binding is included in the denial enumeration. D07's limitation remains. This suite does not newly establish its authorized exception/receipt semantics. |
| M28, M29, M30 | 4 | Tab conversion, indentation and overstrike are routed into the installed command or Tab boundary. No full positive conversion/strategy/virtual-caret and overstrike-state matrix follows from a document-only assertion. |
| M31 | 1 | Copy under drain delivers native TextCopied without a writer ticket. Find and ordinary selection retain native reading routes. Replace Next's navigation regression is B1-14. |
| M32 | 4 | Synchronous text and model publication have guards. ExecuteTextAsync from a property observer is an admitted fresh writer, B1-02. |
| M33, M34 | 4 / 3 | Initial Clear is before the new registration. Alternate attach, duplicate-buffer handling, empty drain, disposal and successor unlink have local defects B1-03, 05, 07, 12 and 13. The app-wide shutdown fence is AP4. |
| M35 | 3 | Picker/original consent and full book admission stay with P8/AP4. |
| M36 | 1 / 3 | The admitted clear now uses the originating service without another queue reservation. End-to-end book intent, job and state finalization stay with P8/AP5. |
| M37 | 1 / 3 | Existing v1 editor mutation rejects current GUI/observer/drain collisions, rechecks after confirmation and keeps original snapshot/identity protections. V2 receipts, credentials and producer binding stay with P8/AP4/AP6. |
| M38, M39 | 3 | Editor execution, anchored return output and final job ownership stay with P8/AP5/AP6. The separate synchronous B1-01 failure is recorded under M04/M05. |
| M40 | 3 | Print/Info posts, navigation and job correlation stay with P7/AP5. Synchronous receiving properties are assessed under OBA14–OBA18. |
| M41, M42 | 3 / 1 | Full asynchronous and synchronous save/file authority stays with AP4. B1's narrower observer rejection exists on capture and both commit routes. |
| M43 | 3 / 4 | Provider/job/epoch shutdown remains AP4/AP6. The regression in disposing already-owned retained registrations, B1-05, is B1 work. |
| M44 | 2 / 1 | AP2.1 identity/mutation locks and change guards remain. The new paired guard passed the legitimate and synthetic finish cases under the host ordering. |
| M45, M46 | 4 | The new GUI ticket and service-local version reject common conflicts. Write-through after detach, same-name replacement and parameter-key substitution remain, B1-06, 10 and 11. |
| M47, M48 | 3 | Password/provider lifecycle and public protocol/leases retain their AP4–AP6 assignments. |
| M49 | 1 / 3 | KeyMap uses its supplied/status service. Posted producer lifetime and job/GUI execution handoff remain P7/P8/AP5. |
| M50, M51 | 4 | Busy/stale indication and detached copies are present. Observed publication can revive an invalid binding, and editable parameter identity can redirect a write. B1-06, 10 and 11. |

### Observer groups · 31 of 31

| AP3-01 item | Class | Independent disposition |
|---|---|---|
| OBA01 | 1 | Event-bound native delegation is consumed before calling subscribers. Direct nested text input and observable document writes are rejected on the tested paths. |
| OBA02 | 1 | Paired UpdateStarted/UpdateFinished guard and Changing veto. Legitimate outer history, undo and redo each completed once in the independent probe. |
| OBA03 | 4 | TextEntered subscribers run under observation, but popup callbacks inside the admitted Return handling can invalidate or redirect the action before job launch. B1-01. |
| OBA04 | 2 | The original caret/status callback uses Observe. Workbench bindable status publication adds an inner scope. This is evidence for that subscriber only. |
| OBA05 | 4 | Normal attach/detach removes named input and caret handlers. Retained registration cleanup/selection transitions are incomplete, B1-03, 05, 07 and 13. |
| OBA06 | 4 | Sender-aware overload closure and provider notifications are present. The caller continues after closure with a live caret, B1-01. |
| OBA07, OBA08 | 1 | Caret/key/document changes update or cancel the original controller. UpdateStarted/Changed aggregate private state and filter the sender. UpdateFinished handles the resulting presentation under Observe. |
| OBA09, OBA10, OBA11 | 4 | Clearing the selected item and checking version handles normal close/replacement. Post-close provider disposal allows a stale commit and can rearm the anchor policy, B1-08. The anchor Changing handler itself checks its sender and updates private state only. |
| OBA12 | 4 / 2 | Original sender/revision reconciliation remains. New registration admission reads a stale cached document earlier in that same notification, B1-03. |
| OBA13 | 2 | History notification is observational. Only the accepted single-use capability enables the two native mutation steps. |
| OBA14, OBA15, OBA16 | 3 | Posted Print/Info and KeyMap callbacks retain P7/AP5 lifetime/accounting work. B1 guards synchronous bindable notification and injects KeyMap's service. |
| OBA17, OBA18 | 4 | Common property dispatch and Main/Info/KeyMap scope exist. The public text-execution route still admits work from that scope, B1-02. |
| OBA19 | 1 | Diagnostics collection writes run in Publish, including the nested HasDiagnostics notification. Interpreter-entry coverage of any such observer is qualified by B1-02. |
| OBA20 | 4 | A reentrant variable collection subscriber can detach the view and the outer publication can later restore write authority. B1-06. |
| OBA21 | 4 | Info-history delivery checks disposal and unsubscribes. Old Markdown commands retire, but detached context changes recreate a live one, B1-15. |
| OBA22 | 2 / 4 | Existing identity/save leases still protect their immediate property transitions. The new multi-view document-change/registration interaction fails B1-03. Save completion is AP4. |
| OBA23, OBA24, OBA25 | 3 | Confirmation and password dialog/provider cancellation, posts and drain remain AP4/AP5/AP6 as assigned. |
| OBA26, OBA27 | 3 | Registry timers, retention, cancellation and pipeline event finalization retain their existing later-package authority boundaries. |
| OBA28 | 3 | Posted book/line presentation and output ownership stay with P8/AP5. Local synchronous stale draft acceptance is covered under OBA20. |
| OBA29 | 3 | Save picker, overwrite, delete/write, unlock and asynchronous finalization remain AP4. |
| OBA30 | 4 / 3 | Local disposal misses retained views in an empty selection, B1-05. Full desktop and provider shutdown remain AP4. |
| OBA31 | 4 | Focus advances input generation and template renewal invalidates the old search panel. Virtual caret motion escapes the clipboard stamp, B1-09. Native drag/keyboard acceptance is open. |

## Independent verification and test limitations

All reruns below used the existing candidate test assemblies in this worktree, .NET SDK 10.0.401 and VSTest 18.7.0. They ran sequentially. The Debug and Release GUI product DLLs loaded from the respective test directories had the same SHA-256 hashes as the corresponding desktop product DLLs. The countercheck also compared portable-PDB source checksums against available non-generated C# files, matching 42 GUI and 35 GUI-test source documents in each configuration with zero differences among the files checked. This was a binary rerun with source corroboration, not an independent locked restore or desktop build.

| Evidence | Independent result |
|---|---|
| Accepted AP3-02 baseline | Read `artifacts/b1/baseline.trx` and the accepted report. 589 executed, 589 passed, zero skipped. This was a historical result, not a fresh run of the prior binary. A multiset comparison found all 589 baseline case names in the new full suite and exactly 35 additions. |
| Candidate Debug GUI | **624/624 passed** in a fresh execution. |
| Candidate Release GUI | **624/624 passed** in a fresh execution. |
| Candidate focused Debug | **187/187 passed** in a fresh execution. The case-name multiset matched the author's targeted TRX. This is a subset of the 624, not an extra 187 distinct cases. |
| Candidate MCP | **89/89 executed tests passed** independently in Debug and Release. NUnit separately listed the explicit `InstalledCodexDiscoversAndCallsStatus` compatibility probe as skipped. It is outside the 89 executed results. |
| Desktop product DLL hashes | Debug `92D36BF92E15473D747966657D655954953BE3125B1BECA3627D3C460BD67D7D`. Release `71A7076530985CF025DC807E1BD25370D7793DE6F78B09E2045902823D284655`. Hash equality identifies a product assembly. It does not exercise a desktop session. |

**Reproduction.** The review-only headless harness, its exact inputs, and its final output are under the ignored `artifacts/b1-independent/` directory. It references the existing Debug GUI assemblies using the repository's existing friend-assembly access. Controls, native routed commands, CompletionWindow, dispatcher, state service, interpreter queue and document guards are the production implementations. The read-only providers and controlled clipboard delays are test doubles. Reflection retrieves a private popup instance and binds the test platform clipboard. No tracked test or product file is needed to run the isolated harness beyond the candidate itself. The findings give enough steps to reconstruct the tests independently if local ignored evidence is unavailable. [Probe source](../../artifacts/b1-independent/probe/Program.cs) · [Probe output](../../artifacts/b1-independent/probe.log)

From the worktree, the full-suite command shape was `dotnet test <project>/bin/<configuration>/net10.0/<project>.dll` with a TRX logger. The focused filter was `FullyQualifiedName~B1EditorIntegrationTests|FullyQualifiedName~EditorCoordinatorTests|FullyQualifiedName~NativeEnterContinuationTests|FullyQualifiedName~History`. The standalone probe runs with `dotnet run --no-restore --project artifacts/b1-independent/probe/probe.csproj -p:UseSharedCompilation=false` after building it and placing the candidate's `runtimes/win-x64/native/libHarfBuzzSharp.dll` in its own output directory. The source-check portion expects the worktree as its current directory. [Debug GUI TRX](../../artifacts/b1-independent/CryptoWorkBenchGuiTest-Debug.trx) · [Release GUI TRX](../../artifacts/b1-independent/CryptoWorkBenchGuiTest-Release.trx) · [Focused TRX](../../artifacts/b1-independent/focused.trx) · [Debug MCP TRX](../../artifacts/b1-independent/CryptoWorkBench.Mcp.Tests-Debug.trx) · [Release MCP TRX](../../artifacts/b1-independent/CryptoWorkBench.Mcp.Tests-Release.trx)

**Clipboard controls.** The independent `late-cut` test forwarded the successful delayed export to the underlying headless platform clipboard. After changing selection during the wait, the clipboard contained `sou`, the document remained `source` and the ticket count returned to zero. With provider cancellation, the clipboard retained `before` and the document stayed intact. Neither wait held a document update or the interpreter queue. In contrast, the permanent B1 proxy returns a supplied Task for delayed SetDataAsync and does not forward the successful export. That test can prove rejection of deletion, but cannot establish what an external clipboard received. Native Copy raises TextCopied synchronously after initiating its export. The owner's approved omission of TextCopied on coordinated Cut and TextPasted on coordinated Paste requires no reconsideration. [Pinned clipboard implementation](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/EditingCommandHandler.cs#L390-L421)

**What the permanent tests establish.** The suite contains real headless routed editing, window insertion and in-process MCP checks. The four search route combinations, stale-panel tests and close-swap/close-dispose cases are meaningful. The paired finish tests count legitimate delivery and bound recursion. The modified baseline tests mostly add a registered workbench editor or align the expectation with Busy/stale behavior. The editor-review correction test now separately asserts denial of a synchronous swap and posts the requested swap. The Enter test now checks an original current ticket with no active-operation rights inside the observer. Those are substantive changes to test timing and expected authority, so retention of all names alone does not prove behavioral equivalence.

**What they miss.** The two-document tests use controls and a test host, not a tab container. They close the selected view during a drain, but do not dispose a service with retained views and no selection. The parameter test checks the wrong *row object* and a version advance in the same service, not mutable key identity or a replaced declaration in the shared interpreter. The completion tests cancel before or during Closed and do not cancel from the subsequent writability callback. The no-op search test checks redo but misses a later differing match. The command-denial test checks text snapshots but omits option/caret/selection assertions. A test that executes an assertion inside a swallowed observer also needs external effect/counter assertions. The existing tests that check `ObserverFailures` avoid that particular false positive.

## Final Release native acceptance

**Open.** No interactive final-B1 Release desktop session was executed in this review. The candidate already fails executable host invariants. The prior native smoke exercised an intermediate Debug build. The physical Swiss keyboard sign-off and historical Return/history desktop matrix apply to AP3-02.

After correction, identify the exact Release binary and record native Return and KP_Enter, history and undo/redo, search panel buttons and navigation, completion by mouse and keyboard, overload selection and closing, variable/parameter cell commit, rectangle and drag gestures, clipboard transfer with an external application, and physical Swiss AltGr/dead-key input. Record visible outcomes on the corrected binary. Synthetic clipboard injection and headless tests remain separate evidence classes. [AP3-02 native acceptance and owner addendum](MCP-AP3-02-N1-R-native-history-acceptance.md) · [B1 native smoke and open cases](MCP-B1-editor-integration.md)

## Boundaries and corrective priorities

- **First, restore admission and ownership.** Address B1-01–07. Bind Return before callbacks, close the fresh-service-entry observer bypass, make buffer registration/selection/disposal atomic across their synchronous notification boundaries, use the same authorized matches for Replace All, and prevent a detached view publication from re-enabling state writes. Keep one workspace coordinator.
- **Then, repair intent lifetime and editing behavior.** Address B1-08–14. Cover post-preflight completion disposal, full caret geometry, stable declaration/parameter identity, empty-slot drain, retired editor unlink and no-op Replace Next progression. Re-run both positive and rejection paths with undo/redo and subsequent admission assertions.
- **Complete presentation cleanup and native evidence.** Address B1-15, repeat the focused and full suites, and run the final Release matrix on the corrected candidate. Keep desktop and historical evidence separately identified.

P7/P8 retain posted producer lifetime and GUI job handoff. IR-N1-01 remains P8/AP5. N1-F02, the Ctrl/Alt history help discrepancy, remains open. AP4 retains full shutdown/provider drains, picker consent and save/file commit. AP5 retains asynchronous execution/output authority and job/state finalization. AP6 retains v2 credentials, epoch/lease, transport and receipt projection. B3 owns public MCP document addressing. Full tab UI, docking, Script Book persistence and shared-buffer multi-view support remain outside B1. The early AvaloniaEdit host boundary, clipboard event decision and unsupported multi-stage CJK IME risk remain accepted.

**Final readiness — CHANGES REQUIRED.** Seven High, seven Medium and one Low finding were reproduced. Correction and independent regression checks precede the outstanding final Release desktop acceptance. No product changes, staging or commits were made by this review.

