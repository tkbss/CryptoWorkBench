# G1-N1-F03 — Group and CryptoScript document redock

Investigation: 2026-11-09, Windows, worktree `artifacts/worktrees/gui-g1`, branch
`feature/gui-g1`, base HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.

**Corrected mouse acceptance is PENDING for both reported failures.** The owner
must verify the final Release application. Earlier F01/F2 reports and their
acceptance evidence remain unchanged; M3–M8 remain open. No commit, push or merge.

**Work stopped at the owner's request (“kannst du den task beenden”).** Changes
and the final Release build are available, but F03 is not fully validated or
accepted. The active input-test runner was stopped; no G1ShellProbe windows
remained in the subsequent Computer Use inventory. No new acceptance run was
started after the stop request.

## Real mouse reproduction

At task start an existing Release application already exhibited the group
failure: the main window (HWND 330086) contained all five tools, while HWND
1378934 was an empty, tracked Dock host. Its accessibility state is preserved in
`artifacts/g1-n1-f03/existing-owner-window-state.json`. This observation alone
does not establish the preceding mutation path.

The actual instrumented Release application was then started with `--dock-shell
--dock-native-trace`, PID **6804**, main HWND **264912**. No Factory command
substituted for the owner's mouse gestures. The Computer Use drag API provides
one window-relative coordinate space; the owner therefore performed the
cross-window gestures. Launch paths and DLL hashes are in
`release-mouse-before-fix-launch.json`.

The owner reproduced the five-tool group and replied **“guppen-drop fertig”**.
For CryptoScript the owner reported no docking menu while moving the window,
then confirmed the distinction after an additional trial:

> Wenn ich den cryptoscript tag nehme und ihn droppe funktioniert es. Ich wollte
> das ganze window droppen. Das hat nicht funktioniert.

The original trace, three immutable intermediate snapshots, owner responses,
native model states and extracted lifecycle events are retained under
`artifacts/g1-n1-f03/`. `analyze-mouse.mjs` produces `mouse-diagnosis.json` from
the completed trace. The recorder enumerates all process top-level HWNDs,
independently of `Factory.HostWindows`, and additionally records full Dock trees,
model identity/owner/children, policy decisions and collapse entry/exit.

## Proven group failure

The owner dragged the original `dock:diagnostics` ToolDock, identity **47484332**,
out of the main layout. Unlike a newly created floating ToolDock in the F01
tests, that original anchor has **IsCollapsable=false**. Diagnostics, Status,
Variables, Output and Info were subsequently grouped in that same anchor.

| Identity | Recorded state after group drop |
| --- | --- |
| HWND 1772150 | Still visible, title `Window`, class `Avalonia-625fbffc-a0af-404f-aa8f-2522f7ad6056` |
| Native ownership | Owner 264912, parent 0, WS_EX_APPWINDOW=true, WS_EX_TOOLWINDOW=false, WS_EX_NOACTIVATE=false |
| Managed host 41778755 | `DockShellLayout.DocumentSafeHostWindow`, still Factory-registered |
| DockWindow 45034544 | Still owned by main root 43584767; floating root 60248619 |
| Floating ToolDock 47484332 | Empty children, IsCollapsable=false; no tool/document remains in its host |

Mouse release is sequence **9667**. The accepted group move invokes successive
`MoveDockable` operations, interspersed with `CollapseDock`. At **9714–9715** the
last tool has left, but `CollapseDock` returns immediately because the source
anchor is non-collapsable. Mouse dispatch completes at **9723** with the native
window still visible. **There is no RemoveWindow or OnClosing call for this
drop**, hence no CloseReason/Cancel decision to blame. Model registration and
native existence agree here: this is an empty retained host, not an unregistered
native orphan.

The F01 correction remains necessary and unchanged in purpose: it permits
closing an owned, empty source window during an already admitted relocation.
F03 fixes the earlier step which never reached that cleanup path. It does not
broaden the F01 permission to other hosts.

## Proven document-window failure

Floating CryptoScript produces HWND **134024**, managed host **57216465**,
DockWindow **34739493**, floating root **11324187**, document group **12996709**.
The original empty `dock:documents` anchor **20929853** remains in the main tree.
`CanRelocate=true`, observer depth 0, and the document retains `CanClose=false`.

Windows records native move loops (EVENT_SYSTEM_MOVESIZESTART/END), including
**10611–12411** and **12447–13971**, for HWND 134024. During those caption drags,
there is no Dock `WindowMoveDragBegin`, no Dock drop-policy evaluation and no
DockManager mutation. This is **not a denied drop or an OnClosing veto**: the
native OS caption never starts Dock's drag pipeline.

The owner then used the inner CryptoScript tab. The trace records successful
DockManager mutations, RemoveWindow, uncancelled OnClosing and native source
destruction, including completed drops **19853** and **26583**. The earlier
group's empty HWND remains separate throughout. Thus tab dragging works before
the F03 behavior changes; whole-window caption dragging was the missing path.

The locally available Dock source matches package **12.1.0.13**, commit
`78da173545501e796f37bc8528e9e55d9cbfd991` (NuGet repository metadata and local
source HEAD agree). `HostWindow.TryBeginMoveDrag` starts tracking, while the
Avalonia 12 document host theme uses an ordinary native caption. The tool chrome
has a Dock-aware route; the OS caption does not. `DockManager.ValidateDocument`
allows the document return anchor and intentionally rejects ToolDock targets.
Neither `DockCapabilityPolicy` nor `IDockDropPolicy` required relaxed permissions.

