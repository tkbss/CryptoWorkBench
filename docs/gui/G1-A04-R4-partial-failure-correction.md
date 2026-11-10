# G1-A04-R4 – Targeted Partial Failure Handling

## 1. Auftrag, Freigabe und Entscheidung

Die Eigentümerfreigabe zur Umsetzung wurde im Chat ausdrücklich erteilt: „Ja, R4 zur Umsetzung freigegeben“. Arbeitsstand: 09.11.2026, Windows x64, ausschließlich Worktree artifacts/worktrees/gui-g1, Branch feature/gui-g1, HEAD 7ecac04483c10722c4cb4b459e3f6cb5157c6af1.

**Abschluss: CONDITIONAL GO für A05 nach ausdrücklicher Eigentümerabnahme.**

Beide R3-Risiken sind gezielt korrigiert. Der unveränderte Partial-Failure-Test besteht, die vollständige Debug-/Release-Matrix ist grün, ebenso 62 fachliche native Prozesse und die unabhängigen Wiederholungen. Die Bedingung betrifft die ausdrücklich erforderliche Eigentümerabnahme und die weiterhin abgegrenzten F03-/Maus-/Plattformnachweise, nicht einen verbleibenden roten R4-Solltest. A05 wurde nicht begonnen.

R4 korrigiert die beiden in [R3][r3] belegten Fehlerpfade. Die [R1-Entscheidung][r1] NO REACHABLE B4 FOUND bleibt bestehen. Die sechs mit [R2][r2] entfernten Anforderungen bleiben entfernt. Es gibt keinen neuen Dock-Transaktionsmanager, keinen allgemeinen Reentranzschutz, keinen Dock-Fork und keinen Operations-Retry.

## 2. Ausgangsstand und Integrität

Vor der ersten Änderung wurden 1.138 vorhandene Quell-/Dokumentationsdateien mit SHA-256 inventarisiert und kopiert. Außerdem wurden 12.392 vorhandene Dateien unter den historischen G1-Artefaktverzeichnissen gehasht. Die Sicherung liegt unter [baseline][baseline].

Der Worktree enthielt bereits umfangreiche uncommittete G1-Arbeit. Deshalb wird die R4-Differenz gegen diese Dateisicherung ausgewiesen, zusätzlich zum vollständigen Git-Status gegen HEAD. Der abschließende [Integritätsvergleich][integrity] und [R4-Patch][patch] trennen diese Änderungen: 1.130 der 1.138 Baseline-Dateien unverändert, acht gezielt geändert, fünf neu, keine entfernt. Alle 12.392 historischen Artefakte sind bytegleich; alle früheren Berichte und Locks sind unverändert.

Dock bleibt auf 12.1.0.13, Quellcommit 78da173545501e796f37bc8528e9e55d9cbfd991; Avalonia bleibt auf 12.1.4. Prism bleibt auf dem unveränderten offiziellen Commit 358118cd640d9a22ff8cf21c8ad197fa038b7990; die Build-Vorbereitung prüft dessen 1.180 gepinnte Dateien. Keine Paket-/Lock-Änderung, kein Eingriff in EditorCoordinator, EditorBridge, MCP-Leases oder InterpreterHostQueue.

## 3. P1: Host-Erzeugung und Wahl der Fehlergrenze

Die gepinnte Dock-Implementierung führt weiterhin diese Schritte aus:

1. SplitToWindow bestimmt die Quellwurzel und Optionen.
2. RemoveDockable entfernt die Pane.
3. CreateWindowFrom baut das neue Dock-/Fenstermodell.
4. AddWindow nimmt es in Root.Windows auf.
5. InitDockWindow wertet GetHostWindow beziehungsweise IDockWindowHostFactory.CreateHostWindow aus.
6. Erst danach werden Host, Owner, Factory und das enthaltene Layout initialisiert.

Die maßgeblichen Quellstellen bleiben FactoryBase.cs:811–844, FactoryBase.Window.cs:38–57 und FactoryBase.Init.cs:241–265 im [gepinnten Checkout][docksrc]. Die reale native Fehlerquelle und der genaue Partial-State sind in R3 belegt.

