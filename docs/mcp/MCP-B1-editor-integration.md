# MCP-B1 GUI and editor integration

**Implementation candidate for independent review, 2 November 2026.** The worktree was clean at entry on `feature/mcp-ap3`, HEAD `dfea2055992254462b937477a8697754fe39a4d6`. A fresh execution of the existing Debug GUI test binary reproduced all 589 AP3-02 results. The worktree is `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`. Product, test and report changes are local and unstaged. The accepted commit remains HEAD.

## Authority and document lifetime

One service owns one bridge and one UI-thread coordinator. The bridge retains open registrations and separately selects the registration presented to existing callers. The single-editor attach operation still replaces its predecessor. Internal callers can retain the previous view when opening another editor and can explicitly activate an existing registration. Each registration pairs a concrete editor with its original document. The existing bridge GUID names this open document lifetime. A view switch preserves the GUID, revision, document, anchors and native undo stack. Closing or replacing the document ends that lifetime. Opening the same contents again creates a fresh GUID. The bridge rejects attaching the same document simultaneously to two editors. Multiple views onto a single shared text buffer remain future work.

Only the selected registered editor can initiate native writes. An active operation, observer, identity lease, mutation lease or releasing workspace prevents a competing switch. Switching between already open views may invalidate a pending clipboard intent. A switch back cannot revive it. Commit validates the original registration, editor, document, revision and generation. Opening another view creates no coordinator or lock. When the selected view closes during an existing drain, only the original completed drain certificate can end that transition. With no selected document, release returns the workspace to Unavailable. A separate explicit activation is required.

The input generation also advances on focus loss. The workspace generation advances on document, registration or ownership transitions. This distinction lets a user click a variable value field without invalidating the draft simply by leaving the text area. State-version validation occurs inside the existing interpreter reservation. A pending input intent holds neither an open document update nor the interpreter reservation across the clipboard wait.

The AP0 host boundary covers the commands, continuations and observers supplied by the application. The pinned editor exposes mutable controls, sealed document/history objects and nonvirtual methods. Arbitrary third-party operations on retained raw library objects remain subject to the accepted AP0 boundary. Future host features must use the same admission paths. [Architecture](MCP-00-architecture.md), [implementation plan](MCP-00-implementation-plan.md), [R2](MCP-00-R2-consolidated-architecture-decision.md), [AP0](MCP-00-AP0-contract-finalization.md), [AP3-01 inventory](MCP-AP3-01-integration-assessment.md), [AP3-02 acceptance](MCP-AP3-02-final-acceptance.md).

## Implementation

