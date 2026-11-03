# MCP-B1-N1-R – Final native acceptance addendum

**NATIVE ACCEPTANCE PASS** · 2 November 2026

Both outstanding native checks passed on the Release candidate that matched the required DLL hashes. The owner confirmed that the editor rejected a real Explorer file drop without changing the document and that the physical Swiss keyboard produced the expected symbols and accented character. No required native check remains open. No blocking defect was confirmed.

## Release identity

Worktree `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3`, branch `feature/mcp-ap3`, HEAD `dfea2055992254462b937477a8697754fe39a4d6`. The hashes below were measured before the new session. Windows opened a fresh `CryptoWorkBenchAvalonia` window, handle `10422806`, for the executable at the following path.

```text
C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp-ap3/CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.exe
```

| File | Measured SHA-256 | Comparison |
|---|---|---|
| `CryptoWorkBenchAvalonia.Desktop.dll` | `2280DB0D6D3BC495BC1890DF3FA5A9C963137BB78A8484BB3B31B1C8E79C2FB9` | Matches the closure brief |
| `CryptoWorkBenchAvalonia.dll` | `D36BA0E5EAE8F86030BCDF66FC47EE0B10007ECBCB28E41AD8830462BA5AD577` | Matches the closure brief |
| `CryptoWorkBenchAvalonia.Desktop.exe` | `C5E414A79FD2B61D46BBD0D0E44004DF772FB1895D0E779B355D4A28C0E3249C` | Matches the preceding native run |

[Closure hash manifest](../../artifacts/b1-n1-r-closure/hashes.csv) · [Final correction and Release identity](MCP-B1-F3-F1-final-correction.md)

## Closure checks

| Check | Verdict | Input and actual observations |
|---|---|---|
| **N12 · Inbound Explorer file drop** | **PASS** | Native automation prepared the unsaved line `B1 baseline` and selected the 28-byte `B1-drop-probe.txt` in Explorer. Using the physical mouse, the owner dragged that file out of Explorer and saw the forbidden cursor over the editor. The owner confirmed that immediately after releasing the button the entire line still read `B1 baseline`. Subsequent typing, Undo and Redo worked in the same document. |
| **N13 · Physical Swiss keyboard** | **PASS** | On the active Swiss layout the owner reported ordinary typing and symbols (`döahfhfä$`), blue selection and working Undo/Redo. Physical AltGr+E produced `€`. The circumflex dead key followed by `e` produced `ê`, with the caret immediately after it. The owner confirmed further letters could be typed. |

N12 setup evidence includes the initial blank document, the text immediately before the handoff and the actual selected file in Explorer. [Blank editor](../../artifacts/b1-n1-r-closure/01-blank.jpg) · [Editor baseline](../../artifacts/b1-n1-r-closure/02-baseline.jpg) · [Explorer selection](../../artifacts/b1-n1-r-closure/03-selected-file.jpg) · [Disposable payload](../../artifacts/b1-n1-r-closure/payload/B1-drop-probe.txt)

The owner supplied the observations of the moving file, the text immediately after release and the physical key sequences. A separate native automated history check in that same window captured the line ending in the owner's `ê` at column 44. Ctrl+Z removed the character and moved the caret to column 43. Ctrl+Y restored it and returned the caret to column 44. [Before Undo](../../artifacts/b1-n1-r-closure/04-owner-composition.jpg) · [After Undo](../../artifacts/b1-n1-r-closure/05-owner-composition-undo.jpg) · [After Redo](../../artifacts/b1-n1-r-closure/06-owner-composition-redo.jpg)

## Prior acceptance and GUI redesign follow-up

The original N01–N14 matrix remains the record of the preceding run. N01–N03, N05–N07, N09–N11 and N14 passed. N04 passed for search and replacement behavior. N08 passed for navigation, dismissal and Return, with a failure for the displayed overload position. N12 had already passed for editor source and move gestures. In particular, rejecting the invalid Boolean preserved the other row's draft, which committed and printed `0x(02)`. Mouse completion inserted the clicked `Hash` and `MacVerify` suggestions. The preceding process exited normally with code 0. [Native acceptance matrix and original evidence](MCP-B1-N1-final-native-acceptance.md)

| GUI redesign follow-up | Recorded effect |
|---|---|
| Overload counter stayed at **1 of 3** as the signature changed. | The displayed signature and description advanced. Dismissal and Return executed the intended line. [Second signature](../../artifacts/b1-n1-final/45-overload-next.jpg) · [Third signature](../../artifacts/b1-n1-final/46-overload-third.jpg) |
| SearchPanel displayed **4 of 4** with the third match selected in one capture. | Find Next subsequently selected the fourth match. Replacement and Undo/Redo worked. [Third match](../../artifacts/b1-n1-final/55-replace-changed.jpg) · [Next match](../../artifacts/b1-n1-final/56-find-next.jpg) |

Both display discrepancies remain nonblocking.

## Owner decisions and repository state

**F2R-10 · KNOWN LOW – DEFERRED.** The internal HistoryEffect receipt may report Applied after rejected completion with initially empty history. Correct or reassess this before a downstream MCP feature relies on that receipt. The owner accepts one Undo transaction for disjoint whole-line Cut (F2R-11), and rejection of unsupported custom protection providers without additional UI feedback (F2R-12). Coordinated Cut/Paste may omit native TextCopied/TextPasted notifications. Dual editor owners and multistage CJK IME composition are unsupported. Interruption by a foreign raw AvaloniaEdit event handler remains an accepted host-boundary risk. [Owner decisions](MCP-B1-F3-owner-decisions.md) · [History acceptance](MCP-AP3-02-N1-R-native-history-acceptance.md) · [Deferred receipt finding](MCP-B1-F3-F1-final-correction.md)

Final Git status contained 34 previously modified tracked paths, 16 previously untracked paths, and this new untracked addendum. The index was empty. The closure evidence and disposable payload are under the ignored `artifacts/b1-n1-r-closure/` directory. This task made no production, permanent test, configuration or prior evidence changes and did not rebuild, stage, commit, push, merge or update master. [Incoming Git status](../../artifacts/b1-n1-r-closure/status-before.txt) · [Final Git status](../../artifacts/b1-n1-r-closure/status-final.txt)

**MCP-B1 READY FOR OWNER ACCEPTANCE**

Both closure checks passed. The project owner can now make the final B1 acceptance decision.