**Vorbereitung geprüft:** InitDockWindow besitzt eine Überladung mit explizitem Host; SplitToWindow besitzt aber keinen Parameter für einen vorab erzeugten Host. Das neue Fenstermodell steht erst nach RemoveDockable zur Verfügung. Auch IDockWindowHostFactory wird erst an der späten Initialisierungsstelle aufgerufen. Eine Reservierung vor Float wäre mit zusätzlicher Zuordnung, Lebensdauerverwaltung und Entsorgung ungenutzter nativer Hosts technisch konstruierbar; sie ist kein bereits unterstützter Vorbereitungsschritt dieser Operation. Das würde außerdem andere spätere Partial-Failure-Quellen nicht abdecken. R4 führt diese zusätzliche Reservierungsverwaltung nicht ein, sondern verwendet die vom Auftrag vorgesehene bestehende A02-Fault-Verwaltung.

Die vorhandene EditorDockFactory.Change-Grenze fängt Exceptions der äußersten Factory-Operation ab, nachdem deren bestehender Observer-/Depth-Scope abgewickelt ist. Sie prüft den Endzustand über DockShellLayout.VerifyFailedDockOperation und wirft mit throw die ursprüngliche Exception weiter. Verschachtelte interne Factory-Schritte prüfen keinen transienten Zwischenzustand.

Korrekturstellen: [EditorDockFactory.Change][factorychange], [VerifyFailedDockOperation][faultverify] und [ValidateCurrentDockModel][modelvalidate].

Die bestehende CompositeDropScope behält ihre synchronen Source-Cleanup-Schritte. Ist darin ein Factory-Schritt fehlgeschlagen, erfolgt die Prüfung erst nach dieser bereits vorhandenen Fortsetzung. Ein zusätzlicher Cleanup-Fehler wird am ursprünglichen Fehler hinterlegt und ersetzt ihn nicht. Das ist keine neue Transaktionsverwaltung und erteilt keiner unabhängigen Operation eine Zulassung.

## 4. Partielle Zustände, Fault und Persistierung

| Endzustand nach Exception | Verhalten |
|---|---|
| Keine Mutation, Modell verifiziert gültig | Ursprüngliche Exception; kein künstlicher Fault. |
| Mutation erfolgt, Modell verifiziert gültig | Ursprüngliche Exception; keine unbegründete dauerhafte Sperre. |
| Modell ungültig oder Prüfung selbst nicht durchführbar | Bestehendes LayoutFaulted wird gesetzt; ursprüngliche Exception bleibt erhalten. |

Die Validierung verwendet ausschließlich VisibleTree und DockLayoutStore.Validate. Sie ruft weder Capture noch window.Save, InitDockable, Show, Close oder eine Kompensation auf. Damit aktualisiert sie insbesondere keine Fensterkoordinaten und erzeugt keine weitere Dock-Mutation.

Validierungs-, Cleanup- und nachgelagerte Diagnosefehler können jeweils unter einem festen Schlüssel in Exception.Data erhalten werden. Es entsteht keine wachsende Fehlerliste. Auch ein Fehler beim Hinterlegen dieser Zusatzinformation ersetzt die ursprüngliche Exception nicht.

Die vorhandenen Layout-/Factory-/Drop-Gates verwenden weiterhin denselben A02-Fault-Zustand. Ein Fault sperrt weitere Layoutaktionen und beschädigte Persistierung. Exiting und die vorhandenen sicheren fachlichen Save-/Shutdown-Wege bleiben verfügbar.

Capture prüft bei einem Fault zuerst rein lesend die normale Struktur, sofern keine A02-Fokusprojektion vorliegt: Ein ungültiger Baum liefert weiterhin InvalidDataException; ein strukturell gültiger, aber von A02 als fehlerhaft markierter Präsentationszustand weiterhin InvalidOperationException. Es werden dabei keine Host-Bounds gespeichert und keine Layoutdateien geschrieben. Beide bestehenden Exception-Verträge bleiben durch unveränderte Tests abgesichert.

