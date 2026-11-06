# MCP-B1-F1-R1 independent correction rereview

**CHANGES REQUIRED** · 2 November 2026 · `feature/mcp-ap3`

The original 15 reproductions no longer produce the effects reported against B1. The broader invariants do not all hold. This rereview confirmed **14 findings: 6 High, 7 Medium and 1 Low**. On the original finding matrix, **5 are FIXED and 10 are PARTIALLY FIXED**. A single invariant can have several counterexamples, so those two counts describe different things.

The most consequential results are observable without a native desktop. A replacement attach published two registrations for the same buffer and the next keystroke faulted the workspace. Replace All wrote a range whose permission had been revoked before acceptance. Paste removed a prefix outside its approved segments. A controlled observer started a real interpreter job through a second service. Tests on the final binaries nevertheless passed **662/662 GUI cases in each configuration**.

## Baseline and evidence identity

| Check | Result |
|---|---|
| Worktree | `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3` |
| Branch, HEAD and index | `feature/mcp-ap3`, `dfea2055992254462b937477a8697754fe39a4d6`, empty index |
| Candidate on entry | 32 unstaged tracked modifications, 953 insertions and 269 deletions. Five untracked files: the two B1/F1 GUI test fixtures and the B1 implementation, independent review and F1 correction reports. The preservation manifest records the individual paths. |
| Final Release desktop DLL | `CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.dll` · SHA-256 `4B1F56C95E2C2F21B78DA4A7CBAE79CB3106E6B111233DA50AB05116598B00C4` |
| Final Release product DLL | `CryptoWorkBenchAvalonia.dll` · SHA-256 `C580EB3888509DCB7631ADCA65A0DB35178DC80ECDE7B089D28FCD384CEA842C` in the desktop and GUI-test output directories |
| Product DLL for new counterchecks | Final Debug `EB1505C088BEEF4FC1E3A162EC1C9ACB0E5702C2A30703697DFFBF382DB1FF6F`. The same harness also ran against the preserved B1 Debug product DLL `92D36BF92E15473D747966657D655954953BE3125B1BECA3627D3C460BD67D7D` in an isolated control directory. |

I compared the cumulative product changes with the accepted HEAD, examined the untracked permanent tests, reran the original probe against F1, and varied callback ordering against the compiled controls and services. The new probes ran on Avalonia Headless. They used real editor documents, native selection objects, commands, job registration and interpreter variables. Reflection was used for fixture access to the popup, the weak identity, and the different internal Mutate signatures in the B1/F1 binary comparison. No product or permanent test files were edited. [Original independent review](MCP-B1-independent-review.md) · [F1 correction report](MCP-B1-F1-corrections.md) · [B1 implementation](MCP-B1-editor-integration.md) · [AP3-02 acceptance](MCP-AP3-02-final-acceptance.md) · [AP0 contracts](MCP-00-AP0-contract-finalization.md) · [AP3-01 inventory](MCP-AP3-01-integration-assessment.md)

## Disposition of the original findings

