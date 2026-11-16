# G2-02 – Final Acceptance

**G2-02 COMPLETE – READY FOR LOCAL COMMIT**

Eigentümerabnahme: **12.11.2026**, ausdrücklich erteilt durch den Projekteigentümer im Auftrag **G2-02-F1 – Final Acceptance**. Dieser Bericht führt G2-02, G2-02-R1 und G2-02-R2 zusammen und ersetzt deren vorläufigen Status „READY FOR VISUAL REVIEW / Eigentümerabnahme offen“. Die älteren Berichte bleiben als historische Prüfprotokolle unverändert erhalten.

## Arbeitsstand

- Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench\artifacts\worktrees\gui-g1`
- Branch: `feature/gui-g1`
- HEAD: `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`
- Hauptcheckout: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench`, Branch `master`, HEAD `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`; sauber und unverändert.
- Kein Commit, Push oder Merge. F1 ändert ausschliesslich diesen Abschlussbericht und legt lokale Prüfprotokolle unter `artifacts/g2-02-f1/` ab. Keine neuen funktionalen Änderungen und keine erneute Testausführung.

## Manuelle Eigentümerabnahme

Quelle dieser Ergebnisse ist die ausdrückliche Bestätigung des Projekteigentümers vom 12.11.2026, nicht eine neue Computer-Use-Prüfung durch Codex.

| Nr. | Manuelle Windows-Prüfung | Ergebnis |
| --- | --- | --- |
| 1 | Native Titelleisten | PASS |
| 2 | Unabhängiges Minimieren | PASS |
| 3 | Unabhängiges Maximieren | PASS |
| 4 | Hauptfenster minimieren | PASS |
| 5 | Zurückdocken | PASS |
| 6 | Anwendung schliessen | PASS |

Das bestätigte Zurückdocken erfüllt den allgemeinen Abnahmepunkt. Es erklärt den ausdrücklich separat offenen speziellen Cross-Window-Mouse-Redock-Fall nicht für behoben. Aus den sechs bestätigten Punkten werden keine zusätzlichen Tests, DPI-Konfigurationen oder separate Taskleisten-Klickprüfungen abgeleitet; die Nachweisgrenzen der historischen Protokolle bleiben nachvollziehbar.

## Technische Nachweise – vorhandene Resultate geprüft

Die Einzelresultate der folgenden TRX-Dateien wurden erneut eingelesen und nach `outcome` gezählt. Die native JSON-Datei wurde auf 49 einzelne `passed=true`-Checks, `failed=0` und fehlende Fehlerobjekte geprüft. Die Zahlen wurden nicht nur aus den vorangegangenen Berichten übernommen.

| Prüfung | Debug | Release | Zusätzlich nicht ausgeführt, je Konfiguration |
| --- | ---: | ---: | ---: |
| GUI | **1.124 PASS**, 0 Fehler | **1.124 PASS**, 0 Fehler | 0 |
| MCP | **176 PASS**, 0 Fehler | **176 PASS**, 0 Fehler | 13 NotExecuted |
| Core | **11.512 PASS**, 0 Fehler | **11.512 PASS**, 0 Fehler | 27 NotExecuted |
| Native Neustartprüfung | **49/49 PASS**, 0 Fehler | kein separater Release-Nativlauf behauptet | — |

Alle verwendeten Abschlussläufe stammen vom 12.11.2026. Nachweispfade relativ zum GUI-Worktree:

| Prüfung | Nachweis |
| --- | --- |
| GUI Debug, finaler Stand | `artifacts/g2-02-r2/final-Debug/gui.trx` |
| GUI Release, finaler erfolgreicher Wiederholungslauf | `artifacts/g2-02-r2/final-r2-Release/gui.trx` |
| MCP Debug | `artifacts/g2-02-r2/validation-final/CryptoWorkBench.Mcp.Tests-Debug.trx` |
| MCP Release | `artifacts/g2-02-r2/validation-final/CryptoWorkBench.Mcp.Tests-Release.trx` |
| Core Debug | `artifacts/g2-02-r2/validation-final/CryptoScriptUnitTest-Debug.trx` |
| Core Release | `artifacts/g2-02-r2/validation-final/CryptoScriptUnitTest-Release.trx` |
| Native Neustart | `artifacts/g2-02-r2/restart-final.json` |
| Finale Builds | `artifacts/g2-02-r2/final-build-Debug.log`, `artifacts/g2-02-r2/final-build-Release.log` |

