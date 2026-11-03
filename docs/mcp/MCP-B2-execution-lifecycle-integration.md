# MCP-B2 execution and lifecycle integration

## Baseline and boundaries

The worktree was clean on `feature/mcp-ap3` at `d6e537935add54988bfaa67aa81edda8bf1e000b`, the owner's accepted B1 commit. The index was empty. Incoming verification was 712 GUI passes and 89 MCP passes plus one unexecuted installed-Codex probe in each configuration. B1's native verdict applies to the B1 binary.

The [architecture](MCP-00-architecture.md), [implementation plan](MCP-00-implementation-plan.md), [AP0 contract](MCP-00-AP0-contract-finalization.md) §§8–12, [AP3 inventory](MCP-AP3-01-integration-assessment.md) P7/P8, [AP3-02 acceptance](MCP-AP3-02-final-acceptance.md) and [B1 integration](MCP-B1-editor-integration.md) distinguish editor admission (C), the process-wide interpreter reservation (Q), and registry ownership. B2 integrates these existing boundaries. AP0 D05 assigns removal of whole-editor inline output (EC21) to AP5. B2 includes this correction under its execution/output workstream. The interactive Return output contract remains separate.

## Coverage and ownership

| Path / requirement | Incoming behavior or gap | B2 behavior | Permanent evidence and boundary |
| --- | --- | --- | --- |
| P7 Print, Info, KeyMap and diagnostics | Editor and KeyMap VMs subscribed to process-wide events. UI work could outlive its producer. | The admitted pipeline collects one parse unit's output under Q. The owning job awaits dispatch to the originating recipient. KeyMap filters its own gesture records. Status, diagnostics and variable publication recheck lifecycle across notifications. Navigation uses controlled observation. | Cross-VM Print/Info isolation, KeyMap diagnostics, reentrant detach and stale final-unit cases. Public execution output remains bounded and redacted. |
| P7 posted work and observers | B1 already suppressed ambient authority at dispatcher enqueue and protected notification delivery. Suppression alone did not identify a job. | A registered GUI job carries a pending C intent until its pipeline, password and UI callbacks finish. No native update group spans an await. Presentation exceptions reach registry finalization. Local Info history and native completion retain B1's recipient/session checks. | EditorCoordinator callback accounting, B1 observer and completion tests, delayed Return, observer failure during Book, password and registry tests. |
| P8 Return and KP_Enter | Native subscribers could discard input after a job had been requested. Late inline output acquired an unrelated edit opportunity. | The editor checks the original event, caret, selection and native ticket after subscribers. The Return job borrows that operation via its registered callback. Its first unit requires successful native completion. Inline output requires the same workspace, registration, document, view lifetime and surviving original anchor. Subsequent typing is compatible. | NativeEnterContinuationTests, including discarded input, registration failure and supported key/command routes. Delayed typing, switch, close and release tests inspect job state, output, histories and cleanup. |
| P8 Script Book | Source, picker, clear, individual lines and results crossed different lifetime checks. | File reads and parse units stay within one job. Clear and append require the originating destination. Each authorized append advances the intent's expected document version. A stale destination stops the next parse unit. A completed final unit keeps its interpreter outcome. | FileBook clear/parse/version test, failing observer recovery, existing parse, syntax, cancellation and password cases. File format and persistence remain unchanged. |
| AP4 file providers | Final Save had snapshot, confirmation, deletion and write guards. Earlier modal work lacked a drain obligation and workspace generation. | An originating VM registration, workspace generation and snapshot bind the picker. Pending picker/confirmation work holds no Q reservation. The accepted save holds its own Q reservation and document lease through delete/write/unlock. The service and C count their respective obligations and storage items are disposed. | Picker cancellation/failure during drain, inactive-view Save, revision conflicts, delayed delete, dirty state and provider exception suites. ASCII/LF/append and possible partial file effects after delete retain their established semantics. |
| AP4 shutdown and editor edits | The shell disposed VMs during close. Confirmation was outside service drain. | Shell closing stops admission, requests cancellation and waits asynchronously for editor quiescence plus service drain before closing. A faulted editor can quiesce for exit, while its GUI-resume certificate remains invalid. Legacy service edits bind confirmation to generation and snapshot, commit through C under the existing identity lease, and count toward service drain. | Faulted-native shutdown quiescence, save/close, edit reentrancy, dialog confirmation and generation tests. The local shutdown signal does not certify credential reuse. |
| AP5 immutable execution and versions | Registry already owned timers, authorization, replay and retained results. Whole-editor Print wrote into source. Admitted Clear advanced the counter independently of the job. | Whole-editor execution parses its admitted snapshot even across typing. Output stays in the result. Registered GUI jobs record source identity/revision. A retired pipeline cannot run in a successor job. Effects, including Book clear, advance the local and shared revisions once at job finalization. | Whole-editor unchanged-source and immutable snapshot/replay tests, stale pipeline/combined Clear test, existing timeout/cancel-before-start/cancel-after-effects/runner-failure suites. |

### Cancellation and multiple documents

C remains the sole writer gate for the registered workspace. Holding a pending intent does not select an editor, keep the application in a native update, or prevent switching between existing registrations. Q remains fail-fast and service-owned until the job unwinds. The dispatcher receives explicit arguments through the existing execution-context-suppressing adapter. Registered UI operations reject a mismatched originating VM. Whole-editor presentation may tolerate later typing because execution owns immutable text. Script Book writes require an exact document version. Interactive Return uses its original anchor and native completion instead.

