# MCP-01.4 – Editor-Adapter, Revisionen und GUI-Save-Commit

Stand: 23.10.2026. Implementiert und lokal auf Windows/.NET 10 validiert; der unabhängige Review erfolgt separat.

## Ausgangsstand und Änderungsgrenze

Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, unveränderter HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Der Worktree war beim Einstieg sauber. Grundlage sind MCP-00-R1, der Implementierungsplan und die MCP-01.3-Validierung einschließlich Review/Korrektur/Re-Review. Keine Änderungen aus master übernommen; kein Commit, Push, Merge, Rebase oder Reset. `.codex/config.toml` wurde weder gelesen noch verändert.

Es gibt keinen produktiven HTTP-Start, keine neue öffentliche MCP-Toolregistrierung, keine Paket-/Projektänderung und keine Core- oder Sprachänderung. Externe Editor-Ausführung bleibt geschlossen. Die nativen Closing-/HTTP-Shutdown-Anschlüsse bleiben MCP-01.6.

## Geänderte Dateien und Komponenten

Alle Pfade relativ zur Repositorywurzel:

| Datei | Funktion |
| --- | --- |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` (neu) | Tatsächlicher AvaloniaEdit-TextEditor als kleine abgeleitete Klasse: Dokumenttausch vor Änderung sperren, direkte TextDocument-Mutationen vor Text-/Anchoränderung zurückweisen, Eingabe und Undo-/Redo-Kommandos sperren. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs` (neu) | Registrierung, Dokumentidentität, Revisionen, atomare UI-Snapshots und lebenszyklusgebundene Präsentation. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs` (neu) | Lokaler modaler Vorher-/Nachher-Dialog mit vollständigen Texten und Client-/Editor-/Revisionsangabe. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs` (neu) | Autorisierte Reads, Insert/Append/Replace und lokale Editor-Gesamtausführung am bestehenden Auftragsregister. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` (neu) | Hostinterner SaveIntent/SaveTarget, fail-fast Admission, Commit-Sperre, Snapshot-Writer und Save-Drain. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` | Editor-Platzhalter ersetzt; Editorverfügbarkeit im Status, lokale Editorrechte, Save-Completion in Drain und Deregistrierung beim Disposal. Bestehende Registry-/Retention-/Cancellation-Algorithmen bleiben erhalten. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | Registrierung und Präsentationsinterface; Save-Orchestrierung vor erstem await; gemeinsamer direkter Append-Pfad; History-Guard; lebenszyklusgebundene Import-/Ausgabeeinfügungen. |
| `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` | Picker/Überschreibentscheidung liefern einen gebundenen Zielauftrag; Delete findet erst hinter der Commit-Prüfung statt. Nur ausdrückliches Yes erlaubt Overwrite. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml` | Verwendet den produktiven `WorkbenchTextEditor`, weiterhin AvaloniaEdit 11.4.1. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | Attach/Detach sowie Lebenszyklusprüfung vor verzögerter Anchor-Ausgabe; ein später Detach einer alten View darf die Nachfolge-View nicht abmelden. |
| `CryptoWorkBenchGuiTest/EditorAutomationTests.cs` (neu) | 47 Headless-Fälle mit produktiver XAML-View, tatsächlichem Editor, ViewModel, History, Dialogen, Writer und kontrollierten Rennen. |
| `docs/mcp/MCP-01.4-validation.md` (neu) | Dieser Bericht. |

Der Service referenziert kein Editor-ViewModel. Die Bridge trägt ein kleines hostinternes Präsentationsinterface, das das bestehende VM implementiert. Die vorhandene gemeinsame Serviceinstanz und deren DI-Registrierungen bleiben erhalten. Es entsteht kein DI-Zyklus und kein zweiter Interpreter- oder Variablenzustand.

## Editor-Bridge und Revisionen

Die View registriert ihren tatsächlichen Editor beim Attach. Beim Detach werden Eventhandler abgemeldet, die Registrierung ungültig und die VM-Referenz entfernt. Jede neue Registrierung bzw. jeder Dokumenttausch erhält eine neue editorId. Innerhalb der Registrierung steigen Revisionen monoton; beim Dokumenttausch wird ebenfalls erhöht. Ein fehlendes Document liefert EditorUnavailable; eine spätere Dokumentzuweisung erhält eine neue Identität. Service-Disposal meldet die Registrierung auf dem Dispatcher ab und verhindert neue Attach-Annahmen.

Die Bridge beobachtet `TextDocument.Changed` und `TextEditor.DocumentChanged`. Damit zählen Benutzereingabe, Paste, Undo/Redo, History, Import, Anchor-Ausgabe und programmgenerierte Einfügungen. Zusätzlich wird die tatsächliche `TextDocument.Version` verglichen. Dies verhindert, dass ein früher abonnierter Changed-/DocumentChanged-Handler bei reentrantem Lesen neuen Text mit alter Revision erhält; der spätere Bridge-Handler zählt dieselbe Änderung nicht doppelt.

Text, Länge, Identität, Revision und IsModified werden gemeinsam auf dem UI-Dispatcher aufgenommen. Längen/Offsets sind .NET-Stringlängen, also UTF-16-Codeeinheiten. Lesen verändert weder Text noch Auswahl, Caret, Undo-Stack oder IsModified. Stabile wiederholte Reads liefern denselben Snapshot. Leserecht ist `editor.read`; Texte können geheime Literale enthalten. Übergroße Read-Antworten liefern LimitExceeded.

Ohne belegte Save-Baseline gibt es keinen Clean-Nachweis: IsModified oder beobachtete Änderungen ergeben Dirty, andernfalls Unknown. Auch Undo oder ein manuell zurückgesetztes IsModified erfinden keine saubere Baseline. ASCII-Konvertierung, Zeilenfilter und Append begründen keine verlustfreie Save-Baseline; Save setzt weder IsModified noch Dirty pauschal zurück.

## Editoroperationen und Bestätigung

Insert fügt exakt am angegebenen Offset ein, Append exakt am Ende ohne zusätzlichen Zeilenumbruch, Replace ersetzt den gesamten Inhalt. Instanz, Editoridentität und erwartete Revision werden geprüft. Die vorhandene Vertragsvalidierung prüft Modus/Offsetkombination und Eingabegrenzen; die Hostprüfung verweigert Offsets außerhalb des Dokuments sowie innerhalb von Surrogat- und CRLF-Paaren. Konkurrierende Admission und reentrante Mutationen während eines Dokumentupdates werden mit Busy abgewiesen.

Jeder reale MCP-Edit verwendet ein `RunUpdate` als Undo-Gruppe und markiert dirty. Identischer Replace sowie leere Inserts/Appends sind nach gültiger Revisionsprüfung No-ops: keine neue Revision, keine weitere Undo-Aktion und kein Dialog. Die Änderungen erzeugen kein Return-Ereignis und lösen weder Ausführung, Save noch Variablen-Clear aus.

Für reales Replace auf nichtleerem oder nicht nachweisbar sauberem Inhalt erscheint die lokale Vorher-/Nachher-Bestätigung. Der private unveränderliche Auftrag hält Principal, instanceId, editorId, erwartete Revision, Modus/Offset und den exakten vollständigen neuen Text. Dadurch ist die Zustimmung direkt an dieselben Inhalte gebunden; ein clientseitiger approved-/force-Schalter existiert nicht. Nach Zustimmung werden Rechte/Aktivierung, Lifecycle, Registrierung, Revision und Änderbarkeit erneut auf dem Dispatcher geprüft. Benutzeränderung während des Dialogs ergibt RevisionConflict, Nachfolgeeditor EditorUnavailable; Ablehnung und fehlender Owner ergeben ConfirmationDenied beziehungsweise ConfirmationUnavailable. Keine spätere Zustimmung wird auf einen neuen Editor übertragen.

Die Admission bleibt während der Replace-Bestätigung reserviert, ohne einen Interpreter-Scope zu öffnen. Normales Tippen bleibt möglich und invalidiert die erwartete Revision. Alle Ausgänge geben die Reservation frei.

## Tatsächliche Ausführungspfade

`ExecuteEditorAsync` nimmt eine unveränderliche, revisionsgebundene Textkopie auf und prüft erwartete stateVersion. Eine vorhandene executionId wird vor erneuter Admission mit dem Fingerprint des ursprünglichen EditorExecution-Auftrags abgeglichen: derselbe Auftrag liefert das vorhandene Resultat, geänderte Argumente ergeben InvalidArgument. Die bestehenden Tombstone-/Retention-/Ownerregeln bleiben erhalten.

Die Annahme verwendet dieselbe `Accept`-/`RunOwnedAsync`-Registry und dieselbe `InterpreterHostQueue.Reserve`-Admission wie GUI/Text-Ausführung. Ein Hostlauf umfasst das ganze Buch; Resultate tragen Source=Editor, editorId und SourceRevision. Der kopierte Text wird über `ExecuteScriptBookAsync` und `WorkbenchExecutionPipeline.ExecuteAsync` pro physischer Zeile ausgeführt, mit den bisherigen frischen Runnern und demselben prozessweiten Variablenbestand. Die exakte ordinale out:-Skip-Regel bleibt erhalten. Kein implizites Clear und kein erneutes Einfügen der ausgeführten Quellzeilen. Print wird einmal eingefügt; Status, Variablen und Diagnosen verwenden die bestehende GUI-Präsentation. Nach Detach/Dokumenttausch werden keine Ausgaben in einen Nachfolgeeditor geschrieben.

Die serviceeigene Cancellation, Deadline, Passwort-Quarantäne, Output-/Diagnoseprojektion, Idempotenz und Ergebnisretention stammen unverändert aus MCP-01.3. Request-Cancellation beendet nach Annahme weiterhin nur den Antwort-Wait. Ausgabe in Standardresultaten bleibt redigiert, IncludeText ist in dieser Phase weiterhin verboten.

**Externe Editor-Ausführung ist auch mit aktiviertem Epoch und execution.run-Recht gesperrt.** Bei vorhandenem Editor liefert sie Forbidden; ohne Editor bleibt die bestehende Fehlerpriorität EditorUnavailable erhalten. Es gibt keinen Ausführungspfad zu Interpreter/Admission für diese externen Aufrufe. Die authentisierte Freigabe samt lokaler auftragsbezogener Ausführungsbestätigung kommt erst in MCP-01.5.

## SaveIntent und Commit-Grenze

Produktiver Pfad:

`SaveScriptBookCommand → GuardAsync → SidebarViewModel.SaveScriptBook → CryptoScriptEditViewModel.SaveScriptBookAsync → CaptureSaveIntent → SelectSaveTargetAsync → CommitSaveAsync → DeleteAsync → WriteSnapshot`.

Die private Hostcapability SaveIntent enthält die konkrete Registrierung und einen unveränderlichen EditorSnapshot mit editorId, erwarteter Revision und Text. Die synchrone Aufnahme erfolgt vor Aufruf des Picker-Delegates und damit vor dem ersten await der gesamten GUI-Befehlskette. Ordnerzugriff, Vorschlagsordnererstellung, Picker und eigener Überschreibdialog halten keine Admission. Benutzeränderungen sind erlaubt. Der ausgewählte lokale Pfad, der konkrete Zielhandle im Delete-Delegate und die ausdrücklich bestätigte Überschreibentscheidung werden zusammen als unveränderlicher SaveTarget weitergegeben.

Nach den Dialogen nimmt CommitSaveAsync nicht wartend die gemeinsame Admission. Noch vor Delete oder File.Open prüft ein UI-Schritt Lifecycle/Cancellation, Zielbestätigung, Registrierung/editorId und Revision und nimmt die editorbezogene Mutationslease. Busy, RevisionConflict, EditorUnavailable, ShuttingDown, Cancellation oder fehlende Zustimmung erzeugen weder Delete noch Streamöffnung. Es gibt keinen stillen Retry mit neuem Inhalt oder anderem Ziel. Ein bereits angelegter Vorschlagsordner bleibt wie bisher bestehen.

Mit aktivierter Lease wird ein serviceeigener Completion-Task veröffentlicht. Die Lease und Admission bleiben über das asynchrone Delete und den anschließenden Worker-Write hinweg aktiv. Nach dieser Commit-Grenze werden weder Request-/Benutzer-Cancellation noch Shutdown zwischen Delete und Write erneut geprüft: der begonnene Speicherversuch wird bis Erfolg oder Fehler finalisiert. Erst nach UI-Freigabe der Lease und Admission wird Save-Completion signalisiert. `DrainAsync` und `DisposeAsync` warten auch auf diese Finalisierung. Save eröffnet keinen Interpreter-Scope und erhöht stateVersion nicht.

Detach invalidiert die Registrierung sofort für weitere Operationen, gibt aber eine bereits gehaltene Commit-Lease nicht vorzeitig frei. Der Save besitzt weiterhin ausschließlich seinen bestätigten Text und die Lease seines alten Controls. Freigabe betrifft nur dieses Control. Ein späterer Editor wird nicht durch den alten Save verändert.

Der bestehende synchrone direkte `SaveScriptBook(path)`-Append-Einstieg benutzt denselben Coordinator, dieselbe Admission und denselben Snapshot-Writer. Capture und Write bilden dort einen synchronen UI-Schritt ohne Picker, await oder Interleaving. Die produktive View erhält auch dort ihre Mutationslease. Der unveränderte alte Return/Save-Test darf weiterhin einen normalen AvaloniaEdit-TextEditor synchron übergeben; das eröffnet keinen asynchronen Delete-/Dialogpfad ohne Sperre.

## Durch die Commit-Sperre geschützte Wege

| Mutationsweg | Schutz |
| --- | --- |
| Tippen/Return/IME-Text | TextEntering wird während der Lease behandelt; zusätzlich read-only TextArea. Die View respektiert Handled und startet keinen Return-Lauf. |
| Paste, Cut, Ersetzen der Auswahl, sonstige direkte TextDocument-Insert/Remove/Replace-Aufrufe | UI-read-only für reguläre Eingabe plus `TextDocument.Changing`-Barriere vor Text-/Anchoränderung. Direktes SelectedText/Document.Insert wird ebenfalls abgefangen. |
| Undo/Redo über Editor-/UI-Kommandos | Die produktiven Defaultbindings werden durch vorab prüfende Bindings ersetzt. Bei Sperre keine Stackoperation; nach Freigabe sind ursprüngliche Undo-/Redo-Einträge weiter nutzbar. |
| History | Gemeinsames IsExecuting/IsBusy verhindert die Einsetzung; die Dokumentbarriere schützt zusätzlich direkte Einfügungen. |
| Return-Anchor-/Print-Ausgabe, Import-/Buchzeilen und programmatische Ausgaben | Gemeinsame Admission verhindert neue Ausführung während Save; direkte Dokumentänderungen treffen zusätzlich die Barriere. Verzögerte Ausgaben sind an Dokument/Lifecycle gebunden. |
| Document-Ersatz | Coercion des tatsächlichen Editor-DocumentProperty weist den Tausch vor der effektiven Zuweisung zurück. |
| Austausch der Editorreferenz im VM | Setter weist einen Nachfolgeeditor während der Lease zurück. Lebenszyklusbedingtes Detach darf die alte Lease weiter finalisieren. |
| MCP Insert/Append/Replace, GUI-/Editorlauf und Variablenmutation | Dieselbe prozessweite nicht wartende Admission liefert Busy. |

Die Sperre stützt sich ausdrücklich auf mehrere Grenzen und nicht allein auf IsReadOnly. Die produktiven Undo-/Redo-Kommandos sind entscheidend: die reine Changing-Barriere verhindert Textänderung, wäre jedoch für den Erhalt der Undo-Historie allein zu spät. Hostcode soll für Undo/Redo diese Editor-Kommandos verwenden und nicht den rohen Bibliotheks-UndoStack während einer Commit-Lease manipulieren.

## Erhaltene GUI- und Dateisemantik

Return/PendingExecution/TextAnchor, ScriptBook-Import, History, Print/Info, Variablenansicht, Diagnosepräsentation, Passwortdialoge, Benutzer-Cancellation und bestehende generische Fehleranzeige bleiben erhalten und sind durch die vollständige GUI-Suite abgedeckt. Ein alter View-Detach und alte Import-/Anchorarbeit können keine neue View abmelden oder mit altem Text verändern.

Der Writer liest ausschließlich den übergebenen String-Snapshot. Er öffnet weiterhin `FileMode.Append`, schreibt pro nichtleerer/nicht ausschließlich aus Whitespace bestehender physischer Zeile `Encoding.ASCII` plus ein LF. Bestehende Ziele werden nach ausdrücklicher Bestätigung zuerst über den ausgewählten Storage-Handle gelöscht. Kein UTF-8-Wechsel, Truncate, atomarer Replace oder CryptoScript-Save/CreateNew. Unicode-Fallback, Leerzeilenfilter, CRLF→LF und direktes Append sind gegen Dateibytes geprüft.

**Die Dateioperation ist nicht transaktional.** Ein Write-Fehler nach erfolgreichem Delete kann die alte Datei verlieren. Der Fehlernachweis löscht einen Sentinel und erzwingt danach einen Open-/Write-Fehler; er behauptet weder Rollback noch Clean-Zustand. Delete-/Write-Fehler geben Admission und Editorlease frei. Externe Dateisystemrennen werden durch Editorrevisionen nicht verhindert.

## Automatisierte Nachweise

47 neue Fälle in `EditorAutomationTests` verwenden das vorhandene Avalonia.Headless-Harness und die produktive XAML-View mit WorkbenchTextEditor/AvaloniaEdit, keine vereinfachte Editorattrappe. Die steuerbare Bestätigungsimplementierung ergänzt echte Dialogfälle; verzögerte Delete-Delegates halten genau die produktive Commit-Grenze offen. Die Dateischreibseite ist der reale Writer.

Abgedeckt sind:

- Attach/Detach, fehlendes Document, Dokumenttausch, Service-Disposal und später Detach einer alten View.
- Revisionen bei echten Documentänderungen, Undo/Redo, History und Outputs; echte Return-/Anchor-Ausgabe; atomare Reads auch von einem Worker und aus früher abonnierten Change-Handlern.
- Insert/Append/Replace, No-ops und einzelne Undo-Gruppen; neun UTF-16-/Surrogat-/CRLF-Grenzfälle.
- Exakter lokaler Vorher-/Nachher-Dialog mit Zustimmung/Ablehnung; inhaltlich gebundene Testzustimmung, Benutzeränderung während Zustimmung und Detach vor Antwort; fehlende Bestätigung.
- Unveränderliche Gesamteditor-Ausführung mit out:-Skipping, gemeinsamer Variablenpräsentation, genau einer Print-Einfügung und Registry-Idempotenz; StateConflict, RevisionConflict und gesperrte externe Ausführung.
- Produktiver Save-Orchestrator vor Picker und Überschreibdialog; veraltete Revision ohne Delete/Write und mit erhaltenem Sentinel.
- Acht Mutationsrennen während verzögertem Delete: Eingabe, Paste/SelectedText, wiederholte produktive Undo-/Redo-Kommandos, History, Ausgabe, Document-Ersatz und VM-Editorwechsel. Dazu Busy für MCP-Änderung/Variablenmutation, responsiver Dispatcher und weiter funktionierende Undo-/Redo-Historie nach Save.
- Bestätigter Snapshot, ASCII/Whitespace/LF/Append, erhaltene Dirty-/Revisioninformation; Deletefehler, Fehler nach Delete und sichere Freigabe.
- Shutdown, Cancellation und Detach nach Beginn des Commits: Drain/Dispose bleiben offen bis zur Finalisierung; kein Abbruchloch zwischen Delete und Write. Verweigerung, Busy, vorherige Cancellation, Detach oder Shutdown vor Commit bewirken keine Zieloperation.

## Ausgeführte Validierung

Logs/TRX liegen im lokalen ignorierten Verzeichnis `artifacts/mcp014/`.

| Prüfung | Ergebnis / Nachweis |
| --- | --- |
| Restore `CryptoWorkBench.sln` | erfolgreich, `restore-core.log` |
| Restore `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | erfolgreich, `restore-gui.log` |
| Release-Build Root-Solution | erfolgreich, 0 Fehler, `build-core.log` |
| Release-Build GUI-Solution, abschließender Stand | erfolgreich, 0 Fehler, `build-gui-final.log`; acht vorhandene Nullable-Warnungen in View/MainViewModel/Sidebar, keine neue Warnungsstelle |
| Vollständige Core-Suite | **7 667 bestanden, 4 übersprungen**, 0 fehlgeschlagen, `core.trx` / `core.log` |
| Vollständige GUI-Suite, abschließender Stand | **385 bestanden**, 0 fehlgeschlagen/übersprungen: 338 bestehende + 47 neue Fälle, `gui-final.trx` / `gui-final.log` |
| Vollständige reguläre MCP-Suite | **89 bestanden**, 0 fehlgeschlagen, `mcp.trx` / `mcp.log` |
| Neue MCP-01.4-Fälle separat, abschließender Stand | **47 bestanden**, `mcp014-final.trx` / `mcp014-final.log` |
| Projektabhängigkeitskontrolle | Core und Abstractions ohne Projektreferenz; MCP ausschließlich → Abstractions; GUI → Core/Abstractions. Keine csproj-/Solution-/Paketänderung. |
| `git diff --check` | erfolgreich |

