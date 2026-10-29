# MCP-01.4-B1 – AvaloniaEdit 12 Mutation Contract Proof of Concept

Stand: 24.10.2026. Ergebnis: **GO FOR B2 DESIGN** für das nachfolgend nachgewiesene optionale Mutationsmodell. **Keine Freigabe einer produktiven Bibliotheksdistribution, GUI-Migration, B2-/B3-Implementierung oder MCP-01.4-Integration.** Der produktive MCP-01.4-Stand bleibt unverändert und seine bestehende Reviewentscheidung wird nicht überschrieben.

## 1. Entscheidung und Nachweisumfang

Die vier roten Sollproben sind im isolierten Bibliotheksprototyp tatsächlich grün. Die unveränderten 46 Assertions bestehen **46/46**, einschließlich leerem Basistyp-Textsetter und Clear aus UpdateStarted sowie beider UpdateFinished-Normalisierer. Die Korrektur benötigt weder eine neue Dokumentengine noch eine zweite Undo-/Anchor-Implementierung. Sie verändert die Eventsemantik ausschließlich bei explizit aktivierter strikter Policy.

Zusätzlich bestehen **243 eigenständige Bibliothekstests**, **354 unveränderte Upstreamtests** ohne aktivierte Policy und **9 neue Host-Solltests** zu den direkten und servicevermittelten R6-/R7-Pfaden. Die ursprünglichen unabhängigen Hostkontrollen laufen ebenfalls durch. Die Zahlen bezeichnen unterschiedliche Prüfprogramme; sie sind kein erneuter Lauf der gesamten CryptoWorkBench-Suite.

Ein zunächst noch fehlerhafter nativer Paste-Pfad wurde ausdrücklich in den Nachweis aufgenommen: Nach einem werfenden Changed-Observer blieb dessen äußere Gruppe offen. Eine auf den strikten Modus begrenzte Ergänzung schließt sie nun zuverlässig, hält keine Gruppe während des Clipboard-await offen und weist einen bereits gesperrten Aufruf vor Updateereignissen ab. Die drei zugehörigen Solltests bestehen.

Die Empfehlung erlaubt **das Design von B2 auf einer belegten Kernlösung**. Sie behauptet keine vollständige Abnahme aller nativen Eingabefunktionen. Insbesondere Overstrike, Search/ReplaceNext, asynchrones Drag-and-drop, Completion-Lifecycle, fremde Provider und die UI-Behandlung abgewiesener Kommandos brauchen die in Abschnitt 9 genannten B2-Abnahmekriterien. Diese Grenzen sind keine nachträgliche Einschränkung von MCP-00: Der spätere integrierte Host muss dessen unveränderten Gesamtvertrag erfüllen, bevor eine Produktfreigabe möglich ist.

## 2. Unveränderte Ausgangsbasis und Isolation

| Bestandteil | Nachgewiesener Stand |
| --- | --- |
| Produktiver Worktree | `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp` |
| Branch / HEAD | `feature/mcp-server` / `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0` |
| Bibliotheksrelease | `Avalonia.AvaloniaEdit 12.0.0` |
| Bibliothekscommit | `86fdebec4cff7affb0e14c7885df71d28edce777` |
| Unveränderte Bibliotheksquelle | `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0` |
| B1-Arbeitsbereich | `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1` |
| Geänderte Bibliothekskopie | `mcp014-b1/library` mit einem Hashmanifest der 362 ursprünglichen Dateien |
| Laufzeit / Build | Windows x64, .NET SDK 10.0.401, `net10.0`, Avalonia/Headless 12.1.4 |
| Lokale Kennzeichnung | `12.0.0-b1.local1+86fdebec4cff7affb0e14c7885df71d28edce777` |

