# MCP-B2-N1 final native Windows acceptance

**NATIVE ACCEPTANCE PASS** · 3 November 2026

The corrected Release populated the startup KeyMap on three fresh launches. Native Return, keypad Enter, Script Book save/open/execution, password cancellation, timeout recovery and ordinary shutdown passed. The desktop exposes no whole-editor execution command or tab-switching interface. Their evidence is identified separately below. No blocking regression was observed.

## Candidate and environment

Windows build 26200, interactive session 8. The branch was `feature/mcp-ap3` at `d6e537935add54988bfaa67aa81edda8bf1e000b`, with an empty index. Before testing, Git listed 19 modified B2 production/test files and the untracked B2 implementation report.

All three launches used `CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.exe` in the specified worktree. The first running process was PID 51516, started at 10:26:11 local time, with native window 199034. The second was PID 44372, started at 10:28:22, with window 591180. Windows reported the expected executable path for both. The third fresh shell had window 458912. The checks used the live Windows desktop, window captures, accessibility status and injected native input. No physical-keyboard claim is made.

| Release file | SHA-256 before launch |
| --- | --- |
| CryptoWorkBenchAvalonia.Desktop.exe | `415C9B7889900C2C1F2BF9184C06BD62D7644F4A5F36C27CDE17FFFFA84D677A` |
| CryptoWorkBenchAvalonia.Desktop.dll | `F1C3DBC4693D41E39933D15AB1BDED08F9F57FE34B52383B4AB7FF1F1D6E74B4` |
| CryptoWorkBenchAvalonia.dll | `1026B35DF4ABC135ADBCD780134F40845CE36E0FC630C7BD74ABA853F2C74B33` |

The managed product hash matched the required hash, the copy in `CryptoWorkBenchGuiTest/bin/Release/net10.0`, and the post-test hash. N1 used the existing binaries without a rebuild. The earlier intermediate binary's empty KeyMap and its Return/close observations are historical. Every native observation below came from the corrected candidate.

## Acceptance matrix

| Case | Result | Observations |
| --- | --- | --- |
| N01 · Startup KeyMap | PASS | The footer visibly rendered the Info-history and editor-line-history entries after initialization on all three launches. Both remained visible after execution, picker use and navigation. On the first instance the actual Alt+PageUp and Alt+PageDown bindings recalled the two distinct Print statements onto the last line. The previously documented Ctrl/Alt help-text discrepancy (N1-F02) remains. |
| N02 · Return and KP_Enter | PASS | Return on `Print(0x(2A))` produced one `out: 0x(2A)` and moved to line 3, column 1. On that line, keypad Enter on `Print(0x(17))` produced one `out: 0x(17)` and moved to line 5, column 1. The editor and saved file showed both pairs in order. The single-job count is corroborated by the permanent native-route tests. |
| N03 · Original intent | PASS for interactive output. Whole-editor command NOT TESTED natively. | The earlier result stayed under its statement as subsequent input executed. A later interactive line printed two imported variables in reverse order and yielded exactly those two outputs in that order. The desktop offers Script Book import and interactive Return. Whole-editor execution has separate automated evidence in `WholeEditorPrintIsReturnedWithoutChangingSource` and `ExecuteEditorUsesImmutableBookAndRegisterWithNoSourceReinsertion`, both passed. |
| N04 · Save/Open lifecycle | PASS | Escape dismissed the Save picker and the GUI reported “Script book save cancelled.” Escape also dismissed Open and retained the editor contents. Save then created the previously absent `artifacts/b2/B2-N1-6fef058c.sbk` and displayed its path. After relaunch, Open imported its exact four nonblank lines and reported success. Subsequent editing and execution worked. The saved file hash remained `3134FEEC7751E36FF0CD7C8F0360BA370D718F8D24CBEAADB2681252352E2B83` through closure. |
| N05 · Password, cancellation and timeout | PASS | A synthetic SecureSave job displayed the password/confirmation dialog. Cancel closed it, the shell reported “Script execution cancelled by user.” and a subsequent Print returned the synthetic value. A second SecureSave prompt was still open 29.982 s after Return and absent at 30.511 s without input. The shell reported “Script execution cancelled.” A new Print succeeded with `out: 0x(3C)`. The GUI exposes cancellation wording for the deadline. The registry's TimedOut classification is covered separately by automation. No password was entered and no encrypted target file was created. |
| N06 · Script Book execution | PASS | Reopening the saved book reached “Script book loaded and all lines successfull parsed” without duplicating either stored output line. In a fresh session a second disposable book declared `nOne=0x(31)` and `nTwo=0x(42)`, then printed each. Import showed exactly the four source statements, no extra inline output, and the same terminal success status. Interactive `Print(nTwo) Print(nOne)` then returned `0x(42)` followed by `0x(31)`, demonstrating both declarations took effect. |
| N07 · Shutdown and relaunch | PASS for the supported close paths | Ordinary Alt+F4 closed the first shell and its process exited. During another SecureSave prompt, clicking the owner's close control left both the modal and owner present. Closing the modal normally reported user cancellation. Closing the owner then exited PID 44372. Relaunch showed an empty editor, Ready, a populated KeyMap and no returning dialog. The final test process also exited normally. Direct shutdown during a live reservation is covered by automated drain tests. |
| N08 · Multiple documents | PASS · automated integration only | `ActiveViewDoesNotOwnAnotherDocumentsIdentityOrHistory` exercised two registrations, rejected wrong-target and stale tickets, independent Undo/Redo and one global drain. `DelayedReturnOutputBelongsToOriginalJobAndAnchor` passed for typing, switch, close and drain. The tests check original job/result identity, the other document and its history, ticket cleanup and a subsequent valid job. Native tab switching is outside the available UI. |