Damit bestehen insgesamt **8 141 reguläre Fälle**, die 47 neuen sind bereits in der GUI-Zahl enthalten. Die vier bisherigen Core-Skips sind der Darwin-ACL-Fall und drei Windows-Symlinkfälle ohne entsprechendes Privileg. Die expliziten Hardwarebenchmark-/Installed-Codex-Fälle wurden nicht aktiviert. Keine bestehenden Tests wurden entfernt, deaktiviert oder verändert. Core/MCP-Produktionsquellen blieben auch nach dem vollständigen jeweiligen Suitelauf unverändert; die abschließenden Korrekturen betrafen ausschließlich GUI-Code/-Tests, danach wurden GUI-Build, vollständige GUI-Suite und die neuen Fälle erneut ausgeführt.

Die wesentlichen Befehle waren `dotnet restore` für beide Solutionpfade, `dotnet build <solution> -c Release --no-restore` und `dotnet test <testprojekt> -c Release --no-build --no-restore --logger trx` für die drei Testprojekte. Der separate neue Lauf verwendete `--filter FullyQualifiedName~EditorAutomationTests`. Zusätzlich wurden die Referenzen mit `dotnet list <projekt> reference` und die unveränderten Projektdateien per Git geprüft.

### Zwischenfehler und Korrekturen

