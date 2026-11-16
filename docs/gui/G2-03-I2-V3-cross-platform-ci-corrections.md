# G2-03-I2-V3 – Cross-Platform CI Diagnosis & Corrections

16.11.2026. **V3 READY FOR OWNER REVIEW**. Korrekturstand im separaten Detached-Worktree `artifacts/worktrees/gui-g2-v3`, Basis d7c59ef69b9bbbd477c5c0e5bdc330d25ec1ff71. Keine Commits, Pushes, PR-Änderungen oder Merges.

## Originalbefunde

PR #7, [CI Attempt 1](https://github.com/tkbss/CryptoWorkBench/actions/runs/43844123051): Windows erfolgreich; Linux GUI 1.122 PASS / 1 Failed / 8 NotExecuted; beide macOS-Architekturen je 1.120 PASS / 3 Failed / 8 NotExecuted. Die Original-TRX/Logs sind im bestehenden Theme-Worktree unter artifacts/g2-03-i2-v2 archiviert und unverändert. Die Fehler sind Assertions im Testkörper, kein Dispatcher-Initialisierungsfehler.

## A – nachgewiesene falsche Dateisperrenannahme

`FailedOrInterruptedWritePreservesLastValidFile(false)` öffnete die bestehende Layoutdatei mit FileAccess.Read/FileShare.Read und verlangte danach Save=false. Windows verweigert den Ersatz bei dieser fehlenden Delete-Freigabe. Unter Unix verhindert ein offener Leser nicht die Ersetzung des Pfades: der offene Handle kann weiterhin die alte Datei lesen, während neue Leser die neue Datei erhalten. Die Original-CI belegt Save=true unter Linux und beiden macOS-Architekturen.

Die [.NET-10-Unix-Implementierung von File.Replace](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs) prüft die Pfade und verwendet den Unix-Rename-Pfad. Eine FileShare.Read-Öffnung ist deshalb kein plattformübergreifender Ersatzfehlergenerator.

`DockLayoutStore.Save()` validiert zuerst JSON, schreibt eine eindeutige temporäre Datei im selben Verzeichnis, flusht deren Inhalt, validiert erneut und ruft dann File.Replace beziehungsweise bei fehlendem Ziel File.Move auf. Es gibt keinen Delete-first-Fallback. Recoverable Exceptions ergeben false und LastError; finally entfernt die temporäre Datei. Der Vertrag schützt die letzte gültige Datei bei einem fehlgeschlagenen Ersatz, verlangt aber nicht, dass ein offener Leser auf allen Plattformen einen Fehler auslöst. Keine Zusage allgemeiner Stromausfall-/Verzeichnis-fsync-Dauerhaftigkeit oder konkurrierender Writer-Serialisierung wird daraus abgeleitet.

### Korrektur in DockLayoutTests.cs

- Der Fall true mit IOException über den vorhandenen BeforeReplace-Testhook bleibt unverändert.
- Der Fall false prüft im selben Hook, dass der vorbereitete Temp-Inhalt exakt dem neuen Layout entspricht, und entfernt ausschliesslich diese eindeutige Test-Tempdatei. Anschliessend scheitert der echte File.Replace-Aufruf an der fehlenden Quelle. Save=false, Fehlerdiagnose, byteidentische alte Layoutdatei und fehlende Temp-Reste werden geprüft. Keine Assertion ersatzlos entfernt.
- Neuer Test `OpenReaderUsesPlatformReplacementSemantics` erhält zusätzlich den ursprünglichen echten FileShare-Test: unter Windows Fehler und alter Zielinhalt; unter Unix Erfolg und neuer Zielinhalt. Der offene Leser muss weiterhin den alten Inhalt liefern. Nach Schliessen des Lesers muss Speichern erfolgreich sein; Inhalt, Laden und Temp-Cleanup werden geprüft. Kein Skip eingeführt.

Damit werden Fehlererhaltung und Plattformsemantik getrennt geprüft, statt einen zulässigen Unix-Erfolg als Datenverlustfehler zu behandeln. Die Unix-Verzweigung ist lokal noch nicht ausgeführt und muss in der nächsten CI bestätigt werden.

## B – nachgewiesene falsche AvaloniaEdit-Tastengeste

Beide `CopyExampleCopiesWholeRenderedCodeWhileCtrlCCopiesOnlySelection(false/true)`-Fälle senden bisher unverändert Control+C. Das eingebettete CodePad verwendet einen AvaloniaEdit-TextEditor. Die Paketmetadaten von Avalonia.AvaloniaEdit 12.0.0 nennen Commit 86fdebec4cff7affb0e14c7885df71d28edce777. Dessen [ApplicationCommands-Definition](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/AvaloniaEditCommands.cs) definiert Copy mit Meta unter OSX und Control sonst. Damit ist die alte Simulation auf macOS nachweislich nicht die definierte Copy-Geste. Die CI meldete nach Auswahl statt „VAR hex“ einen leeren Clipboard-Text.

### Korrektur in DockToolTests.cs

Der Test liest `ApplicationCommands.Copy.Gesture`, übersetzt Control/Meta/Shift/Alt explizit in RawInputModifiers und sendet echten KeyPress/KeyRelease an das Fenster. Der Fokus auf das TextArea wird zusätzlich assertiert. Kein direkter Command-Aufruf als Abkürzung, keine Veränderung des Clipboards zum erwarteten Ergebnis und keine Produktänderung.

