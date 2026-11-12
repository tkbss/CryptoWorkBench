# G1-N1-F01-F2 — Native Windows mouse-redock investigation

Investigation date: 2026-11-09. Worktree: `artifacts/worktrees/gui-g1`, branch
`feature/gui-g1`, base HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.

**M2 is PASS: the owner accepted the normal Release mouse workflow without a
recorder on 2026-11-09. M3–M8 remain OPEN.** This acceptance is based on the
owner's explicit confirmation, not automated checks alone.
This report supplements, and does not rewrite, the earlier G1 and N1 evidence.

## Actual reproduction and current finding

The owner performed an actual mouse float and redock in the real Release
`CryptoWorkBenchAvalonia.Desktop.exe --dock-shell`, PID **40336**, with opt-in
native recording. This was not a G1ShellProbe or a Factory-driven substitute.
Computer Use's available `drag` API has one window argument and coordinates in
that window; the cross-window gesture was therefore handed to the owner.

The first reply reported empty windows. The immediately preserved trace showed
only the main window after the drop, including an independent enumeration of
all top-level HWNDs owned by this PID. On clarification the owner replied
**“Keine leeren Fenster mehr sichtbar”**. The discrepancy must not be represented
as a reproduced orphan-window failure. This recorded title-bar redock succeeds;
the reported earlier failure is not yet explained by this run.

The owner subsequently performed additional real mouse cycles while Diagnostics
remained floating and replied **“Tab-Redock fertig – Diagnostics bleibt, keine
leeren Fenster”**. The journal captures Variables hosts **461412** and **5311058**
being closed and destroyed; Diagnostics HWND **461042** remains visible and
registered. Evaluation of intervals 6529–9925 and 10209–10659 passes ten native
checks each, including preservation of Diagnostics. The requested inner
tab/header gesture is confirmed by the owner; the routed event records its Grid
source and Dock Factory drag events, not the complete internal method call stack.

After all final Debug/Release checks, the ordinary Release entry point was
started again with **only `--dock-shell`**, PID **5672**, without an active
recorder. After the mouse trial, the owner explicitly confirmed
**“M2 bestätigt: keine leeren Fenster, Diagnostics bleibt”** on 2026-11-09.
This establishes M2 acceptance for the normal Release workflow. The latest
read-only window inventory contains only the main window
(HWND **5244086**); an earlier inventory observed a real Variables floating
window (HWND **2425530**). This inventory is not a lifecycle journal; the final
normal-run acceptance rests on the owner's confirmation. The application was
left open for the owner. Exact final DLL identities are in
`release-uninstrumented-launch.json`.

The completed evaluator also checks exact equality of the complete visible
native-window set before/after removal of the source HWND, regardless of managed
mapping or window styles. The final `*-evaluation-complete-census.json` results
are **10 + 11 + 11 passed, zero failed** for these three mouse intervals. Earlier
evaluation files are retained as intermediate evidence.

No second behavior fix has been applied on the strength of an unverified
hypothesis. The first correction remains a demonstrated fix for its particular
mutation-scope veto, not proof that every mouse path is fixed.

Raw evidence is under `artifacts/g1-n1-f01-f2/`:

- `release-mouse-01-at-owner-failure.jsonl`: immutable snapshot taken immediately
  after the first owner reply; despite its chronological filename it does **not**
  demonstrate a surviving empty window.
- `owner-failure-state.json`: corresponding final native/managed/model census.
- `mouse-window-lifecycle.json`: selected native lifecycle events.
- `native-mouse-01-evaluation.json`: nine checks passed, zero failed, for the
  observed HWND and sequence interval; no acceptance certification.
- `release-mouse-02-owner-success.jsonl`, `owner-second-success-state.json`,
  `native-mouse-02-evaluation.json` and `native-mouse-03-evaluation.json`: further
  owner-operated cycles, ten checks each with the other floating window retained.
- `release-binary-identities.json`: actual loaded build-directory DLL hashes.
- `release-launch-01.json`: launch command identity and PID.

## Native identities and observed lifecycle

Main window HWND **329508**, managed identity **53027535**, is a
`DockShellWindow`. It remains visible, unowned, `WS_EX_APPWINDOW=true`.
Not being a Dock HostWindow, it is correctly absent from `Factory.HostWindows`.

