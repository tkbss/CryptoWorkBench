# A12-01-R1 – Independent Avalonia 12 Migration Review

Datum: 27.10.2026. Technische Bewertung: **CHANGES REQUIRED**.

Ein konkreter, reproduzierbarer Abbruchfehler ist offen: Das Schließen der Importbestätigung wird wie „No / anhängen“ behandelt und führt das gewählte ScriptBook aus (A12-R1-F01, Medium). Die fehlerhafte Verzweigung bestand bereits vor der Migration. Im geprüften Migrationsdelta wurde keine zusätzliche Regression der MCP-/Coordinator-Sicherheitsverträge nachgewiesen. Der veröffentlichte Prism-Binärblocker besteht im tatsächlichen Worktree weiterhin; A12-02 belegt einen technisch funktionierenden, isolierten Quellbuild, keine integrierte Lösung.

Dies ist ein methodisch separater Review mit neuer Bestandsaufnahme, eigener Quellprüfung, frischen Builds/Testläufen und zusätzlichen Gegenproben. Er ist keine personell oder organisatorisch unabhängige Zertifizierung: Der ausführende Agent verfügt über den bisherigen Arbeitskontext. Frühere PASS-Aussagen wurden nicht als Ersatz für Prüfung übernommen.

## 1. Ausgangsbasis und Integrität

Maßgeblich war der tatsächliche, uncommittete Worktree `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`, Branch `feature/avalonia12-migration`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Die Aufnahme erfolgte vor den Reviewläufen am 27.10.2026 um 08:33:45 UTC.

Neue Evidenz liegt ausschließlich in [artifacts/a12-01-r1](../../artifacts/a12-01-r1/). Dort dokumentieren `baseline/identity.json`, `git-status.txt`, `staged.patch`, `unstaged.patch`, `source-manifest.json` und `file-inventory.json` Ausgangsstand, 772 Quelldateien mit SHA-256 und 9.300 ursprünglich vorhandene Dateien mit Größe/Änderungszeit. Geschützte Konfigurationsverzeichnisse wurden von der Inhaltsaufnahme ausgeschlossen; insbesondere wurde `.codex/config.toml` weder gelesen noch verändert. Die Staging Area war leer.

Der Vergleich zum ursprünglichen A12-01-Manifest mit 767 Dateien ergibt **20 geänderte und fünf neue Dateien**, keine entfernten Dateien. Neu sind UiDispatch, Avalonia12DispatcherTests, GuiTestSession sowie die beiden A12-Berichte. Seit dem A12-02-Ausgangsmanifest sind dessen 771 Dateien unverändert; lediglich der A12-02-Bericht ist hinzugekommen. Details: `baseline/migration-delta.json`.

Die historischen Evidenzmanifeste wurden gegen die vorhandenen Dateien geprüft: A12-01: 187 Einträge ohne Hashabweichung; A12-02: 116 gehashte Dateien und ein Selbstreferenzeintrag, ohne Abweichung. Das ist ein Integritätsbeleg innerhalb des vorhandenen Bestands, keine externe Signatur oder Bestätigung jedes historischen Ablaufs. Nachweis: `baseline/historical-evidence-verification.json`.

Builds und Tests liefen in einer neuen Kopie unter `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-A12-01-R1-Review\app`. Der zusätzliche Harness liegt daneben unter `probes/review`; seine überprüfbare Kopie liegt in `artifacts/a12-01-r1/probe`. Er referenziert die unveränderten Projekte. Der Assemblyname nutzt die bereits vorhandene Test-Friend-Assembly, ohne Produktionsattribute zu verändern. Separate NuGet-/CLI-Verzeichnisse verhindern Buildartefakte im Original.

Abschlussnachweis: [integrity-final.json](../../artifacts/a12-01-r1/integrity-final.json) bestätigt **772/772 unveränderte Originaldateien, 772/772 identische Dateien der Testkopie sowie 0 Metadatenabweichungen bei den 9.300 anfangs erfassten Dateien**. Zeitstempel wurden als UTC-Ticks verglichen. HEAD und Branch sind unverändert. Hinzugefügt wurden nur dieser Bericht und die neue Reviewevidenz. Kein Produktivcode und kein vorhandener Test wurde korrigiert; keine Prism-Integration, kein Commit, Push, Merge, Reset, Rebase oder Stash wurde durchgeführt.