Zwischenstände bleiben in den lokalen Logs sichtbar; sie sind keine bestandenen Endnachweise:

1. Der erste Build zeigte fehlende `CryptoScript.Model`-Imports für ExecutionBusyException; korrigiert ohne Vertragsänderung.
2. Der erste neue Test erwartete Unknown trotz bereits von der View gesetztem IsModified. Der neue Test stellt jetzt ausdrücklich einen unmarkierten Zustand ohne Baseline her und prüft dafür Unknown. Ein synchrones NUnit-Async-Assert innerhalb des UI-Dispatchers blockierte beim künstlichen Writefehler; auf das tatsächlich awaitbare Assert.ThatAsync umgestellt. `mcp014-diagnostic.trx` und der Blame-Dump protokollieren diesen Testfehler; dies war keine Headless-Dispose-NRE.
3. Zwei Unicode-Testliterale waren in einer lokalen Bearbeitungskopie falsch kodiert. Sie sind als explizite C#-UTF-16-Literale wiederhergestellt und die Offset-/ASCII-Byteprüfungen bestehen (`mcp014-second.trx` dokumentiert den vorherigen Stand).
4. Erweiterte Undo-/Redo-Prüfungen zeigten, dass ein erst im Changing-Event verworfener Undo die Historie bereits verbrauchen kann. Zwischenversuche mit Stacktausch oder offenem Undo-Group sind verworfen. Der endgültige Code ersetzt die produktiven Defaultbindings und prüft vor dem Stackzugriff. Er tauscht keinen Undo-Stack aus und hält keine synthetische Undo-Gruppe während Save. Erhalt und Nutzbarkeit beider Historien nach Save sind nun geprüft (`mcp014-third` bis `mcp014-sixth` und `mcp014-command-lock.trx`).
5. Der echte Dialogtest musste die Dispatcher-Anzeige abwarten, bevor OwnedWindows geprüft werden kann; danach bestehen beide Auswahlfälle (`mcp014-expanded-final.trx`).
6. Im ersten vollständigen GUI-Lauf erwartete der bestehende MCP-01.3-Fall bei autorisiertem externem Aufruf ohne registrierten Editor weiterhin EditorUnavailable, statt des zunächst gelieferten Forbidden. Die bisherige Fehlerpriorität ist erhalten, ohne externe Ausführung bei vorhandenem Editor zu öffnen. Der unveränderte alte Test und der neue Forbidden-Fall bestehen im vollständigen Endlauf.

Die in MCP-01.3 dokumentierten sporadischen `HeadlessUnitTestSession.Dispose`-NullReferenceExceptions bleiben ursächlich ungeklärt. In den MCP-01.4-Läufen trat diese Dispose-NRE nicht auf. Das ist kein allgemeiner Nachweis, dass der bekannte Lifecycle-Race behoben ist. Kein Master-Backport und keine Abschwächung bestehender Tests.

## Verbleibende Grenzen und Übergabe

- MCP-01.5 muss authentisierte externe Caller, lokale Ausführungsbestätigung, Ausgabe-Freigabe und die HTTP-Toolanbindung ergänzen. Die heutige externe ExecuteEditor-Sperre darf erst zusammen mit diesen Schutzschritten geöffnet werden.
- MCP-01.6 muss natives Closing/ShutdownRequested vorläufig abweisen, StopAccepting/BeginShutdown und den erweiterten Drain vollständig abwarten, bevor Dispatcher/Controls/Webhost endgültig beendet werden. Auch laufende Dialoge und Aktivierungswiderruf sind dort vollständig einzubinden. Der hiesige Headless-Nachweis betrifft den Servicevertrag und Save-Lease, keine native Betriebssystem-Closing-Garantie.
- Ein nicht zurückkehrendes Delete kann Drain absichtlich offen halten. Ein erzwungener Prozess-/Betriebssystemabbruch garantiert keinen abgeschlossenen Write. Die nichttransaktionale Dateisemantik und externe Dateisystemrennen bleiben bestehen.
- Für Dirty bleibt die Erkennung bewusst konservativ; dieser Schritt führt keine verlustfreie Save-Baseline ein. Raw-UndoStack-Manipulation ist kein produktiver Hostweg; produktive Undo-/Redo-Aufrufer müssen die geschützten Editor-Kommandos verwenden.
- Es erfolgte eine lokale Windows-Validierung, keine neue Ausführung der macOS-/Linux-CI und kein produktiver HTTP-/Installed-Codex-Roundtrip. Die bestehende Print-/Info-Multicast-Grenze aus MCP-01.3 wird hier nicht allgemein umgebaut.

MCP-01.4 endet mit diesen Änderungen und Nachweisen. Ein unabhängiger Review wurde nicht vorweggenommen.


---

## Korrekturstatus R1–R5 nach unabhängigem Review – 24.10.2026

**Implementierungsstatus: READY FOR RE-REVIEW. Keine unabhängige Freigabe.** Der vorstehende ursprüngliche Implementierungsnachweis bleibt unverändert als Historie erhalten. Maßgeblich für den korrigierten Quellstand sind dieser Nachtrag und der entsprechende Korrekturanhang in `MCP-01.4-review.md`; dessen ursprüngliche Entscheidung CHANGES REQUIRED und sämtliche Findings bleiben ebenfalls unverändert.

### Ausgangsstand und Umfang der Korrektur