Beide Buildlogs bestätigen erfolgreiche Builds. GUI wurde nach der abschliessenden R2-Fenstertitelkorrektur vollständig geprüft; die MCP-/Core-Nachweise stammen aus dem davor abgeschlossenen R2-Gesamtlauf. Diese spätere Korrektur änderte weder MCP noch Core. F1 hat keine neuen Tests durchgeführt. Nicht ausgeführte Fälle werden ausdrücklich nicht als PASS gewertet.

Die GUI-Abschlussläufe verwendeten `NUnit.NumberOfTestWorkers=0`. Fehlgeschlagene Zwischenläufe bleiben erhalten, insbesondere `artifacts/g2-02-r2/validation/CryptoWorkBenchGuiTest-Debug.trx` und `artifacts/g2-02-r2/final-Release/gui.trx`: jeweils ein intermittierender Headless-Initialisierungsfehler vor dem Testkörper. Ein erfolgreicher Wiederholungslauf ist kein Nachweis, dass dieser Restpunkt behoben wurde.

Die F1-Nachweisprüfung einschliesslich SHA-256 der sechs TRX-Dateien und der nativen JSON-Datei ist unter `artifacts/g2-02-f1/evidence-check.json` und `native-check.json` festgehalten.

## Zusammenfassung der G2-02-Änderungen

- Standardlayout: Editor links oben (70 %), Variables/Info rechts oben (30 %), Output/Diagnostics unten (25 % der Gesamthöhe); aktive Standardtabs Variables und Output. Splitter, Rückkehranker und Reset bleiben verfügbar.
- Einheitliche obere Tab-Leisten, keine redundanten Tool-Header; Gruppen-Drag über freie Tabfläche und bestehende Dock-Befehle bleiben erhalten.
- Zentrale Statusleiste für Ausführungsstatus, Layoutfeedback und Zeile/Spalte. Der Dock-Editor blendet seine zusätzliche Statusleiste aus. Die klassische GUI bleibt unverändert; der Legacy-Status-Dock bleibt für gespeicherte Altlayouts verfügbar.
- Kompakter Output mit vollständigem selektierbarem Text, horizontalem/vertikalem Scrollen und unveränderter Quellennavigation. Variablenübersicht kürzt lange Werte; Details zeigen weiterhin vollständige Werte und verschwinden ohne Auswahl.
- Floating Hosts: native Windows-Titelleisten, keine native Owner-Zuordnung, eigene Taskbar-Präsentation und unabhängige WindowStates. Fenstertitel bleiben nach Restore verständlich. Fensterbewegung über OS-Titelleiste, Docking über Dock-Tabs; gemeinsamer Shutdown über die bestehende Shell-Bereinigung.
- Layoutschema, Editor-/Document-/Undo-Identität und MCP-Koordination bleiben erhalten. Keine Multi-Dokument-Erweiterung, keine neue Sprachkernfunktion und keine neue MCP-Schnittstelle.
- Regressionstests und native Proben für Standardlayout, kompakte Darstellung, Tab-Drag/Admission, Fensterunabhängigkeit, Wiederherstellung, Reset und Shutdown ergänzt bzw. an die geänderten Anforderungen angepasst.

## Vollständiges Datei-Inventar G2-02 einschliesslich R1, R2 und F1