**Kein Rollbackversprechen:** Nach dem P1-Fehler bleibt das unvollständige Fenstermodell zunächst vorhanden. R4 erkennt und sperrt diesen Zustand. Die Korrektur rekonstruiert weder das vorherige Layout noch eine fehlende native Ressource.

## 5. P2: Diagnose-I/O

Der neue interne DockTraceOutput kapselt ausschließlich die synchrone Diagnoseausgabe. NativeDockWindowTrace behält seinen bisherigen JSON-Inhalt, seine Registrierungen und Beobachtungspunkte; die Erstellung und Ausgabe eines Datensatzes laufen innerhalb dieser Diagnosegrenze.

Korrekturstellen: [DockTraceOutput.Write][traceoutput], [DockLifecycleTrace.Write][lifecyclesink] und [NativeDockWindowTrace.Record][nativerecord].

Bei IOException oder UnauthorizedAccessException:

- Kein Schreibfehler propagiert aus dem Recorder in den Factory-Callback.
- Genau ein begrenzter Fehlerstatus enthält Typ, HResult und höchstens 512 Zeichen Meldung.
- Eine einmalige Meldung auf stderr beginnt mit „Dock trace disabled“.
- Weitere Datensätze werden weder erzeugt noch gepuffert; es gibt keine Queue und keinen automatischen Retry.
- Der FileStream verwendet keinen verwalteten Schreibpuffer. Nach einem Fehler wird beim Dispose nur der zugrunde liegende Stream geschlossen, ohne den fehlgeschlagenen StreamWriter-Puffer erneut zu flushen.

DockLifecycleTrace sichert dieselben erwartbaren I/O-Fehler eines direkt angeschlossenen Diagnose-Sinks ab, hält dessen Fehlerstatus vor und trennt diesen Sink nach dem ersten Fehler ab. Ein ausdrücklich neu installierter Sink beginnt eine neue Diagnoseinstanz. NativeDockWindowTrace meldet seinen eigenen Writer-Status über Failure und stderr.

Die Fehlergrenze umfasst keine eigentliche Dock-, Editor- oder MCP-Ausführung. Insbesondere bleibt die Win32Exception aus dem originalen Host-Locator sichtbar. Andere Exceptions eines Diagnose-Sinks werden nicht pauschal unterdrückt. Die Regression für eine InvalidOperationException vor der Mutation belegt diese Unterscheidung.

Die Absicherung betrifft Diagnose-Schreiben und dessen Abschluss, nicht beliebige Fehler beim Starten des optionalen Recorders oder jeder nativen Diagnose-API.

## 6. Unveränderter bestehender Partial-Failure-Test

Die gesamte Datei DockOperationContractTests.cs ist gegenüber dem R4-Ausgangsstand bytegleich. Das gilt somit ausdrücklich für alle Assertions von PartialMoveFailureMustLatchExistingLayoutFault. Auch DockOperationNativeProbe.cs ist bytegleich.

| Nachweis | Debug | Release |
|---|---|---|
| Historisches R3-Ergebnis des bestehenden Tests | 0 bestanden / 1 fehlgeschlagen | 0 bestanden / 1 fehlgeschlagen |
| R4-Abschlussergebnis | 1 bestanden / 0 fehlgeschlagen | 1 bestanden / 0 fehlgeschlagen |

R3 bleibt unverändert unter [targeted-results.json][r3results] erhalten. Die sechs in R2 entfernten künstlichen B4-Fälle werden weder als bestanden gezählt noch wieder eingeführt. Der erhaltene Fall ClosingMustRejectIndependentOtherHostClose(False) bleibt aktiv.

Die beiden bestehenden Public-Hook-Charakterisierungstests bleiben ebenfalls unverändert. Insbesondere belegt PublicHookDisposeExceptionMasksOriginalManagerException weiterhin die Eigenschaft der öffentlichen Upstream-Schnittstelle; R4 erklärt den allgemeinen F03-Completion-Vertrag damit nicht für gelöst.

## 7. Neue permanente Regressionen

DockPartialFailureTests ergänzt genau neun Fälle:

| Test | Fälle | Aussage |
|---|---:|---|
| HostCreationFailureLatchesFaultAndPreservesBusinessSession | 2: Dokument, Variables-Tool | Kontrollierter Win32Exception am Host-Locator; Partial-State/Fault, Sperren, ursprünglicher Fehler, Identität, nichtleerer Scripttext, Interpreter-Revision, echter sicherer Save und Shutdown. |
| FailureBeforeMutationDoesNotFaultValidLayout | 1 | Fehler vor Mutation erhält gültiges Layout ohne Fault. |
| FailureAfterValidMovePreservesErrorWithoutLatchingFault | 1 | Vollständig verifizierbarer Endzustand trotz sichtbarem Fehler; nachfolgende erlaubte Aktion möglich. |
| SecondaryDiagnosticFailureCannotReplaceOriginalPartialFailure | 1 | Original bleibt sichtbar; sekundärer Diagnose- und Validierungsfehler sind zugeordnet. |
| RealTraceWriteFailureDoesNotInterruptMove | 2: Writer, Lifecycle-Sink | Echte Windows-Byte-Sperre löst den Schreibfehler nach Remove aus; Move und Identität bleiben intakt; Ausgabe wird ohne Retry stillgelegt. |
| UnverifiableEndStateLatchesFaultWithoutReplacingOriginal | 1 | Nicht lesbares Modell setzt Fault; Prüfexception ersetzt den ursprünglichen Fehler nicht. |
| GroupFailureIsValidatedAfterItsExistingCompletionScope | 1 | Kein Fault mitten in der bestehenden Gruppen-Fortsetzung; ungültiger Endzustand wird bei deren Abschluss erkannt. |

Die Host-Injektion erschöpft keine Windows-Ressourcen. Sie ist ein deterministischer Test des statisch belegten P1-Aufrufpfads. Die Trace-Tests werfen keine künstliche IOException: Ein zweiter FileStream sperrt das nächste Byte, die echte WriteLine-Operation scheitert mit HResult 0x80070021. Normale Ausgabe wird vorher gelesen; nach dem Fehler werden Schreibversuche und Dateigröße kontrolliert. Die zwei Byte-Lock-Fälle benötigen Windows; im hier ausgeführten Windows-Lauf werden sie ausgeführt, nicht übersprungen.

Die neue native Probe verwendet per Reflection den tatsächlichen Desktop-NativeDockWindowTrace. Dadurch wird keine neue Projekt-/Paketreferenz des GUI-Testprojekts benötigt. Die OS-Sperre wird direkt vor dem vorhandenen produktiven DockableRemoved-Subscriber gesetzt; sie injiziert weder eine zweite Dock-Operation noch eine künstliche Exception.

## 8. Vollständige Regression und Inventardifferenz

| Suite | Debug bestanden | Debug fehlgeschlagen | Release bestanden | Release fehlgeschlagen | Nicht ausgeführt je Konfiguration |
|---|---:|---:|---:|---:|---:|
| GUI | 1.103 | 0 | 1.103 | 0 | 0 |
| MCP | 176 | 0 | 176 | 0 | 13 |
| Core | 11.512 | 0 | 11.512 | 0 | 27 |
| Headless | 12 | 0 | 12 | 0 | 0 |

GUI: Die 1.087 A03-Fälle bleiben vollständig grün; hinzu kommen die sieben erhaltenen A04-Fälle und neun neue R4-Fälle. Kein bisheriger Test wurde entfernt. Die einzige Ergebnisänderung gegenüber R2 ist PartialMoveFailureMustLatchExistingLayoutFault: Failed → Passed. Die sechs verworfenen B4-Fälle bleiben außerhalb des Inventars. Die finale GUI-Ausführung steht in [gui-final-accepted][guifinal]. Die unabhängigen Wiederholungen bestehen in Debug und Release jeweils mit 10/10 Fällen ([Wiederholungsresultate][repeat]).