Die vorhandenen Assertions bleiben erhalten: normaler Copy kopiert genau die sieben ausgewählten Zeichen; Copy Example kopiert anschliessend den vollständigen Unicode-/mehrzeiligen Beispieltext; Script-Snapshot und Interpreterrevision bleiben unverändert. Beide Parameterwerte prüfen docked und floating. Windows/Linux erhalten weiterhin die Control-Geste, macOS die Meta-Geste. Der historische Testname bleibt zur Nachverfolgung der CI-Fälle unverändert.

## Audit weiterer Tests

Repositorysuche nach Control-/Copy-Simulation und FileShare-Annahmen:

- EditorCoordinatorTests verwendet bereits die tatsächliche ApplicationCommands-Geste samt Modifierabbildung; dies dient auch als vorhandenes Fixture-Muster.
- DockDiagnosticsTests, DockOutputTests und die anderen Copy-Tests in DockToolTests zielen auf SelectableTextBlock/Variablentext beziehungsweise Markdown-Prosa, nicht auf den hier betroffenen AvaloniaEdit-Codeeditor. Sie bestanden im ursprünglichen macOS-CI-Lauf. Keine pauschale Umstellung aller Controls auf Meta vorgenommen.
- DockPartialFailureTests grenzt Mandatory-File-Lock-Semantik bereits ausdrücklich auf Windows ein. Dieser bestehende Scope wurde nicht erweitert oder durch neue Skips verändert.
- Native Trace-Probes verwenden ihre spezifischen Datei-/Trace-Verträge und gehören nicht zu den fehlgeschlagenen headless Cross-Platform-Fällen. Keine spekulative Änderung.

## Lokale Validierung

Windows x64, .NET SDK 10.0.401, offizieller Prism-Pin 358118cd640d9a22ff8cf21c8ad197fa038b7990. Keine Paket-Upgrades. Locked Restore/Build beider Lösungen je Konfiguration. Zuerst vollständige Klassen DockLayoutTests und DockToolTests, danach vollständige GUI-Suite, seriell mit NUnit.NumberOfTestWorkers=0.

Nachweise in diesem Worktree: `artifacts/g2-03-i2-v3/validate.ps1`, `build-Debug-final.log`, `build-Release-final.log`, `targeted-Debug.trx`, `targeted-Release.trx`, `gui-Debug.trx`, `gui-Release.trx` samt Logs. Die neue Plattformsemantik-Prüfung erhöht die GUI-Anzahl von 1.131 auf 1.132. Keine Tests deaktiviert. Vorhandene Compilerwarnungen und erwartete Dock-Trace-Lockdiagnosen werden nicht als neue Fehlerbehebung ausgegeben.

Linux und macOS x64/ARM64 lokal **NICHT VERIFIZIERT**: WSL-Abfrage meldet nicht installiert; kein Docker-Befehl verfügbar und keine lokale macOS-Ausführungsumgebung. Kein Betriebssystem emuliert, keine CI ausgelöst. Quellcodebeleg der Plattformgeste ersetzt keinen ausgeführten macOS-Test. Keine erneute Core-/MCP-Regression, da ausschliesslich GUI-Tests geändert wurden.

Abgeschlossene Ergebnisse (jeweils erster Korrekturlauf, keine Wiederholung nötig):

| Prüfung | Debug | Release |
|---|---:|---:|
| Locked Restore + Builds beider Lösungen | PASS | PASS |
| Gezielt: DockLayoutTests + DockToolTests | 68 PASS / 0 Failed / 0 NotExecuted | 68 PASS / 0 Failed / 0 NotExecuted |
| Vollständige GUI | 1.132 PASS / 0 Failed / 0 NotExecuted | 1.132 PASS / 0 Failed / 0 NotExecuted |

Maschinenlesbare Zusammenfassung: `artifacts/g2-03-i2-v3/test-summary.json`. Kein neuer Headless-Initialisierungsfehler in diesen Läufen. `git diff --check` erfolgreich.

## Änderungen, Isolation und Empfehlung

Genau zwei Testdateien geändert: `CryptoWorkBenchGuiTest/DockLayoutTests.cs` und `CryptoWorkBenchGuiTest/DockToolTests.cs`; zusätzlich dieser Bericht. Kein Produktcode, Core, MCP, Theme-Ressourcen oder Paketdateien geändert. Der bestehende Unterbrechungstest bleibt erhalten, der zusätzliche Test verbessert die Prüfung des echten Speichervertrags.

Lokaler master und integration/g2-predecessors bleiben d7c59ef; Remote-Integrationsbranch ebenfalls d7c59ef, Remote-master fbbadae. Theme-HEAD ed6b8d18 bleibt unverändert; dessen drei vorhandene unversionierte Preflight-/CI-Berichte bleiben unangetastet. Der Korrektur-Worktree hat Detached HEAD auf d7c59ef; keine Branchverschiebung und kein neuer Commit. Index bleibt leer.

Nach Eigentümerreview separat Commit-/Integrations-/Push-Freigabe für genau diese Korrekturen einholen. Erst dann PR #7 aktualisieren und einen neuen vollständigen Vier-Plattform-CI-Lauf auswerten. Den ursprünglichen roten Attempt unverändert erhalten. Erwartete GUI-Fallzahl danach 1.132 auf jeder Plattform, bestehende plattformbedingte NotExecuted-Fälle weiterhin separat. Keine neue Merge-Freigabe ableiten.

Risiken: neue Unix-Semantikprüfung noch nicht ausgeführt; korrigierte macOS-Geste noch nicht nativ/CI bestätigt. Der bekannte intermittierende Headless-Initialisierungsfehler ist nicht behoben. Bestehende DPI-, OS-Theme-, Dialog-/Popup-/High-Contrast- und Docking-Restpunkte bleiben unberührt.