Vor Änderungen wurden der tatsächliche Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0` und alle vorhandenen uncommitted MCP-01.4-Dateien geprüft. Ein SHA-256-Ausgangssnapshot umfasst 747 Quelldateien/Dokumente, ohne Lesen der ausgeschlossenen `.codex/config.toml`. Beide Originalberichte wurden zusätzlich bytegetreu gesichert.

Gegenüber diesem Ausgangssnapshot wurden ausschließlich die folgenden sechs Produktionsdateien geändert, eine neue Testdatei angelegt und die beiden angeforderten Dokumente ergänzt:

| Datei | Korrektur und aktuelle Einstiegslinien |
| --- | --- |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | R2/R4: frühe Dokument-Guards, `BeforeUpdate` ab :83, `BeforeChange` :94, `LockIdentity` :100, `AllowSingleEditChange` :110, verzögertes Lifecycleende ab :118, Save-Lease :145. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs` | R4: Editorwechsel während Identity-Lease verweigern, Detach ab :87 bis zum Ende des synchronen Edits aufschieben; R5: `BeginExecution` im lokalen Präsentationsinterface :15. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs` | R4: ursprüngliche Dokumentbindung und unveränderlicher Mutationsabschluss ab :100; R1: `ContinueEditor` :206 und Zeilengrenze :224; R5: einmalige Initialisierung :217–221. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs` | R1: serviceeigene Cancellation unmittelbar nach dem beforeLine-Callback erneut prüfen, :47–59; verhindert den tatsächlichen Parse-/Runneraufruf. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` | R3: Completion vor Lease-Callbacks veröffentlichen, :50–59; Rollback/Finalisierung :74–89; derselbe Schutz für direkten synchronen Append :95–125. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | R4: Detach bis zum Editabschluss aufschieben :50–61 und Referenzwechsel während Identity-Lease verweigern im TextEditor-Setter; R5: `BeginExecution` :79–83. |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs` (neu) | 12 Testmethoden, parametrisiert 49 neue Fälle, echte XAML-View und tatsächlicher WorkbenchTextEditor. |
| `docs/mcp/MCP-01.4-validation.md`, `docs/mcp/MCP-01.4-review.md` | Ausschließlich getrennte Nachträge; ursprüngliche Inhalte bytegetreu als Präfix erhalten. |

`WorkbenchAutomationRegistry`, Sidebar, View, die bisherigen 47 EditorAutomationTests und alle übrigen bestehenden Tests bleiben gegenüber dem Korrektur-Ausgangsstand unverändert. Ebenso unverändert: CryptoScript, Abstractions, MCP-Transport, Projektdateien, Pakete und Solutions. Kein allgemeines Refactoring, keine Masterübernahme, kein Commit/Push/Merge/Rebase/Reset und kein Beginn von MCP-01.5. Die Konfiguration wurde weder gelesen noch verändert.

### R1 – Editoridentität steuert die tatsächliche Ausführung

Ein ungültiger Editor wird im zugelassenen lokalen Lauf über `RequestCancel` und die **serviceeigene** Execution-CancellationSource kontrolliert abgebrochen. Der Auftrag erhält den terminalen Zustand Cancelled; Request-Cancellation wird dafür nicht verwendet. Die gemeinsame Buchschleife prüft diesen Token zusätzlich unmittelbar nach dem UI-beforeLine-Callback, bevor sie die Pipeline überhaupt aufruft. Auch reentrantes Invalidieren aus `Presentation.Begin` wird vor dem Parse erfasst.

Detach/Dokumenttausch vor dem ersten Runner erzeugt keine Variable, Effects.None und keine stateVersion-Erhöhung. Nach einer bereits ausgeführten Zeile bleiben ihre Wirkung und die eine Versionsgrenze erhalten, weitere Runner werden verhindert. Ergebnisregister, Admission, Retention und idempotenter Retry bleiben über dieselbe Accept-/RunOwned-Pipeline erhalten. Veraltete Präsentation wird unterdrückt und nach Präsentationscallbacks erneut identitätsgeprüft. Eine schon erfolgreich abgeschlossene **letzte** Parse-Einheit wird durch einen erst danach erfolgenden Viewwechsel nicht rückwirkend auf Cancelled gesetzt; bei noch ausstehenden Zeilen greift deren nächste verbindliche beforeLine-Grenze.

Nachweise: `R1InvalidIdentityStopsBeforeFirstOrNextRunnerAndRetryDoesNotRun` :82, vier Vorstart-/Zwischenzeilenvarianten mit Retry, sowie `R1DetachAfterCompletedFinalRunnerDoesNotRewriteSuccessfulOutcome` :111, zwei Abschlusskontrollen.

### R2 – Textsetter/Clear vor Historieneffekten sperren

Der Guard wird über die Metadaten-Coercion schon beim Binden eines Dokuments installiert, bevor TextArea ihre optionalen Caret-/History-Updatebeobachter anmeldet. Während der Save-Lease verwirft `BeforeUpdate` bereits den Beginn eines Dokumentupdates. Dies erfasst auch `Text=""` auf einem leeren Dokument, direkte `Document.Text`-Zuweisung, Clear und SelectedText, bei denen möglicherweise kein Changing folgt. Der reale geerbte Setter bleibt dadurch auch bei einer als `TextEditor` deklarierten Referenz geschützt; es wird keine Property nur versteckt.

AvaloniaEdit 11.4.1 öffnet in BeginUpdate seine eigene Update-/Undo-Gruppe **vor** UpdateStarted und beginnt das callerseitige finally erst danach. Der Guard schließt deshalb genau dieses soeben von der Bibliothek eröffnete, noch nicht wirksam gewordene Update mit EndUpdate und wirft dann ExecutionBusyException. Er erstellt keinen Ersatz-UndoStack, keine synthetische Reparaturgruppe und keine zusätzliche Gruppe für die Dauer des Save. Damit bleiben Text, Caret, Revision, IsModified und nutzbare Undo-/Redo-Einträge erhalten. Die frühere Changing-Barriere bleibt als ergänzende Grenze bestehen. Die produktiven vorab prüfenden Undo-/Redo-Kommandos sind unverändert erhalten.

Nachweise: `R2EmptySetterAndClearPreserveHistoryThroughConcreteAndBaseReferences` :134, zwölf Undo-/Redo-/Concrete-/Base-/Clear-/Document-/SelectedText-Varianten, und `R2IdenticalNonemptyTextAssignmentDoesNotTouchTextCaretOrUndo` :174, zwei identische nichtleere Textsettervarianten. Nach jedem abgewiesenen Setter wird auch IsInUpdate=false und die tatsächliche nachfolgende Undo-/Redo-Ausführung geprüft.

### R3 – Save-Ownership vor Property-Callbacks

Nach Admission und initialer Identitäts-/Revisionsprüfung wird die serviceeigene Save-Completion **vor** dem Aufruf von EditorBridge.Lock veröffentlicht. Ein IsReadOnly-Callback kann daher Shutdown, Drain oder Dispose reentrant aufrufen, ohne reservierte Save-Arbeit zu übersehen. Nach der Lease-Aktivierung werden Cancellation, Lifecycle, Editoridentität und Revision erneut geprüft. Ein bis dahin eingetretener Shutdown/Detach/Cancellation führt vor Delete/Open zum Abbruch ohne Zieldateieffekt.

Wenn die Initialisierung fehlschlägt, entfernt der innere Control-Cleanup eine gegebenenfalls schon aktivierte Lease; der äußere Save-finally gibt Admission frei und signalisiert die veröffentlichte Completion. Das gilt auch beim direkten synchronen Append-Einstieg, der jetzt dieselbe frühe Ownership und die erneute Prüfung nach Property-Callbacks verwendet. Das Completion-Signal wird erst nach Freigabe der UI-Lease und Admission abgeschlossen. Die bestehende Drain-Integration selbst musste dafür nicht geändert werden.

Hinter der erneut geprüften Commit-Grenze bleibt der vorhandene Vertrag erhalten: Delete und der gesamte Write einschließlich Stream-Dispose werden ohne neue Cancelprüfung finalisiert. Save erhält weiterhin keine executionId, keinen Interpreter-Scope und erhöht stateVersion nicht.

Nachweise: `R3OwnershipPrecedesLeaseCallbacksAndEveryRejectedInitializationUnwinds` :196, sechs Varianten für Shutdown/Drain, Dispose, Detach, Cancel, Initialisierungsfehler und Shutdown plus Fehler; `R3SynchronousAppendPublishesOwnershipBeforeReadOnlyNotification` :233; `R3ShutdownCancellationDetachAfterActualDeleteStillFinalizeWrite` :249. Letzterer löscht den Sentinel **vor** dem kontrollierten Halt und bestätigt die notwendige Finalisierung danach.

### R4 – Identitätslease über den gesamten synchronen Edit

Wirksame MCP-Edits halten nun zusätzlich eine editorbezogene Identity-Lease bis nach Changed-/UpdateFinished-Callbacks und Aufnahme der Mutationsantwort. Sie sperrt effektiven Documenttausch über die bestehende Coercion sowie Editor-/ViewModel-Referenzwechsel. Bridge-/VM-Detach wird bis zum Ende des synchronen Edits kontrolliert aufgeschoben, sodass eine tatsächliche visuelle Abmeldung keine Mutationsantwort auf einen Nachfolger umbiegen kann.

