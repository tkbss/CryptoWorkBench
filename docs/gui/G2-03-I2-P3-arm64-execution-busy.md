# G2-03-I2-P3 – ARM64 ExecutionBusy Diagnosis & Conditional Correction

16.11.2026. **G2-03-I2-P3 READY FOR OWNER REVIEW**.

Nachgewiesener Test-Lifecycle-Fehler im neuen Theme-Test: Der Headless-Dispatch liefert einen verschachtelten Task, dessen innerer asynchroner Ablauf vom zurückgegebenen NUnit-Testtask nicht abgewartet wird. Eine kleine Testkorrektur und zwei deterministische Regressionen sind lokal umgesetzt. Keine Änderung an Core-Synchronisationsregeln, Produktcode oder PR #8. Windows Debug/Release vollständig erfolgreich; neue ARM64-CI noch ausstehend.

## Isolationsbasis

Separater Detached-Worktree `artifacts/worktrees/gui-g2-p3` auf dem tatsächlichen GitHub-PR-Test-Merge `495030237476da11f1907b1d5009383abfcec572`, Tree `b9d7235a187370f2e4e2db9b28726b5ea1fc31c1`. Parents: master `b2be1cc7591a5a8bf65f9512f300d1e31fb66714` und PR-Head `1a41b101fca2949231cea911d1dc2eb1b31ff934`. Dieser bestehende GitHub-Commit wurde nur abgerufen und ausgecheckt; kein eigener Merge/Commit erzeugt. Damit enthält die Diagnose sowohl die Theme- als auch die bereits integrierten V3-Testkorrekturen.

master bleibt b2be1cc, feature/gui-g2-03 bleibt 1a41b101. Der abgenommene Theme-Commit ed6b8d18ede9334db79584cb8e016455dcdd5e8e und sein Dokumentationsnachfolger sind unverändert. PR #8 ist weiterhin offen, ungemergt und auf demselben Head. Index leer; ausschliesslich Änderungen im Korrektur-Worktree.

## Originalbefund und CI-Vergleich