## Targeted changes and additional regression findings

- `EditorDockFactory.CollapseDock` permits the ordinary collapse of an empty
  fixed anchor only when it belongs to an owned floating window and an admitted
  relocation is already in progress. The main workspace's fixed anchors remain
  protected. The normal RemoveWindow/F01 close lifecycle performs destruction.
- A whole-anchor float recreates its missing main return anchor inside the same
  admitted mutation. The initial regression demonstrated that whole-document
  dragging otherwise removes the DocumentDock return area. Repeated group
  cycles also require a surviving tool return area. Pane and editor instances
  are moved, not recreated.
- An exploratory nested-group regression exposed an upstream live-list iteration
  failure in `DockManager.ValidateChildDocks`: collapse removes child docks while
  the manager indexes that same list. `IDockOperationObserver` now defers only
  empty docks from that particular proportional source tree until synchronous
  scope disposal. Cleanup re-enters the existing admission gate; there is no
  timer, global window scan, exception suppression or lease bypass.
- Productive Windows document hosts receive a caption with a real pointer drag
  surface calling the public `HostWindow.TryBeginMoveDrag(..., FullWindow)` API.
  Dock's existing content template and overlays are retained. Minimize,
  maximize/restore, double-click maximize, resizing and protected Close remain
  available. The window title and taskbar presence remain. Tab dragging continues
  through its existing path. The caption's admission check uses `CanChange`;
  Dock's own policy checks still run afterwards.
- The native recorder adds model-tree snapshots, source/target drop-policy
  observations and collapse observations. Recording remains opt-in.

## Validation

Final matrix results are recorded in `docs/gui/evidence/G1-N1-F03-results.json`.
`verified-final/` completed successfully for both configurations:

| Suite | Debug passed | Release passed | Failed |
| --- | ---: | ---: | ---: |
| GUI | 984 | 984 | 0 |
| MCP | 176 | 176 | 0 |
| Core | 11,512 | 11,512 | 0 |
| Headless prototype | 12 | 12 | 0 |

Existing exclusions remain: 13 explicit MCP tests and 27 Core tests (26 skipped,
one explicit) per configuration. Native Debug automatic modes passed **514**
checks; the expanded native Debug redock probe passed **134** checks. Native
Release was built but its matrix/redock run was not performed before stopping.

**External native input validation is not green.** Debug editor-input completed
with two failed completion-popup checks (`completion-in-floating-host` and
`completion-retired-on-relocation`). The cause was not resolved before stopping;
the automatic editor mode passed, which does not erase these failures. Debug
tools-input timed out waiting for clipboard content after the floating-value
selection (the copy action had not been completed). Output-input timed out
waiting for external input. Diagnostics-input was interrupted; layout-input and
all Release external-input modes were not completed. All partial/failed results
are retained. The requested complete native Debug/Release matrix therefore
remains outstanding.

Permanent regression coverage includes single tools, the original fixed anchor
with all five tools, nested proportional groups, document tabs and whole document
docks. Repeated Fill/Right/Bottom drops retain another legitimate floating host,
the original editor/TextDocument/UndoStack, working undo and redo, all pane
identities and global tool contexts. Active MCP leases reject drop and caption
drag, still authorize MCP editing, and are exercised through normal shutdown.
The original F01 regression suite remains enabled.

The native redock probe compares the **complete visible top-level HWND set**
before and after every move, checks source HWND destruction independently of
Dock registrations, and checks document-host destruction on shutdown. Its
programmatic DockManager operations do not certify mouse acceptance.

Intermediate failures are retained: sandbox build/testhost limitations, a build
blocked by the owner's running Release process, the initial red group/anchor
tests, nested-group failures, and a full-matrix failure in a presentation-free
layout test. The new caption is consequently restricted to productive editor
sessions, whose Dock theme/content template exists. An early caption test run
used the previous binary after a compile failure; it is not final evidence.

## Manual acceptance of the final Release build

Use the final executable under this worktree's
`CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/` with `--dock-shell` only.

1. Float Variables alone and mouse-redock it. Repeat while Diagnostics remains
   separately floating; no empty native/taskbar window may survive.
2. Float the original Diagnostics/Status group using its header. Move Variables,
   Output and Info into it, then mouse-redock the entire five-tool group. Repeat
   using center and side targets. For this all-tool group keep CryptoScript
   separately floating as the legitimate other window.
3. Float CryptoScript. Drag the **outer caption** of the corrected floating
   window over the free document area. Dock targets must appear and the whole
   window must redock and disappear. Repeat using the inner CryptoScript tab.
   Keep Diagnostics separately floating; it must survive.
4. Verify editor text, undo/redo and tool contents, then close the main window
   normally with a document floating. No native host may survive shutdown.
5. If a failure occurs, leave the window open. Repeat with a fresh, explicit
   `--dock-native-trace <new-path>` output file to preserve the complete lifecycle.

The corrected whole-window workflow has not yet been confirmed by the owner.
No automated or single-window input check changes that acceptance status.
The corrected document caption was visually inspected during Debug editor-input;
this was not a cross-window redock acceptance.

## Risks and limits

The original five-tool failure and native-caption failure were both reproduced
by the owner. The additional whole-anchor and nested-group defects were proven
by targeted regression tests, not claimed as separate owner mouse reproductions.
The title-bar integration is Windows-specific; no new Linux/macOS desktop
acceptance is claimed. Native recording affects timing and retains managed
identities until exit, so final acceptance must use the ordinary uninstrumented
Release build. Earlier G1 evidence is preserved rather than retroactively edited.