Die Freigabe genau einer Dokumentänderung erfolgt erst, nachdem die äußeren UpdateStarted-Beobachter zurückgekehrt sind. Eine reentrante erste Änderung kann diese Freigabe deshalb nicht stehlen. Nach dem ersten Changing werden weitere direkte Änderungen in derselben Operation verweigert; reentrante Serviceaufrufe treffen weiterhin Busy über dieselbe Admission. Die Dokumentbindung bleibt auch für das Setzen von IsModified und den Snapshot stabil. Die Antwort verwendet ausdrücklich die angefragte editorId und wird vor dem Ausführen aufgeschobener Lifecycleaktionen kopiert.

Ein werfender Changed-/UpdateFinished-Callback kann nach bereits realisiertem Texteffekt auftreten. Dieser wird nicht zurückgerollt oder als wirkungsloser Busy-Fehler ausgegeben: Der tatsächliche ursprüngliche Dokumentstand liefert die EditorMutation. Updates/Undo-Gruppe, Identity-Lease und Admission werden geschlossen. Ohne Texteffekt bleibt die Ablehnung ein Busy-/InternalError-Ergebnis. Die ursprüngliche ReentrantSwap-Probe ergibt jetzt eine Antwort zur ursprünglichen ID und `old edit` im ursprünglichen Dokument; das Nachfolgedokument wird nicht übernommen oder markiert.

Nachweise: `R4EditKeepsOriginalIdentityAcrossReentrantCallbacksAndExceptions` :275, vierzehn Changed-/UpdateFinished-Varianten für Document-/Editor-/Viewwechsel, Detach, Same-Document-/Service-Reentrancy und werfende Callbacks, jeweils mit anschließend erfolgreichem Edit; `R4UpdateStartedCannotStealTheAuthorizedChange` :326, zwei ergänzende Vorprüfungen.

### R5 – Einmaliger Präsentationsbeginn je neuem Editorlauf

Der lokale Präsentationsvertrag besitzt BeginExecution. Nur der tatsächlich neu angenommene Worker ruft diesen nach Identitätsprüfung einmal auf, bevor das ScriptBook beginnt. Das VM leert dabei vorherige Diagnosen und setzt den Status auf den neuen Lauf. BeginLine bleibt zeilenbezogen und leert keine laufenden Buchdiagnosen. Busy-/RevisionConflict-Ablehnungen und idempotente Retries erreichen die Initialisierung nicht. Frühere immutable Registry-Ergebnisse werden nicht verändert.

Nachweise: `R5NewEditorRunClearsPriorDiagnosticsAndKeepsPriorImmutableResult` :352, zwei semantische/Syntax-Vorläufe mit fehlerfreiem Folgelauf; `R5BookKeepsAllCurrentLineDiagnosticsAndRetryDoesNotResetPresentation` :375, mehrere fortsetzbare Diagnosen und Retry ohne Collection-/Statusreset; `R5RejectedBusyOrRevisionConflictDoesNotChangeDiagnosticsOrStatus` :390, zwei Ablehnungsvarianten.

### Unveränderte unabhängige Gegenproben erneut ausgeführt

Die vom unabhängigen Reviewer entwickelte externe `mcp014-independent-review/Program.cs` samt Probeprojekt wurde ohne Quelländerung gegen den Korrekturstand ausgeführt. Dieser Wiederholungslauf wurde **vom Implementierer** ausgeführt und ist kein neuer unabhängiger Review. Log: `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-corrections/original-probes-final.log`.

| Originalprobe | Korrigiertes Ergebnis |
| --- | --- |
| Execute vor Detach / Documenttausch | Beide Cancelled, Effects.None, `reviewShouldNotRun` fehlt. |
| Reentranter Save-Shutdown | ShuttingDown; Drain im Callback nicht abgeschlossen; deletes=0; Sentinel erhalten. |
| Leerer Textsetter mit Undo | before=True, after=True; ExecutionBusyException. |
| ReentrantSwap aus Changed | Erfolgsantwort zur ursprünglichen ID; ursprünglicher Text `old edit`; kein Nachfolge-Document übernommen. |
| Vorherige Diagnose → fehlerfreier Editorlauf | previous=1, currentGui=0, currentResult=0, HasErrors=false, Completed. |

Unverändert erfolgreiche Kontrollen: produktive Undo-/Redo-/Input-/Paste-/Cut-Sperren, nutzbare History nach Save, Finalisierung nach tatsächlich erfolgtem Delete trotz Shutdown/Cancel/Detach, Cancellation vor Commit ohne Delete, Epoche widerrufen → Forbidden, Retry ohne weiteren Print und externe Ausführung weiterhin Forbidden, Writefehler nach Delete mit freigegebenen Sperren und unverändertem Dirty-Snapshot, alter Import verändert keinen Nachfolgeeditor.

Die rohe `Document.UndoStack.Undo()`-Negativkontrolle bleibt ausdrücklich **kein** vollständig geschützter Bibliothekseinstieg: Sie kann weiterhin einen Stackeintrag verbrauchen und wirft in der Probe InvalidOperationException. Das ist die beauftragte beibehaltene Bibliotheksgrenze. R2 wurde für den echten Textsetter und Basistypzugang behoben, nicht durch Wegdefinieren dieser Setter. Für produktive Undo-/Redo-Aufrufe sind weiterhin die geschützten Editor-Kommandos erforderlich.

### Vollständige Validierung des Korrekturstands