| Package | Production behavior |
|---|---|
| P2 native commands and search | The text area obtains the installed editing command set from the native handler. One delegation exposes only the original binding for that invocation. Both panel commands and the text-area fallback route ReplaceNext and ReplaceAll through admission. Template renewal uninstalls the previous panel. An obsolete panel consumes an unhandled replace event before it can bubble to a successor. Document replacement closes the search session. Search preflight uses the native search strategy for pattern validity and matches. Empty results, identical text and read-only rejection preserve redo. ReplaceAll checks writable segments before delegating the native grouped algorithm. Find, panel-field editing and navigation keep their native routes. |
| P3 completion and overload | Completion items belonging to a workbench session cannot write through a retained item reference. The actual insertion request owns the commit. The controller snapshots the selected item and range at close and removes its request handler. The native event's already captured invocation list delivers the original request once. Clearing the selection prevents native construction of an anchor on a document installed by a later Closed subscriber. The handler checks session, editor, document, generation, invocation anchor, range, document version and write access before a single replace. The acceptance callback runs under observation. Typing may update matches during a session. The reopen policy checks its original document and accepted generation. Overload notifications check the originating window and document. |
| P4 clipboard | Native Copy reads the current selection or whole line and publishes TextCopied under observation, including during a drain. Cut awaits successful export before deletion. Paste reads before opening a native update group. The saved selection, caret, document, revision and generation must still match at commit. Whole-line cut removes deletable segments in reverse order. Rectangle selection uses the native selection object. Paste keeps document newline and tab conversion. A failed provider or stale intent produces no subsequent deletion or insertion. A completed external export can survive a rejected cut. |
| P5 variables and parameters | The selected row and parameter must belong to the published draft. The draft captures variable identity, parameter membership, service state version and workspace generation. The commit uses the same service as the editor, obtains coordinator admission, then uses the interpreter reservation and revalidates the version before writing. Refresh publishes detached copies. A busy refresh keeps the existing display, marks it stale and exposes a manual refresh action. Variable-view detach invalidates its draft. Model, collection and selection publication run under observation. A state-only edit leaves the document revision unchanged. KeyMap execution and the already-admitted book clear use the injected service. |
| P6 observers and lifecycle | The native input continuation is consumed before the view handles an admitted input. Synchronous TextEntering and TextEntered subscribers run under observation. The original Return execution starts at its existing point before subscriber notification. Editor TextChanged and workbench property/collection publication also use the coordinator. Existing synchronous presentation failures propagate after cleanup when their caller depends on the exception. View detach removes input, caret, completion and popup subscriptions. Disposed completion sessions and obsolete Markdown commands cannot acquire rights through a successor. |
| Existing service entry points | The existing editor update checks coordinator activity and drain state at initial validation and again before mutation. A competing incoming write gets Busy. A GUI execution, save or state write cannot acquire fresh admission from a controlled presentation observer. The interpreter queue, external execution policy, public DTOs and activation protocol keep their existing roles. |

**Owner decision for P4.** On 2 November the owner chose the safe host adapter and accepted that coordinated Cut does not raise the native TextCopied event and coordinated Paste does not raise TextPasted. Pure Copy still raises TextCopied. The pinned library exposes the two event raisers internally. Its native paste holds a document update across an asynchronous provider read. No application subscriber requires either omitted notification. The test proxy delays the actual platform interface. Production uses public APIs. [Pinned clipboard handler](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/EditingCommandHandler.cs), [pinned TextArea](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs).

**UpdateFinished recursion.** The late guard must close the native group opened by a rejected BeginUpdate. In this version the library sets its update count to zero and ends the undo group before publishing UpdateFinished. The workbench installs a paired handler alongside its early UpdateStarted guard. It stops delivery of the synthetic finish for a rejected start. Later subscribers that never received that start cannot recursively initiate another rejected edit from its finish. Legitimate completion of the outer history group still delivers one finish. Permanent bounded tests exercise both the accepted history transaction and the observation-only path, checking text, group closure, pending count and subsequent availability. The proof covers the workbench installation order. It does not establish general safety for foreign handlers installed on a raw document before handing it to the host. [F2 reproduction](MCP-AP3-02-F2-independent-rereview.md), [native BeginUpdate and EndUpdate](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Document/TextDocument.cs).

## AP3-01 mutation crosswalk

The following groups cover M01 through M51. Mixed rows distinguish the B1 delivery from the remaining work.