## 2. Geprüfter Code und Architekturgrenzen

Die Prüfung umfasste den gesamten oben genannten Delta und die angrenzenden Verträge:

| Bereich | Geprüfte Dateien/Verträge |
|---|---|
| Dispatch/Sicherheit | Services/UiDispatch.cs; PasswordDialogHost; EditorReplaceConfirmation; WorkbenchEditorOperations; CryptoScriptEditViewModel; KeyMapViewModel; InterpreterExecution; InterpreterHostQueue; WorkbenchExecutionPipeline; Registry-/Berechtigungsübergänge |
| Editor | EditorCoordinator und Callback-/Receipt-Verträge; EditorBridge; WorkbenchTextEditor/TextArea; Clipboard; View-Lifecycle; Save- und Importübergänge |
| Migration | App.axaml.cs; GUI-/Desktop-/Testprojekte; SidebarViewModel; GuiTestSession; TestApp; geänderte Keyboard-/Clipboard-Testadapter; Ressourcenpfade |
| Historie | A12-01/A12-02-Berichte und Rohbelege; MCP-01.4-Architektur/Validierung/Review; AP2.1-Implementierung, unabhängiger Review, F1-Korrektur und Re-Review |

Die Referenzgrenzen bleiben bestehen: Core ohne Hostreferenz, Abstractions ohne GUI/Core, MCP ohne GUI-/Core-Kopplung; GUI besitzt die hostinternen Präsentationsadapter. Es entstand keine zweite Interpreterpipeline, kein zweiter Variablenbestand, keine zusätzliche Undo-Historie und keine neue MCP-Tool- oder Transportfreigabe. Parser, Runner und Kryptomechanismen sind gegenüber der Ausgangsbasis unverändert.

## 3. R1 – ExecutionContext und Sicherheitsgrenzen

**Ergebnis:** Der geprüfte UiDispatch-Vertrag funktioniert für den produktiven Worker-zu-UI-Übergang. Er ist keine globale Bereinigung aller UI-Kontexte.

Avalonia 12.1.4 erfasst im DispatcherOperation-Konstruktor den ExecutionContext und führt den Callback bei vorhandenem Capture darin aus. Die neue Probe bestätigt dies positiv sowohl mit AsyncLocal als auch mit dem tatsächlichen InterpreterExecution-Zugriffsscope. [Upstream DispatcherOperation](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Threading/DispatcherOperation.cs).

UiDispatch unterdrückt den Fluss nur um den synchronen Schedulingaufruf. Der using-Scope enthält kein await. Bereits unterdrückter Fluss bleibt unterdrückt; bei Erfolg und synchronem Enqueue-Fehler wird der ursprüngliche Zustand des Aufrufers erhalten. Action, Func<T>, Func<Task<T>> und Post delegieren die tatsächliche Operation an Avalonia. Die Originaltests enthalten drei passende Grundregressionen; allein daraus wäre noch keine Aussage über alle Fehler-/Reentrancypfade zulässig.

Die zusätzlichen Proben prüfen:

- Rohdispatcher als positive Kontrolle; UiDispatch ohne Worker-AsyncLocal und ohne Interpreterberechtigung.
- Wiederherstellung beim Enqueue-Fehler sowie bei bereits unterdrücktem Fluss.
- Identische synchrone und nach Task.Yield auftretende Callback-Exceptions.
- Callback-Cancellation einschließlich CancellationToken sowie Abort einer noch wartenden Operation ohne Callbackausführung.
- Verschachteltes asynchrones Enqueue und UI-Fortsetzung ohne geerbte Interpreterberechtigung.
- Unveränderten Post-Fehlerkanal über Dispatcher.UnhandledException.
- Fortbestehende Berechtigung des ursprünglichen Workers nach dem UI-Übergang.