Logs, TRX, Zwischenstände und Befehlsliste liegen außerhalb des Zielrepositories unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-corrections/`; der ausgeführte Ablauf ist in `validate.ps1` und `validation-results.json` festgehalten.

| Prüfung | Ergebnis / aktueller Nachweis |
| --- | --- |
| Restore Root-Solution / GUI-Solution | beide erfolgreich; `restore-core.log`, `restore-gui.log` |
| Release-Build Root-Solution / GUI-Solution | beide erfolgreich; `build-core.log`, `build-gui.log`; keine neue Warnungsstelle, acht vorhandene GUI-Nullable-Warnungen |
| Neue R1–R5-Regressionen | **49 bestanden**, `results/corrections-final.trx` |
| Bisherige MCP-01.4-Fälle separat | **47 bestanden**, `results/existing47-final.trx` |
| Vollständige GUI-Suite | **434 bestanden**, 0 fehlgeschlagen/übersprungen, `results/gui-final.trx` |
| Vollständige Core-Suite | **7 667 bestanden, 4 bisherige Skips**, 0 fehlgeschlagen, `results/core-final.trx` |
| Vollständige reguläre MCP-Suite | **89 bestanden**, 0 fehlgeschlagen, `results/mcp-final.trx` |
| Unveränderte Originalgegenproben | vollständig ausgeführt, erwartete korrigierte Ergebnisse/positiven Kontrollen und abgegrenzte rohe Undo-Grenze; `original-probes-final.log` |
| Projektabhängigkeiten | unverändert; `project-dependencies.log`; keine csproj-/Paket-/Solutionänderung |
| `git diff --check` und Scope-/Hashkontrolle | erfolgreich; bestehende Tests unverändert und beide alten Dokumentpräfixe bytegetreu erhalten |

Damit bestehen **8 190 reguläre Fälle**. Die 49 Korrekturfälle und die bisherigen 47 Editorfälle sind in der GUI-Zahl enthalten und werden nicht doppelt addiert. Die bisherigen vier Core-Skips bleiben der Darwin-ACL-Fall und drei Windows-Symlinkfälle ohne entsprechendes Privileg. Expliziter Hardwarebenchmark und Installed-Codex-Roundtrip wurden nicht aktiviert. Keine bestehenden Tests wurden geändert, entfernt, deaktiviert oder abgeschwächt.

### Zwischenfehler und Evidenzgrenzen dieses Korrekturlaufs

- Ein erster Build zeigte einen C#-Namenskonflikt mit einer bereits vorhandenen `error`-Patternvariable; die lokale Catchvariable wurde umbenannt. Ein durch die Guardumstellung unbenutzter Lease-Konstruktorparameter wurde entfernt. Keine neue Compilerwarnung bleibt zurück.
- Der erste neue Testbuild griff direkt auf das nur über INotifyCollectionChanged angebotene Ereignis der ReadOnlyObservableCollection zu. Der neue Test verwendet jetzt ausdrücklich dieses Interface.
- Im ersten ausführbaren neuen Lauf bestanden 44 von zunächst 45 Fällen; die Syntax-Vorläuferprobe erwartete fälschlich eine nichtleere semantische GUI-Diagnosecollection. Die bestehende Syntaxpräsentation nutzt den Statustext; der neue Test prüft nun diesen und den immutable SyntaxError-Registryeintrag. Produktivcode und bestehende Tests wurden dafür nicht abgeschwächt. `corrections-second.trx` erhält diesen Zwischenstand; die nachfolgenden erweiterten 49 Fälle bestehen.
- Die alten Originalproben verwenden vier obsolete Headless-Tastenoverloads; deren Warnungen wurden durch unveränderte Wiederholung nicht kaschiert. Es gibt keinen neuen Produktivpaketwechsel.
- In diesem Korrekturlauf wurde keine `HeadlessUnitTestSession.Dispose`-NullReferenceException beobachtet. Die aus MCP-01.3 bekannte sporadische Dispose-Race gilt damit nicht als allgemein behoben; kein Master-Backport wurde übernommen.

### Verbleibende Grenzen und Übergabe

Die Datei-Save-Semantik bleibt ASCII, Whitespace-Filter, LF, Append und bestätigtes Delete vor Write; weiterhin keine Transaktion/Rollbackgarantie und kein pauschaler Clean-Nachweis. Ein nicht zurückkehrendes Delete kann Drain offen halten. Rohe UndoStack-Manipulation bleibt die oben beschriebene Bibliotheksgrenze. Der frühe UpdateGuard ist gegen die gepinnte AvaloniaEdit-Version geprüft; ein Paketupgrade muss die BeginUpdate-/UpdateStarted-/EndUpdate-Reihenfolge und diese Regressionen erneut bestätigen.

Unverändert offen bleiben MCP-01.5 mit Authentisierung, lokaler konkreter Ausführungsbestätigung und HTTP-Anbindung sowie MCP-01.6 mit nativer Closing-/HTTP-Shutdown-Integration, bevor der Dispatcher beendet wird. Externe Ausführung bleibt in diesem Stand auch mit execution.run und Aktivierungsepoche gesperrt. Dieser Lauf belegt Windows/Avalonia.Headless, keine native IME-/OS-Closing-Garantie oder neue macOS-/Linux-CI-Ausführung. Eine zusätzliche unabhängige Freigabe wurde nicht vorgenommen.

**READY FOR RE-REVIEW.** Die fünf Korrekturen sind implementiert und mit den oben genannten neuen Regressionen, Originalgegenproben und Gesamtsuiten belegt. Der unabhängige Re-Review bleibt der nächste gesonderte Schritt. Hier stoppen; kein Commit und kein MCP-01.5-Beginn.


---

## Korrekturrunde 2 – R2/R4/R6/R7 – 24.10.2026

**BLOCKED – nachgewiesene Architekturgrenze der öffentlichen AvaloniaEdit-11.4.1-API.** R2, R4, R6 und R7 bleiben offen. Es wurde keine weitere produktive Guard-Reparatur eingebaut. Der Auftrag sieht diesen Abschluss ausdrücklich vor, wenn die zugesagten Eigenschaften im erlaubten Umfang nicht zuverlässig herstellbar sind. Die nachfolgende Analyse ersetzt keine unabhängige Freigabe. Sie nimmt insbesondere die weitergehenden Schutzbehauptungen des historischen ersten Korrekturanhangs nicht wieder als gültig an.

### Stand, Quellen und Nachweiskette

Geprüft wurde der vorhandene Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Architektur, Implementierungsplan, Validierungsverlauf und der vollständige Final Re-Review ab Zeile 339 mit den R6-/R7-Reproduktionen wurden berücksichtigt. Vor jeder Änderung wurden 748 Dateien gehasht und beide Dokumente vollständig bytegetreu gesichert. Produktquellen, bestehende Tests, Projektdateien und Pakete blieben unverändert. Geändert werden ausschließlich diese beiden Dokumente durch Append.

Der Projekt- und Restoregraph verwendet `Avalonia.AvaloniaEdit` **11.4.1**. Dessen installierte NuGet-Metadaten verweisen auf Commit `b56a35a5d4838f3c9a9adeba90dd80952f70e774`. Genau dessen tatsächlicher Bibliotheksquellcode wurde heruntergeladen und vor der Entscheidung gelesen. Die tatsächlich geladene Assembly bestätigt per Reflection denselben Commit in `AssemblyInformationalVersion`: `11.3.0+b56a35a5d4838f3c9a9adeba90dd80952f70e774`; die AssemblyVersion `11.3.0.0` ist nicht die NuGet-Paketversion. Es wurde keine andere Bibliothek eingesetzt.

Unveränderliche Primärquellen:

- [TextEditor.cs am Paketcommit](https://github.com/AvaloniaUI/AvaloniaEdit/blob/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit/TextEditor.cs): Document-Bindung, Textsetter, IsReadOnly und Clear.
- [TextDocument.cs am Paketcommit](https://github.com/AvaloniaUI/AvaloniaEdit/blob/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit/Document/TextDocument.cs): Updatezähler, Ereignisse, Replace, Versionierung und UndoStack-Bindung.
- [UndoStack.cs am Paketcommit](https://github.com/AvaloniaUI/AvaloniaEdit/blob/b56a35a5d4838f3c9a9adeba90dd80952f70e774/src/AvaloniaEdit/Document/UndoStack.cs): Gruppenverwaltung, ClearAll und Fehlerverhalten.

Quellkopien mit Lizenzkopf, Hashmanifest, neue Probequellen, Befehle, Logs und TRX liegen ausschließlich außerhalb des Zielrepositories unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-corrections2/`. `source-manifest.json` hält URLs, SHA-256 und Paketmetadaten fest. Die unabhängigen ursprünglichen Probequellen in `mcp014-independent-review` und `mcp014-final-independent-review` wurden nicht geändert; ihre Ausgangshashes sind separat gesichert.

### Tatsächliche Reihenfolge und gemeinsame Ursache

Die folgenden Zeilen beziehen sich auf die unveränderten Rohquellen des oben festgehaltenen Bibliothekscommits:

| Grenze | Tatsächliches Verhalten | Konsequenz für den Schutz |
| --- | --- | --- |
| `TextEditor.Text`, :257–273; `Clear`, :1071 | Nichtvirtueller Setter ruft zunächst Document.Text auf, setzt danach CaretOffset und ruft UndoStack.ClearAll auf. Clear delegiert an diesen Setter. | Ein versteckter Setter im abgeleiteten Control schützt keine Basistypreferenz. Auch ein inhaltlicher No-op kann Historie ändern. |
| `TextDocument.Text`, :227–248 | Delegiert unmittelbar an Replace. | Kein gesonderter abbrechbarer Textsetter-Hook. |
| `BeginUpdate`, :451–461 | Prüft InDocumentChanging, erhöht den Updatezähler, startet beim Übergang 0→1 die Undo-Gruppe und ruft dann UpdateStarted auf. Bei verschachteltem BeginUpdate gibt es kein erneutes UpdateStarted. | Der früheste öffentliche Update-Observer kommt nach Zustandsänderungen. Eine Exception wird dort nicht automatisch zurückgerollt. |
| `RunUpdate`, :437–441; `Replace`, :810–838 | BeginUpdate liegt vor der Rückgabe des Disposable beziehungsweise vor dem try/finally der Replace-Operation. | Werfen aus UpdateStarted überspringt die reguläre EndUpdate-Absicherung dieses Aufrufs. |
| `DoReplace`, :841–875 | Leere Ersetzung auf leerem Dokument kehrt vor Changing zurück. Bei wirksamer Ersetzung folgt Changing vor Undo-Push, Versions- und Textänderung. | Changing eignet sich für echte Replace-Effekte, erfasst aber keinen leeren Setter und dessen nachfolgendes ClearAll. |
| `Changed`, :930–932 | Text, Version, Historieneintrag und Anker sind bereits geändert; InDocumentChanging ist noch gesetzt. | Eine dort geworfene Exception darf nicht als wirkungsloser Abbruch ausgegeben werden. Direkte verschachtelte Änderungen werden hier bereits bibliotheksintern vor BeginUpdate zurückgewiesen. |
| `EndUpdate`, :468–487 | Beim letzten Ende zuerst TextChanged/Propertyevents innerhalb der Gruppe, dann Undo-Ende, dann Updatezähler 0, dann UpdateFinished. | EndUpdate aus einem ablehnenden Guard ist eine echte Abschlussauslieferung. Ein UpdateFinished-Handler kann synchron das nächste Update eröffnen. |
| `UndoStack.ThrowIfUndoGroupOpen`, :252–264 | Setzt bei offener Gruppe deren Tiefe auf 0 und wirft anschließend. ClearAll ruft diese Methode vor dem eigentlichen Leeren auf (:436–450). | Der Ausnahmefall ist bereits eine Zustandsänderung. Nachträgliches EndUpdate trifft einen Dokumentzähler ohne passende Undo-Gruppe. |
| Document-Bindung, `TextEditor.cs:110–151` | StyledProperty-Wechsel bindet TextArea und Beobachter an das neue Dokument. | Die vorhandene Property-Coercion kann einen effektiven Wechsel abweisen; sie wird nicht bei jeder Textoperation oder jedem Zugriff über eine bereits gehaltene Dokumentreferenz aufgerufen. |

Die Reflectionprobe gegen die tatsächlich geladene Assembly bestätigt: TextDocument und UndoStack sind `sealed`; Textsetter, BeginUpdate und ClearAll sind nicht virtuell. Ein neuer Dokumentsubtyp oder UndoStack-Override ist deshalb kein verfügbarer Erweiterungspunkt. Das interne InDocumentChanging schützt Changing/Changed, wird aber erst nach UpdateStarted gesetzt und ist bei UpdateFinished wieder frei. Es kann nicht über einen unterstützten Hostvertrag zur allgemeinen Save-Lease umfunktioniert werden.

**R6:** Beim äußeren Host-Update läuft UpdateStarted, bevor die eine autorisierte Textänderung freigegeben wird. Ein leerer Basistyp-Setter aus diesem Callback startet ein verschachteltes Update ohne neues UpdateStarted. Die leere Ersetzung erzeugt auch kein Changing. Danach erreicht der Setter ClearAll, dessen Fehlerpfad die Undo-Gruppentiefe zurücksetzt. Das äußere Dokumentupdate bleibt später offen; die autorisierte Textänderung kann trotzdem schon angewandt sein. Der vorhandene einzelne nachträgliche EndUpdate-Versuch in `WorkbenchEditorOperations.cs:121–142` kann die verlorene Gruppenstruktur nicht rekonstruieren. Die neue Clear-Variante reproduziert denselben Pfad.