| Finding | Status | Independently verified behavior and limit |
|---|---|---|
| B1-01 · Return handoff | **PARTIALLY FIXED** | The original overload-close/drain/caret attack produced no job or variable. The view captures its line before popup callbacks and checks again after diagnostics. An injectable registry callback can still revoke the intent before the interpreter is reserved (§08). |
| B1-02 · Observer execution | **PARTIALLY FIXED** | Same-service fresh requests are rejected before reservation. Existing replay succeeds and a subsequent ordinary submission creates the variable. A synchronous observer can reach the same interpreter through another service (§06). |
| B1-03 · Duplicate buffer | **PARTIALLY FIXED** | Attach now reads the live editor document in DocumentChanged. The original duplicate is rejected and ordinary independent buffers remain editable. Replacement attach can publish a duplicate after its first check (§01). |
| B1-04 · Protected Replace All | **PARTIALLY FIXED** | Changing the search pattern during preflight leaves `open LOCK` intact. The plan freezes offsets and replacement. Decisions from one mutable provider can still be inconsistent before the plan is accepted (§02). |
| B1-05 · Disposal | **PARTIALLY FIXED** | Disposal with retained registrations but no selection retires them. Old native input is denied and a new service can bind the retired control. An in-flight attach can publish a registration after disposal completes (§07). |
| B1-06 · Variable-view lifetime | **PARTIALLY FIXED** | Detach during publication leaves the original draft stale, creates no receipt and preserves state. After a successor attaches to the same VM, an already-retained refresh command can discard the successor's draft (§12). |
| B1-07 · Native groups and switching | **PARTIALLY FIXED** | Direct attach during an outgoing native group throws Busy before binding B. It succeeds after the group ends. Swapping the outgoing document first defeats the group check (§04). |
| B1-08 · Completion disposal | **PARTIALLY FIXED** | Disposing during preflight prevents insert/rearm. Disposing or moving the caret during UpdateStarted also prevents insertion, but the rejected request destroys redo (§10). |
| B1-09 · Paste geometry | **PARTIALLY FIXED** | The delayed virtual caret and provider relocation attacks preserve the original text. Successful rectangular paste and grouped undo work. The snapshot can be displaced during native selection processing (§03), and a legitimate selection wholly in virtual space no longer pastes (§09). |
| B1-10 · Replacement declaration | **FIXED** | The strong local reference obtained from the draft's weak origin must equal the live dictionary entry under reservation. A different same-name declaration keeps `OTHER`; a fresh draft succeeds. Removal and collection of the origin also prevent writing. In-place version conflicts and stale presentation are separate findings (§11, §14). |
| B1-11 · Parameter key | **FIXED** | Changing the captured PAD row's displayed key to IV writes neither key and does not advance the service version. The positive fresh PAD edit succeeds. The command uses the captured key and the grid exposes key names read-only. |
| B1-12 · Empty drain | **PARTIALLY FIXED** | The never-attached, already-completed certificate resumes to Unavailable and first attachment succeeds. Foreign and consumed certificates remain invalid. With an actual group that closes during release, a logically empty drain may never signal (§05). |
| B1-13 · Retired editor | **FIXED** | Retired A rebound to another service. Changing its orphan document left B's generation at 5. The permanent delayed-paste test verifies B can complete its original paste. The explicit unfinished-drain case in §05 occurs before retirement. |
| B1-14 · Replace Next | **FIXED** | The `cat CAT` example progresses to `cat cat` with the same final selection as the pinned editor. Both command routes, wraparound, redo preservation for navigation and undo of the real replacement pass. Mixed-match **Replace All** has a separate anchor regression (§13). |
| B1-15 · Markdown commands | **FIXED** | On detach, both the old command and the command obtained after an off-tree DataContext change remained inert. Reattachment produced working navigation on the replacement model; the old command stayed disabled. The B1 control allowed the off-tree navigation. |

## Confirmed findings

Source locations below are relative to `CryptoWorkBenchAvalonia/` in the identified worktree. Each probe name identifies a row in the new log. “B1 control” refers to the same experiment run against the preserved pre-F1 binary. [Countercheck source](../../artifacts/b1-f1-r1/probe/Program.cs) · [F1 results](../../artifacts/b1-f1-r1/counterchecks.log) · [B1 control results](../../artifacts/b1-f1-r1/b1-control.log)

### R1-01 · High · Replacement attach can still alias a retained document

**Location.** `Services/Automation/EditorBridge.cs:84–100`; `Services/Automation/EditorCoordinator.cs:142–149,305–312`. Residual B1-03, also reproduced on B1.

**Sequence and effect.** Keep A open, select B, then use ordinary replacement attach for incoming C with a distinct buffer. The initial uniqueness check succeeds. Detaching B publishes WorkspaceChanged. In that observer, assign A's document to the still-unregistered C. The second BeforeAttach checks the slot but omits document uniqueness. Both registrations finish attached to the same document. Typing on selected C leaves the text unchanged and the global coordinator **Faulted**, with zero tickets and no open update. The expected outcome is refusal of C, one registration for that buffer, and a usable workspace. This needs no handler ahead of the workbench's raw document guard. Probe `attach-alias`.