A rejected native event creates no job. An admitted job can be canceled before its first unit or between units. A unit that already ran can retain effects even if its UI receiver disappears. Replaying the original execution ID returns the retained record under the existing principal, fingerprint and authorization checks. No source, secret, key material or plaintext output is added to the registry as acceptance evidence.

### History receipts and later gates

A repository search for `EditorHistoryEffect.Applied` found the producer in EditorCoordinator and test assertions. The B2 Return continuation uses the original operation's native **text** outcome, error state, generation and anchor. Job finalization uses runner/clear effects and the registry. Neither path consumes the history classification. **F2R-10 remains KNOWN LOW / DEFERRED.** Consumers must not use this receipt as authoritative Undo/Redo evidence until a later gate corrects or explicitly reassesses the empty-history case. AP3-02's owner-bound History capability remains in place.

AP6 credentials, epoch reuse, public v2 receipts, external execution authorization and public document addressing remain later work. The local drain is not an AP6 lease or a formal D02 certificate. Visible tabs, docking, multiple windows and persistence redesign remain outside B2. The two GUI counter issues, whole-line Cut Undo, native clipboard events, foreign raw handlers, the host library and multistage CJK IME retain their [B1 owner dispositions](MCP-B1-F3-owner-decisions.md).

## Independent review

One read-only review examined the working implementation. It identified pre-admission exceptions allowing a native newline, a Script Book busy flag stranded by a throwing observer, a faulted coordinator blocking window close, inactive VM commands targeting the selected document, cleanup ordering and stale publication after reentrant detach. The corrections suppress the rejected newline, move Book diagnostic reset inside the admitted job, split shutdown quiescence from permission to resume, check the caller's registration, isolate cleanup failure and revalidate recipients across notification boundaries. Permanent cases cover these product paths. The review preceded these corrections and the final verification.

## Verification

On 3 November 2026, locked restore with the repository NuGet.Config and .NET SDK 10.0.401 succeeded for Debug and Release. The repository Prism preparation verified commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` and its 1,180 pinned files. Builds used the desktop solution, `--no-restore` and one MSBuild node after a parallel development build exhausted memory. Both final builds succeeded with zero errors and six existing diagnostics each (four CS8618 in MainViewModel, AVA2001 in ViewModelBase and CS8619 in Avalonia12DispatcherTests). No Core source changed.

| Suite | Total cases | Passed | Failed | Unexecuted |
| --- | ---: | ---: | ---: | ---: |
| Debug GUI | 728 | 728 | 0 | 0 |
| Release GUI | 728 | 728 | 0 | 0 |
| Debug MCP | 90 | 89 | 0 | 1 |
| Release MCP | 90 | 89 | 0 | 1 |

The installed-Codex compatibility probe accounts for both unexecuted entries. The final TRX counters give 90 total and 89 executed. The focused Debug run matched the NativeEnterContinuationTests, EditorAutomationTests, CryptoScriptExecutionPresentationTests, WorkbenchRegistryTests and AsyncPasswordGuiTests fixtures. All 178 passed and are already included in the full suite. The net increase over B1 is 16 permanent GUI cases per configuration. EC21 replaces the one historical whole-editor output assertion under AP0 D05. Build logs and TRX files are in the ignored `artifacts/b2` directory.

### Native Windows observations and owner checklist

An intermediate Release binary launched on Windows. Entering a harmless Print expression and pressing Return displayed one matching inline output, a success status and the caret at line 3, column 1. Alt+F4 closed the window. The startup KeyMap panel was empty. The final correction allows shell-owned output from before the first registration to reach that first editor. It rejects a successor registration. Both orderings have permanent headless tests and passed in both configurations.

The owner stopped Computer Use with physical Escape before the corrected Release candidate could be inspected. **Final native acceptance is open.** The observed Return and close results belong to the earlier binary. The final `CryptoWorkBenchAvalonia.dll` in the Release desktop output has SHA-256 `1026B35DF4ABC135ADBCD780134F40845CE36E0FC630C7BD74ABA853F2C74B33`.

1. Launch the Release desktop executable. Confirm the shortcut text appears in the KeyMap panel. Execute harmless Print lines with Return and keypad Enter and check for one result per request.
2. Open and cancel the Save and Open pickers. Save a disposable Script Book, reopen it and check the confirmation, source and completion status.
3. Cancel a disposable password-prompting job, then let a second prompt reach the 30-second job deadline. Verify the dialog closes and a subsequent execution succeeds.
4. Check shutdown and relaunch, including with work pending. The product currently has no visible tab switch or document-close UI for the two-registration scenarios. Those paths were exercised with real document instances in the dispatcher tests.

### Submission state

The implementation and independent review corrections are available for owner verification. Final Git checks found the original branch and HEAD, an empty index and only B2 production, test and report changes. No commit, push or merge occurred. Native sign-off is pending.

## Changed files

- `CryptoWorkBenchAvalonia/App.axaml.cs`
- `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs`
- `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs`
- `CryptoWorkBenchAvalonia/Services/PasswordDialogHost.cs`
- `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/KeyMapViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs`
- `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs`
- `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs`
- `CryptoWorkBenchGuiTest/CryptoScriptExecutionPresentationTests.cs`
- `CryptoWorkBenchGuiTest/EditorAutomationTests.cs`
- `CryptoWorkBenchGuiTest/NativeEnterContinuationTests.cs`
- `CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs`
- `docs/mcp/MCP-B2-execution-lifecycle-integration.md`