| IDs | Disposition |
|---|---|
| M01, M02, M03 | Native input and Unicode use one event-bound delegation. Existing early IME configuration and accepted CJK limitation apply. |
| M04, M05 | Return, KP_Enter and Shift+Enter retain the native newline and execution semantics. Pending job and output authority belongs to P8/AP5. |
| M06, M07, M08, M09 | Editing commands cover delete families, direct routed delete, Tab/Shift+Tab and native undo/redo. Tests also retain the AP3-02 anchor/group matrix. |
| M10, M11, M12, M13 | Strict asynchronous cut/paste intent, export failure, whole-line, rectangle, read-only, tab/newline and undo behavior. The P4 owner decision applies. |
| M14, M15, M16 | Native panel, nested handler, panel installation/renewal, programmatic routed commands and document-change closure. |
| M17, M18 | Original-session insertion, cancellation, close ordering and guarded refresh/reopen/overload presentation. |
| M19 | Accepted AP3-02 history command and capability remain the writing authority. |
| M20, M21, M22, M23 | Existing early drag disable covers inbound, source and move paths. Options and template behavior remain covered by GUI regressions. Native drag matrix is open for this candidate. |
| M24, M25, M26, M27, M28, M29, M30 | DeleteLine, whitespace, case, TitleCase, tab conversion, indentation and overstrike use the installed command/Tab boundaries. D07 retains the native TitleCase limitation. No new user gesture was introduced. |
| M31 | Find, navigation, selection and native Copy remain available while writes are blocked. |
| M32, M33, M34 | Controlled publication, initialization before registration, original-view lifecycle and document invalidation. The app-wide shutdown fence belongs to AP4. |
| M35, M36 | Picker intent and full book admission belong to P8/AP4/AP5. B1 supplies the instance-bound already-admitted clear and guarded synchronous publication. |
| M37, M38, M39, M40 | B1 supplies the synchronous collision/observer checks and the existing Return entry. End-to-end edit receipts, execution admission, anchored output and posted presentation remain P7/P8/AP4–AP6 work. |
| M41, M42, M43, M44 | Controlled observers cannot start saves. The P6 document guard handles rejected-group recursion. Save/provider shutdown, file commit, jobs and recovery retain the AP4–AP6 boundary. |
| M45, M46 | Selected variable and parameter commits check original draft, state version and coordinator ownership. |
| M47, M48 | Password/provider lifecycle and public MCP protocol are later packages. |
| M49, M50, M51 | Same-service KeyMap wiring, guarded variable refresh and versioned detached drafts. KeyMap's asynchronous producer and job completion remain P7/P8/AP5. |

## AP3-01 observer crosswalk

| IDs | Synchronous B1 treatment and later boundary |
|---|---|
| OBA01, OBA02, OBA03 | Admitted view input, observed native subscribers, early update/change guards and synthetic finish regression. Return output remains P8/AP5. |
| OBA04, OBA05, OBA06 | Caret status, original-view attach/detach, sender-aware overload closure and observed provider notifications. |
| OBA07, OBA08, OBA09, OBA10, OBA11 | Completion caret/key/document listeners, update aggregation, window closure, commit completion and reopen anchors. Aggregation and anchor cancellation only update private session bookkeeping. Dispose removes original subscriptions. |
| OBA12, OBA13 | Bridge identity/revision bookkeeping remains synchronous authority code. History navigation retains its single-use AP3-02 capability. Its notifications convey observation only. |
| OBA14, OBA15, OBA16 | Synchronous receiving-side property publication is guarded. Off-thread Print/Info/KeyMap posts and producer accounting belong to P7/AP5. |
| OBA17, OBA18, OBA19, OBA20 | Common bindable publication includes Main, Info, KeyMap, sidebar, status and detached variable/parameter models. Diagnostics and variable collection delivery have explicit observation boundaries. Failure propagation and stale-draft handling have separate tests. |
| OBA21, OBA22 | Info-history publication, Markdown command context, TextChanged and the existing native identity/dirty/read-only leases. Full save completion belongs to AP4. |
| OBA23, OBA24, OBA25, OBA26, OBA27 | Confirmation, password, cancellation, registry timers and pipeline events remain AP4/AP5/AP6 obligations. |
| OBA28, OBA29, OBA30 | Local view disposal and subscription cleanup are covered. Complete book/job, save/provider and desktop shutdown drains remain with their original packages. |
| OBA31 | Focus invalidates input intents. Completion anchors, selection, template renewal and disabled drag/IME compose with this boundary. |

## Changed files

Paths below are relative to the product worktree. These are the candidate's source, test and report files.