**Correction and permanent test.** Treat replacement as one transition. Revalidate the incoming live buffer and all publication preconditions after lifecycle callbacks, before insertion into the open set. Define a consistent failure selection. Reproduce the three-editor sequence and verify unique ownership, subsequent typing and separate Undo/Redo.

### R1-02 · High · The last provider callback can revoke permission for an earlier match

**Location.** `Controls/WorkbenchTextEditor.cs:99–118,125–137`. Residual B1-04 in the F1 concrete-plan implementation.

**Sequence and effect.** Search `X---X`, replace with `Y`. The provider authorizes offsets 4 and 0 in the first pass. In the final pass it authorizes 4, then marks 4 protected while evaluating 0. The same provider instance remains installed and all options, text and generations are unchanged. This revocation occurs **before** the final all-ranges check returns and before RunUpdate. The recorded visits were `first:4, first:0, final:4, final:0`. F1 produced `Y---Y` and advanced the revision twice. No accepted plan should include the now-protected right match. This differs from the documented case of search options changing after acceptance. Probe `search-last-permission`.

**Correction and permanent test.** Authorize the batch against an immutable or versioned protection policy, or use a conservative supported path for providers that cannot establish consistent batch permissions. Repeating the same sequence of callbacks is insufficient. Change a different range on the final visit and assert zero text, revision and redo effects on rejection.

### R1-03 · High · Native paste can replace text outside the captured permission set

**Location.** `Controls/WorkbenchTextArea.cs:283–303,307–309`. Residual P4 protection gap in the F1 temporary-provider design. The broader live-provider behavior was also observable on B1.