| HWND | Managed identity/type | Lifecycle in recorded run |
| --- | --- | --- |
| 983738 | 17153593, `Dock.Avalonia.Controls.DragPreviewWindow` | Native create seq 1497, show 1501, managed Closed 1816, native hide 1826 and destroy 1827. Never a registered Dock host. |
| 2296018 | 4306258, `DockShellLayout.DocumentSafeHostWindow` | Factory locator return 1808, native create 1818, show 1823, foreground 1824; registered Variables floating host. Closed during redock, native destroy 2605. |
| 329508 | 53027535, `DockShellWindow` | Main workspace before, during and after the gesture. |

The trace stores the exact native class (including Avalonia's per-process class
suffix), title, `GetWindow(GW_OWNER)`, `GetParent`, native visibility, full style
and extended style, APPWINDOW/TOOLWINDOW/NOACTIVATE flags, managed ShowInTaskbar,
managed identity, current/last HWND, model identity, pane IDs and owning groups.
`GetParent` is recorded as returned by Win32; for owned top-level windows it can
return the owner. Taskbar flags are evidence of eligibility, not a query of
Explorer's rendered taskbar buttons.

At the final snapshot, seven top-level HWNDs remain: the visible main window,
two hidden AvaloniaSimpleWindows, one hidden AvaloniaMessageWindow, and hidden
IME/MSCTFIME helper windows. All are listed even when no managed Dock object
maps to them. There is no visible orphan HostWindow, DragPreviewWindow or
unmapped APPWINDOW in that snapshot.

### MouseDown through completed MouseDrop

1. MouseDown on Variables tab: seq 1407. A real drag preview is created and shown.
2. MouseUp outside the main workspace: seq 1802. Floating host creation follows;
   the drag preview is closed and its HWND destroyed.
3. The floating window receives native move-loop events, including an initial
   repositioning and then the redock gesture starting at MouseDown seq 2052.
4. MouseUp seq 2579 enters the real HostWindow drag pipeline. Factory
   `MoveDockable:before` is seq 2580, followed by pane/group removal.
5. `RemoveWindow:before` seq 2583; integration `OnClosing` entry seq 2584 has
   `CloseReason=WindowClosing`, `Cancel=False`. Avalonia Closing and Factory
   WindowClosing are observed, followed by `OnClosing` exit seq 2587 with
   **Cancel=False**. No integration veto is emitted.
6. Managed Closed seq 2590 finds the HWND absent from the native census even
   though the host is still briefly in the Factory list. Factory Closed and
   Removed follow at 2591/2592. `RemoveWindow:after` is 2593.
7. Move completes at 2596; SplitToDock completes at 2599. Native destroy event
   `EVENT_OBJECT_DESTROY` for 2296018 arrives at 2605. The posted observation
   `MouseUp:dispatch-complete` is seq 2607 and still shows only the main window.

Thus registration and native lifetime are deliberately checked independently.
In this run the transient difference is a destroyed HWND still registered
during the Closed callback, not a live HWND surviving deregistration.

## Relationship to N1 and limits of diagnosis

N1 admitted cleanup of exactly the empty owned source model during an existing
relocation. It did not grant permission for a new move, close a populated host,
or relax coordinator/MCP admission. The observed close in this run traverses
that path and is not canceled. No missing Close callback or surviving native
handle is evidenced here.

Source inspection distinguishes `HostWindowState` (native title-bar move) from
`DockControlState` (tab drag), and identifies separate `DragPreviewWindow` and
`DockAdornerWindow` types. DragPreviewHelper and AdornerHelper contain their
own close paths. The actual recorded floating operation created a preview
window; no persistent DockAdornerWindow was observed in this run. Source
inspection alone is not proof of a failure in any of these components.

The main checkout also contains an older Release GUI DLL dated 2026-11-06,
SHA-256 `E89AE5D4FE69C5A69A06819FEE8A6829628FCC6399AA5E322581DCE4450762E6`.
The instrumented worktree's Release GUI DLL is
`C6FDE1575D037B657A720E400A5B0C4435EADC40CB5B73E9C297256BF3D71E5F`.
These are distinct builds. The old DLL lacks the N1 method metadata name
`IsCompletingEmptyWindowRemoval`; the tested DLL contains it
(`first-fix-binary-marker.json`). **The owner explicitly confirmed the earlier
launch path was the gui-g1 worktree directory.** A main-checkout path mix-up is
therefore not a supported explanation. There is no saved binary hash of the
earlier failing launch with which to establish exact binary identity.
The executable apphost hash alone is insufficient to identify managed code.

## Changes made for investigation

- Explicit `--dock-native-trace <new-jsonl-path>` option on the real desktop
  entry point, usable only with `--dock-shell`. Normal launches do not install
  the recorder. Existing files are not overwritten.