Die produktive Suche ergab elf UiDispatch-Aufrufe in fünf Dateien: PasswordDialogHost (Anfrage und Cancel-Close), EditorReplaceConfirmation (Dialog und Cancel-Close), WorkbenchEditorOperations (UI-Übergang), CryptoScriptEditViewModel (Print, Info, Präsentation) und KeyMapViewModel (Info, Präsentation, Fehler). Die vier direkten Enqueues innerhalb des Helpers sind beabsichtigt. Übrige produktive Dispatcher-Verwendungen prüfen Threadzugriff; ein verbliebener direkter produktiver Enqueue außerhalb des Helpers wurde nicht gefunden. Suchbeleg: `research/dispatcher-callsites.txt`.

InterpreterExecution prüft weiterhin Runner-/Scope-/Threadidentität. InterpreterHostQueue unterdrückt den Fluss beim Workerstart; RunState beendet seinen synchronen Zugriff vor der Präsentation. Übergebene Präsentationsdaten sind explizite Werte; Admission und ausstehende Antworten bleiben Eigentum der bestehenden Ausführungspipeline. Der unmittelbare UI-Zweig von WorkbenchEditorOperations ist kein Enqueue und wird von UiDispatch nicht umdefiniert.

**Nachgewiesene Grenzen:**

1. Bei einem verschachtelten synchronen UI-Pump bleibt ein bereits auf dem UI-Thread installierter AsyncLocal-Wert sichtbar: `BOUNDARY nested synchronous pump value=ui-pump`. Fehlendes Capture bedeutet nicht, dass Avalonia einen frischen leeren ExecutionContext installiert.
2. Ein ausdrücklich mitgegebenes ExecutionContext.Capture und anschließendes ExecutionContext.Run kann die Berechtigung bewusst wieder einsetzen. Die Probe bestätigt dies als Reichweitengrenze. An den geprüften produktiven Aufrufstellen existiert dieser Pfad nicht.
3. Bibliotheksinterne Dispatcher-/SynchronizationContext-Pfade benutzen den Wrapper nicht. [AvaloniaSynchronizationContext](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Threading/AvaloniaSynchronizationContext.cs) delegiert selbst an den Dispatcher. UiDispatch ist daher kein Schutz gegen beliebigen Fremdcode oder absichtlich transportierte Capabilities.
4. Cancellation wird nicht automatisch für jede Operation erfunden: Der Helper besitzt keinen allgemeinen Tokenparameter. Die tatsächlichen Caller registrieren Close-Cancellation beziehungsweise verwenden die Operation/den Callback-Vertrag.

Diese Grenzen sind keine nachgewiesene produktive Umgehung. Eine Aussage wie „AsyncLocal-Berechtigungen können niemals auf dem UI-Thread existieren“ wäre jedoch nicht gedeckt.

## 4. R2 – EditorCoordinator und AvaloniaEdit 12

**Ergebnis:** Keine migrationsbedingte Regression der geprüften AP2.1-Verträge festgestellt. Die 35 Coordinator-Fälle bestehen unverändert im Fallbestand; Anpassungen betreffen die Avalonia-Testinfrastruktur, nicht deren fachliche Assertions.

| Referenzkorrektur | Prüfung am tatsächlichen Code |
|---|---|
| F01 – Control-/Coordinator-Zuordnung | Attach entfernt die alte Zuordnung nur bei tatsächlichem Besitz; vorherige und neue Attach-Verantwortung überlappen nicht. Coordinator-Produktionscode unverändert. |
| F02 – Callback-/Receipt-Finalisierung | Ticket hält laufende Callbacks trotz Dispose-Anforderung bis zur letzten Finalisierung. Fehler werden vor Abschluss erfasst; Drain darf nicht durch vorzeitiges Receipt-Ende erfolgreich werden. |
| F03 – Drain nach Fokus-/Intentwechsel | Drainreferenz bleibt unabhängig von der aktuellen interaktiven Generation. Fokusverlust invalidiert Intents, nicht nachträglich die Identität laufender Finalisierung. |
| F04 – Reentrancy vor Mutation | Commit prüft unmittelbar vor der Aktion erneut IsCurrent/CanBegin und die ursprüngliche Dokument-/Control-/Revisionsbindung. |

