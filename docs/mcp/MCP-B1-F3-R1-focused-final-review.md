# MCP-B1-F3-R1 focused final independent review

**CHANGES REQUIRED** · 2 November 2026. All six mandatory corrections passed. The remaining blocker is a reproducible loss of an unrelated, unsaved edit in the supported Variable view after Boolean validation. A second, nonblocking issue affects an internal history receipt. This review found **one Medium and one Low finding**. The five owner decisions stand.

## Findings

**F3R-01 · Medium · Boolean rejection discards another row's unsaved edit.** In one mounted Variable view, edit the string textbox from `0x(01)` to `0x(02)` without submitting, select the Boolean, enter an invalid value and execute the enabled row button's bound Modify command. The Boolean returned to `true` and both interpreter versions stayed unchanged. Selecting the string again showed `0x(01)`. Its row object had been replaced, and Modify committed the old value. In the otherwise identical navigation-only control, the textbox retained `0x(02)` and Modify committed it. This loses user input before it reaches the interpreter. It violates the F3 requirement that an unrelated valid draft remain committable. The historical F2R-09 version-advance finding remains Low and that specific effect is fixed. [Independent headless source](../../artifacts/b1-f3-r1/focused/Review.cs) · [Observed control and failure](../../artifacts/b1-f3-r1/focused.log)

The invalid path at `VariableViewModel.cs:48,90` passes a null mutation **and** the ordinary full-snapshot presenter. The service recaptures at `WorkbenchAutomationService.cs:95`. The presenter clears selection, removes the published rows and adds fresh rows at `VariableViewModel.cs:161–172`. The permanent test at `B1F3CorrectionTests.cs:329–343` uses two view models, which explains why its unrelated row survives. **Minimum correction:** keep the reservation, identity and revision checks, then restore only the rejected Boolean row's canonical value. Add a single-view regression that commits the other pending value without re-entry. [Variable VM](../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs) · [Service](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs) · [Fixture](../../CryptoWorkBenchGuiTest/B1F3CorrectionTests.cs)

**F3R-02 · Low · A rejected first completion can report applied history.** Open completion on `Info(AES` with an initially empty native history and dispose the controller during UpdateStarted. The original snapshot and limit survived, Undo and Redo were unavailable before and after, and executing both left the text unchanged. The group closed, pending tickets returned to zero, and subsequent typing and Undo worked. The receipt nevertheless reported `Applied`. The sole observed stack notification was `CanUndo`. [Countercheck output](../../artifacts/b1-f3-r1/focused.log)

The no-redo branch at `InfoCompletionController.cs:247–255` opens a native group before its final check. At `EditorCoordinator.cs:265–269,282`, a transient property notification is sufficient to report an applied effect. F3 correctly excludes its own temporary SizeLimit notifications on the seeded-redo path. The empty-stack path has a different notification. Record a no-effect receipt for a finally rejected completion when native history ends unchanged, while preserving genuine history effects. Add an initially empty stack beside the existing seeded-redo and accepted controls. This is an accounting issue with no observed text or executable-history loss. [Completion](../../CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs) · [Receipts](../../CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs)

## Verification of the eight corrections

I traced the production paths and inspected the assertions in the permanent fixture. The fresh replay executed all 15 selected independent predicates against the identified F3 product. [F3 tests](../../CryptoWorkBenchGuiTest/B1F3CorrectionTests.cs) · [Replay output](../../artifacts/b1-f3-r1/replay.log) · [Verified predicates and test retention](../../artifacts/b1-f3-r1/verification.txt)

| Historical finding | Disposition | Independent evidence |
|---|---|---|
| F2R-03 · High | Verified | Clipboard planning reads the witness before grants and after enumeration, rejects incoherent or unversioned multi-range plans, and freezes accepted edits. Revoking offset 5 while checking offset 1 left both lines and the revision unchanged. Seeded redo executed and later rectangular paste worked. The single contiguous partial paste produced `LOCKP` without querying the successor provider. |
| F2R-04 · High | Verified | Native CanExecute runs inside synchronous observation with exception propagation and finally cleanup. A disabled Paste query saw depth 1. The foreign fresh job was Busy, its ID was absent, state and versions stayed unchanged, and later submission succeeded. The pre-admission native availability exception and recovery test also passed. |
| F2R-05 · High | Verified | Whole-line Cut bypasses rectangle reconstruction. Both caret directions copied and deleted the intended line, remained in Gui, undid and redid in one step, and permitted subsequent typing. |
| F2R-06 · High | Verified | After UpdateStarted and any limit restoration, the controller reads the policy witness under observation, checks the original session, range, geometry and provider, then checks the active original ticket and stopping state before pushing accepted history or writing. Revocation, stop and disposal preserved text, revision, limit and executable seeded redo, with no open group or ticket. Valid completion remained undoable and redoable. |
| F2R-07 · Medium | Verified | Known-registration attach rechecks ownership after activation notifies. Reentrant disposal produced Busy and returned no registration. Old input was rejected and a fresh binding could type. Ordinary retained activation succeeded. |
| F2R-08 · Medium | Verified | The zero-due timeout canceled the private job before publication. Lookup of its actual ID returned NotFound. No newline, declaration or version change occurred. The timer was disposed, the reservation released, and retry executed once. A separate controlled deadline fired **after** admission on the validator-bearing GUI registry route: the original ID remained queryable as TimedOut and the queue was released. |
| F2R-09 · Low | Partially verified | Canonical Boolean restoration, unchanged versions, a separate view model's draft and a later valid Boolean edit all passed. The single-view unrelated draft failed as described in F3R-01. |
| F2R-10 · Low | Partially verified | The temporary-limit rejection correctly reports None and retains executable redo. A successful completion reports Applied with working Undo/Redo. F3R-02 remains for empty initial history. |