- Process-filtered EnumWindows census and read-only WinEvent hook, global
  managed window discovery, activation/Closing/Closed, routed mouse events,
  Dock diagnostics and Factory operation boundaries.
- Integration OnClosing entry/exit logging around the veto, so canceled closes
  that never reach the base event are visible. No close-policy change.
- Retained identity/last-HWND mapping after removal from Dock registrations.
- Native regression now compares the **entire set of visible process HWNDs**
  with the baseline containing the main and legitimate Diagnostics window on
  each of six programmatic redocks. This adds six checks to the former 32.
- `eng/gui/Test-DockNativeTrace.ps1` independently evaluates a real manual
  trace interval and checks native destruction, complete managed close,
  unregistered hosts, leftover overlays and optional retained legitimate HWNDs.

The 250 ms timer samples state only. It never closes, hides, disposes or changes
a window. The posted MouseUp callback only records state after dispatch.
The recorder observes native destruction and loss of platform handles; Avalonia
does not expose a public Window.Dispose event. Internal preview IDisposable
callbacks are not individually instrumented. Absence of their callbacks must
not be claimed from this journal.

## Validation status

The complete final matrix was rerun against the final source and is green:

| Suite | Debug passed | Release passed | Exclusions per configuration |
| --- | ---: | ---: | --- |
| GUI | 965 | 965 | none |
| MCP | 176 | 176 | 13 Explicit |
| Core | 11,512 | 11,512 | 26 skipped, 1 Explicit |
| Headless prototype | 12 | 12 | none |
| Native automatic baseline | 514 | 514 | none |
| Native external mouse/keyboard baseline | 205 | 205 | none |
| Native redock regression | 38 | 38 | none |
| **Native total** | **757** | **757** | prior 751 + 6 new census checks |

Every final run passed on its first matrix attempt. Raw results: `verified/`,
`native-Debug/`, `native-Release/`, `input-Debug/`, `input-Release/`, and
`redock-Debug.json` / `redock-Release.json`. The earlier
`diagnostic-validation-Debug/` run is retained separately. The real owner mouse
trace evaluations are additional evidence, not mixed into these baseline counts.
Native probes remain programmatic where labeled; only the input modes used
external Computer Use input, and only the owner trials certify the real
cross-window mouse gesture.

The UI driver re-observed window changes instead of reusing closed window
handles. After Alt+PageUp the menu held focus in the completion fixture; an
explicit click refocused the editor before its completion keystroke. This was
not counted as a new acceptance item or used to claim M3–M8 complete.

An initial failed recorder compilation and an initial smoke startup without
the shell attachment are retained in raw logs. The recorder was moved to
Application.Initialize before the real owner trials; none of these preparation
attempts is counted as a successful mouse reproduction.

## Reproducible manual check

From **this worktree**, after a Release build with the existing Prism workflow:

```powershell
& ./CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.exe `
  --dock-shell --dock-native-trace "$PWD/artifacts/g1-n1-f01-f2/manual-new.jsonl"
```

Use a fresh output filename. The application's normal saved layout is used;
the pre-investigation profile was copied to `profile-layout-before.json` without
resetting it. Float Variables using the mouse, inspect its content, then redock
using both the native title bar and the inner tab/header in separate trials.
Keep Diagnostics floating during at least one trial. Do not close a suspected
orphan before its HWND and state have been captured. Record the exact drop and
remaining windows, then close the main window normally to flush shutdown.

Select the source HWND and before/after sequence numbers from the journal:

```powershell
./eng/gui/Test-DockNativeTrace.ps1 `
  -Trace artifacts/g1-n1-f01-f2/release-mouse-01-at-owner-failure.jsonl `
  -SourceHwnd 2296018 -BeforeSequence 2052 -AfterSequence 2607 `
  -Output artifacts/g1-n1-f01-f2/manual-evaluation-new.json
```

For a trial with a legitimate other floating window, also pass its HWND in
`-RetainedHwnd`. Evaluation is not a replacement for the owner's acceptance.

## Open risks and remaining work

The earlier failure has not yet been assigned to a specific surviving HWND or
proven defect. Further owner-operated cycles preserved another legitimate
floating window without reproducing an orphan. Logging affects timing and retains
managed identities until process exit; the subsequent normal Release run without
logging has now been explicitly accepted by the owner for M2. This does not
establish the cause of the earlier failure. M3–M8 remain open.
No speculative cleanup fix, blanket close, taskbar concealment,
coordinator bypass or exception suppression has been introduced. No commits,
pushes or merges were made.