| Directory | Files and purpose |
|---|---|
| `CryptoWorkBenchAvalonia/` | `App.axaml.cs` disposes the info-history subscription at exit. |
| `Controls/` under the GUI project | `WorkbenchTextArea.cs` for native input, command and clipboard boundaries. `WorkbenchTextEditor.cs` for template/search, document identity, observed text publication and paired update guards. |
| `Services/Automation/` | `EditorBridge.cs`, `EditorCoordinator.cs`, `WorkbenchEditorOperations.cs` for open registrations, selection, target validation, global transitions and collision checks. `WorkbenchAutomationService.cs`, `WorkbenchAutomationRegistry.cs`, `WorkbenchSaveCommit.cs` for service-bound state, version checks and synchronous observer rejection. |
| `Services/` and `Models/` | `InfoCompletionController.cs`, `InfoCompletionReopenPolicy.cs` for insertion, popup lifecycle and original-document checks. |
| `ViewModels/` | `CryptoScriptEditViewModel.cs`, `VariableViewModel.cs`, `ViewModelBase.cs`, `StatusViewModel.cs`, `MainViewModel.cs`, `InfoViewModel.cs`, `KeyMapViewModel.cs`, `SidebarViewModel.cs` for shared service wiring, drafts, observable presentation and cleanup. |
| `Views/` | `CryptoScriptEditView.axaml.cs`, `InfoView.axaml.cs`, `VariableView.axaml.cs`, `VariableView.axaml` for session/lifetime bindings, stale controls and explicit refresh. |
| `CryptoWorkBenchGuiTest/` | New `B1EditorIntegrationTests.cs`. `EditorCoordinatorTests.cs` extends the existing clipboard proxy to delay/fail exports. `AsyncPasswordGuiTests.cs`, `CwmInfoTests.cs` and `WorkbenchReviewRegressionTests.cs` exercise real registered-editor admission, stale refresh and exception cleanup. `EditorReviewCorrectionTests.cs` checks immediate observer-swap denial before requesting the original lifecycle change on the dispatcher. `NativeEnterContinuationTests.cs` checks that the original ticket stays current while observers have zero write authority. All original test cases are retained. |
| `docs/mcp/` | This report. |

## Verification

Per-configuration locked restore and single-node build used the pinned SDK and the repository's `eng/prism/Enter-Prism.ps1` hooks on `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln`. The hooks verified the pinned official Prism source. Local logs and TRX results are under `artifacts/b1/`. The final runs have `candidate` in their filenames. Intermediate failed/probing runs in this directory are not final evidence.

| Run or test group | Result |
|---|---|
| AP3-02 baseline | 589/589 GUI executions passed, zero skipped |
| Debug and Release locked restore and GUI solution builds | Both passed. Each final build reported 8 warnings and 0 errors. |
| Debug GUI suite | 624/624 executions passed, zero skipped. The suite adds 35 B1 cases to the reproduced 589-case baseline. |
| Release GUI suite | 624/624 executions passed, zero skipped. |
| Focused B1, coordinator, native Enter and history filter, Debug | 187/187 passed in a separate execution. This is a subset of the full GUI suite. |
| MCP suite, Debug and Release | 89/89 executions passed in each configuration. NUnit separately listed the explicit installed-Codex compatibility probe as skipped. It is outside the 89 executed results. |
| Two-document and real in-process MCP contention | Distinct lifetimes, A/B ticket rejection, switch during pending work, independent undo/redo, close/reopen, drain across close and an authorized MCP append competing with GUI writes |
| Commands and search | Installed mutation bindings denied during drain, uppercase/undo accepted, four panel/text-area × next/all cases, stale template, invalid pattern, read-only and no-op redo retention |
| Completion and overload | Real CompletionWindow requests, Tab/Enter acceptance, stale item, generation change, dispose, document swap, close-time swap/dispose and undo. Overload data/provider checks also run in the existing suite. |
| Clipboard | Real platform interface plus delayed provider. Copy during drain, callback reentry, Unicode, failed/late cut, delayed paste, rectangle, whole line, protected prefix and normalized paste/undo |
| Variables, parameters, properties | Value-domain validation, foreign selection/parameter rejection, stale state and generation, fresh commit, mutation/presentation failures and subsequent availability |
| Document observers and AP3-02 | Bounded recursive UpdateFinished probe with and without history, direct input-observer write rejection, original receipts, anchor matrix, history capability, disposal and Return/KP_Enter regressions |