MCP: 189 TRX-Einträge = 176 Passed + 13 unverändert NotExecuted. Core: 11.539 TRX-Einträge = 11.512 Passed + 27 unverändert NotExecuted; die Konsolensumme enthält 26 Skips und lässt den zusätzlichen Explicit-Fall aus. Diese nicht ausgeführten Fälle werden nicht als bestanden gezählt. Core und Headless haben keine Inventar- oder Ergebnisdifferenz; MCP ebenso nach der unten beschriebenen begrenzten Anzeigenamensnormalisierung.

Roh-TRX und Logs des ersten vollständigen Laufs liegen unter [regression][regression]. Nach Ergänzung der nichtleeren Daten, Interpreter-Revision und des echten Saves wird ausschließlich die betroffene GUI-Suite erneut vollständig gebaut und ausgeführt, anschließend werden die zehn kritischen Fälle (neun neue plus der bestehende Partial-Failure-Test) in einem neuen Testhost wiederholt. Die Produktkorrektur war bereits vor dem ersten vollständigen Lauf unverändert.

Der [Inventarvergleich][inventory] enthält jeden alten/neuen Fall, Ergebnisänderungen und die sieben erhaltenen A04-Fälle. Gleiche Anzeigenamen mehrfach ausgeführter bestehender Fälle bleiben als Multimenge mit Auftretensnummer erhalten; Groß-/Kleinschreibung wird unterschieden. Bei MCP werden nur UTC-Zeitstempel in den von FakeAutomationService zur Laufzeit erzeugten ContractTests-Anzeigenamen normalisiert; rohe Namensdifferenzen bleiben zusätzlich erhalten. Kein Testcode wird dafür geändert.

Entwicklungsläufe werden nicht als erfolgreiche Abnahme ausgegeben:

- Der erste Debug-Aufruf ohne Restore traf auf zuvor vorhandene Release-Assets und scheiterte beim Build. Der konfigurierte Locked-Restore behob dies.
- Der erste gezielte Lauf bestand 30 Fälle; zwei neue Trace-Tests scheiterten an der FileShare-Einstellung ihrer vorbereitenden Leseoperation. Die Korrektur betrifft ausschließlich diese Testvorbereitung.
- Danach bestanden 34 gezielte Fälle.
- Die abschließende Save-Ergänzung benötigte einen fehlenden Namespace-Import. Ein weiterer Lauf zeigte eine zu frühe neue Shutdown-Assertion nach dem asynchronen Save-/Close-Ablauf. Der Test wartet nun begrenzt auf den vorhandenen Abschluss; seine Zustandsassertionen bleiben erhalten.

Diese Logs und fehlgeschlagenen Läufe bleiben unter den separaten R4-Entwicklungsverzeichnissen erhalten.

## 9. Native Windows-Matrix

**Finale fachliche Matrix: 31/31 Prozesse je Debug und Release, insgesamt 62/62 bestanden, kein Timeout.** Die Ansteuerung ist programmgesteuert auf echten Windows-Hosts. Sie ersetzt keine externe Maus-/Tastaturabnahme.

| Gruppe | Debug-Prozesse bestanden | Release-Prozesse bestanden |
|---|---:|---:|
| Neue R4-Partial-Failure-Probe, je 22 Checks | 2 | 2 |
| Unveränderte A04-Probe, je 21 Checks | 2 | 2 |
| A03-Persistenz/Cleanup | 20 | 20 |
| Layoutrollen/Maximize, Layout, Restart, Invalid, Redock, Editor, Integration | 7 | 7 |
| Summe | 31 | 31 |

Die Layoutrollen-Probe besteht je Konfiguration mit 194 Checks. Die übrigen sechs Bestandsmodi bestehen je Konfiguration mit 48/56/49/134/54/28 Checks. Ergebnisse und Prozesse stehen in [native-results.json][nativeresults], [native/commands.json][nativecommands] und [native-legacy/commands.json][legacycommands].

**Separat erhaltene Treiberfehlversuche:** Im ersten breiten Aufruf wurde eine ein-elementige PowerShell-Modusliste als String behandelt. IncludeExistingNative verkettete daraufhin sieben Modi zu einem ungültigen Argument. Je ein Debug-/Release-Prozess endete mit Exit 2, bevor eine Probe gestartet wurde; beide werden nicht als bestanden gezählt. Der Treiber führt die Modusliste nun ausdrücklich als String-Array. Die fehlenden sieben Modi wurden danach in beiden Konfigurationen einzeln ausgeführt und bestanden. Die bereits korrekt ausgeführten R4-/A04-/Cleanup-Prozesse wurden deshalb nicht erneut wiederholt. Die ursprüngliche Treiber-Gesamtausführung bleibt mit Exit 1 dokumentiert.