Clipboard erfasst Ticket, Document, Selection und Caret vor dem await. Nach dem Lesen werden ursprüngliche Selection/Caret und aktuelle Ticketgültigkeit geprüft; Commit prüft erneut. Ein verspätetes Ergebnis sucht keinen Nachfolgeeditor als neues Ziel. Textanker und Präsentation behalten ihre ursprüngliche Dokument-/Lebenszyklusbindung.

Native Undo-/Redo-Aktionen werden weiterhin einmalig durch den bestehenden Adapter delegiert; es wurde kein Ersatzstack eingeführt. Exklusive Save-Lease, Admission und dauerhafte Eingabesperren wurden nicht aufgeweicht. Die IME-Sperre in WorkbenchTextEditor bleibt erhalten. Die ausdrücklich akzeptierte CJK-IME-Einschränkung bleibt unverändert; der Review enthält keinen neuen nativen IME-Nachweis.

Das ist eine Prüfung der hostinternen Adapter und ihrer bestehenden Verträge. Sie beansprucht keine globale Sandbox um alle öffentlichen AvaloniaEdit-Raw-APIs.

## 5. R3 – API-Migration

**Clipboard/DataTransfer:** Der Wechsel von GetTextAsync zu TryGetTextAsync entspricht der neuen API. Die Avalonia-Erweiterung übernimmt den Transferbesitz und entsorgt ihn; der Host erhält Text, keinen unverwalteten, dauerhaft gehaltenen Transfer. Die Testproxies verzögern jetzt TryGetDataAsync und erhalten ihre bisherigen Late-Result-Assertions. Der Aufrufer bleibt auf dem UI-Kontext; die interne Fortsetzung nach dem eigentlichen Datenzugriff wird nicht als neue Hostberechtigung behandelt. [ClipboardExtensions 12.1.4](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Avalonia.Base/Input/Platform/ClipboardExtensions.cs).

**Tastatur:** Die Headless-Eingaben für Ctrl+Z/Y/X/V geben PhysicalKey passend zur neuen Signatur an. Die produktiven Tastaturhandler wurden nicht durch eine neue Interpretation der Modifier ersetzt. Bestand und Assertions der betroffenen Tests bleiben erhalten. Dies belegt die ausgeführten Gesten, keine vollständige Layout-/IME-Matrix.