**R7:** `WorkbenchTextEditor.BeforeUpdate` (:83–91) ruft zur Ablehnung EndUpdate auf. Dessen echtes UpdateFinished ruft einen bedingten Normalisierer erneut auf. Weil dessen Textänderung verworfen wurde, bleibt seine Bedingung wahr. Der nächste abgewiesene Versuch liefert synchron wieder UpdateFinished aus, bevor der vorige Handler seine Busy-Exception erhalten kann. Ohne explizite Terminierungsregel ist dies keine sichere Ablehnung. Das ist derselbe grundlegende Konflikt zwischen spätem Beobachter und bereits begonnenem Bibliotheksupdate, nicht ein separater Fehler im Save-Writer.

### Warum keine weitere Guard-Sammlung implementiert wurde

Zusätzliche isolierte Bibliotheksproben bestätigen die Alternativen am tatsächlichen Laufzeitpaket:

| Ansatz | Ausgeführter Gegenbeleg / Architekturkonflikt |
| --- | --- |
| Nur aus UpdateStarted werfen, EndUpdate entfernen | Text bleibt unverändert, aber `IsInUpdate=True`; kein Abschlussereignis. Der geöffnete Zustand bleibt zurück. |
| Nur Changing verwenden | Gleicher leerer Basistyp-Setter: `Changing` wird 0-mal aufgerufen; vorhandenes Undo wechselt von True auf False. |
| Nur IsReadOnly/ReadOnlySectionProvider | Ein TextEditor mit IsReadOnly=true akzeptiert trotzdem direkte Textzuweisung. Die UI-Eingabepolicy ist kein Dokumentmutationsgate. |
| EndUpdate im Guard beibehalten | Unveränderte Originalproben erreichen Rekursionstiefen 8 und 32; neue Probe erreicht das absichtliche Limit 16 sowohl unter Save- als auch Identity-Lease. |
| Setter/Clear im WorkbenchTextEditor verstecken | Der nichtvirtuelle Basistypaufruf bleibt erreichbar; im Auftrag ausdrücklich ausgeschlossen. |
| Dokument/UndoStack austauschen, künstliche Gruppen öffnen, Counter/Delegates per Reflection verändern | Kein unterstützter früher Mutationshook. Austausch verletzt Identität/Historie; UndoStack-Austausch ruft selbst ClearAll auf. Gruppenreparatur und Manipulation privater Bibliothekszustände würden gerade den geforderten Nachweis umgehen. Nicht implementiert. |
| Fehler fangen und nur Erfolg zurückgeben | Meldet allenfalls den schon erfolgten Text, behebt aber weder offene Updategruppe noch Historie oder Rekursion. Keine vollständige R4/R6-Korrektur. |
| Handler per Dispatcher später aufrufen oder während einer Lease ausblenden | Der Host besitzt die öffentliche Event-Abonnentenliste nicht. Dies würde direkte Bibliotheksereignisse und deren Semantik ändern und die Callbackkombinationen nicht vollständig absichern. |

Eine zuverlässige Ablehnung muss **vor** Updatezähler/Undo-Gruppe und auch vor den Folgeschritten des Basistyp-Textsetters stattfinden. Diese Grenze existiert im unveränderten öffentlichen Paketvertrag nicht. Der Konflikt ist auf die zugesagte Unterstützung beliebiger direkter Basistyp-/Dokumentaufrufe in reentranten Callbacks bezogen; es wird nicht behauptet, dass eine kontrollierte Anwendung grundsätzlich keinen Editor verwenden könne.

### Konsistenter Lösungsweg für sämtliche produktiven Hostaufrufer

**Für den unveränderten zugesagten Basistypvertrag erforderlich:** eine separat freigegebene, gepflegte Bibliothekskorrektur beziehungsweise eine konkret nachgewiesene Bibliotheksversion mit früher Mutationszulassung und ausnahmesicherem Updateabschluss. Eine solche Version wurde hier weder behauptet noch installiert. Das erfordert eine Erweiterung des derzeitigen Umfangs; ein lokaler Binärpatch oder stiller Quellfork wurde nicht vorgenommen.

Die Bibliotheksgrenze müsste vollständige Operationen erfassen: Text/Clear einschließlich Caret und Historie, Document.Text/Replace einschließlich verschachtelter Aufrufe sowie alle Start-/Ende- und Ereigniswege. Ein zugelassener Host-Edit benötigt eine nicht an Eventhandler vererbte, explizite Autorisierung für seine internen Delegationen; ein bloßes ambient/threadlokales Allow-Flag wäre erneut reentrant nutzbar. Während der Callbackzustellung dürfen gewöhnliche direkte Aufrufe dieses Recht nicht übernehmen. Abweisung vor Zustandsänderung darf keine Abschlussereignisse erzeugen. Fehler nach realisiertem Texteffekt müssen beim unverändert ursprünglichen Dokument ausgeglichen werden und diesen Effekt zurückmelden. Erst danach können die jetzt roten Garantietests als Produktregressionen bestehen.

**Alternative nur mit ausdrücklicher Vertragsänderung:** alle produktiven Mutationen über einen kontrollierten Hostadapter führen, rohe Editor-/Dokumentmutationen aus beliebigen Callbackabonnenten aus dem unterstützten Vertrag herausnehmen und diese Grenze technisch durch Kapselung durchsetzen. Das erfüllt den aktuellen Basistypvertrag gerade nicht und wird hier nicht als Fix ausgegeben. Eine bloße neue Helfermethode ohne Migration und ohne Kapselung genügt nicht.

Für beide Wege wurde die notwendige Migration konkret erfasst; sie benötigt weder eine zweite Interpreterpipeline noch eine zweite Editorinstanz:

| Produktiver Einstieg | Erforderlicher gemeinsamer Schutz im freizugebenden Lösungsweg |
| --- | --- |
| View-Initialisierung `CryptoScriptEditView.axaml.cs:38` | Clear als vollständige Operation zulassen, bevor Bibliothekszustand geändert wird. |
| History `CryptoScriptEditViewModel.cs:125–137` | Remove+Insert und Modified auf der gebundenen Instanz als eine Hostmutation führen; keine lose Kette mit reentrantem Wechsel. |
| Import `CryptoScriptEditViewModel.cs:265–271` | Clear und Zeilen-Insert an erfasste Registrierung/Dokumentinstanz binden; vorhandene Ausführungsadmission nutzen. |
| Interaktive Ausgabe `CryptoScriptEditView.axaml.cs:109` | Anchor-basierter Insert über dieselbe Mutationsgrenze, weiterhin mit ursprünglicher View-/Dokumentprüfung. |
| Editor-Book-Ausgabe `WorkbenchEditorOperations.cs:240` | Bereits vorhandene serviceeigene Ausführung und Lifecycleprüfung erhalten, dokumentgebundenen Output mutieren. |
| Completion `InfoCompletionController.cs:405` | Den tatsächlich ausgeführten Replace der Completion über dieselbe Zulassung führen. Die vorhandenen Update-Observer (:183/:202) verwalten aktuell Präsentationszustand; sie sind kein allgemeines Mutationsgate. |
| MCP-Edit `WorkbenchEditorOperations.cs:100–145` | Admission und Identity-Lease bis nach Wirkung, ausgeglichener Gruppe, Dirty-Markierung und Antwortsnapshot halten. |
| Tastatur, IME, Paste/Cut, Suche/Replace, Undo/Redo | UI-/Commandgrenzen plus Bibliothekszulassung konsistent erfassen. Bestehende Undo-/Redo-Kommandos vor Stackzugriff bewahren. Eine Änderung nur der VM-Aufrufer würde diese Wege nicht vollständig erfassen. |
| GUI-Save, synchron und asynchron | Completion weiterhin vor beobachtbaren Lease-/ReadOnly-Callbacks registrieren; alle Mutationswege durch dieselbe Dokumentsperre sperren. Nach Delete keine neue Cancellationlücke. |

Bei einer Vermittlungslösung dürfen reentrante Callbackmutationen nicht automatisch später wiederholt werden: Busy bleibt eine Ablehnung, kein verdecktes Replay. Ein ausdrücklich später gestellter Edit muss die aktuelle Identität/Revision erneut prüfen. Die bestehenden MCP-01.1-DTOs werden dadurch nicht stillschweigend verändert. Die Auswahl und Implementierung einer dieser Architekturänderungen gehört in einen gesondert freigegebenen Folgeauftrag; diese Runde stoppt mit BLOCKED.

### Neue Vertragsproben und Abdeckung

`BoundaryProbe.cs` mit `probe.csproj` ist ein isolierter externer Prüfstand. Die 46 neuen Vertragsprüfungen verwenden die tatsächliche XAML-View, produktiven WorkbenchTextEditor, Registry und Avalonia.Headless. Separate primitive Bibliotheksvergleiche verwenden zusätzlich einen nackten TextEditor/TextDocument, um die Grenze des jeweiligen Hookansatzes zu isolieren. Weder Zielquellen noch bestehende Tests wurden dafür verändert.

**42 bestanden, 4 fehlgeschlagen; Prozess-Exitcode 1.** Der Prüfstand verlangt korrekte Wirkung/Revision/Identität, geschlossene Gruppen, erfolgreiche Folgeedits und ausführbares Undo/Redo; er erwartet nicht einfach die bekannten Fehler als Erfolg. Die vier roten Fälle sind:

- leerer Basistyp-Textsetter aus UpdateStarted mit Historie: InternalError, Text bereits ` edit`, Revision 2→3, offene Gruppe, Folgeedit Busy;
- Clear derselben leeren Ausgangslage: dieselben Fehlerfolgen;
- bedingter UpdateFinished-Normalisierer während Identity-Lease: Tiefe 16 bis zum absichtlichen Schutzlimit;
- derselbe Normalisierer während Save-Lease: Tiefe 16 bis zum absichtlichen Schutzlimit.

Die Matrix umfasst 24 Callbackfälle (UpdateStarted/Changed/UpdateFinished × leer/nichtleer × gleicher Setter/anderer Setter/Clear/Document.Text), 16 Savekombinationen (leer/nichtleer × mit/ohne Documentwechselversuch × dieselben vier Operationen), zwei Normalisiererfälle, einen werfenden UpdateFinished-Abonnenten während Save und drei reentrante Saveversuche aus den drei Ereignisphasen. Jede Savekombination weist drei unmittelbar aufeinanderfolgende Mutationen ab und prüft danach reale Undo-/Redo-Ausführung und einen Folgeedit. Die Saveversuche aus Editcallbacks enden fail-fast Busy ohne Delete und lassen den ursprünglichen Edit abschließen.

Die schon vorhandenen, unverändert erneut bestandenen R4-Tests ergänzen Document-/Editor-/Viewwechsel während der offenen Operation und werfende Changed-/UpdateFinished-Handler mit bereits angewandter Änderung. Die unveränderten unabhängigen Proben ergänzen den natürlichen Normalisierer-Kontrollfall ohne Lease, Bibliotheksherkunft des R6-Falls, reentrantes Dispose/Drain, Unlock-Exceptions und verzögertes Detach mit werfendem Observer. Die zwölf geforderten Kombinationsthemen sind damit in neuen Proben plus den bestehenden erneut ausgeführten Fällen erfasst; daraus wird wegen der vier roten Garantien ausdrücklich keine erfolgreiche Korrektur abgeleitet.

Es wurden keine künstlich grünen, deaktivierten oder abgeschwächten neuen Tests ins Zielrepository aufgenommen. Weil die Architekturgrenze vor der Produktimplementierung nachgewiesen wurde, verbleibt der reproduzierbare rote Prüfstand im Scratchordner. Er ist eine Übergabe für die spätere Korrektur, keine bestandene Produktregression.

### Unveränderte unabhängige Originalgegenproben frisch wiederholt

Die folgenden Wiederholungen wurden in dieser Runde vom Implementierer ausgeführt; ihre ursprüngliche unabhängige Herkunft macht diesen Lauf nicht selbst zu einem unabhängigen Review.

| Probe / Log unter `mcp014-corrections2` | Frisches Ergebnis |
| --- | --- |
| `original-probes-final.log` | R1: beide Vorstart-Lifecyclefälle Cancelled/Effects.None ohne Variable; R3: kein Delete bei reentrantem Shutdown, Drain im Callback noch offen, Finalisierung nach tatsächlichem Delete intakt; R5: GUI-Diagnosen des Vorlaufs entfernt. Auch ursprünglicher Setter-/Swapkontrollfall, Retry, External-Forbidden und Roh-Undo-Grenze unverändert. |
| `r6-original.log`, separater Prozess mit `started-empty` | Host: InternalError trotz Text `edit`, Revision 2→3, IsInUpdate=True und tatsächlicher Folgeauftrag Busy. Direkte Bibliothekskontrolle zeigt denselben Gruppenfehler. |
| `r7-original.log`, separater Prozess mit `normalizer` | Ohne Lease 2 Aufrufe/Tiefe 2 und Präfix eingefügt; Save-/Identity-Lease erreichen die absichtlichen Grenzen 8 und 32. |
| `additional-original.log`, unverändertes Standardprogramm | UpdateStarted-Exception ohne Effekt sauber abgewickelt; Dispose/Unlock-Fehler und synchroner Save-Dispose enden ohne vorzeitiges Drain; verzögerter Detachfehler bewahrt den ursprünglichen Mutationsabschluss. R6/R7 bleiben reproduzierbar. |

Die ursprünglichen R6-/R7-Programme haben Exitcode 0, weil sie die gefundenen Symptome als Beobachtungen absichern. Das ist **kein** bestandener Produktvertrag. Die neue Sollvertragsmatrix hat dagegen wegen der vier verletzten Garantien Exitcode 1. Alle Rekursionsproben besitzen eine künstliche Obergrenze; kein unlimitierter Lauf und kein tatsächlicher Stackoverflow wurden ausgeführt oder als Nachweis behauptet.

### Vollständige erneute Validierung des unveränderten Produktstands

| Prüfung | Ergebnis |
| --- | --- |
| Restore beider Solutions | erfolgreich |
| Release-Build beider Solutions | erfolgreich; inkrementell jeweils 0 Warnungen/0 Fehler |
| Erste Korrekturrunde separat | 49 bestanden, einschließlich 6 R1-, 8 R3- und 5 R5-Fällen |
| Ursprüngliche EditorAutomationTests separat | 47 bestanden |
| Vollständige GUI-Suite | 434 bestanden, 0 Fehler/Skips |
| Vollständige Core-Suite | 7 667 bestanden, 4 bisherige Skips, 0 Fehler |
| Vollständige reguläre MCP-Suite | 89 bestanden, 0 Fehler/Skips |
| Neue externe Sollvertragsmatrix | 42 bestanden, **4 fehlgeschlagen**, Exitcode 1 |
| Alle unveränderten ursprünglichen Gegenproben | frisch ausgeführt, einschließlich weiterhin fehlerhafter R6-/R7-Fälle |
| Paket-/Projektgraph und `git diff --check` | erfolgreich geprüft; gepinntes AvaloniaEdit 11.4.1, keine neue Abhängigkeit |

Reguläre Gesamtsumme: **8 190 bestanden**; die 49 und 47 sind Teil der 434 GUI-Fälle und werden nicht nochmals addiert. Vier reguläre Core-Skips bleiben der Darwin-ACL-Fall und die drei Windows-Symlinkfälle ohne passendes Privileg. Expliziter Hardwarebenchmark und Installed-Codex-Roundtrip wurden nicht aktiviert. Kein nativer IME-/StorageProvider-/Closing-Test und keine neue macOS-/Linux-Prüfung wird behauptet. Diese grünen Suiten belegen den unveränderten Stand und R1/R3/R5, sie widerlegen die roten Callbackproben nicht.

`validate.ps1`, `validation-results.json`, die einzelnen Logs und `results/*.trx` enthalten die vollständigen Ausführungen. `dependencies.log` enthält den tatsächlichen Paketgraph. Die finale Hashkontrolle vergleicht alle 748 Ausgangsdateien und alle Originalprobequellen; einzig die beiden angeforderten Dokumente dürfen ein geändertes Hash haben, jeweils mit exakt erhaltenem altem Bytepräfix.

### Zwischenfehler und verbleibende Grenzen

Der erste neu geschriebene Scratch-Prüfstand behandelte `EditorSnapshot.Revision` fälschlich als Zahl; tatsächlich ist das DTO-Feld ein String. `before.Revision + 1` verkettete deshalb Text und markierte alle 24 Callbackfälle als falsch. Der Prüfstand wurde auf eine explizite kulturunabhängige numerische Prüfung des hier konkret untersuchten Hostzählers korrigiert. Das erste Log bleibt als `boundary-matrix-first.log` erhalten; nach dieser Korrektur bestehen 22 der 24 Callbackfälle, während die zwei echten R6-Fälle weiter scheitern. Keine Produktquelle und keine bestehende Assertion wurde dafür geändert. Die abschließende erweiterte Matrix und deren vier Fehlschläge stehen in `boundary-matrix-final.log`.

Ein erster Callsite-Suchlauf nannte einen nicht existierenden `CryptoScriptCompletionController.cs`; die tatsächliche Datei `InfoCompletionController.cs` wurde über die Dateiliste gefunden und gelesen. Das war ein Suchfehler, kein Build- oder Produktfehler. Es gab keine fehlgeschlagenen Produktbuilds. Die erwarteten roten Vertragsproben sind keine erfolgreich erledigten Korrekturen.

R2/R4/R6/R7 werden weder auf die bestehende ausgeschlossene rohe UndoStack-API umdefiniert noch als neue pauschale GUI-Grenze stillschweigend freigegeben. Die direkten Setter-/Callbackgarantien sind weiterhin offen. Rohe UndoStack-Aufrufe bleiben separat außerhalb des unterstützten Hostvertrags. Nichttransaktionales Save, ASCII/Whitespace-Filter/LF/Append und bestätigtes Delete vor Write, externe Dateisystemrennen, die native GUI-Testlücke, sporadische Headless-Dispose-Ausnahmen und Print-/Info-Multicast-Grenzen bleiben unverändert. Keine dieser Grenzen wurde als behoben erklärt. R1/R3/R5 haben in dieser Runde keine nachgewiesene Regression.

**Abschluss: R2 offen; R4 offen; R6 offen; R7 offen. BLOCKED.** Für die verlangte Vollabsicherung muss zuerst die Bibliotheks-/Hostgrenze erweitert oder der direkte API-Vertrag ausdrücklich geändert werden. Keine Scheinlösung, kein Commit/Push/Merge/Rebase/Reset, keine Änderung an MCP-01.1, kein Beginn von MCP-01.5 und kein Zugriff auf `.codex/config.toml`. Hier stoppen; ein unabhängiger Re-Review bleibt ein separater Auftrag.