Validate-G1LayoutRoles führt je Konfiguration die bisherigen Layoutrollen, Layout/Restart/Invalid, Redock, Editor, Integration und 20 Persistenz-/Cleanup-Prozesse aus, außerdem je zwei unabhängige A04- und R4-Prozesse. Die R4-Probe prüft echten Trace-I/O-Ausfall, native Source-HWND-Bereinigung, gültigen Move, einen verbleibenden unabhängigen Host mit korrektem Owner, den kontrollierten Host-Erzeugungsfehler, Fault/Sperren, Editor-/Save-Erhalt, einen tatsächlich geschriebenen fachlichen Save und kontrollierten Shutdown.

Jede neue R4-Probe enthält 22 Checks. Die früheren vier erfolgreichen nativen Debug-Entwicklungsläufe bleiben separat ausgewiesen; deren R4-Version hatte vor der zusätzlichen Save-Prüfung jeweils 21 Checks. Sie werden nicht mit der finalen Abnahme vermischt.

## 10. Editor-/MCP-Sicherheit

Die Korrektur verändert ausschließlich Layout-/Diagnoseverhalten. Die vorhandene Session.Relocate-/Coordinator.Observe-Grenze wird weiter benutzt. Allowed, MCP-Lease-Regeln, Busy/Pending/Modal-Prüfungen und Ausführungszulassung erhalten keinen neuen Sonderfall. Die bestehende Mutationstiefe dient weiterhin der internen Fortsetzung; ihre Pflege wurde auch im sessionlosen/Exiting-Pfad vereinheitlicht, damit Fehler am äußeren Rand geprüft werden.

Die permanenten P1/P2-Tests vergleichen dieselben Editor-, Document- und UndoStack-Objekte, SaveIntent-Snapshot und WorkspaceGeneration. Nichtleerer Scripttext und unveränderte InterpreterHostQueue.StateRevision sichern zusätzlich Datenbestand und fehlende Ausführung ab. Nach dem P1-Fault funktioniert CommitSaveAsync mit PreserveText tatsächlich. Registrierungen und native Hosts werden beim Shutdown freigegeben.

Weitere bestehende A02-/A03-/F01-/F03-, Lease-, Pending-/Busy-/Modal-, SaveIntent- und Owner-/Host-Cleanup-Prüfungen bleiben unverändert Bestandteil der vollständigen GUI-/MCP-/Headless- und nativen Matrix.

## 11. Vollständige Änderungsliste gegenüber R4-Baseline

| Datei | Änderung |
|---|---|
| CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs | Fehler am äußeren Factory-Rand verifizieren; bestehende Gruppen-Fortsetzung respektieren; Original-/Sekundärfehler unterscheiden; Cleanup-Marker vor nachgelagertem Trace zurücksetzen. |
| CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs | Bestehenden LayoutFaulted-Zustand bei fehlgeschlagener Endzustandsprüfung setzen; begrenzte sekundäre Fehlerzuordnung. |
| CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs | Rein lesende Modellprüfung; beschädigte Persistierung sperren und bestehende Exception-Verträge erhalten. |
| CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs | Erwartbaren Sink-I/O-Fehler abfangen, Status erhalten, Sink trennen. |
| CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs (neu) | Begrenzter synchroner Diagnose-Writer und Fehlerstatus. |
| CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs | Vorhandene Datensätze über den abgesicherten Writer ausgeben. |
| CryptoWorkBenchGuiTest/DockPartialFailureTests.cs (neu) | Neun permanente R4-Fälle. |
| CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs (neu) | Tatsächlicher Desktop-Trace, native P1-/P2- und Save-/Shutdown-Nachweise. |
| CryptoWorkBenchGuiTest/DockShellNativeProbe.cs | Neue Probe dispatchen. |
| eng/gui/native/Program.cs | Neuen Probe-Modus zulassen. |
| eng/gui/Validate-G1LayoutRoles.ps1 | Optionalen R4-Modus zweimal je Konfiguration ausführen; vorhandene Modusliste als String-Array führen, damit IncludeExistingNative keine verkettete Zeichenfolge erzeugt. |
| docs/gui/G1-A04-R4-partial-failure-correction.md (neu) | Dieser Bericht. |
| docs/gui/evidence/G1-A04-R4-results.json (neu) | Maschinenlesbare Resultate, Inventar, Integrität und Git-Prüfungen. |

