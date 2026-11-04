# MCP-B1-F3-F1 final Boolean draft correction

**MCP-B1-F3-F1 READY FOR NATIVE ACCEPTANCE**

The remaining Medium finding, F3R-01 against F2R-09, is corrected. In the original single-view reproduction, rejecting an invalid Boolean restored its canonical value while the other row kept its pending `0x(02)` edit. The user could then commit that value without re-entering it. [Independent replay](../../artifacts/b1-f3-f1/independent-replay.log)

## Cause and correction

The previous rejection path correctly skipped the interpreter write but called the same full-snapshot presenter as a successful mutation. That presenter cleared selection and replaced every displayed row, discarding unrelated input. The earlier regression used two view models, so refreshing one did not exercise a second draft in the same grid. [Focused independent review](MCP-B1-F3-R1-focused-final-review.md)

The rejection now reads the original declaration's canonical value under the existing interpreter admission and publishes it only to the selected row's `Value` property. The reservation extends through presentation. The read and mutation paths share the service's observer, stopping, recovery and validation checks. At use time the VM checks the original publication, selected row, active editor ticket, shared state revision and declaration identity. Its command lifetime, stale-snapshot and workspace-generation checks still apply. Successful mutations continue to advance state versions and present their new snapshot. [Variable view model](../../CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs) · [Automation service](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs)

## Regression and independent reproduction

The permanent test mounts one actual Variable view, edits the bound text box and executes the grid buttons' bound Modify commands. On the incoming implementation the test failed because rejection cleared the Boolean selection. On the corrected implementation the selected Boolean row retained its identity and displayed `true`. The other row retained its identity and displayed `0x(02)` while its interpreter declaration still held `0x(01)`. Both local and shared versions stayed unchanged. The editor snapshot stayed equal and its seeded redo and undo remained executable. Committing the string then stored `0x(02)` and advanced both versions once. Submitting a valid Boolean edit stored `false` and advanced both once more. [Permanent regression](../../CryptoWorkBenchGuiTest/VariableBooleanDraftTests.cs) · [Before](../../artifacts/b1-f3-f1/boolean-baseline.trx) · [After](../../artifacts/b1-f3-f1/Debug-Boolean.trx)

The independent review's unchanged probe source ran against the corrected Debug product in an isolated directory. Its before/after records show the visible string changing from the lost draft's old value `0x(01)` to the retained draft `0x(02)`, with row retention changing from false to true. The navigation-only control also retained and committed `0x(02)`. The probe's PDB checks matched the examined product and GUI-test sources in both configurations. [Original output](../../artifacts/b1-f3-r1/focused.log) · [Corrected output](../../artifacts/b1-f3-f1/independent-replay.log) · [Checked results](../../artifacts/b1-f3-f1/verification.txt)

## Final verification

The .NET 10 solution completed locked restore and build in Debug and Release with the repository Prism hooks and verified official Prism commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Each final build completed with seven warnings and zero errors. [Debug restore](../../artifacts/b1-f3-f1/restore-Debug.log) · [Release restore](../../artifacts/b1-f3-f1/restore-Release.log) · [Debug build](../../artifacts/b1-f3-f1/final-build-Debug.log) · [Release build](../../artifacts/b1-f3-f1/final-build-Release.log)

| Test group | Passed | Failed | Unexecuted |
|---|---:|---:|---:|
| [Full Debug GUI](../../artifacts/b1-f3-f1/Debug-GUI.trx) | 712 | 0 | 0 |
| [Full Release GUI](../../artifacts/b1-f3-f1/Release-GUI.trx) | 712 | 0 | 0 |
| [Full Debug MCP](../../artifacts/b1-f3-f1/Debug-MCP.trx) | 89 | 0 | 1 |
| [Full Release MCP](../../artifacts/b1-f3-f1/Release-MCP.trx) | 89 | 0 | 1 |
| [Debug targeted Boolean regression](../../artifacts/b1-f3-f1/Debug-Boolean.trx) | 1 | 0 | 0 |
| [Debug existing F3 fixture](../../artifacts/b1-f3-f1/Debug-F3.trx) | 17 | 0 | 0 |
| [Debug variable, parameter, draft and revision filter](../../artifacts/b1-f3-f1/Debug-Variable-Parameter.trx) | 54 | 0 | 0 |
| [Debug AP3-02 History/Enter filter](../../artifacts/b1-f3-f1/Debug-History-Enter.trx) | 130 | 0 | 0 |

Both GUI runs retained all 711 incoming cases by name. Filtered counts overlap the full suites. Each MCP run records `InstalledCodexDiscoversAndCallsStatus` as the explicit unexecuted compatibility probe.

## F2R-10 — KNOWN LOW – DEFERRED

Open completion on `Info(AES` with initially empty Undo and Redo stacks, then dispose the completion controller during native UpdateStarted. The internal receipt can report `HistoryEffect.Applied` after a transient `CanUndo` notification. The reviewed document text and revision remained unchanged, neither Undo nor Redo was executable, the recording limit was restored, the update closed, and no ticket remained pending. Correct or explicitly reassess this reporting limitation **before downstream MCP functionality relies on these receipts as authoritative evidence of a history effect**. AP3-02 History behavior and receipt accounting were outside this change. [Independent replay](../../artifacts/b1-f3-f1/independent-replay.log) · [Owner decisions](MCP-B1-F3-owner-decisions.md)

## Delivery state

The worktree remains on `feature/mcp-ap3` at `dfea2055992254462b937477a8697754fe39a4d6` with an empty index. The incoming inventory had 34 modified tracked paths and 13 untracked paths. This correction edited two already-modified production files and added the test fixture and this report. The final inventory has 34 modified tracked paths and 15 untracked paths, plus Git-ignored verification evidence under `artifacts/b1-f3-f1/`. No commit, push, merge or master update occurred. [Incoming status](../../artifacts/b1-f3-f1/incoming-status.txt) · [Final status](../../artifacts/b1-f3-f1/final-status.txt) · [Preservation check](../../artifacts/b1-f3-f1/preservation.txt)

The corrected Release desktop DLL has SHA-256 `2280DB0D6D3BC495BC1890DF3FA5A9C963137BB78A8484BB3B31B1C8E79C2FB9`. The product DLL has SHA-256 `D36BA0E5EAE8F86030BCDF66FC47EE0B10007ECBCB28E41AD8830462BA5AD577`, matching the Release GUI test copy. Native Windows acceptance remains open on this build. [Release paths and hashes](../../artifacts/b1-f3-f1/binary-hashes.csv)