The final TRX counters were read back from `debug-candidate-CryptoWorkBenchGuiTest.trx`, `release-candidate-CryptoWorkBenchGuiTest.trx`, the corresponding two `CryptoWorkBench.Mcp.Tests.trx` files and `candidate-targeted.trx`. The final desktop GUI assembly SHA-256 was `92D36BF92E15473D747966657D655954953BE3125B1BECA3627D3C460BD67D7D` in Debug and `71A7076530985CF025DC807E1BD25370D7793DE6F78B09E2045902823D284655` in Release. Intermediate integration failures identified the initial dirty-state change, pre-B1 test admission assumptions and cross-session access to a completed drain's document. The candidate restores pre-publication initialization, updates the relevant regression setups and avoids inspecting the document of an already signalled drain.

### Native Windows observations

An isolated, unsaved Windows Debug session used synthetic script data. The accessibility tree and window pixels were observed between actions. The application was closed after the smoke. The following are observations on the intermediate B1 Debug build.

| Action | Observed result |
|---|---|
| Paste Unicode, execute with Return, execute a second line with KP_Enter | Each command produced a single expected output. The caret reached line 5. The Unicode output retained the umlaut and euro sign. |
| Alt+PageUp, Ctrl+Z, Ctrl+Y | The first test command appeared on line 5. Undo restored the empty line and redo restored the command. Earlier output remained visible. |
| Ctrl+H, fill both fields, click Replace All, Escape | Three occurrences changed to the replacement, including the recalled line. Previous outputs stayed intact. |
| Ctrl+X, Ctrl+V on the selected match | The selection was removed and its text returned at the same position. |
| Enter Info and the opening parenthesis, accept with Tab | The native popup displayed its choices. The selected completion appeared once on the same line. |
| Open Variables, edit value, click its Modify button | The recaptured grid displayed the new value. The editor source stayed visible. |
| Edit a parameter grid cell using F2, Tab and the row Modify button | The recaptured parent parameter serialization contained the edited value. |

**Open native evidence.** The user stopped Computer Use with physical Escape before a repeat on the final Release binary. Later changes to input observation, search preflight, close-time invalidation and variable-view refresh have final automated coverage. A final-binary desktop repeat, overload navigation, mouse acceptance of completion, native rectangle/drag gestures, external-application clipboard interoperability and physical Swiss AltGr/dead-key input remain open. Provider failure was injected through the platform interface. The physical Swiss keyboard pass on 2 November applies to AP3-02. [AP3-02 native acceptance](MCP-AP3-02-N1-R-native-history-acceptance.md).

## Review boundary

B1 prepares internal document targeting. B2 follow-up work on P7/P8 must carry the original GUI intent through posted producers and execution handoffs. IR-N1-01 remains open for P8/AP5. N1-F02 remains open for the Ctrl/Alt line-history help text. The accepted unsupported multi-stage CJK IME risk remains in force. Public MCP document addressing belongs to B3. Workspace lease acquisition, epoch credentials, complete shutdown/provider drain, job output finalization, transport and Script Book persistence remain at their existing later gates.

Final Git verification confirmed the original branch and HEAD, an empty index and a clean whitespace check. The diff is confined to the GUI, its tests and this report. The implementation is submitted for independent B1 review. No known Critical or High finding remains within the implemented host paths. The unexecuted native cases above remain review inputs. Final B1 acceptance belongs to the owner after the independent review.