Neue ausführbare Auswertungs-/Laufskripte, Baselinekopien, Logs, TRX und native Rohdaten liegen ausschließlich im ignorierten artifacts/g1-a04-r4-Verzeichnis. Frühere Berichte und Roh-Evidence werden nicht geändert.

## 12. Grenzen und Übergabe

R4 behauptet weder vollständige Rollbackfähigkeit noch Schutz vor jeder möglichen Framework-Exception. Der Host-Test simuliert die nachgewiesene Fehlergrenze, keine tatsächlich beobachtete Windows-Ressourcenerschöpfung. Die I/O-Regression dagegen führt eine echte fehlgeschlagene Dateioperation aus.

Der allgemeine F03-Completion-Vertrag, externe Mausprüfungen und offene Plattformabnahmen bleiben offen. Die R1-B4-Entscheidung wird nicht erweitert oder rückgängig gemacht. Historische Fehlläufe werden nicht nachträglich als bestanden ausgewiesen.

Die 12.392 historischen Artefakte und alle nicht ausdrücklich geänderten Baseline-Dateien wurden abschließend erneut gehasht. Der vollständige [Git-Status][gitstatus] enthält 144 Zeilen einschließlich der bereits vorhandenen G1-Arbeit. [final-checks.json][gitchecks] enthält zusätzlich alle Statuszeilen, Branch, unveränderten HEAD und Prüfresultate.

git diff --check: **Exit 0, keine Ausgabe**. Da ein Großteil der G1-Dateien bereits vor R4 untracked war, wurden zusätzlich alle 13 R4-Dateien mit git diff --no-index --check gegen Baseline beziehungsweise eine leere Datei geprüft: keine Whitespace-Diagnose; Exit 1 bezeichnet dort die vorhandene Dateidifferenz. Diese Zusatzprüfung ändert keine Git-Konfiguration.

Der master-Checkout und der gepinnte Dock-Quellcheckout sind sauber. Keine Schreiboperation im L1-Worktree, keine Commits, Pushes oder Merges. Die [maschinenlesbaren Ergebnisse][results] enthalten auch die getrennt ausgewiesenen Entwicklungs-/Treiberfehler. **R4 endet hier. A05 erst nach ausdrücklicher Eigentümerabnahme.**

[r1]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-R1-actual-callback-audit.md>
[r2]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-R2-test-cleanup.md>
[r3]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/G1-A04-R3-partial-failure-audit.md>
[r3results]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r3/targeted-results.json>
[baseline]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/baseline/source-manifest.json>
[integrity]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/integrity.json>
[patch]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/r4.patch>
[docksrc]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1/dock-source/src/Dock.Model/FactoryBase.cs:811>
[inventory]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/test-inventory-difference.json>
[regression]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/regression/commands.json>
[guifinal]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/gui-final-accepted/commands.json>
[repeat]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/repeat-results.json>
[factorychange]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs:129>
[faultverify]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs:33>
[modelvalidate]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs:24>
[traceoutput]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs:16>
[lifecyclesink]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs:15>
[nativerecord]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs:138>
[nativeresults]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/native-results.json>
[nativecommands]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/native/commands.json>
[legacycommands]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/native-legacy/commands.json>
[gitstatus]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/final-git-status.txt>
[gitchecks]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/artifacts/g1-a04-r4/final-checks.json>
[results]: <C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/artifacts/worktrees/gui-g1/docs/gui/evidence/G1-A04-R4-results.json>