[Theme-PR-Lauf 43866769518](https://github.com/tkbss/CryptoWorkBench/actions/runs/43866769518), macOS ARM64: 1.135 GUI Passed / 1 Failed / 8 NotExecuted. Fehler: `VariableBooleanDraftTests.InvalidBooleanRestoresOnlyItsRowAndPreservesTheOtherEdit`, `ExecutionBusyException` in InterpreterExecution.VerifyAccess():120, aufgerufen bei `dictionary.GetVariables().ToArray()` in Testzeile 27. Noch keine Bearbeitungsassertion ausgeführt.

Die originalen TRX wurden erneut gelesen und zeitlich verglichen:

- Theme-Test `SwitchingPreservesSessionAndFlowsToExistingAndNewFloatingWindows`: gemeldeter Start 12:34:41.118133 UTC, Ende 12:34:41.177376 UTC, Passed.
- Unmittelbar folgender VariableBoolean-Test: Start 12:34:41.177566 UTC, Ende 12:34:41.194525 UTC, Failed.
- Im erfolgreichen [Master-Lauf 43853699074](https://github.com/tkbss/CryptoWorkBench/actions/runs/43853699074) fehlt der neue Theme-Test; vor VariableBoolean läuft ein synchroner Diagnostics-Test. VariableBoolean: 11:07:21.113827–11:07:21.184900 UTC, Passed.

Die Reihenfolge ist nur unterstützende Evidenz. Der Ursachenbeleg besteht zusätzlich aus der konkreten Overload-Auflösung und der unten beschriebenen deterministischen Reproduktion. Die historischen Logs enthalten keine Owner-ID; der konkrete ursprüngliche Worker ist deshalb nicht nachträglich identifizierbar. Keine Behauptung, alle denkbaren konkurrierenden Besitzer ausgeschlossen zu haben.

## Ownership- und Lifecycle-Vertrag

`InterpreterExecution.RunAsync` setzt einen prozessweiten Owner und löscht ihn erst im finally. `AccessState` erteilt einem passenden asynchronen Scope kurzzeitig threadgebundenen StateAccess, niemals über await hinweg. `VerifyAccess` verweigert Zugriffe aus Host-Callbacks oder bei einem fremden aktiven Owner ohne passende synchrone/stateAccess-Berechtigung. Das ist korrekt und bleibt unverändert.

`VariableDictionary` ist ein globales Singleton. GetVariables prüft VerifyAccess und liefert die Values-Sicht; ToArray liest diese unmittelbar. Der fehlschlagende Test liest ausserhalb einer eigenen Interpreter-Lease und setzt daher zu Recht voraus, dass der vorherige Test vollständig beendet ist. Eine nicht abgewartete Vorgängeroperation verletzt diese Testisolation. Pauschales Zurücksetzen, Schlucken der Ausnahme oder Aufweichen des Zugriffsschutzes wäre falsch.

`B1EditorIntegrationTests.Host.DisposeAsync` schliesst sein Fenster und wartet Service.DisposeAsync ab. Dieser startet Shutdown, wartet DrainForExitAsync einschliesslich ausstehender Servicearbeit ab und entfernt die Editorregistrierung. Der B1-Ui-Wrapper verwendet bereits ein async Lambda mit `return true`, somit den richtigen Dispatch-Overload. Textinput/Undo und EditorBridge ändern Dokument/Revision synchron über den EditorCoordinator; im untersuchten B1-Host wird dadurch keine Script-Ausführung registriert. Kein nachgewiesener Fehler in diesem Host oder in Core.

Der Theme-Test verwendet dagegen DockToolTests.Host, führt ParseLineAsync aus und besitzt ein await-using sowie ein finally zum Entfernen der Styles/Ressourcen und Wiederherstellen der Theme-Variante. Dessen Host wartet Fensterabschluss und Service.DisposeAsync ebenfalls korrekt ab — **aber nur, wenn der umgebende Testtask bis zum Ende awaited wird**. Die gemeinsame Headless-Session erzeugt normalerweise pro Dispatch eine isolierte App; das ersetzt nicht das Abwarten eines versehentlich als Ergebnis zurückgegebenen inneren Tasks.

## Nachgewiesene Overload-Ursache

Die Paketmetadaten pinnen Avalonia.Headless 12.1.4 auf Commit 2d79ba55fae1b1e71c69f93a668080c9029e6a31. XML-Dokumentation des installierten Pakets und [versionsgenaue Upstream-Quelle](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs) bestätigen drei Overloads: Action, Func<T>, Func<Task<T>>. Ein eigener Func<Task>-Overload fehlt. Ein async Lambda ohne Ergebnis bindet hier an Func<T> mit T=Task. Dispatch liefert folglich Task<Task>; die synchrone Ergebnisvariante wartet nicht den inneren Task ab. Die Session kann App/Dispatcher bereits abbauen, während Async-Fortsetzungen weiterarbeiten.

Genau dieses Muster stand im Theme-Test. Dessen Signatur `Task` verwirft beim Hochcast den inneren Task. NUnit sieht nur den äusseren Abschluss. Das erklärt sowohl vorzeitiges Passed trotz noch ausstehender Assertions/Cleanup als auch einen aktiven Interpreter beim Nachfolgetest. Dies ist kein ARM64-spezifischer Core-Fehler; das Timing macht den plattformneutralen Testdefekt auf ARM64 sichtbar. Das frühere Passed dieses Theme-Testfalls darf nicht als verlässlicher Nachweis seines gesamten Async-Ablaufs gelten.

## Minimale Korrektur und deterministische Regression

Geänderte/ergänzte Testdateien:

- `CryptoWorkBenchGuiTest/GuiTestSession.cs`: RunAsync(Func<Task>) adaptiert mit await und `return true` ausdrücklich auf den asynchronen generischen Headless-Overload. App, Ausnahmen und Cleanup bleiben an denselben Testtask gebunden.
- `CryptoWorkBenchGuiTest/ThemeTests.cs`: nur die Dispatch-Hülle auf diesen Helfer umgestellt. Alle bisherigen Assertions, Host-Disposal- und Theme-finally-Anweisungen unverändert.
- `CryptoWorkBenchGuiTest/HeadlessAsyncLifetimeTests.cs`: zwei deterministische Regressionen mit TaskCompletionSource-Gates.

1. **Negative Kontrolle:** Explizite Zuweisung an Task<Task> beweist bereits beim Kompilieren die ursprüngliche Overload-Auswahl. Eine echte InterpreterExecution.RunAsync-Operation hält Ownership hinter einem Gate. Der äussere Dispatch ist fertig, der innere Task nicht; GetVariables wirft dieselbe ExecutionBusyException. Anschliessend wird das Gate in finally freigegeben und der innere Task ausdrücklich abgewartet. Der Owner ist danach frei. Kein globaler Reset, keine Produktinstrumentierung.
2. **Korrigierter Ablauf:** Der neue Helfer bleibt während gegateter Interpreterarbeit und danach während eines separat gegateten IAsyncDisposable-Cleanups unvollständig. Eine nach dem await ausgelöste definierte Ausnahme erreicht den Aufrufer erst nach Dispose. Danach ist InterpreterExecution.IsBusy=false. Damit werden auch späte Assertion-/Testfehler nicht mehr als Erfolg versteckt.

Keine Sleeps oder Retry-Schleifen. Die 10-Sekunden-WaitAsync-Grenzen begrenzen nur fehlerhafte Testhänger; Gates, nicht Zeitablauf, bestimmen die Reihenfolge. Die erwartete ExecutionBusyException wird nur in der negativen Kontrollassertion geprüft, nicht im Produkt oder fehlschlagenden Variablentest verschluckt. Keine Tests deaktiviert.

## Prüfung weiterer Dispatch-Stellen

Der B1-Wrapper und mehrere andere vorhandene Testwrapper verwenden bereits den korrekten bool-Rückgabewert. Zusätzlich wurden ältere gleichartige Task-ohne-Ergebnis-Stellen in DockIndependentWindowTests (zwei Methoden), DockStandardLayoutTests.TabsAndCentralStatusRemainVisible und DockVisualCorrectionTests.Run gefunden. Sie sind nicht als konkrete Besitzer des ursprünglichen Fehlers belegt. Sie bleiben in dieser eng auf den neuen Theme-Test begrenzten Korrektur unverändert und sollten separat auf denselben Helfer migriert und validiert werden. Ein grüner Gesamtzähler allein beseitigt dieses ältere Evidenzrisiko nicht.

## Lokale Validierung

Windows x64, .NET SDK 10.0.401, gepinnte Prism-Quellen 358118cd640d9a22ff8cf21c8ad197fa038b7990. Locked Restore und Builds beider Lösungen in Debug und Release erfolgreich. Der initiale Abhängigkeitsdownload war im Sandbox-Netz blockiert; die neue unvollständige lokale Prism-Kopie wurde am unveränderten Pin fertiggestellt. Keine Paket-Upgrades oder Änderungen an fremden Checkouts. Debug-Testprojekt nach der Testkorrektur zusätzlich gebaut.

| Prüfung | Debug Passed / Failed / NotExecuted | Release Passed / Failed / NotExecuted |
|---|---:|---:|
| Gezielt: beide Lifecycle-Regressionen, 12 Theme-Fälle, VariableBoolean | 15 / 0 / 0 | 15 / 0 / 0 |
| Vollständige GUI | 1146 / 0 / 0 | 1146 / 0 / 0 |

Alle vier Testläufe im ersten Anlauf erfolgreich; keine verdeckten Wiederholungen. Erwartete neue Gesamtzahl 1.144 + 2 = 1.146. Bestehende Compilerwarnungen und erwartete Dock-Trace-Dateisperrdiagnosen sind in Logs erhalten. Kein neuer Headless-Initialisierungsfehler beobachtet. git diff --check erfolgreich.

Nachweise im Korrektur-Worktree unter `artifacts/g2-03-i2-p3/`: build-Debug.log, test-build-Debug.log, build-Release.log, targeted-Debug/Release.trx samt Logs, gui-Debug/Release.trx samt Logs, test-summary.json und ci-comparison.json mit Original-TRX-SHA-256 und Zeitstempeln. Die früheren CI-Artefakte im Theme-Worktree wurden nur gelesen.

**Nicht ausgeführt:** Linux und macOS x64/ARM64 mit korrigiertem Stand. WSL ist nicht installiert, Docker nicht verfügbar, keine lokale macOS-Umgebung. Keine neue GitHub-CI ausgelöst. Keine erneuten Core-/MCP-Tests, da nur GUI-Testcode geändert wurde; Core/MCP wurden als Teil der Lösungen gebaut. Der Produktions-Ownership-Vertrag blieb unangetastet.

## Risiken und Empfehlung

Die deterministische Reproduktion belegt den fehlerhaften Task-/Ownership-Mechanismus unabhängig vom CI-Timing. Sie ersetzt keine korrigierte ARM64-CI und behauptet keinen nachträglichen Thread-Dump des Originalfehlers. Nach Eigentümerreview separaten Commit-/Push-Auftrag für diese Testkorrektur einholen und PR #8 erneut auf allen vier Plattformen validieren, mit tatsächlich awaited Theme-Test und den zwei Lifecycle-Regressionen. Originalen roten Lauf unverändert erhalten; keinen blinden Rerun als Ursachenbehebung ausgeben.

Die zusätzlichen älteren Dispatch-Stellen bleiben ein ausdrücklich dokumentierter Folgepunkt. Bekannte Restpunkte (100 %/125 % native DPI, tatsächlicher OS-Theme-Wechsel, vollständige Dialog-/Popup-/High-Contrast-Prüfung und intermittierender Headless-Initialisierungsfehler) bleiben offen. Keine neue visuelle Abnahme behauptet.

Keine Commits, Pushes, PR-Änderungen oder Merges. PR #8 bleibt bis zur gesonderten Freigabe unverändert.