Vergleichsbasis ist der vor G2-02 gesicherte G1-Arbeitsstand, **nicht** der bereits damals stark abweichende Git-HEAD. Shell, GUI-Tests und Editor-View wurden mit `artifacts/g2-02/baseline/gui.zip` verglichen; neue Dateien und die Änderungen am nativen Launcher bzw. die Berichte wurden mit den Teilberichten und den R1/R2-Sicherungen abgeglichen. Das Inventar enthält **40 Pfade: 29 geändert, 10 neu, 1 entfernt**, einschliesslich dieses Berichts. „Neu“ bezieht sich auf die G2-Baseline; viele schon vor G2 vorhandene Shell-Dateien sind gegenüber Git ebenfalls untracked.
| Datei | G2-Änderung |
| --- | --- |
| `CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs` | entfernt |
| `CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockOutputView.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellStyles.axaml` | neu |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | geändert |
| `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs` | geändert |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml` | geändert |
| `CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockIndependentWindowNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockIndependentWindowTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockLayoutTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockMouseRedockTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockNativeChromeTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockShellTests.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockStandardLayoutNativeProbe.cs` | neu |
| `CryptoWorkBenchGuiTest/DockStandardLayoutTests.cs` | neu |
| `CryptoWorkBenchGuiTest/DockToolNativeProbe.cs` | geändert |
| `CryptoWorkBenchGuiTest/DockVisualCorrectionTests.cs` | neu |
| `docs/gui/G2-02-final-acceptance.md` | neu |
| `docs/gui/G2-02-R1-visual-corrections.md` | neu |
| `docs/gui/G2-02-R2-floating-windows.md` | neu |
| `docs/gui/G2-02-standard-layout.md` | neu |
| `eng/gui/native/Program.cs` | geändert |

Maschinenlesbare Liste: `artifacts/g2-02-f1/baseline-delta.json`. Der Vergleich bestätigt ausserdem, dass `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` gegenüber der G2-Ausgangssicherung unverändert ist.

Lokale Nachweise liegen zusätzlich in `artifacts/g2-02/`, `artifacts/g2-02-r1/`, `artifacts/g2-02-r2/` und `artifacts/g2-02-f1/`: Baselines, Build-/Testlogs, TRX, native JSON-Journale und Screenshots. Sie sind Prüfartefakte, keine weiteren Produktänderungen. Der vollständige aktuelle Git-Status inklusive bereits vor G2 vorhandener Änderungen ist separat im F1-Protokoll enthalten.

## Bekannte, separat offene Restpunkte

1. **Spezieller Cross-Window-Mouse-Redock-Fall:** bleibt offen. Die allgemeine Eigentümerprüfung „Zurückdocken“ ist PASS; daraus folgt keine vollständige Abdeckung aller Sonderfälle zwischen nativen Fenstern.
2. **Intermittierender Headless-Teststartfehler:** `Dispatcher.VerifyAccess` / `DefaultRenderLoop.Add` bei isolierter App-Initialisierung; auch seriell beobachtet. Erfolgreiche vollständige Wiederholungsläufe sind belegt, die Ursache ist nicht behoben.
3. **Windows-Skalierung 125 %:** noch nicht vollständig verifiziert. Native 150-%-Prüfungen und Tests logischer Clientgrössen ersetzen keine vollständige 125-%-Abnahme.

Diese Punkte bleiben gemäss ausdrücklicher Eigentümerfreigabe ausserhalb des abgeschlossenen G2-02-Abnahmeumfangs. Ausgelassene externe/plattformspezifische Tests behalten ihren ausgewiesenen NotExecuted-Status.

## Abschlusskontrolle

- `git status --short --branch` und vollständiger Status mit `--untracked-files=all` geprüft: Branch `feature/gui-g1`, vorhandene uncommittete G1/G2-Änderungen nachvollziehbar; kein unerwarteter Branchwechsel.
- `git diff --check`: erfolgreich, keine Whitespace-Fehler. Da neue Shell-/Testdateien noch untracked sind, erfasst dieser Git-Befehl nicht deren Inhalt; der zusätzliche Baseline-/Dateihashvergleich dokumentiert den F1-Umfang.
- SHA-256-Vergleich aller **1.159** vor F1 vorhandenen, nicht ignorierten Arbeitsdateien nach Berichtserstellung: **0 verändert, 0 entfernt**. Einzige neue nicht ignorierte Datei ist dieser Abschlussbericht; keine funktionale Änderung.
- Hauptcheckout auf `master`: sauber, HEAD unverändert; keine Arbeiten an anderen Worktrees.
- Keine Dateien gestaged, kein Commit, Push oder Merge ausgeführt.

Prüfprotokolle: `artifacts/g2-02-f1/status-before.txt`, `status-after.txt`, `files-before.json`, `final-check.json` und `diff-check.txt`.

**G2-02 COMPLETE – READY FOR LOCAL COMMIT.** Dieser Status erteilt keine automatische Commit-Freigabe; es wurde nichts committed.