The two disposable books are in the ignored `artifacts/b2` test directory. The pre-existing user Script Books were neither selected for writing nor overwritten.

## Focused closure verification

The governing contracts are AP0 D05/EC21 and the AP4/AP5 ownership and drain assignments, together with the AP3 P7/P8 handoff and dispatcher inventory. The B2 report maps these to the final implementation. [AP0 contract](MCP-00-AP0-contract-finalization.md) · [AP3 integration inventory](MCP-AP3-01-integration-assessment.md) · [B2 implementation and review](MCP-B2-execution-lifecycle-integration.md)

| Correction | Final implementation and passing Release regression evidence |
| --- | --- |
| Rejected Return | Input failure sets Handled. `RegistrationExceptionRejectsNewlineAndAllowsNextReturn` checks unchanged source and history, absence of a job ID, cleanup and the next successful Return. |
| Book observer failure | Diagnostic reset runs inside the admitted job and the Book flag unwinds in finally. `FileBookClearsAndExecutesAsOneVersionAndCanRecoverFromObserverFailure` exercises an exception and a successful subsequent import. |
| Faulted coordinator on exit | The shell awaits shutdown quiescence and service drain. The four `NativeCallbackExceptionsInvalidateDelegationAndFaultConservatively` cases distinguish quiescence from permission to resume editing. |
| Inactive view | The VM checks its original registration before file or execution admission. `InactiveViewCannotOpenSaveOrExecuteTheSelectedDocument` checks both documents, picker count, file effects and job identity. |
| Cleanup exceptions | Registry cleanup records failures while continuing release. The file-dialog completion signal is released in finally even if ticket disposal fails. `DeadlineDuringResourceCleanupIsObservedAndCleanupFailureTerminates`, `MultipleCleanupFailuresAreReportedAndBindingReleased` and the picker-drain failure case passed. This is source and integration evidence for cleanup, without a native injected dispatcher failure. |
| Reentrant detach | Publication rechecks the recipient across notifications. `ReentrantDetachStopsRemainingPublicationAfterCommittedUnit` preserves the completed job and its captured variable while suppressing retired-view variable publication. |
| First KeyMap recipient | The coordinator distinguishes first attachment from a successor. Both `ShellKeyMapMayReachFirstEditorButCannotFollowASuccessor` cases passed. N01 independently verified the visible result. |

Existing final TRX records in `artifacts/b2` were checked by suite, case identity and outcome. The Debug and Release GUI suites each passed 728 of 728. The Debug and Release MCP suites each passed 89 of 90, with zero failures and the installed-Codex discovery/status probe unexecuted. The 178 passing focused cases overlap the GUI suite. N1 added no runs to these totals.

## Dispositions and repository state

**F2R-10 — KNOWN LOW / DEFERRED.** The production search for `EditorHistoryEffect.Applied` found its assignment in EditorCoordinator. Return checks the original native text effect, error and destination. Registry finalization uses interpreter effects. Neither consumes Applied as authoritative Undo/Redo evidence. Correct or explicitly reassess the empty-history receipt before enabling a downstream dependency.

The single editor owner and global coordinator, document-bound authority, process-wide interpreter reservation and shared revision contracts remain in force. The accepted whole-line Cut Undo, native clipboard event limitation, foreign raw AvaloniaEdit boundary and unsupported multistage CJK IME remain as recorded in B1. The overload and SearchPanel counters remain GUI redesign follow-ups. Public document addressing, visible tabs and AP6 transport/credentials remain later work. [B1 owner decisions](MCP-B1-F3-owner-decisions.md) · [B1 native acceptance addendum](MCP-B1-N1-R-final-acceptance-addendum.md)

The final branch and HEAD match the starting baseline. The index is empty. The only new Git-visible N1 file is this report, alongside the preserved unstaged B2 changes. No production or permanent test change, staging, commit, push, merge or master update occurred.

**MCP-B2 READY FOR OWNER ACCEPTANCE**
