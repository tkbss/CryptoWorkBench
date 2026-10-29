# A12-01-F1 – ScriptBook Import Cancellation Fix

Stand: 27.10.2026. Ergebnis: **F1 COMPLETE**.

A12-R1-F01 ist korrigiert. Nur eine explizite Yes- oder No-Auswahl setzt den Import fort. Close/X, Escape, Cancel, Owner-Schließen und ungültige Ergebnisse brechen vor dem Zugriff auf den ausgewählten Dateipfad und vor dem Aufruf der Importpipeline ab. Die acht neuen Regressionen bestehen headless und in der nativen Windows-Probe. Der Arbeitsstand ist für einen gezielten unabhängigen F1-Re-Review vorbereitet; dies ist keine Produktfreigabe.

## Ausgangsbasis und Umfang

Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`; Branch `feature/avalonia12-migration`; unveränderter HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

Vor der Änderung wurden der tatsächliche uncommittete Stand, Git-Status, beide Diffs, 773 Quelldateien mit SHA-256 und sämtliche 6.335 vorhandenen Dateien unter A12-01/A12-02/R1-Evidenz aufgenommen. Die Staging Area war leer. Nachweise: [baseline](../../artifacts/a12-01-f1/baseline/).

Der F1-Delta besteht ausschließlich aus:

| Datei | Änderung |
|---|---|
| CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs | Importdialog: Cancel/Escape-Zuordnung und explizite Yes-/No-Freigabe |
| CryptoWorkBenchGuiTest/ScriptBookImportCancellationTests.cs | Acht neue GUI-Integrationsfälle mit tatsächlicher Sidebar, View, AutomationService und MessageBox |
| docs/migration/A12-01-F1-import-cancellation-fix.md | Dieser Bericht |
| artifacts/a12-01-f1/ | Neue Baseline, Rohbelege, externe Probe und Prüfskripte |

Builds und Tests wurden in einer neuen, nicht als Worktree registrierten Kopie unter `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-A12-01-F1-Validation` ausgeführt. `app/` enthält den geprüften Quellstand; der zusätzliche native Probeprozess liegt außerhalb davon unter `probes/native/`. NuGet-Cache und CLI-Home sind dort isoliert. Die Kopie wurde mit den Originaldateien abgeglichen.

Keine anderen Worktrees wurden verändert. `.codex/config.toml` wurde weder gelesen noch verändert. Keine vorhandenen A12-Berichte oder Rohbelege wurden überschrieben. Kein Commit, Push, Merge oder Prism-Upstream-Import.

## Ursache und genaue Korrektur

Die Sidebar setzte bisher nur bei `r == "Yes"` die Variable `clear` auf true. Jedes andere Ergebnis erreichte ebenfalls `OpenScriptBookAsync(..., clear)`. Damit wurde das leere Schließergebnis wie die ausdrückliche Auswahl No behandelt.

Die Ergebnisprüfung in SidebarViewModel.cs:172–174 lautet jetzt:

```csharp
// Closing the dialog (including Escape) is not consent to import.
if (r is not ("Yes" or "No")) return;
clear = r == "Yes";
```

Der Importaufruf bleibt unverändert in Zeile 178. Der frühe Rücksprung liegt bereits vor `result[0].Path.LocalPath`; es wird weder ein Dateipfad für die Importpipeline ausgewertet noch `CryptoScriptEditViewModel.OpenScriptBookAsync` aufgerufen.

Zusätzlich enthält ausschließlich der Importdialog in Zeile 160 einen eigenen Button:

```csharp
new ButtonDefinition { Name = "Cancel", IsCancel = true },
```

Diese kleine sichtbare Erweiterung ist für die geforderte Escape-Semantik notwendig: Im Vorher-Lauf blieb der vorhandene Yes/No-Dialog bei Escape offen. MessageBox leitet die IsCancel-Eigenschaft an den Avalonia-Button weiter. Escape aktiviert nun ausdrücklich Cancel, nicht No. Die zugrunde liegende Avalonia-Semantik ist in der [offiziellen Button-Dokumentation](https://docs.avaloniaui.net/controls/input/buttons/button) beschrieben und wurde mit dem tatsächlich installierten MessageBox-Paket 12.0.0 getestet.

Der bestehende Owner-/ShowWindowDialogAsync-Pfad und der ShowAsync-Fallback bleiben unverändert. Der Save-/Overwrite-Dialog wurde nicht angefasst. Exakter Produktdelta: [f1-product.diff](../../artifacts/a12-01-f1/f1-product.diff).

## Tatsächliche Dialogsemantik

| Aktion/Zustand | Ergebnis |
|---|---|
| Yes | Bisherigen Inhalt leeren und Buch importieren/ausführen. Das bisherige Leeren der Interpretervariablen bleibt ebenfalls erhalten. |
| No | Inhalt und bestehende Variablen erhalten, Buch anhängen und ausführen. |
| Close/X | Leeres Ergebnis; vollständiger Abbruch. |
| Escape | Eigener Cancel-Button wird ausgelöst; vollständiger Abbruch. Auch bei fokussiertem Yes wird kein Yes ausgelöst. |
| Cancel anklicken | Vollständiger Abbruch. |
| Owner schließen | Dialog wird mitgeschlossen; keine nachträgliche Importoperation. |
| Sonstiges Ergebnis, einschließlich null/leer | Durch die explizite Freigabe abgewiesen. |
| Editor bereits leer | Wie bisher kein Bestätigungsdialog; direktes Importieren mit clear=false. |

Der Abbruch verändert auch den bestehenden Status nicht. Die Yes-/No-Ausführungssemantik und der bestehende Dateipicker bleiben erhalten.

## Neue Regressionen und Aussagekraft

`ScriptBookImportCancellationTests.ActualDialogPreservesImportIntent` umfasst acht Fälle: Yes, No, Close, Escape, Owner, EmptyEditor, Cancel und Unknown.

Die Fälle verwenden den öffentlichen `OpenScriptBookCommand` über Prisms `IAsyncCommand` einschließlich GuardAsync. Sie erzeugen echte MessageBox-Fenster und rufen den tatsächlichen Button.OnClick-Pfad inklusive Command auf. Close und Owner-Schließen benutzen die realen Window.Close-Pfade. Headless-Escape wird über KeyPress/KeyRelease geliefert; der native Lauf führt das KeyDown-Ereignis vom fokussierten Yes-Button durch die tatsächliche Ereignisroute und prüft, dass der IsCancel-Handler es behandelt.

Nur der Dateipicker wird durch einen IStorageProvider-/IStorageFile-Proxy ersetzt. Keine Ergebnisstrings werden an einer neuen Produktionsschnittstelle injiziert. Für den zusätzlichen Unknown-Fall wird absichtlich ein ungültiger CommandParameter am tatsächlichen No-Button eingesetzt; dieser Fall ist eine gezielte Fehlerinjektion über den MessageBox-Command, keine normale Benutzeraktion.

Verwendetes harmloses Testbuch:

```text
VAR f1Imported=0x(AB)
```

Bei Yes, No und leerem Editor werden exakter Dokumentinhalt, erfolgreicher Import, tatsächlich erzeugte Variable samt Wert, bestehende Variable und neue Ausführungsidentität geprüft. Damit wird echte Runner-Ausführung positiv belegt.

Bei Abbruch wird eine Kombination von Beobachtungen geprüft:

- Kein Zugriff auf IStorageFile.Path. Die Datei ist zusätzlich mit FileShare.None gegen Lesen gesperrt; ein versehentlicher Leseversuch kann nicht unbemerkt erfolgreich sein.
- Keine neue LastExecutionId nach Drain, unveränderte StateVersion, kein aktiver oder beschäftigter Ausführungszustand.
- Keine importierte Variable, unveränderter vorhandener Variablenbestand, keine neuen Diagnosen und unveränderter Status.
- Keine Document.Changed-Ereignisse, identische Dokumentinstanz und Dokumentversion, unveränderter Text.
- Bei weiter offenem Owner unveränderte Editoridentität und Bridge-Revision. Nach Owner-Schließen wird keine künstliche fortbestehende Registrierung verlangt.
- Beide vorhandenen Undo-/Redo-Richtungen bleiben erhalten und führen anschließend die ursprünglichen Edits aus.

Die Undo-/Redo-Prüfungen verändern das Testdokument erst nach dem nachgewiesenen Abbruch. Native Protokolle unterscheiden deshalb die vorher gezählten Dokumentänderungen ausdrücklich von diesen späteren Testaktionen.

Die Nichtausführung wird nicht allein aus unverändertem Text gefolgert: Der Rücksprung vor der Dateipfadauswertung, unveränderte Registry-/Ausführungsidentität und der unveränderte Interpreterzustand belegen gemeinsam, dass keine Importqueue-Operation und damit kein Parser-/Runnerstart dieser Datei stattfindet. Produktionscode, Core oder Queue benötigen dafür keine zusätzlichen Testhooks.

## Vorher-/Nachher-Nachweis

Die neuen Fälle liefen zuerst gegen die unveränderte Sidebar in der isolierten Kopie:

| Fall | Vorher | Nachher |
|---|---|---|
| Yes | PASS | PASS |
| No | PASS | PASS |
| Leerer Editor | PASS | PASS |
| Close | FAIL: Dateipfad trotz Abbruch ausgewertet | PASS |
| Owner-Schließen | FAIL: Dateipfad trotz Abbruch ausgewertet | PASS |
| Escape | FAIL: Dialogtask blieb offen, Timeout | PASS |
| Cancel | FAIL: der separate Button existierte noch nicht | PASS |
| Unknown | FAIL: ungültiges Ergebnis erreichte Importpfad | PASS |

Die drei Positivfälle belegen die unveränderte Yes-/No-/Leer-Semantik. Der Vorher-Nachweis verwendet für Abbruchfälle gesperrte Dateien; der frühere R1-Nachweis einer tatsächlich ausgeführten Importvariable bleibt zusätzlich unverändert erhalten. Der alte Sidebar-Quelltext wurde aus der unveränderten R1-Testkopie übernommen und gegen den zu F1-Beginn aufgenommenen SHA-256 verifiziert.

Belege: `tests/before-runtime.trx` (3 PASS, 5 FAIL), `tests/after-focused.trx` (8 PASS) und `tests/native.log` (8 PASS, Exit 0). Ein früher Kompilationsversuch des neuen Tests verwendete irrtümlich ExecuteAsync auf dem konkreten AsyncDelegateCommand. Der Fehler und dessen Log `tests/before.log` bleiben erhalten; korrigiert wurde ausschließlich der neue Test auf die vorhandene IAsyncCommand-Schnittstelle. Dieser Kompilationsfehler zählt nicht als Produktregression.

## Ausgeführte Validierung

Umgebung: Windows 10.0.26200, win-x64, .NET SDK 10.0.401, MSBuild 18.9.11, Hostruntime 10.0.12. Ausführungsskript: [run-validation.ps1](../../artifacts/a12-01-f1/run-validation.ps1). Das Skript dokumentiert die erfolgte Ausführung; erneute Läufe müssen ein neues Ausgabeverzeichnis verwenden, damit diese Rohbelege erhalten bleiben.

| Prüfung | Ergebnis |
|---|---|
| Release-Restore und Build der GUI-Solution | Exit 0 |
| Debug-Restore und Build der GUI-Solution | Exit 0 |
| Acht gezielte neue GUI-Fälle | 8/8 PASS |
| Vollständige GUI-Suite Release | 480/480 PASS |
| Separater Coordinator-Lauf | 35/35 PASS |
| Core Release | 7.672 Fälle: 7.665 PASS, 2 FAIL, 5 NotExecuted; Exit 1 |
| MCP Release | 90 Fälle: 89 PASS, 1 NotExecuted; Exit 0 |
| Native Windows-Probe | 8/8 PASS; Exit 0 |
| Debug-/Release-Paketgraph gegenüber R1 | Keine Paket-/Versionsabweichung |

Die 35 Coordinator-Fälle sind bereits in der GUI-Suite enthalten. Die bisherigen 472 GUI-Fälle sind unverändert im Fallbestand und Ergebnis; genau acht neue Fälle kommen hinzu. Kein vorhandener Test wurde bearbeitet.

**Core bleibt ausdrücklich rot:** RejectsInvalidPsnWithoutNormalizing("1") und ("A1") scheitern wie bei R1, weil die Assertions die gesamte Exception.ToString-Ausgabe inklusive Quellpfad durchsuchen. Im neuen Testpfad `CryptoWorkBench-A12-01-F1-Validation` treten beide Suchwerte auf. Quellcode und Ergebnisse sind gegenüber R1 unverändert. Der bereits dokumentierte R1-PathMap-Gegenbeweis wurde nicht als neuer F1-PASS ausgegeben; in F1 wurde weder der Quellpfad maskiert noch eine Assertion geändert.

Die fünf Core-Skips betreffen drei Fälle ohne Symlinkberechtigung, Darwin-ACLs und den ausdrücklich manuellen Hardwaretest. Der MCP-Skip InstalledCodexDiscoversAndCallsStatus benötigt CRYPTOWORKBENCH_CODEX_PATH; er wurde nicht durch Konfigurationsänderungen aktiviert.

Die Auswertung zählt die tatsächlichen UnitTestResult-Einträge. Die TRX-Summary meldet bei den Skips teilweise notExecuted=0, und die Konsolensumme lässt den expliziten Hardwarefall aus; deshalb sind diese summarischen Felder hier nicht die Zählgrundlage. [summary.json](../../artifacts/a12-01-f1/tests/summary.json) enthält die Einzelbefunde.

Der Vergleich mit R1 zeigt identische Core-/Coordinator-Fallnamen und Outcomes. Bei MCP enthalten sechs parameterisierte Anzeigenamen aktuelle UTC-Zeitstempel; nach ausschließlicher Normalisierung dieser Zeitstempel bleiben keine Unterschiede. Rohvergleich und Normalisierung liegen getrennt unter `tests/baseline-comparison.json` und `tests/mcp-name-normalization.json`.

Die native Probe verwendet UsePlatformDetect, tatsächliche Windows-Fenster mit nativem Handle und eine laufende Desktop-Message-Loop. Sie startet aus einem leeren Working Directory. Geprüft werden die reale Sidebar und ihre View-/Service-Kette; es ist kein erneuter vollständiger Prism-App-Bootstrap. Button- und Tastatureingaben werden programmatisch ausgelöst, nicht über eine manuelle Maus-/Tastatursitzung. Es wird keine visuelle Designabnahme oder vollständige Betriebssystem-Tastaturmatrix beansprucht. Der native Dateipicker bleibt durch den kontrollierten Provider ersetzt.

Beispiel aus dem nativen Ergebnis:

```text
PASS Close: pathReads=0, imported=False, accepted=False, documentChangesBeforeHistoryCheck=0
PASS Escape: pathReads=0, imported=False, accepted=False, documentChangesBeforeHistoryCheck=0
PASS Owner: pathReads=0, imported=False, accepted=False, documentChangesBeforeHistoryCheck=0
F1 NATIVE COMPLETE: 8/8 scenarios passed
```

## Architekturverträglichkeit und Integrität

[verify-integrity.ps1](../../artifacts/a12-01-f1/verify-integrity.ps1) und [integrity-final.json](../../artifacts/a12-01-f1/integrity-final.json) belegen:

- Von 773 ursprünglich vorhandenen Quelldateien wurde ausschließlich SidebarViewModel.cs geändert; die übrigen 772 Dateien sind bytegleich.
- Damit sind insbesondere EditorCoordinator, EditorBridge, Admission/Leases, Kontrollgeneration, Undo-/Redo-Delegation, UiDispatch, Core/Parser/Runner und sämtliche vorhandenen Tests unverändert.
- Alle 6.335 historischen A12-01-/A12-02-/R1-Evidenzdateien sind hashgleich.
- Die Validierungskopie entspricht dem endgültigen Produkt-/Teststand.
- HEAD und Branch bleiben unverändert; Staging Area bleibt leer.

Neue Tests und Bericht sind gesondert im finalen Quellmanifest enthalten. Der neue Evidenzmanifest sichert den Produktstand und die Reviewunterlagen mit SHA-256, ohne eine externe Signatur zu behaupten.

## Verbleibende Risiken und F1-Re-Review

Linux und macOS bleiben **OPEN**. Die CJK-IME-Risikoakzeptanz bleibt unverändert. Die vorhandenen pfadabhängigen Core-Testfehler und der nicht ausgeführte Codex-Integrationstest sind keine durch F1 gelösten Punkte.

Der bekannte veröffentlichte Prism-Dialogblocker, die isolierten A12-02-Quellbuild-Ergebnisse sowie Lizenz-/Distributionsentscheidung bleiben unverändert getrennt. F1 integriert weder Prism-Upstream noch neue Pakete und erteilt keine Releasefreigabe.

Für den gezielten unabhängigen F1-Re-Review liegen der enge Produktdiff, der hashverifizierte Ausgangstext, die neue Testdatei, Vorher-/Nachher-TRX, die native Probe einschließlich Quelle, vollständige Suite-Ergebnisse und der Integritätsnachweis bereit. Prüfschwerpunkt ist die frühe Ablehnung jedes Nicht-Yes-/Nicht-No-Ergebnisses einschließlich der neuen Escape-Zuordnung. Keine automatische Fortsetzung weiterer Arbeitspakete.