**Sequence and effect.** Select `LOCKok` and paste `P` with a provider that permits deletion only of the final two characters. The control case yields `LOCKP`. During the selection's native UpdateStarted, install a legitimate successor provider allowing the whole selection. F1 had already exposed its temporary provider. Native SimpleSelection queries the text area's **current** provider after that notification, yielding `P` and deleting the originally protected prefix. The finally correctly leaves the successor installed. The defect is the expanded write set. With a restrictive successor, text was unchanged but the seeded redo entry vanished. A throwing successor left its provider installed and the slot Faulted. Probe `clipboard-policy`. [Pinned SimpleSelection ordering](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/SimpleSelection.cs#L51-L87)

**Correction and permanent test.** Make the captured write set authoritative independently of the publicly replaceable provider, or prevent displacement over the native mutation boundary. Preserve a legitimate successor at finalization. Test expansion, narrowing and exception with a partial selection and seeded redo. Confirm actual changed ranges, provider identity and recovery.

### R1-04 · High · Swapping the outgoing document conceals an open update group

**Location.** `Controls/WorkbenchTextEditor.cs:150–163`; `Services/Automation/EditorCoordinator.cs:112–115,131–138`. Residual B1-07, present on the B1 control.

**Sequence and effect.** Begin a native update on A without a coordinator ticket. Replace A.Document while the old group remains open. Attach and select B with retention, then type on B. BeforeDocumentChange checks phase, active operation and observation but omits the old document's update state. OutgoingViewBusy subsequently inspects the replacement document. The swap and attach succeeded, the original group was still open, and B changed to `!B`. The direct attach without the intervening swap is correctly denied. AP0 M34 requires document replacement at GUI idle. Probe `swap-open`.

**Correction and permanent test.** Apply the outgoing group check at the document transition itself, or retain the obligation against the original document through closure. Check both activate and attach after an attempted swap, then allow them once the original group ends.

### R1-05 · High · A native group can finish without waking the pending drain

**Location.** `Services/Automation/EditorCoordinator.cs:305–326,342–347`; `Controls/WorkbenchTextEditor.cs:209–214`. Residual drain liveness failure, also present on B1.

**Sequence and effect.** Open a native update without a ticket. Begin the drain with the selection retained, or close the selection before ending the update. EndUpdate completes with no remaining operation. In both cases the certificate remains incomplete, ResumeGui returns false, attach throws Busy, and the phase stays Releasing. An unrelated empty Observe triggers CheckDrain and only then can the certificate complete and resume. A caller legitimately awaiting that certificate has no obligation to manufacture another callback. Probe `drain-native-group`.

**Correction and permanent test.** During drain, observe the bound **original** document group's actual completion and release that observation with the transition. Keep certificate identity and synthetic-finish safeguards. Test both selected and closed variants by awaiting the certificate with a bounded timeout, without another coordinator operation.

### R1-06 · High · A controlled observer can execute through another service instance

**Location.** `Services/Automation/WorkbenchAutomationService.cs:65–77`; `Services/Automation/WorkbenchAutomationRegistry.cs:291–299`. Residual B1-02, reproduced on both binaries.

**Sequence and effect.** From service A's real StatusViewModel.PropertyChanged, call service B's public ExecuteTextAsync with B's own authorized GUI identity, instance, state version and fresh ID. A's observer depth was 1. B checked its own coordinator, accepted the job, and created `foreignR1` in the process-wide interpreter. The execution ID was queryable. A's version remained 0, B's became 1. This is a synchronous in-process authority bypass requiring a second service. The normal application registers one singleton, and no remote caller elevation was demonstrated. Probe `foreign-observer`.

**Correction and permanent test.** Enforce the prohibition at the shared interpreter's fresh-admission boundary, or enforce a single owning service for that state. Preserve per-service caller/replay authorization. Test a second legitimate service from the observer, absence of a job or state effect, followed by ordinary successful submission outside observation.

### R1-07 · Medium · An attach can publish after reentrant service disposal returns

**Location.** `Services/Automation/WorkbenchEditorOperations.cs:26–33`; `Services/Automation/EditorBridge.cs:90–100`. Residual B1-05.

**Sequence and effect.** Start replacing the selected editor. In the outgoing WorkspaceChanged notification, complete DisposeAsync. The outer Attach resumes and publishes the incoming registration. Its Attached flag is true, the coordinator reports Gui, and an independent service cannot bind the editor. F1's stopping gate correctly denies input. The B1 control additionally allowed input on that editor. Disposal is expected to retire local registrations, including one racing on the same dispatcher stack. Probe `attach-dispose`.

**Correction and permanent test.** Check stopping at final publication after reentrant callbacks or serialize shutdown and the entire attach transition. Verify no new binding/listeners, refusal of native input, and successful reassignment to another service without calling Dispose twice.

### R1-08 · Medium · Return can still be invalidated before interpreter reservation

**Location.** `ViewModels/CryptoScriptEditViewModel.cs:237–254`; `Services/Automation/WorkbenchAutomationRegistry.cs:316–328,516–522`. Residual B1-01 with an injected dependency. It also occurs on the B1 control.

**Sequence and effect.** Seed one completed job, then submit a real Return in the edit view. During RunGuiAsync, Prune calls the injected TimeProvider's timestamp accessor. Its callback began a drain and moved the caret after the view's last validation. The recorded caller was Prune and the interpreter queue was still free. A **new** job ID was subsequently published, the source declaration executed, the newline was suppressed and the phase remained Releasing. The line text was the captured original; the defect is starting it after revocation. The default system clock has no application callback. This probe uses the internal injectable clock and is separate from IR-N1-01, where admission has already occurred. Probe `pre-reservation-return`.

**Correction and permanent test.** Carry the original Return validator into fresh admission, across synchronous registry/clock callbacks, to the reservation and publication boundary. Use deterministic invalidation before reservation, compare both job IDs and actual state, and keep a normal exactly-once control.

### R1-09 · Medium · Paste does nothing for a nonempty selection entirely in virtual space

**Location.** `Controls/WorkbenchTextArea.cs:215–243,285–287,303`. **New F1 functional regression.**

**Sequence and effect.** On `abcd`, enable virtual space and make a native SimpleSelection from visual columns 6 to 9 at document column 5. It is nonempty but its document length is zero. Paste `P`. Both the pinned plain TextEditor and B1 workbench produced `abcd  P`. F1 left `abcd`. ClipboardAccess records insertion permission but omits the zero-length deletion segment that this selection implementation uses. This requires no reentrant provider. Probe `virtual-selection-paste`.

**Correction and permanent test.** Preserve the native zero-document-length segment semantics for a nonempty visual selection while retaining the geometry stamp. Compare forward/reverse virtual selections, padding, insertion and one-step undo against the pinned editor.

### R1-10 · Medium · Aborting completion during UpdateStarted destroys redo

**Location.** `Services/InfoCompletionController.cs:191–202`. Residual B1-08 in the new F1 late-rejection path.

**Sequence and effect.** Seed redo by typing and undoing a character after `Info(AES`. Request a completion and either dispose the controller or move the caret in the resulting UpdateStarted. The inner Current check returns false. Text and revision are unchanged, no ticket leaks, but CanRedo changes from true to false. B1 inserted the completion in this sequence; F1 prevents insertion but loses history on rejection. A provider that throws before the group opens preserves redo. Probe `completion-late`. [Native optional caret entry](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs#L356-L359)

**Correction and permanent test.** Ensure an operation still subject to rejection cannot publish an undo effect, while preserving stale-session refusal. Seed redo and assert that redo actually reapplies the original edit after disposal/caret invalidation. Check the reopen policy, not only final text and pending count.

### R1-11 · Medium · An in-place foreign mutation escapes the draft's state-version check

**Location.** `ViewModels/VariableViewModel.cs:62–78`; `Services/Automation/WorkbenchAutomationService.cs:83–94`. Adjacent P5 contract gap, also observed on B1. The same-name **different object** finding itself is fixed.

**Sequence and effect.** A captures a row at value `0x(01)` and edits its draft. B legitimately changes the same live declaration to `0x(02)` through its serialized Mutate path. A's expected version still matches A's counter, and the weak-origin identity still matches. A commits the stale draft, overwriting B's value with `0x(03)`, and reports a fresh snapshot. The editor revision stays 0. This requires two service instances sharing the process-wide interpreter. AP3-01 explicitly calls out independent version domains over one dictionary. Probe `foreign-in-place`.

**Correction and permanent test.** Validate a mutation revision from the authoritative state/declaration under the interpreter reservation, or enforce one version authority for all permitted writers. Test foreign in-place edits as well as replacement objects. After conflict, require a fresh capture before committing.

### R1-12 · Medium · A retired view's refresh command discards its successor's unsaved draft

**Location.** `ViewModels/VariableViewModel.cs:24–28,97–119`; `Views/VariableView.axaml.cs:11–25`. Residual B1-06, also observed on B1.

**Sequence and effect.** Retain the RefreshCommand from view A, detach A, and bind a different VariableView B to the same VM. Set B's current row draft to `0x(02)`. Invoke the old command with no row argument. Its identity is unchanged, and SetupVariables sees an attached VM. It replaces B's row and restores the interpreter's old `0x(01)`. The lost edit was unsaved UI data. This test did **not** demonstrate a subsequent unauthorized interpreter write. Probe `old-view-command`.

**Correction and permanent test.** Bind view-originated refresh commands to their captured view generation, with a fresh command for the successor. Exercise the old command after B has begun editing, verifying the same row, selection and draft value survive; B's own refresh should still work.

### R1-13 · Medium · Mixed Replace All changes native text-anchor behavior

**Location.** `Controls/WorkbenchTextEditor.cs:99–100,113–118`. **New F1 semantic regression.**

**Sequence and effect.** Place a normal anchor and a SurviveDeletion anchor inside the first word of `cat CAT` at offset 1. Replace All case-insensitively with `cat`. The text becomes `cat cat` everywhere. The pinned editor and B1 delete the normal anchor and move the surviving one to offset 3. F1 retains both at offset 1 because it skips the identical match individually. An entirely no-op command can preserve redo; this example is an effective batch with an authorized native replacement. Probe `mixed-anchor`. [Pinned Replace All loop](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Search/SearchPanel.cs#L379-L392)

**Correction and permanent test.** Preserve native anchor semantics for all authorized matches in an effective batch, while preserving wholly no-op history behavior. Compare anchor deletion, movement and grouped Undo/Redo on the native and workbench controls.

### R1-14 · Low · A collected declaration leaves a retired draft visibly fresh

**Location.** `ViewModels/VariableViewModel.cs:62–64`; `Views/VariableView.axaml:23`. New F1 weak-origin rejection behavior.

**Sequence and effect.** Capture a row, remove its declaration through the same service, release strong references and force collection. Modify on the displayed row safely does nothing, but the weak-origin early return leaves IsSnapshotStale false. The row stays displayed and the stale-refresh button remains hidden. An explicit refresh removed the row. The version remained 2 and no declaration was recreated. Probe `weak-expired`.

**Correction and permanent test.** Mark the published draft stale when its original declaration is gone. Exercise actual weak-reference expiration, then verify unchanged interpreter state, visible refresh and successful recovery.

## R1–R7 assessment

| Area | Independent assessment |
|---|---|
| **R1 · Registration, selection and lifecycle** | Per-registration identity, original document/revision and input generation are still the right units for multiple tabs under one workspace writer. A normal A/B switch preserves history isolation, and F1 retires a drained association so the old control can be reassigned. The checks for attach, document replacement, reentrant retirement and final publication do not yet compose (§01, §04, §05, §07). More coordination objects per tab would leave those defects in place. |
| **R2 · Execution** | The native continuation is consumed before input subscribers. The view captures the source, caret, selection and lifecycle before popup closure. Same-service observation is checked after authorized replay lookup and before fresh reservation. The synchronous service boundary still has the two conditional gaps in §06 and §08. Scope is explicit: one uses a second authorized service and the other uses the internal injected clock. Neither shows unauthorized remote MCP execution. |
| **R3 · Search** | Ordinary native/F1 comparisons agreed on text, undo and redo for regex literal replacement, a lookahead with zero-length hits, multiline zero-length anchors, overlapping candidate strings, case folding and deletion. For example, pattern `ab(\d+)` and replacement `[$1]` yielded literal `[$1] [$1]` on both engines. The pinned algorithm has no capture substitution here. Offset order and single undo held on these cases. Preflight pattern replacement and provider-instance changes were rejected. A mutable policy can still revoke an earlier range before acceptance (§02); identical matches in an effective batch have different anchor effects (§13). |
| **R4 · Clipboard** | The strict intent rejects the original delayed offset, virtual-column, selection and registration changes. The original replay showed no update or interpreter reservation over the delayed export; successful export followed by stale Cut left the clipboard changed and document intact. Partial selection with a stable provider and ordinary rectangular paste work. The temporary provider is restored only if still installed, including on exception. The native consumer can nevertheless discard the scoped answers (§03); a valid virtual-only selection fails even with no callback (§09). Coordinated Cut/Paste retain the approved event omission; native Copy remains distinct. |
| **R5 · Completion and focus** | I delivered headless MouseMove/MouseDown/MouseUp to the popup row's translated coordinates. Its list received one routed press and issued one insertion request. F1 inserted the selected second row exactly once (`Info(AES-CBC`), kept editor focus and generation 2. On B1 the same routed request closed the list without inserting and advanced the generation. Genuine focus transfer to an external button advanced F1's generation and a retained request did not insert. Escape dismissed without inserting. The standalone window hit-test did not consistently identify the overlay row; the list's actual routed-event counter and inserted item establish delivery. Dispose during a provider callback also left text unchanged. The rejected-group redo loss remains (§10). None of these headless observations prove native Win32 popup behavior. |
| **R6 · State and drafts** | Comparing object identity under reservation prevents a deleted/recreated declaration from receiving the edit. Key identity protects IV from a renamed PAD draft and permits a valid fresh edit. Publication stamps prevent an interrupted Clear/Add/selection publication from making an old row authoritative. The remaining defects concern an in-place write in another version domain, a command whose lifetime spans two views, and the freshness indicator after collection (§11, §12, §14). |
| **R7 · Baseline and scope** | The accepted original-owner history capability, native single-use Return continuation, IME disable, drag disable and paired rejected-update guards are still present. No public transport/lease or permission expansion was found in the diff. The service editor writer retains caller/identity/revision checks and the additional GUI collision check. The synchronous save and save-commit paths retain observer rejection and the existing reservation/lease discipline. These checks do not neutralize the newly demonstrated transition failures. |

### AP3-02 and crosswalk impact

The 122-case History/Enter filter passed. In the independent paired-finish replay, history, undo and redo moved the text `a → b → a → b` with three legitimate starts, three legitimate finishes and three rejected recursive edits. It ended outside an update, with zero pending tickets and fresh admission available. §13 concerns **search** replacement and does not establish a regression in the accepted two-step history capability. The effective native drag/IME settings remain early host controls; native final-binary input was not exercised here. [Accepted AP3-02 contracts](MCP-AP3-02-final-acceptance.md)

I reconciled the existing 51 mutation paths and 31 observer groups against the F1 deltas. The following are the changes or qualifications to the earlier coverage assessment; all other items retain the earlier verified, previously accepted, deferred or insufficient-evidence classification. The original full inventory remains the item-by-item index. [B1 crosswalk](MCP-B1-independent-review.md) · [F1 crosswalk claims](MCP-B1-F1-corrections.md) · [AP3-01 inventory and package boundaries](MCP-AP3-01-integration-assessment.md)

| Items | Rereview disposition |
|---|---|
| M04–M05, M32; OBA03, OBA17–OBA18 | Ordinary Return and same-service observer work are verified. Fresh synchronous admission remains incomplete for §06 and §08. |
| M10–M13, M31; OBA31 | Prior virtual-caret relocation now rejects; clipboard protection and positive virtual-selection behavior remain incomplete (§03, §09). |
| M14–M16 | Both Replace Next routes now progress without a no-op history entry, and the old template checks remain. Replace All requires the corrections in §02 and §13. |
| M17–M18; OBA06–OBA11 | The original stale request and handler ordering are improved. Actual pointer routing is corroborated headlessly. Rejection with a seeded redo entry and final Windows acceptance remain open (§10). |
| M33–M34, M43–M44; OBA02, OBA05, OBA12, OBA22, OBA30 | Normal close, retirement, duplicate rejection and empty-certificate resume are verified. Atomic replacement, original-group accounting, release notification and disposal during attach remain incomplete (§01, §04, §05, §07). The previous synthetic-finish result stands. |
| M45–M46, M50–M51; OBA20–OBA21 | Declaration replacement, parameter key substitution and the detached Markdown token are corrected. Full fresh-state and view-lifetime claims are qualified by §11, §12 and §14. |
| M19, M37, M41–M42; OBA13, OBA29 | No additional failing original history capability, v1 MCP editor authorization or synchronous observer/save test was found. Their later-package responsibilities remain as assigned. |

## Verification and test quality

These are fresh executions of the existing compiled binaries. The original F1 **build logs** independently contain “Build succeeded”, eight warnings and zero errors in both configurations. I did not rebuild the product or replace prior build outputs. Both supplied Release hashes matched. The original probe's source/PDB check again matched 42 available product and 36 GUI-test source documents in each configuration, with zero reported differences.

| Execution | Result | Scope |
|---|---:|---|
| Full Debug GUI | **662/662 passed** | Zero failures or skips |
| Full Release GUI | **662/662 passed** | Zero failures or skips, same case-name multiset as Debug |
| Full Debug MCP | **89/89 executed passed** | TRX has 90 total, including one explicit NotExecuted compatibility probe |
| Full Release MCP | **89/89 executed passed** | Same individual NotExecuted result, `InstalledCodexDiscoversAndCallsStatus` |
| F1 fixture, Debug | **38/38 passed** | Subset of the full GUI suite |
| History/Enter filter, Debug | **122/122 passed** | Subset of the full GUI suite |

The test commands used `dotnet test <project>/bin/<configuration>/net10.0/<project>.dll`, a TRX logger and the rereview results directory. The filters were `FullyQualifiedName~B1F1CorrectionTests` and `FullyQualifiedName~History|FullyQualifiedName~NativeEnterContinuationTests`. Every filtered case name appeared in the full execution. [Debug GUI](../../artifacts/b1-f1-r1/gui-debug.trx) · [Release GUI](../../artifacts/b1-f1-r1/gui-release.trx) · [Debug MCP](../../artifacts/b1-f1-r1/mcp-debug.trx) · [Release MCP](../../artifacts/b1-f1-r1/mcp-release.trx) · [F1 fixture](../../artifacts/b1-f1-r1/F1.trx) · [History/Enter](../../artifacts/b1-f1-r1/history-enter.trx)

**The original probe's three Busy results were checked for effects.** In `validated-busy`, duplicate attach, attach during the original native group and activation after completed disposal each threw the expected exception. A remained selected after the first refusals. It accepted `1`, B subsequently accepted `2` on its separate document, and post-disposal input left A at `A1`. A new service then rebound it and produced `A13`. The old registration was detached, the old ticket count was zero and the new service remained usable. These observations validate those three original sequences. They do not cover the different callbacks in §01 or §07. [Original probe rerun](../../artifacts/b1-f1-r1/original-replay.log) · [Independent controls and cases](../../artifacts/b1-f1-r1/counterchecks.log)

**The permanent tests contain real positive controls.** They inspect job IDs, variable contents, revisions, actual document text, seeded redo and later successful writes. The Return tests exercise a real view and the search tests exercise both routed targets. Some assertions, however, validate only the immediate result. The single-match protection test cannot invalidate a previously checked sibling. The disposal test calls StopAccepting before disposing, rather than disposing during replacement attach. The group test keeps the outgoing document unchanged. The completion tests cancel before RunUpdate and do not seed redo there. Rectangle and empty virtual-caret tests miss the nonempty virtual SimpleSelection. The popup test explicitly calls row.Focus and then RequestInsertion, so it cannot prove an actual pointer issued the request. The new routed probe closes that last gap only on Headless. The key/identity tests cover a distinct declaration, whereas §11 keeps the same object. [F1 permanent fixture](../../CryptoWorkBenchGuiTest/B1F1CorrectionTests.cs)

## Native acceptance, preservation and recommendation

**Final native Windows acceptance remains open.** No Windows desktop smoke was performed in this rereview. The preceding F1 Release build failed mouse completion; physical Escape stopped the proposed test on the rebuilt candidate. Repeat keyboard and mouse completion on the identified final or corrected Release binary, followed by Return/KP_Enter with overload navigation, search buttons, history and undo/redo, external-application clipboard, variable/parameter commits, rectangle and drag gestures, and physical Swiss AltGr/dead-key entry. AP3-02 owner acceptance and the intermediate F1 smoke retain their historical scope.

The before/after SHA-256 check covered the candidate files and the original independent/F1 evidence trees, including their existing logs and reports. New artifacts are isolated under `artifacts/b1-f1-r1/`. The only new nonignored file from this review is this report. Branch and HEAD were retained, the index was empty, and no product, test, prior report or prior evidence file was modified. [Entry manifest](../../artifacts/b1-f1-r1/before.csv) · [Preservation result](../../artifacts/b1-f1-r1/preservation.txt)

**Recommendation.** Correct the High registration, protection, drain and interpreter-boundary findings before a native acceptance decision. Add the callback orderings and positive controls above to permanent regression coverage, then rerun both complete suites and the binary-identified Windows checks. Keep the accepted clipboard event tradeoff, one workspace coordinator and the owner-bound AP3-02 history capability. IR-N1-01 remains P8/AP5, N1-F02 remains open, and the accepted multi-stage CJK IME limitation remains. Public document addressing belongs to B3. Full provider/shutdown drain, asynchronous execution/output finalization, and public lease/epoch/transport remain with AP4–AP6. None of the High reproductions needs a tab UI or a new public MCP API.