## Candidate and automated results

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, branch `feature/mcp-ap3`, HEAD `dfea2055992254462b937477a8697754fe39a4d6`. The incoming index was empty, with 34 modified tracked paths and 12 untracked paths. I used the existing binaries. Portable PDB checks matched all 42 product and 38 GUI-test worktree source files examined in each configuration. [Incoming inventory](../../artifacts/b1-f3-r1/incoming-status.txt) · [Source checks](../../artifacts/b1-f3-r1/focused.log)

| Release file | Independently measured SHA-256 |
|---|---|
| Desktop DLL | `B3328F669ECFF85B88B6609011A5622EE56F8D279BE320807F8466979F5A8A41` |
| Product DLL | `1C7A0D4C1A1C05F0D7FE9E80203C9943A2C8CE42E2FED549D9392CF2D232551C` |

Both hashes match the requested candidate. The F3 Debug and Release build logs each report seven warnings and zero errors. The reviewer built only the isolated countercheck. [Measured identities, including executable](../../artifacts/b1-f3-r1/identity.csv) · [Debug build log](../../artifacts/b1-f3/final-build-Debug.log) · [Release build log](../../artifacts/b1-f3/final-build-Release.log)

| Fresh run | Debug | Release |
|---|---:|---:|
| Full GUI | [711 passed](../../artifacts/b1-f3-r1/Debug-GUI.trx) | [711 passed](../../artifacts/b1-f3-r1/Release-GUI.trx) |
| Full MCP | [89 passed, 1 unexecuted](../../artifacts/b1-f3-r1/Debug-MCP.trx) | [89 passed, 1 unexecuted](../../artifacts/b1-f3-r1/Release-MCP.trx) |

Both GUI runs retained all 694 incoming case names. The Debug subsets passed **17 F3**, **105 prior B1/F1/F2**, and **130 History/Enter** cases. These overlap the full GUI totals. The unexecuted MCP case was `InstalledCodexDiscoversAndCallsStatus`. [F3](../../artifacts/b1-f3-r1/Debug-F3.trx) · [Prior fixtures](../../artifacts/b1-f3-r1/Debug-Prior-B1-F1-F2.trx) · [History/Enter](../../artifacts/b1-f3-r1/Debug-History-Enter.trx)

## Owner decisions, AP3-02 and next gate

| Owner decision | Review result |
|---|---|
| OD-F3-01 · F2R-01, historical High | Accepted boundary confirmed. Desktop DI binds the same Shared instance for the concrete service and interface. The view attaches through its VM and the MCP transport receives the interface. The documented dual-owner/shared-buffer reproduction remains technically possible outside that composition. |
| OD-F3-02 · F2R-02, historical High | Accepted boundary confirmed. The early guard interrupts synthetic rejected updates only. Workbench completion and caret finish callbacks contain observer failures. The supported failure test induced a real list-box notification exception, awaited the original drain, resumed and typed. A foreign raw-event subscriber can still interrupt multicast dispatch. |
| OD-F3-03 · F2R-11, historical Low | Accepted. A disjoint authorized whole-line Cut retains protected text, exports the whole line, and gives one logical Undo/Redo group. |
| OD-F3-04 · F2R-12, historical Low | Deferred feedback remains documented. Ordinary and stable versioned Replace All work; unsupported unversioned policies fail closed. |
| OD-F3-05 | Applied. The additional checks were confined to Boolean validation in one view, completion receipt accounting, and the pre-/post-admission timeout distinction. |

AP3-02 still binds History to the original owner, ticket and document and consumes the capability once before native Remove/Insert. Its anchor, grouped Undo/Redo, Return/KP_Enter and cleanup regressions passed. Early IME/drag restrictions, the accepted multi-stage CJK risk and the coordinated clipboard-event waiver stand. P7/P8 handoff including IR-N1-01 in P8/AP5, AP4 shutdown/save, AP5 output finalization, AP6 transport/credentials, B3 addressing, full tabs/docking and Script Book persistence retain their assignments. N1-F02 remains open. [AP3-02 acceptance](MCP-AP3-02-final-acceptance.md) · [Owner decisions](MCP-B1-F3-owner-decisions.md)

**Next gate:** correct F3R-01 and repeat the single-view control and rejection test. F3R-02 is nonblocking and may be explicitly deferred. No general audit is needed. Native Windows acceptance remains open on the eventual candidate. Headless Tab/Enter and pointer completion, including one routed press and one selected insertion, do not establish native mouse, keyboard or cross-application clipboard behavior. [Headless pointer evidence](../../artifacts/b1-f3-r1/pointer.log) · [Final Git status](../../artifacts/b1-f3-r1/final-status.txt) · [Preservation check](../../artifacts/b1-f3-r1/preservation.txt)