Das bereits bei U12 geprüfte Releasearchiv wird erneut verwendet. Sein gespeichertes NuGet-Manifest nennt ausdrücklich diesen Commit, MIT und die Frameworkabhängigkeit Avalonia ≥12.0.0. Es wurde kein beweglicher Master übernommen. Die Quellen lassen sich am [festen Releasecommit](https://github.com/AvaloniaUI/AvaloniaEdit/tree/86fdebec4cff7affb0e14c7885df71d28edce777) vergleichen. Die Verwendung von Avalonia **12.1.4** ist die bereits vorhandene isolierte U12-Testkonfiguration; sie ersetzt nicht den festgehaltenen AvaloniaEdit-12.0.0-Quellstand. Eine zusätzliche Laufzeitmatrix mit Avalonia 12.0.0 wurde nicht durchgeführt.

Verbindliche Grundlagen bleiben [MCP-00](MCP-00-architecture.md), die [Editor-Architekturentscheidung](MCP-01.4-editor-architecture-decision.md), [Review](MCP-01.4-review.md), [Validierung](MCP-01.4-validation.md) und [U12-Machbarkeit](MCP-01.4-avalonia12-feasibility.md). Besonders relevant sind MCP-00 §4.4/4.5 und die Architekturentscheidung §5: frühe Zulassung, vollständige logische Operation, getrennte Vor-/Nachwirkungsfehler, interne Delegation ohne geerbtes Callbackrecht und keine neue Text-/Undohistorie.

Die Hostproben referenzieren die **bereits vorhandenen U12-Testassemblies**. Die produktiven C#-Quellen, deren dirty/untracked MCP-01.4-Arbeit und die vorhandenen Host-Guards wurden nicht verändert. `WorkbenchTextEditor`, EditorBridge, Service, Registry, Save-Commit, ViewModels und echte XAML-View laufen aus dieser bestehenden Testbasis. Keine neue Prism-Integration und kein neuer Prism-Build wurden vorgenommen. Die Bibliothek und ihre eigenen Tests benötigen keine CryptoWorkBench- oder Prism-Abhängigkeit.

Die AssemblyVersion bleibt für die vorhandenen binären Fixtures `12.0.0.0`; die InformationalVersion kennzeichnet den Versuch eindeutig. `IsPackable=false`, lokale Projekt-/DLL-Referenzen und ein separater NuGet-Cache verhindern eine vermeintliche offizielle Ersatzdistribution. Es wurde weder ein offizielles Paket überschrieben noch ein Paket erstellt oder veröffentlicht. Vier Builddateien enthalten ausschließlich den lokalen Versions-/Framework-/SourceLink-Testzuschnitt; diese Änderungen sind vom eigentlichen Bibliotheksvorschlag zu trennen.

## 3. Frühes Mutationsmodell

### 3.1 Öffentliche Zulassung, private Ausführung

Der Prototyp ergänzt `TextDocument.MutationPolicy` mit dem optionalen Interface:

```csharp
public interface IMutationPolicy
{
    void VerifyMutation(TextDocument document, MutationKind kind, bool isReentrant);
}
```

`null` erhält das bisherige Verhalten. Eine konfigurierte Policy wird vor Dokumentzähler, Undo-Gruppe, Texteffekt und Setter-Nacharbeiten geprüft. Der Host kann eine Ablehnung durch seine vorhandene Busy-Exception ausdrücken. Der zusätzliche Reentrancyschutz ist nicht allein vom Wohlverhalten der Policy abhängig: Ein öffentlicher Wiedereintritt wird auch dann verworfen, wenn die Policy ihn versehentlich akzeptiert. Ein Wiedereintritt während der Policyabfrage selbst wird vor einem erneuten Policyaufruf abgewiesen.

Die private, dokumentgebundene `MutationOperation` enthält Fehler und die Information über bereits angewandten Text. Sie ist **kein öffentlich zugängliches Allow-Flag**. Jeder neue öffentliche Text-/Gruppen-/Undo-Einstieg durchläuft `RunMutation` erneut. Eine aktive Operation beziehungsweise Callbackzustellung macht diesen neuen Eintritt reentrant. Es gibt keine ThreadStatic-/AsyncLocal-Freigabe und keinen fortgepflanzten ExecutionContext als Berechtigung.

Legitime interne Delegation geschieht durch getrennte `…Core`-Methoden: Beispielsweise `TextEditor.Text → SetTextCore → TextDocument.ReplaceCore → BeginUpdateCore/EndUpdateCore`, danach Caret und `ClearAllCore`. Diese Zugänge sind nicht öffentlich. Ein Benutzerhandler ruft weiterhin die öffentliche Methode und erhält damit **nicht** die interne Berechtigung. Für eine spätere API-Prüfung sind diese Callsite-Grenzen ebenso wichtig wie das Interface selbst; interne Core-Aufrufe dürfen nicht an Fremdcode weitergereicht werden.

Der Host-Fixtureadapter ist bewusst schmal:

```csharp
editor.Document.MutationPolicy = new B1Adapter(editor);
// VerifyMutation:
if (editor.MutationLocked || isReentrant)
    throw new ExecutionBusyException();
```

Er liest den bereits vorhandenen Save-Lock und überlässt die bisherige Identitäts-/Einzeländerungslogik dem unveränderten Host. Die alten späten Guards werden nicht entfernt oder umgeschrieben. Die frühe Bibliotheksprüfung verhindert gerade, dass sie bei den bisher roten Ablehnungen überhaupt ein Update eröffnen und durch EndUpdate bereinigen müssen. Dies ist Testhost-Verdrahtung der neuen optionalen API, keine Änderung einer Assertion und keine produktive Integration.

### 3.2 Abgedeckte Einstiege und Vollständigkeit des Textsetters

Die öffentlichen Replace-Endpunkte einschließlich `ISegment`- und Mapping-Varianten werden früh zugelassen. Insert, Remove und `TextDocument.Text` gelangen darüber in denselben Pfad. Fremde Segment-/TextSource-Getter laufen bereits innerhalb dieser Grenze. Fremde TextSource-Snapshots werden im strikten Modus vor Gruppen-/Historieneffekten in eine bekannte immutable Textquelle materialisiert; unveränderte native RopeTextSource-Pfade bleiben bestehen.

`BeginUpdate`, `RunUpdate`, `EndUpdate` und die entsprechenden Editor-/IDocument-Delegationen bleiben nutzbar. Ein explizites äußeres Update darf mehrere nacheinander aufgerufene öffentliche Änderungen enthalten. Das ist keine Erlaubnis für einen Ereignishandler: Während der Zustellung ist der neue öffentliche Aufruf gesperrt. Legales Begin/End-Pairing bleibt erforderlich; absichtlich falsch gepaarte Raw-Aufrufe werden nicht automatisch repariert.

`TextEditor.Text` hält seine Operation bis **nach** Caret-Reset und ClearAll. `Clear()` delegiert unverändert an diesen nichtvirtuellen Setter. Gleiches beziehungsweise leeres Textsetzen wird ebenfalls vor diesen Effekten geprüft. `SelectedText` hält die Operation bis zur abschließenden Auswahl; `Load(Stream)` im strikten Modus bis Encoding-/IsModified-Finalisierung. `AppendText` hat nach seinem Dokumentinsert keine weitere Setter-Nacharbeit.

`Undo`/`Redo` werden vor Pop/Push geprüft. Native `DocumentChangeOperation`-Objekte delegieren ihren Replay intern; ihre öffentlichen Callbackaufrufer erhalten dieses Recht nicht. Auch `ClearAll` ist früh geschützt. Ein verbotenes ClearAll innerhalb einer vorhandenen Gruppe zerstört im strikten Modus nicht mehr zuerst deren Tiefe. Rohe beliebige Stackmanipulationen, fremde IUndoableOperation-Implementierungen und absichtlich unzulässige Gruppenpaare sind weiterhin die schon in der Architekturentscheidung ausgeschlossenen Grenzen; sie werden nicht als vollständige unterstützte Host-API verkauft.

### 3.3 Eigentümerschaft und unveränderte Engine

Der Versuch arbeitet mit einem dokumenteigenen UndoStack. Shared-UndoStack-Konfigurationen über mehrere Dokumente sind **nicht nachgewiesen und für den strikten Host nicht zu konfigurieren**. Die Policy prüft einen bereits als strikt gebundenen fremden Stack; beim Replay wird ein fremdes betroffenes Dokument zurückgewiesen. Das ist kein vollständiger Shared-Stack-Vertrag und ersetzt keine künftige Ownership-Prüfung.

Rope, DocumentLineTree, TextAnchorTree, OffsetChangeMap, Dokumentidentität und native Undo-Operationen werden weiterverwendet. Der Algorithmus der Text-/Anchor-Verarbeitung wurde nicht ersetzt. Die vorhandene Host-Coercion bleibt für Document-/Editor-/Viewwechsel zuständig. Die neue Policy ist eine Kooperations- und Reentrancygrenze, kein Schutz gegen absichtliches Umkonfigurieren der Policy durch den Besitzer, Reflection oder willkürliche rohe Stackeingriffe.

## 4. Event- und Exception-Vertrag des Prototyps

| Situation | Striktes Verhalten |
| --- | --- |
| Policy lehnt vor Start ab | Exception vor Zähler/Gruppe/Text/Anchor/Caret/Selection/History; keine Update-/Text-/Abschlussereignisse dieser abgewiesenen Operation |
| UpdateStarted-Observer wirft | Die tatsächlich begonnene Gruppe wird genau einmal geschlossen; kein Texteffekt; kein verwaistes Undo-Update; anschließend Fehlerausgabe |
| Changing-Observer wirft, noch keine Textwirkung | Kein Textcommit; korrektes Gruppenende; vorhandene tatsächliche Undo-/Redo-Folgen bleiben nutzbar |
| Changed/TextChanged/PropertyChanged/Anchor-/Caret-/Selection-/Undo-Observer wirft | Erforderliche weitere Zustellung und native Finalisierung laufen weiter; angewandter Text wird nicht zurückgerollt; Fehler danach sichtbar |
| EndUpdate/UpdateFinished wirft nach früheren Änderungen einer expliziten Gruppe | Zähler und Undo-Gruppe geschlossen; Fehler kennzeichnet die bereits erfolgte Gruppen-Textwirkung |
| Undo/Redo scheitert vor erster Textwirkung | Der zuvor umgehängte native Historieneintrag wird zurückgelegt; ein anschließendes echtes Undo/Redo funktioniert |
| Gruppierter Replay hat schon Text angewandt und ein späterer Observer wirft | Verbleibender nativer Replay wird abgeschlossen, Fehler gesammelt; keine Behauptung einer wirkungslosen Ablehnung und kein Text-Rollback |
| Native strikte Paste | Frühe Prüfung vor Clipboard-await, keine offene Gruppe während des awaits, erneute Prüfung am Commit; ursprüngliche Dokumentreferenz prüfen; EndUpdate auch bei Fehlern im Text-/Caretpfad |

Die normale Ereignisreihenfolge bleibt bei erfolgreicher Mutation erhalten. Das einfache dokumentierte Testbeispiel liefert `Started → Changing → Changed → TextChanged → PropertyChanged(Text) → PropertyChanged(TextLength) → PropertyChanged(LineCount) → Finished`. Die erstmalige LineCount-Meldung entspricht dem Ausgangsstand; sie wurde nicht aus dem Test entfernt, um eine gewünschte Reihenfolge zu erzwingen.

Im strikten Modus werden Abonnenten einzeln abgewickelt. Ein werfender Abonnent verhindert die notwendigen späteren Empfänger nicht. Das gilt auch innerhalb vorhandener Weak-Event-Fanouts, für verzögerte Anchorereignisse und für die fünf LineTracker-Benachrichtigungsstellen. Native Caret-/Selection-/TextEditor-Textmeldungen werden ebenso behandelt. Die schon vorhandene WeakEventManager-Reflection wurde nicht durch private Reflection auf Dokumentzustand ergänzt; es gibt keine neue Reflection-Reparatur der Engine.

Fehler aus einer zugelassenen Operation werden als `MutationCallbackException : AggregateException` mit `TextChanged` ausgegeben. Dieses Flag bezeichnet **Textwirkung**, nicht die Transaktion aller denkbaren Benutzerhandler-, Clipboard-, Metadaten- oder Dateieffekte. `false` ist insbesondere kein pauschales `effects=None` und keine Rollbackzusage. Der spätere Host muss weiterhin ursprüngliche Dokumentidentität und tatsächliche Version für seinen Abschluss verwenden. Die vorhandenen MCP-DTOs wurden nicht geändert.

Optionales Caret-Undo-Bookkeeping darf im strikten Modus noch keine Redo-Historie löschen, wenn ein Start-/Changing-Observer vor der ersten Textoperation scheitert. Erst eine nichtoptionale Änderung verwirft Redo. Diese zusätzliche Grenze wurde mit **tatsächlichem vorhandenen Redo** geprüft; bloßes Vergleichen von `CanUndo` genügte ausdrücklich nicht.

Ohne Policy gelten die bisherigen Multicast- und Normalisierungsregeln. Die alte freie UpdateFinished-Normalisierung terminiert weiterhin bei zwei Aufrufen/Tiefe 2. Mit aktivierter strikter Policy ist dieser unmittelbare Wiedereintritt absichtlich abgewiesen; wer Normalisierung benötigt, muss sie später als gesonderte, erneut zugelassene Operation gestalten. Keine automatische Wiederholung oder versteckte Warteschlange wird hinzugefügt.

## 5. Konkrete Bibliotheksänderungen

Die folgende Liste bezieht sich auf `mcp014-b1/library/src/AvaloniaEdit/`. Der vollständige lokale Unified Diff liegt in `mcp014-b1/final-library.patch`; `git apply --check` gegen die unveränderten Ausgangsdateien war erfolgreich.

| Datei | Geänderte/ergänzte Methoden und Zweck |
| --- | --- |
| `Document/MutationPolicy.cs` – neu | `IMutationPolicy`, `MutationKind`, `MutationCallbackException`: optionaler Vertrag und wahrheitsgemäßer Fehlerabschluss |
| `Document/TextDocument.Mutation.cs` – neu | `MutationPolicy`, `RunMutation`, `NotifyObserver`, `NotifyHandlers`, `Notify`, `StopOnObserverErrors`, `PrepareMutationSource`, `MarkTextEffect`: dokumentgebundene Eintritts-/Callbackgrenze |
| `Document/TextDocument.cs` | `BeginUpdate/BeginUpdateCore`, `EndUpdate/EndUpdateCore`, Replace-Endpunkte/`ReplaceMappingCore`/`ReplaceCore`, `OnPropertyChanged`, `FireChangeEvents`, Ereignisaufrufe in `DoReplace`, UndoStack-Zuweisungsgrenze; keine neue Rope-/Anchor-Engine |
| `TextEditor.cs` | Textsetter/`SetTextCore`, `SelectedText`, `Load(Stream)`, `OnTextChanged`; vollständige Setter-Finalisierung. `Clear` delegiert weiterhin an Text |
| `Document/UndoStack.cs` | `Undo/UndoCore`, `Redo/RedoCore`, `ClearAll/ClearAllCore`, `ThrowIfUndoGroupOpen`, `RegisterAffectedDocument`, `CallEndUpdateOnAffectedDocuments`, optionales `Push`, `NotifyPropertyChanged` |
| `Document/DocumentChangeOperation.cs` | Native Undo-/Redo-Delegation an `ReplaceCore` |
| `Document/LineManager.cs` | Fünf Tracker-Zustellstellen kontrolliert ausführen, vorhandene Zeilenalgorithmen erhalten |
| `Editing/Caret.cs` | PositionChanged-Zustellung bei unmittelbarer Änderung und Updateabschluss |
| `Editing/TextArea.cs` | SelectionChanged-Zustellung |
| `Editing/EditingCommandHandler.cs` | `OnPaste` optionaler strikter Zweig, `PasteStrictAsync` mit korrektem Gruppenabschluss; Legacy-Zweig unverändert |
| `Utils/DelayedEvents.cs` | `EventCall.Call`, `RaiseEvents`: kontrollierte Zustellung einzelner verzögerter Empfänger |
| `Utils/WeakEventManagerBase.cs` | `WeakHandlerList.DeliverEvent`: erforderliche spätere Empfänger trotz Fehler erreichen |

Damit umfasst die funktionale Änderung zehn bestehende Bibliotheksdateien und zwei neue Vertragsdateien. Hinzu kommen vier lokale Buildkonfigurationen und zwei neue eigenständige Testdateien. Der Gesamtpatch enthält 804 Einfügungen/88 Löschungen, einschließlich 364 Zeilen neuem Testprogramm. Das ist ein reviewbarer PoC, kein fertig abgestimmter Upstream-PR. Die ursprünglichen Upstreamtestquellen wurden nicht geändert.

## 6. Exakte 46er-Sollmatrix

Quelle: `mcp014-corrections2/BoundaryProbe.cs`. Mechanische Testhostanpassungen: asynchrones Headless-Dispose für Avalonia 12 und genau eine Policy-Anbindung nach `Window.Show()`. Entfernt man diese beiden Anpassungen und normalisiert die Zeilenenden, ist die Probe identisch zum Original. Das wurde maschinell geprüft; alle Bedingungen der 46 Assertions sind erhalten.

Ergebnis des abschließenden Laufs: **46 PASS, 0 FAIL, Exit 0**. Die Ausgabe `nested=ExecutionBusyException`, `open=False`, `next=True` und echte Undo-/Redo-Prüfungen gehören zu den Callback-Solltests. Save-Fälle verlangen jeweils drei Ablehnungen, unveränderten Snapshot, ursprüngliche Dokumentidentität, geschlossene Gruppe, reale Historie, erfolgreichen Save und Folgeedit.

| Nr. | Sollprobe | B1 | Exakte Prüfausgabe |
| --- | --- | --- | --- |
| 1 | `callback/Started/True/same` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 2 | `callback/Started/True/different` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 3 | `callback/Started/True/clear` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 4 | `callback/Started/True/document` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 5 | `callback/Started/False/same` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 6 | `callback/Started/False/different` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 7 | `callback/Started/False/clear` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 8 | `callback/Started/False/document` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 9 | `callback/Changed/True/same` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 10 | `callback/Changed/True/different` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 11 | `callback/Changed/True/clear` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 12 | `callback/Changed/True/document` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 13 | `callback/Changed/False/same` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 14 | `callback/Changed/False/different` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 15 | `callback/Changed/False/clear` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 16 | `callback/Changed/False/document` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 17 | `callback/Finished/True/same` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 18 | `callback/Finished/True/different` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 19 | `callback/Finished/True/clear` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 20 | `callback/Finished/True/document` | PASS | `reply=True; error=; nested=ExecutionBusyException; after= edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 21 | `callback/Finished/False/same` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 22 | `callback/Finished/False/different` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 23 | `callback/Finished/False/clear` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 24 | `callback/Finished/False/document` | PASS | `reply=True; error=; nested=ExecutionBusyException; after=xy edit; rev=2->3; open=False; next=True/; undoRedo=True` |
| 25 | `save/True/False/same` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 26 | `save/True/False/different` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 27 | `save/True/False/clear` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 28 | `save/True/False/document` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 29 | `save/True/True/same` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 30 | `save/True/True/different` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 31 | `save/True/True/clear` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 32 | `save/True/True/document` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 33 | `save/False/False/same` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 34 | `save/False/False/different` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 35 | `save/False/False/clear` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 36 | `save/False/False/document` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 37 | `save/False/True/same` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 38 | `save/False/True/different` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 39 | `save/False/True/clear` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 40 | `save/False/True/document` | PASS | `rejected=3; swapBlocked=True; unchanged=True; open=False; history=True/True/True; save=; next=True` |
| 41 | `normalizer/save=False` | PASS | `calls=1; depth=1; intentionalLimit=16; open=False; next=True` |
| 42 | `normalizer/save=True` | PASS | `calls=0; depth=0; intentionalLimit=16; open=False; next=True` |
| 43 | `save/throwingFinished` | PASS | `observerCalls=0; unchanged=True; open=False; undo=True; next=True` |
| 44 | `saveFromCallback/Started` | PASS | `pending=False; error=Busy; deletes=0; reply=True; open=False; next=True` |
| 45 | `saveFromCallback/Changed` | PASS | `pending=False; error=Busy; deletes=0; reply=True; open=False; next=True` |
| 46 | `saveFromCallback/Finished` | PASS | `pending=False; error=Busy; deletes=0; reply=True; open=False; next=True` |

Die vier vorher roten Fälle sind damit nicht nur umetikettiert:

- `callback/Started/True/same` und `…/clear`: äußeres Edit erfolgreich, Revision 2→3, Gruppe geschlossen, verschachtelter Setter früh Busy, echtes Undo/Redo und Folgeedit erfolgreich.
- `normalizer/save=False`: ein Callback, maximale Tiefe 1 statt 16.
- `normalizer/save=True`: null Callbacks, Tiefe 0 statt 16; die abgewiesene Operation erzeugt kein UpdateFinished.

Gegenkontrolle mit `B1_POLICY=off`: **42 PASS / 4 FAIL, Exit 1**, exakt dieselben roten Fälle. Die drei ausgegebenen Alternativkontrollen ohne Policy bleiben ebenfalls unverändert: UpdateStarted-Throw kann eine Gruppe offen lassen; Changing allein schützt einen leeren Setter nicht vor History-Clear; IsReadOnly allein verhindert keinen direkten Textsetter. Das sind bewusst erhaltene Legacy-Negativkontrollen, keine roten strikten Solltests.

## 7. Ursprüngliche R6-/R7-Programme und zusätzliche Solltests

Das ursprüngliche `mcp014-final-independent-review/Program.cs` enthält `Require`-Assertions, welche die **Fehlersymptome** absichern. Sie dürfen bei einer Korrektur nicht als positive Produktgarantie gelesen werden. Die Assertions wurden unverändert erhalten und für alle sieben Szenarien einzeln aufgerufen, damit ein erwarteter Abbruch die übrigen Szenarien nicht auslässt.

| Originalfall | Policy aus | Policy an | Positive B1-Sollkontrolle |
| --- | --- | --- | --- |
| R6 direkter Insert | Exit 0: alter offener Updatezustand, Folge-Busy | Alte Fehlerassertion schlägt fehl | Direkter Insert und Clear-Variante: geschlossen, ursprüngliche Identität, Anchor korrekt, Revision +1, Undo/Redo/Folgeedit erfolgreich |
| R6 Hostedit | Exit 0: alter InternalError nach Textwirkung | Alte Fehlerassertion schlägt fehl | Hostedit und Clear-Variante mit denselben positiven Anforderungen bestanden |
| R7 freie Kontrolle | Exit 0: zwei Aufrufe/Tiefe 2, normalisierter Text | Alte Legacy-Erwartung schlägt unter bewusst strikter Policy fehl | Ein Aufruf/Tiefe 1, verschachtelter Insert abgewiesen, äußerer Text und Folgeedit korrekt |
| R7 Save, Limit 8 | Exit 0: acht Aufrufe/Tiefe 8 | Alte Fehlerassertion schlägt fehl | Null Aufrufe/Tiefe 0, früher Busy, Folgeedit erfolgreich |
| R7 Save, Limit 32 | Exit 0: 32 Aufrufe/Tiefe 32 | Alte Fehlerassertion schlägt fehl | Null Aufrufe/Tiefe 0, früher Busy, Folgeedit erfolgreich |
| R7 Edit, Limit 8 | Exit 0: acht Aufrufe/Tiefe 8 | Alte Fehlerassertion schlägt fehl | Ein Aufruf/Tiefe 1, Edit und Folgeedit erfolgreich |
| R7 Edit, Limit 32 | Exit 0: 32 Aufrufe/Tiefe 32 | Alte Fehlerassertion schlägt fehl | Ein Aufruf/Tiefe 1, Edit und Folgeedit erfolgreich |

Die sieben strikten Originalläufe enden mit der unveränderten `InvalidOperationException("Probe observation changed: …")`, Windows-Prozesscode `-532462766`. Sie werden **nicht als sieben bestandene Tests** gezählt. Die ergänzenden positiven Anforderungen stehen getrennt in `host-contracts/Program.cs`: **9/9 PASS, Exit 0**. Dazu gehören vier R6-Kombinationen, die freie R7-Kontrolle und je zwei Save-/Editlimits.

Eine notwendige mechanische Harnesskorrektur kam hinzu: Die alte Save-Normalisiererprobe gab ihr künstlich blockiertes Delete erst **nach** ihrer Fehlerassertion frei. Sobald die alte Fehlererwartung nicht mehr zutrifft, wartete das Dispose deshalb auf die noch gehaltene eigene Save-Lease. Ein erster eigener Prozess wurde anhand seiner konkreten PID/Commandline beendet. Im kopierten Testhost steht die Freigabe nun im `finally`; die Require-Bedingung ist byteinhaltlich unverändert. Danach liefern auch diese beiden alten Beobachtungsprogramme den erwarteten sauberen Fehlerabschluss statt eines Harness-Hangs. Dies war keine Bibliotheks-Deadlockkorrektur und keine abgeschwächte Assertion.

Weitere unveränderte Originalkontrollen: `original/Program.cs` liefert Exit 0, unter anderem Save-Drain, Shutdown nach Delete, Widerruf, Retry/Registry, ursprüngliche Identität bei Callbacktausch, echtes Undo/Redo nach Save, blockierte Texteingabe/Clipboard-Kommandos und Fehler nach Delete. Die rohe Undo-Negativbeobachtung verbessert sich in dieser Variante zu früher Busy mit erhaltenem Eintrag; daraus wird keine allgemeine Freigabe beliebiger Raw-Stackmanipulation abgeleitet. Die separat ausgeführten zusätzlichen Start-/Detach-/Dispose-Kontrollen liefern ebenfalls Exit 0.

## 8. Eigenständige Bibliothekstests

Die Tests liegen direkt unter `library/test/MutationContractTests/`, verwenden Avalonia/Headless und die lokale AvaloniaEdit-Projektreferenz, aber **keine CryptoWorkBench-, MCP-, CryptoScript- oder Prism-Referenz**. Das ausführbare Testprogramm protokolliert jede Sollprüfung und liefert bei irgendeinem Fehlschlag Exit 1.

| Testgruppe | Bestandene Solltests |
| --- | --- |
| `anchors` | 4 |
| `clearall` | 1 |
| `events` | 1 |
| `foreign-callback` | 6 |
| `foreign-source` | 1 |
| `group-exception` | 6 |
| `grouped-replay` | 8 |
| `IDocument` | 1 |
| `legacy` | 3 |
| `line-trackers` | 6 |
| `live-undo-and-redo` | 12 |
| `load` | 2 |
| `manual-group` | 1 |
| `native-paste` | 3 |
| `native-text-input` | 3 |
| `noop` | 1 |
| `policy` | 1 |
| `pre-effect-error-preserves-redo` | 2 |
| `reentry` | 132 |
| `reject` | 24 |
| `setter-finalization` | 6 |
| `standalone-callback` | 2 |
| `throw` | 11 |
| `undo-observer` | 5 |
| `weak-event-later-listener` | 1 |
| **Gesamt** | **243** |

Die Ablehnungstests vergleichen Text, Version, Dokumentreferenz, Caret, Auswahl, Anker, Eventanzahl und offene Updates; sie führen anschließend echte Undo-/Redo-Folgen und einen Folgeedit aus. Weitere zwölf Fälle starten ausdrücklich mit **gleichzeitig gefüllter Undo- und Redo-Historie**. Die 132 Reentrancyfälle sind elf Callbackflächen × zwölf öffentliche Operationen. Der Full-Setter-Test verlangt bei einem nachwirkenden Observerfehler trotzdem den abschließenden Caret-Reset und das vorgesehene ClearAll.

Die Gruppentests prüfen sowohl Fehler beim ersten als auch beim zweiten Schritt und beide Replayrichtungen. LineTracker-Proben prüfen native Zeilensummen, Anker und tatsächliches Undo/Redo trotz werfendem Tracker; sie garantieren nicht, dass beliebiger fremder Tracker seinen eigenen privaten Zustand korrekt repariert. UTF-16-/CRLF-Ankerfälle prüfen Bewegungsrichtung, Löschung und SurviveDeletion anhand der bestehenden Engine.

Die nativen Paste-Proben besitzen einen echten Headless-Window-/Template-/Clipboard-Kontext und prüfen: erfolgreicher Paste; bereits angewandter Paste plus Changed-Fehler mit geschlossenem Update; frühe Policyablehnung ohne Started/Finished, Historie-, Caret- oder Auswahleffekt. Erwartete async-void-Kommandofehler werden im **Testhost** über `Dispatcher.UnhandledException` erfasst. Das ist keine produktive Fehlerbehandlung; B2 muss die UX dafür ausdrücklich entwerfen.

Zusätzlich wurde die unveränderte vorhandene AvaloniaEdit-NUnit-Suite ausgeführt: **354 bestanden, 0 fehlgeschlagen, 0 übersprungen**, einschließlich vorhandener Dokument-/Anchor-/Undo-/TextMate-/Search-/Renderingtests. Ihre Testquellen sind gegenüber dem Release unverändert. Sie verwenden keine neue strikte Policy und belegen damit das erhaltene Standardverhalten in ihrer Abdeckung; sie beweisen keine Fehlerfreiheit aller denkbaren Fremderweiterungen.

Zwischenstände werden erhalten: Der erste eigenständige Lauf deckte echte Observer-/Undo-Probleme sowie Fehler im neuen Testaufbau auf. Der leere Anchor wurde zunächst an einem ungültigen festen Offset angelegt; für Undo-Benachrichtigungen musste zunächst ein tatsächlich wechselnder Originalzustand hergestellt werden; die erwartete Erst-LineCount-Meldung und eine tatsächlich bewegte Caretposition wurden korrigiert. Keine ursprüngliche Assertion wurde geändert. Ein erster Paste-Test ohne angewandtes Template besaß keinen angebundenen TextArea-TopLevel; erst nach korrektem Fixtureaufbau wurde die reale offene Paste-Gruppe sichtbar. Dieser rote Lauf bleibt in `native-paste-second.log` dokumentiert.

## 9. Native Editorfunktionen und verbleibende Risiken

| Fläche | B1-Evidenz / Konsequenz für B2 |
| --- | --- |
| Text, Clear, Document.Text, Insert/Remove/Replace, AppendText, SelectedText | Frühe Zulassung und Vollsetterabschluss nachgewiesen; originale nichtvirtuelle Basistypen bleiben erhalten |
| Undo/Redo/ClearAll | Native History und Gruppen bleiben maßgeblich; echte Folgen, vorhandenes Redo und werfende Observer geprüft |
| Caret/Selection/Anchors/Zeilen | Reentrancy und Zustellung im nachgewiesenen Mutationspfad geprüft; native UTF-16-/CRLF-/Anchor-Algorithmen erhalten |
| Textinput, Newline, Tab | Positive direkte Headless-TextArea-Proben mit einem echten Undo/Redo bestanden; kein physischer Tastatur-/IME-Nachweis |
| Paste | Erfolg, früher Busy und Changed-Fehlerabschluss nachgewiesen; strikter Zweig hält keine Gruppe über await und prüft ursprüngliche Dokumentreferenz. Verzögerte native Clipboardanbieter, Plattformownership, Lifecyclegeneration und UI-Fehlerbehandlung bleiben B2-/Plattformtests |
| Cut/Copy und Save-Lock-Kommandos | Vorhandene Hostkontrollen während Save unverändert erfolgreich. Vollständige asynchrone Clipboard-Nebenwirkungen sind damit nicht abgenommen |
| Search/ReplaceAll/ReplaceNext | Vorhandene Legacy-Upstreamtests grün. Für strikte Compositezulassung separat prüfen: `ReplaceNext` sucht/selektiert vor der anschließenden Textmutation; ein Dokumentgate alleine schützt nicht diese vorbereitende Auswahl |
| Overstrike / Rechteckauswahl / Provider / Indentation | Im strikten integrierten Modus noch vollständig abzunehmen. `PerformTextInput` kann im Overstrikepfad vor dem Dokumentaufruf selektieren; Drittprovider sind Fremdcode. Keine pauschale Behauptung zustandsneutraler Ablehnung jedes nativen Compositekommandos |
| Drag-and-drop / Completion | Native Dokument-/Anchor-Bausteine erhalten. Asynchrones Move mit ursprünglicher Dokumentbindung, Popup-/Selection-Lifecycle und komplette Commandgrenzen bleiben konkrete B2-Abnahmeaufgaben |
| Shared UndoStack / fremde rohe Undo-Operationen | Kein nachgewiesener strikter Mehrdokumentvertrag; im Host nicht konfigurieren. Bestehender Raw-Ausschluss bleibt sichtbar |
| Frameworkmigration / ExecutionContext | Kein B1-Fix. Die U12-Befunde zu Dispatcher-Capabilityfluss und Retention bleiben unabhängig offen; keine Änderung am CryptoScript-Core |

Die erkennbaren vorbereitenden UI-Effekte bei Overstrike/ReplaceNext werden ausdrücklich **nicht** als durch den Dokumenthook behoben erklärt. Sie machen eine vollständige GUI-Integration weiterhin unzulässig, nehmen aber den konkret beauftragten B1-Kernnachweis für direkte Text-/Setter-/Gruppen-/History-Einstiege und die vier roten Sollfälle nicht zurück. B2 muss vollständige native logische Operationen zulassen und interne Delegation kontrollieren; es darf hierfür weder Callbackrechten pauschal vertrauen noch den MCP-00-Vertrag einschränken. Sollte sich beim B2-Design zeigen, dass dies nur durch einen unvertretbaren Umbau erreichbar ist, ist erneut BLOCKED zu entscheiden.

Weitere Risiken: Methodennamen und Exception-API sind noch nicht mit Upstream abgestimmt; die strikte Eventzustellung ist eine explizite neue Opt-in-Semantik; Fehlerlisten können innere Exceptiondetails enthalten und dürfen nicht ungefiltert in externe MCP-Antworten gelangen; Materialisieren fremder Textquellen kostet Speicher; Delegate-/Callbackpfade benötigen später Performancebewertung. Callbackfehler werden nachwirksam sichtbar, nicht verschluckt. Ein nie zurückkehrender Handler, Prozessabbruch, OOM oder fremder unsicherer Code wird durch diesen PoC nicht beherrscht.

## 10. Upstreamfähigkeit und empfohlene B2-/B3-Grenze

Der Ansatz ist technisch upstreamfähig: generisches optionales Interface, eigenständige Tests, keine Hostnamen im Bibliothekscode, kein Dispatcher-/Interpreterdienst als Abhängigkeit und keine zweite Engine. Die ursprünglichen öffentlichen Typen bleiben sealed beziehungsweise die Setter nichtvirtuell; die Lösung hängt nicht von einem versteckenden Workbench-Setter ab. Der lokale Patch ist anwendbar, aber weder eingereicht noch von Upstream akzeptiert.

Für eine spätere Reviewaufteilung eignen sich:

1. **Strikter Operations-/Exception-Vertrag und Testfälle:** öffentliche Zulassung versus interne Core-Delegation, genaues Eventverhalten, Vor-/Nachwirkung, Policyfehler, Undo-/Redo-Abschluss.
2. **Native Composite- und Hostdesignentscheidung:** vollständige Commandgrenzen, Save-/Identity-Anbindung, neue Dokumente/Detach, UI-Behandlung früher Ablehnung, Async-Paste/Drag/Completion und die konkret benannten vorbereitenden UI-Effekte. Die bisherige Hostbereinigung darf erst nach diesem Design gezielt ersetzt werden.
3. **Separater B3-/Migrationsentscheid:** geprüfte Bibliotheksdistribution und Pflege, Framework-/Prism-/Contextintegration, Plattformen, Publish und vollständige Produktregression. Die lokale Source-DLL ist keine Produktionsabhängigkeit.

Keine dieser Einheiten wurde im produktiven Worktree umgesetzt. B1 gibt insbesondere keine Freigabe, die bisherigen Reviewberichte auf VERIFIED FIXED umzuschreiben. Die Eigentümerentscheidung lautet jetzt: auf Basis des belegten Kerns B2 entwerfen oder den generischen Upstreamvertrag vor weiterer Hostarbeit zuerst separat reviewen lassen. Ein Upstream-Issue oder Pull Request erfordert weiterhin einen eigenen Auftrag.

## 11. Reproduktion und Evidenz

Alle nachstehenden Versuchspfade liegen unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1/`.

| Evidenz | Datei |
| --- | --- |
| Produktiver Ausgangsstatus / 659 erfasste Ausgangsdateien | `status-before.txt`, `staged-before.txt`, `source-manifest.json` |
| Unveränderte Bibliotheksbasis / Änderungen | `library-baseline.json`, `library-changes.json` |
| Reviewbarer vollständiger lokaler Patch | `final-library.patch`, `diff-stat.txt`, `patch-check.exit` = 0 |
| Unveränderte Soll-/Originalassertions | `probe-integrity.json` |
| Finale 46er-Matrix | `boundary-final.log`, `boundary-final.exit` = 0 |
| Legacy-Gegenmatrix | `boundary-policy-off.log`, `.exit` = 1 |
| Original-R6-/R7-Beobachtungen | `legacy-r6-*.log`, `legacy-r7-*.log`, `strict-r6-*.log`, `strict-r7-*.log`, jeweils eigene Exitdatei |
| Positive ergänzende R6-/R7-Solltests | `host-contracts/Program.cs`, `host-contracts.log`, `.exit` = 0 |
| Weitere ursprüngliche Hostkontrollen | `original-controls-final.log`, `.exit` = 0; `strict-other.log`, `.exit` = 0 |
| Eigene Bibliothekstests | `library/test/MutationContractTests/Program.cs`, `library-tests-final.log`, `.exit` = 0 – 243/0 |
| Zusätzliche echte Paste-Gegenprobe vor Korrektur | `native-paste-second.log` – 241/1, offenes Update nach Textwirkung |
| Unveränderte Upstreamtests | `upstream-tests-final.log`, `.exit` = 0; `test-results/upstream-final.trx` – 354/0/0 |
| Abschließender Schutz des Originalworktrees | `integrity-final.json` |

Reproduktion der eigenständigen Bibliotheksnachweise mit dem vorhandenen lokalen Stand:

```powershell
$b1 = 'C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-b1'
dotnet build "$b1/library/test/MutationContractTests/MutationContractTests.csproj" -c Release --configfile "$b1/NuGet.Config"
dotnet "$b1/library/test/MutationContractTests/bin/Release/net10.0/MutationContractTests.dll"
dotnet restore "$b1/library/test/AvaloniaEdit.Tests/AvaloniaEdit.Tests.csproj" --configfile "$b1/NuGet.Config"
dotnet test "$b1/library/test/AvaloniaEdit.Tests/AvaloniaEdit.Tests.csproj" -c Release --no-restore
```

Die Hostprojekte `boundary`, `additional`, `original` und `host-contracts` verwenden die im Projektfile explizit referenzierten U12-Fixtureassemblies. Für deren native Headless-Textdarstellung wurde die vorhandene win-x64-`libHarfBuzzSharp.dll` in das jeweilige Ausgabeverzeichnis kopiert. `run-final.ps1` dokumentiert die getrennten strikten/Legacy-Beobachtungsläufe. `prepare.ps1` und `patch-library.ps1` sind historische Aufbauschritte; insbesondere der erste Patchversuch ist **nicht** der Reproducer des später vervollständigten Quellstands. Maßgeblich sind `library/`, `final-library.patch` und die finalen Logs.

SHA-256 des finalen Patches: `2265133825B9ED3364EA6F2646FDCBF1D8DA7C384D6218C0E4F81A671AF394B9`.

SHA-256 der getesteten lokalen `AvaloniaEdit.dll`: `97C6497157F85AD9B7286D1690C3FFD63FE739D7C717552380A6847443232A6B`.

Originalproben unverändert:

- BoundaryProbe: `174706C331B9D5F50CDE1A78FF468F20B662BF3029921CCAFE7184983C7DE857`.
- Ursprüngliche unabhängige Gegenproben: `A893703D94A57EE4CA032433B85E86F7342FF49AB94F87AA4E921ED65C775294`.
- Ursprüngliches R6-/R7-Programm: `84B11377EC1729F4573A6A12A456E41B068E78D97E192694F6B93CFA60D7A382`.

## 12. Abschluss und Eigentümerentscheidung

**GO FOR B2 DESIGN.** Das B1-Exit-Gate von 46/46 ist erfüllt; der frühe optionale Kernvertrag, die vollständigen Textsettereffekte und die untersuchten Exception-/Reentrancy-Grenzen sind mit einer isolierten lokalen Bibliotheksvariante nachgewiesen. Der separate native Paste-Befund wurde nicht ausgeblendet, sondern korrigiert und erneut geprüft.

Im produktiven Worktree wird ausschließlich dieser Bericht ergänzt. Die 659 erfassten Ausgangsdateien einschließlich der bisherigen Berichte, Branch/HEAD und Staging werden vor und nach der Ablage geprüft. Keine produktiven Quellen oder Pakete, keine MCP-/Core-Verträge und keine Interpreterpipeline wurden verändert. Kein Commit, Push, Merge oder Rebase; keine HTTP-Aktivierung; kein Zugriff auf `.codex/config.toml`; keine Upstream-Veröffentlichung; kein MCP-01.5.

Hier endet B1. Der Projekteigentümer entscheidet über B2-Design, unabhängiges Review und den späteren Integrations-/Distributionsweg. Die produktive GUI-Migration bleibt separat gesperrt.