**Headless-Session:** GuiTestSession verwendet einen gemeinsam genutzten Dispatcherthread. Die einzelnen Dispatch-Aufrufe behalten isolierte App-Instanzen gemäß Headless-Session-Vertrag; TestApp lädt jetzt die tatsächlichen Fluent-/AvaloniaEdit-Styles. Die gemeinsamen statischen KeyBindings von AvaloniaEdit dürfen nicht auf wechselnden Dispatcherthreads gelesen werden. Das erklärt den historischen Fehler in `KeyBinding.get_Gesture → VerifyAccess`, dessen Rohbeleg separat extrahiert wurde. [AvaloniaEdit EditingCommandHandler](https://raw.githubusercontent.com/AvaloniaUI/AvaloniaEdit/12.0.0/src/AvaloniaEdit/Editing/EditingCommandHandler.cs), [HeadlessUnitTestSession](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.4/src/Headless/Avalonia.Headless/HeadlessUnitTestSession.cs).

Die frische vollständige Suite bestätigt die aktuelle Sessionlösung. Sie beweist weder beliebige Testreihenfolgen noch generelle Leckfreiheit eines prozessweiten Dispatchers.

**Validierung/Bindings:** Das entfernte pauschale Entfernen des ersten Validators passt zum in Avalonia 12 geänderten Standard: DataAnnotations werden nicht mehr standardmäßig doppelt aktiviert. Reflection-Bindings bleiben bewusst konfiguriert, um das bisherige Bindungsverhalten zu erhalten. Daraus folgt keine Aussage über erfolgreich migrierte CompiledBindings oder AOT. [Offizielle Breaking Changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes).

**Ressourcen/Pfade:** Das Sidebar-Icon wird über avares statt relativ zum Working Directory geladen. Der native R1-Start erfolgte aus einem leeren Arbeitsverzeichnis und erreichte echte MainWindow-/Region-/Dialoginstanzen. Bestehende AppContext.BaseDirectory-Pfade wurden nicht verändert. Ressourcen wurden hinsichtlich Schreibweise im Quellbestand geprüft; eine Windows-Ausführung kann Linux-Groß-/Kleinschreibung nicht beweisen.

**Paketgraph:** Beide frischen Builds verwenden Avalonia und Markup.Xaml.Loader 12.1.4, AvaloniaEdit 12.0.0, DataGrid 12.1.2, MessageBox 12.0.0 sowie DialogHost 0.12.3. Prism bleibt beim veröffentlichten Stand 9.0.537.11130; Prism.Core/Events 9.0.537 und Container 9.0.107. DiagnosticsSupport 2.2.3 erscheint im Debug-, nicht im Release-Graphen. Belege: `tests/package-comparison.json` und beide vollständigen Assets-Dateien. Unterschiedliche kompatible Patch-/Paketversionsschemata wurden nicht allein aufgrund abweichender Nummern als Fehler eingestuft.

## 6. R4 – Navigation und Dialoge

Der neue Harness startet die tatsächliche App mit DI, MainWindow und fünf Regionen. Info → Variable → Info aktiviert jeweils die erwartete Regionview. Die Windows-Ausführung besitzt ein natives Fensterhandle und eine reale Desktop-Message-Loop. Dies belegt Bootstrap und die geprüften Navigationen, keine vollständige manuelle Fokus-/Aktivierungsprüfung.

Für die tatsächliche MessageBox-Bibliothek wurden Yes, No, Schließen und Owner-Schließen in Headless und Windows geprüft: korrekter Owner, ausstehender Dialogtask, erwartetes Ergebnis und Fensterbereinigung. Yes/No werden über den tatsächlichen Button.OnClick-Pfad inklusive Command ausgelöst. Schließen und Owner-Schließen liefern ein leeres Ergebnis. Die produktive Importauswertung behandelt dieses Ergebnis falsch; siehe Finding.

Save behandelt dagegen alle Nicht-Yes-Ergebnisse der Überschreibbestätigung als Abbruch. Die anschließenden Snapshot-/Admission-/Lease-Prüfungen und das bisherige Append-/ASCII-/Delete-vor-Write-Verhalten sind unverändert. Die vorhandenen Tests decken die relevanten Commit-, Fehler- und Freigabeverträge ab. Ein neuer kompletter nativer Dateipicker-/Overwrite-/Dateisystemdurchlauf wurde in R1 nicht ausgeführt. Die historische Windows-Evidenz belegt insbesondere SaveNew und Import mit No; sie ersetzt keine vollständige Abbruchmatrix.

PasswordDialogHost und EditorReplaceConfirmation verwenden direkte Avalonia-Dialoge, nicht den defekten Prism-IDialogService-Pfad. Ihre Cancel-Close-Übergänge sind auf UiDispatch umgestellt; bestehende GUI-Tests und neue Dispatchproben decken die einschlägigen Verträge ab.

**Prism getrennt bewertet:**

- Im unveränderten Original-Paketgraphen reproduziert der neue Harness den bekannten A12-F01-Binärblocker beim tatsächlichen IDialogService-Aufruf (fehlende ReflectionBindingExtension). Er wird nicht als neues R1-Finding gezählt.
- A12-02 verwendet isoliert den offiziellen Quellcommit `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Die gespeicherten Quellen, Logs und Ergebnisdateien decken die dokumentierten Dialogregistrierungen, Titelbindung/-änderung, NoTitle, OK-Parameter, Schließen/None, OnError und fehlende Registrierung ab.
- Die abschließenden Retentionsproben zeigen nach 20 Zyklen 60 WeakReferences ohne verbleibende Targets in drei nativen Läufen; NoInlining und drei Sekunden Nachlauf sind Teil der Versuchsanordnung. Frühere 6/12 verbleibende Targets sind erhalten. Das ist begrenzte Retentionsevidenz, kein Heap-Root-Beweis und keine allgemeine Leckfreiheitsgarantie.
- Throwing OnCloseAsync, der vollständige Produktdialogkatalog und alle Plattformen sind damit nicht abgedeckt. R1 hat diesen Quellbuild nicht erneut gebaut oder integriert; hier wurden die erhaltenen A12-02-Belege unabhängig ausgewertet.

## 7. R5 – Ausgeführte Tests und Evidenzqualität

Umgebung: Windows 10.0.26200 win-x64; .NET SDK 10.0.401, MSBuild 18.9.11, Host 10.0.12. Frischer Restore mit isoliertem Cache. Ablauf in `run-tests.ps1`, Umgebung in `tests/dotnet-info.txt`, vollständige Logs/TRX und Exitcodes unter `tests/`.

| Neuer R1-Lauf | Ergebnis |
|---|---|
| GUI-Solution Restore/Build Release | Exit 0 |
| GUI-Solution Restore/Build Debug | Exit 0 |
| GUI Release | 472 gesamt, 472 bestanden |
| Coordinator separat | 35 gesamt, 35 bestanden |
| Core Release | 7.672 gesamt: 7.665 bestanden, 2 fehlgeschlagen, 5 nicht ausgeführt |
| MCP Release | 90 gesamt: 89 bestanden, 1 nicht ausgeführt |
| Core gezielt, nur PDB-PathMap geändert | 9/9 bestanden |
| Zusatzprobe Headless | 36 erfolgreiche Prüfungen und bestätigtes Import-Gegenbeispiel; Exit 2 |
| Zusatzprobe native Windows | 37 erfolgreiche Prüfungen und dasselbe Gegenbeispiel; Exit 2 |

Die Coordinator-Fälle sind eine Teilmenge der GUI-Suite und dürfen nicht als 35 zusätzliche einzigartige Tests gezählt werden. Die 469 bisherigen GUI-Fallnamen sind erhalten; genau drei Dispatcherfälle kommen hinzu. Die 35 Coordinator-Fallnamen sind identisch. `tests/test-name-comparison.json` hält den Vergleich fest.

**Core-Pfadfehler:** Die Fälle RejectsInvalidPsnWithoutNormalizing("1") und ("A1") prüfen sensible Eingaben gegen die gesamte Exception.ToString-Ausgabe einschließlich PDB-Quellpfaden. Im Reviewpfad `CryptoWorkBench-A12-01-R1-Review` kommen beide Suchwerte vor. Es handelt sich nicht um einen nachgewiesenen kryptographischen Datenabfluss. Der Original-Gesamtlauf bleibt rot und ist unverändert gespeichert.

Die gezielte Gegenprobe änderte ausschließlich den Compiler-PathMap, keinen Quelltext und keine Assertion:

```powershell
dotnet test "$lab\app\CryptoScriptUnitTest\CryptoScriptUnitTest.csproj" -c Release --no-restore "-p:PathMap=$lab\app=C:\review" --filter 'FullyQualifiedName~RejectsInvalidPsnWithoutNormalizing' --logger 'trx;LogFileName=core-pathmap.trx' --results-directory "$ev\tests"
```

Alle neun Fälle bestehen damit. Das bestätigt die Pfadursache der zwei aktuellen Fehler; es ersetzt keinen zweiten vollständigen grünen Core-Lauf. Historisch hatte A12-01 einen, A12-02 zwei entsprechende Fehlschläge. Der Parser-/Runner-/Kryptocode wurde für die Gegenprobe nicht geändert.

**Skips:** Drei Core-Fälle benötigen nicht verfügbare Symlinkrechte, einer prüft Darwin-ACLs, MeasureLocalHardware ist ein ausdrücklich manueller Hardwaretest. MCP InstalledCodexDiscoversAndCallsStatus benötigt CRYPTOWORKBENCH_CODEX_PATH und blieb unausgeführt. Kein Skip wird als PASS oder Plattformbeleg gezählt.

**Historischer Headless-Dispose-Fehler:** Das A11-Roh-TRX weist 469 Fälle, 468 bestanden und einen Fehler aus. Betroffen ist SecretImportSyntaxErrorsReachGuiWithoutShareMaterial; der Stack beginnt mit einer NullReferenceException in HeadlessUnitTestSession.Dispose. Das stützt die Einordnung als historischer Teardownfehler, nicht die Behauptung eines kryptographischen Fehlers oder einer generell behobenen Bibliotheksursache. Aktuell besteht der entsprechende Fall im vollständigen Lauf. `tests/comparison.json` enthält den extrahierten Stack und die historischen Zähler.

**Windows-Smokes:** Das A12-01-Journal ist nachvollziehbare Beobachtungsevidenz; im Chat gezeigte Screenshots liegen nicht als vollständiger eigenständiger Bildbestand vor. R1 bestätigt durch eigene native Proben Bootstrap, Working-Directory-unabhängige Ressourcen, ausgewählte Regionen, modale MessageBox-Verträge, Import-Schließen und den bekannten Prism-Binärfehler. Es beansprucht keine erneute visuelle Prüfung aller früheren Screenshots oder eine manuelle Fokus-/Dateipicker-Matrix.

**Harness-Transparenz:** Ein erster zusätzlicher Dialogversuch löste nur ein Click-RoutedEvent aus und erreichte den Command nicht; der Timeout ist als `probe-incomplete-click-attempt.log` erhalten. Korrigiert wurde ausschließlich der neue Harness auf Button.OnClick. Frühere Discovery-Logs mit bloßer Abschlussmeldung bleiben erhalten. Maßgeblich sind `probe-headless.log` und `probe-native.log` samt Exit 2: Wegen des Gegenbeispiels sind die Gesamtproben ausdrücklich nicht PASS.

## 8. Neues Finding

### A12-R1-F01 – Import wird nach Schließen der Bestätigung ausgeführt

**Severity:** Medium. **Status:** Offen, neu im R1 reproduziert; die fehlerhafte Verzweigung ist bereits in der Vormigrationslogik vorhanden.

**Datei/Position:** `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs:168–178`, insbesondere Ergebnisprüfung 171–174 und anschließender OpenScriptBookAsync-Aufruf 178.

**Technische Ursache:** Der Dialog fragt, ob vor dem Laden geleert werden soll. Nur das Ergebnis "Yes" wird speziell ausgewertet. "No", leeres Ergebnis beim Fensterschließen und sonstige Ergebnisse fallen gemeinsam in den Importpfad mit clear=false. Eine Abbruchverzweigung fehlt. Die Migration änderte die Dialoganzeige auf ShowWindowDialogAsync(owner), nicht diese Ergebnislogik.

**Reproduktion:**

1. Tatsächliche App starten, Editor-/Sidebar-ViewModels über deren echte DI auflösen und den Editor auf `BASE\n` setzen.
2. Als gewählte Testdatei ein ScriptBook mit `VAR reviewCancel=0x(AB)\n` bereitstellen.
3. Tatsächliches Sidebar-OpenScriptBook aufrufen und die sichtbare, dem Hauptfenster gehörende Bestätigung über Close schließen.
4. Den Task vollständig abwarten. In Headless und unter Windows wird der Buchtext angehängt und LastScriptBookSucceeded=true gesetzt.

Der Harness ersetzt ausschließlich den nativen StorageProvider-Auswahldialog durch einen kontrollierten IStorageFile-Rückgabewert. Er ruft die reale private Sidebar-Methode auf; dadurch wird nur die äußere Command-Auslösung umgangen, nicht deren Import-/Ausführungspipeline. Die Providerreferenz wird anschließend wiederhergestellt. Testdatei und Build bleiben in der isolierten Umgebung.

Konkrete Ausgabe beider abschließenden Proben:

```text
IMPORT_CLOSE_OBSERVATION imported=True succeeded=True text=BASE|VAR reviewCancel=0x(AB)|
COUNTEREXAMPLE import continues after closing confirmation
```

**Auswirkung:** Der Benutzer kann die Bestätigung schließen und dennoch das ScriptBook laden und ausführen. Abhängig vom Buch sind über den unerwarteten Editoreintrag hinaus Interpreter-/Scripteffekte möglich. Das ist ein Abbruch-/Intentfehler, kein nachgewiesener Authentisierungsbypass. Die Probe verwendet nur eine lokale ungefährliche Variablendefinition.

**Empfehlung für einen gesonderten Korrekturauftrag:** Ergebnisse explizit unterscheiden: Yes = leeren und importieren; No = anhängen und importieren; Schließen/kein Ergebnis = ohne Dateilesen, Parse-/Runnerstart oder Mutation zurückkehren. Regressionen für Yes, No, X/Close und Owner-Schließen aufnehmen; Escape-Verhalten separat bestimmen und prüfen. Der vorhandene Save-Overwrite-Pfad bietet bereits eine explizite Nicht-Yes-Abbruchbehandlung.

**Abgrenzung:** Keine CJK-/IME-Frage, kein Prism-Binärproblem, kein neuer Core-Fehler. Der alte Quellstand hat dieselbe fehlende Abbruchverzweigung; ein ursprünglicher nativer Avalonia-11-Lauf dieses Falls wurde hier nicht wiederholt. Der aktuelle Fehlpfad ist direkt nachgewiesen. Er fällt in R4 und ist konkret korrigierbar, auch wenn er nicht erst durch Avalonia 12 eingeführt wurde. Deshalb verlangt die Gesamtbewertung eine Korrektur statt einer uneingeschränkten nächsten Phase.

## 9. Bekannte offene Punkte und Empfehlung

| Punkt | Einordnung |
|---|---|
| A12-F01, veröffentlichtes Prism-Binary | Bekannt, im tatsächlichen Worktree erneut reproduziert. A12-02-Quellkandidat technisch positiv belegt, nicht integriert. |
| Prism-Lizenz/Distribution | Separat offen. Keine neue rechtliche Bewertung, keine Integrations- oder Veröffentlichungsfreigabe. |
| Linux/macOS | **OPEN**: keine realen R1-Läufe, keine plattformweite Ressourcen-/Clipboard-/Dialog-/Fokusfreigabe. |
| CJK-IME | Bestehende ausdrückliche Risikoakzeptanz unverändert. |
| Core-Testpfade | Bekannte Testrobustheitsschwäche, aktuelle Ursache durch PathMap-Gegenprobe belegt. Keine stillschweigende Testkorrektur. |
| Vollständige Windows-Dialog-/Fokusmatrix | **OPEN** außerhalb der konkret ausgeführten Proben; insbesondere native Picker-/Overwrite-Abbrüche, Escape und vollständiger Produktdialogkatalog. |
| A12-02-Buildabhängigkeit/Provenienz | Die erhaltene NU1902-Warnung zu Microsoft.Build.Tasks.Git 10.0.300 (SourceLink-Buildpfad) bleibt ein bekannter CI-/Release-Prüfpunkt; nicht unterdrückt oder korrigiert. Paketidentität, Signing, SBOM und frische CI bleiben offen. |
| Publish/Trimming/AOT und allgemeine Leckfreiheit | Durch diese Reviewläufe nicht nachgewiesen. |

MCP- und Coordinator-Kompatibilität sind im geprüften Umfang erhalten: unveränderte Kernverträge, elf kontrollierte UI-Übergänge, 472 erfolgreiche GUI-Fälle, 35 erfolgreiche Coordinator-Fälle und 89 erfolgreiche MCP-Fälle. Die offene Codex-Integration und Plattformskips bleiben ausdrücklich unbestätigt.

Empfohlene nächste technische Arbeit ist ein eng begrenzter Korrekturauftrag für A12-R1-F01 mit Abbruchregressionen und anschließendem Re-Review. Danach können die verbleibende Windows-Matrix und echte Linux-/macOS-Nachweise geplant werden. Eine dauerhafte Prism-Integration benötigt unabhängig davon eine separate Eigentümerentscheidung zu Lizenz und Distribution. Dieser Review startet keine dieser Implementierungsarbeiten.

Die oben genannte technische Bewertung ist keine Freigabe für Produktintegration, Redistribution oder Release.

