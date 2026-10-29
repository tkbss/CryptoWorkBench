# MCP-00-AP2.1 – EditorCoordinator-Grundkern: Implementation Review

Stand: 26.10.2026. Arbeitsbasis: `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`.

## 1. Bewertung und Freigabegrenze

**Für einen separaten unabhängigen Review vorbereitet; AP2.1 noch nicht als abgeschlossen freigegeben.** Der serviceeigene GUI-Grundkern und die native Eingangsvermittlung sind produktiv eingebunden. Die vollständigen GUI-/Core-Regressionen und die bestehenden MCP-Tests wurden ausgeführt. Neue Tests prüfen die tatsächlichen Produktklassen unter Avalonia Headless, keine kopierte H4-Laborimplementation.

Der normale Betrieb bleibt bei GUI-Besitz. Es gibt **keine** MCP-Acquire-/Owner-Publikationsmethode, keinen öffentlichen Testschalter und keine aktive MCP-Besitzphase. Ein späterer MCP-Übergang muss die hier vorbereiteten Zulassungs-, Generation-, Ticket- und Drainbedingungen mit AP3–AP6 vervollständigen. AP2.1 stellt die dafür notwendige Sperr-/Übergangsbasis bereit, implementiert jedoch bewusst keine Lease- oder Credentialautorität. Bestehende v1-Service-/Transportverträge werden nicht zu v2 erklärt und keine externe Übernahme wird neu aktiviert.

Die Eigentümerentscheidung zum akzeptierten IME-Restrisiko ist als datierte, rein angehängte Ergänzung in R2 und AP0 dokumentiert. Frühere Abschnitte und alle historischen Untersuchungsberichte bleiben erhalten. Die U2-Bewertung ist weiterhin INCONCLUSIVE; der fehlende native CJK-Negativtest ist gemäss neuer Entscheidung kein zwingendes Freigabegate mehr. Alle übrigen Gates bleiben bestehen.

## 2. Ausgangsstand und Erhalt der bestehenden Arbeiten

Vor der ersten Änderung wurden HEAD, Branch, Porcelain-v2-Status, leerer staged Diff und 23.368 Dateihashes erfasst. Der Ausgangsstand enthielt sechs modifizierte getrackte und 21 ungetrackte Dateien. Alle damals uncommitteten Dateien wurden zusätzlich separat originalgetreu gesichert; der Ausgangsdiff ist ebenfalls erhalten.

Prüfbereich ausserhalb des Produktworktrees: `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp00-ap21`. Dort liegen `baseline.json`, `baseline.diff`, `originals/`, alle Build-/Testlogs und TRX-Dateien sowie `verification.json` und `ap21-only.diff`. Jede Veröffentlichung prüft zuvor den erwarteten Dateihash und bricht bei konkurrierender Änderung ab. Keine vorhandene Änderung wurde durch einen Git-Reset, Stash oder Zurücksetzen auf HEAD ersetzt.

Die abschliessende Prüfung unterscheidet autorisierte Quelländerungen, Buildausgaben und unverändert zu erhaltende Evidenzen. `bin/obj`-Änderungen durch die geforderten Produkt-Testläufe sind gesondert ausgewiesen; sie werden nicht als unveränderte Originaldateien ausgegeben. `.codex/config.toml` ist vor Inhaltszugriff ausgeschlossen, wurde weder gelesen noch kopiert noch verändert. Kein Commit, Push, Merge, Reset, Rebase oder Stash.

## 3. Dateien und Einbindung

| Datei | Änderung gegenüber dem tatsächlichen uncommitteten Ausgangsstand |
|---|---|
| `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` | Neu: serviceeigener Coordinator, Generation, gebundene Operations-/Callbacktickets, Observerrahmen, interner D01-Beleg und lokaler Drain |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | Neu: tatsächliche TextArea-Unterklasse mit frühen nativen Eingängen, einmaliger Delegation, gebundenem Clipboard und permanenter IME-/Textdrag-Sperre |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | Bestehender Guard erhalten und erweitert; neue TextArea im Basiskonstruktor; ursprüngliche Undo-/Redo-Registrierungen erhalten statt entfernt/ersetzt; StyleKey; Identitäts-/Observergrenze |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs` | Einbindung des Coordinators in Attach/Detach; Admission vor Publikation einer neuen Registrierung; bestehende Identität/Revisionsquelle unverändert zuständig |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs` | Ausschliesslich serviceeigener Coordinatorzugriff und Initialisierung beim Attach. UpdateEditor-/ExecuteEditor-Methodenkörper unverändert, einschliesslich EC21 |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | Caretstatus- und TextEntered-Präsentationscallback einzeln über kontrollierten Observerrahmen vermittelt; Ausführungs-/Return-Methodenkörper unverändert |
| `CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs` | Neu: 23 produktbezogene Headless-Integrationstests mit echter TextArea, Commandrouting, Clipboard und Bridge |
| `docs/mcp/MCP-00-R2-consolidated-architecture-decision.md` | Ausschliesslich angehängte Eigentümerergänzung vom 26.10.2026 |
| `docs/mcp/MCP-00-AP0-contract-finalization.md` | Dieselbe angehängte Entscheidung; frühere Inventur mit 51 Mutationspfaden und 31 Observergruppen erhalten |
| `docs/mcp/MCP-00-AP2.1-implementation-review.md` | Dieser neue Reviewbericht |

Keine Änderung an AXAML, Projekt-/Paketdateien, CryptoScript-Core, Interpreterpipeline, Variablenbestand oder AvaloniaEdit-Bibliothek. Keine Avalonia-12-Migration und kein B2. Die zuvor vorhandenen Registry-, ViewModel-, Sidebar-, Save-, Dialog- und historischen Teständerungen bleiben erhalten; nur die in der Tabelle bezeichneten Dateien wurden im AP2.1-Delta erweitert.

## 4. Architektur und Schnittstellen

### 4.1 Genau ein serviceeigener Editor-Slot

`WorkbenchAutomationService.EditorCoordinator` erstellt UI-affin höchstens einen Coordinator für seine vorhandene `EditorBridge`. Es gibt keine zweite Editorregistrierung, Textkopie zur Bearbeitung, Revision oder Interpreterinstanz. Jedes Ticket bindet die originale Bridge-Registration, das originale `TextDocument`, editorId, Originalrevision, Version und Kontrollgeneration. Ein zweiter Service kann einen bereits koordinierten sichtbaren Editor nicht übernehmen.

Die aktuell erreichbaren Phasen sind `Unavailable`, `Gui`, `Releasing` und `Faulted`. GUI-Zulassung verlangt eine live Bridge-Registration, geschlossene Dokumentupdates, keine aktive Operation, keinen Observerrahmen und keine bestehenden Identity-/Mutationlocks. `IsReadOnly`, `CanExecute` oder ViewModel-Busy ersetzen diese Prüfung nicht. Eine mögliche künftige `Mcp`-Besitzphase ist in diesem Paket absichtlich nicht publizierbar; ihre vollständige Übergangslogik ist Bestandteil der gesondert freizugebenden Hostintegration.

Generation invalidiert bei Registrierung, Dokumentwechsel, Fokusverlust und Beginn beziehungsweise Ende eines lokalen Kontrollübergangs. Überlauf wird geschlossen abgewiesen. Ein neuer Attach wird vor Änderung der Bridge geprüft. Während einer nativen Operation darf die Dokumentidentität nicht wechseln. Detach kann dagegen den Zugang sofort sperren; die bereits angenommene Originaloperation bleibt gebunden und kann ihren Abschluss durchführen. Vollständig abgelöste und drainierte Dokumente werden nicht dauerhaft durch den früheren Host eingefroren.

### 4.2 Operations- und Callbacktickets

| Interne Schnittstelle | Zweck / Grenze |
|---|---|
| `TryBegin()` | Frühe Admission und Registrierung genau eines Originaltickets; Ablehnung liefert kein Schreibrecht |
| `RunGui(...)` | Synchrone native/hostinterne Aktion mit Admission, Wirkungserfassung und Abschluss |
| `Commit(ticket, ...)` / `IsCurrent(ticket)` | Erneute Bindungs-/Generations-/Revisionsprüfung unmittelbar vor verspäteter Wirkung; kein Auflösen auf einen Nachfolger |
| `Observe(Action)` | Auch ohne aktive Operation nichtschreibender Observerrahmen; reentrante neue Zulassung gesperrt, Exceptions begrenzt |
| `RegisterCallback()` / `SealProducer()` | Callback vor Dispatch zählen; nach Sealing keine neue Registrierung. Bereits angenommener Producer kann während Drain seine registrierten Pflichten vervollständigen |
| `OperationTicket.Completion` | Terminaler Beleg erst nach Bodyabschluss, Sealing und allen registrierten Callbackabschlüssen |
| `CallbackTicket.Observe(...)` | Originalgebundener später Observer, nach Generation-/Identitätswechsel keine Wirkung; sein Ticket muss dennoch geschlossen werden |
| `BeginDrain()` | Idempotentes, gebundenes lokales Sperr-/Warteticket; kein Timeout und kein Zählerreset |
| `ResumeGui(certificate)` | Nur eigenes aktuelles erfolgreich abgeschlossenes lokales Drainticket; keine Wiederfreigabe bei Faulted oder stale Zertifikat |

Die Summe der ausstehenden Operationen und Callbacks ist auf 64 begrenzt. Das ist ein Schutz des AP2.1-Grundkerns, **kein** Ersatz für D02-Leasekapazität oder die 4-MiB-Verwaltung. Ein nicht beendetes Ticket blockiert den lokalen Drain dauerhaft; das Abbrechen eines externen Waits beendet keine Verpflichtung. `Dispose` aus dem aktiven eigenen Mutationscallback kann einen Receipt nicht vorzeitig publizieren.

Das lokale Drainticket ist ausdrücklich **kein L2-Epochenzertifikat**. Save, Passwortprovider, Registryjobs, Returnausgabe, sämtliche Dispatcherposts, Resultatkopien und Shutdown sind noch nicht vollständig an diesen Drain angeschlossen. Es gibt keine Tombstonefreigabe, keinen Credentialwechsel und keine Behauptung einer sicheren produktiven Epochenrotation.

### 4.3 D01-Wirkungs- und Fehlererfassung

`EditorOperationReceipt` enthält originale editorId, Originalrevision, nullable beobachtete Originalrevision, `textEffect`, `historyEffect`, `finalization` und eine feste interne Fehlerkategorie. Keine Exception.Message, Scriptdaten oder Schlüssel werden gespeichert. `RetryPolicy` ist immer `noAutomaticRetry`.

Die Bridge liefert die tatsächliche Revision; es wird keine geschätzte `+1`-Revision erzeugt. Bei einem Nachfolgedokument wird keine fremde Revision als Originalrevision ausgegeben. Native Rückkehr, Versionsänderung, ausdrücklich bekannte erwartete Textwirkung sowie Historyaufruf/-Benachrichtigung werden getrennt ausgewertet. Ein normal zurückgekehrter History-only-Vorgang kann `none/applied/complete` ergeben; dies ist interne Evidenz für native GUI-Operationen, noch keine projektionstreue v2-Editantwort. Der v2-Transport und seine engeren Erfolgsinvarianten bleiben AP6.

Ein begrenzter kontrollierter Observerfehler ergibt bei nachgewiesener Wirkung `applied/applied/complete` mit `CallbackFailed`; Folgeeingabe und echte Undo-/Redo-Schritte werden geprüft. Ein unkontrolliert entkommener nativer Fehler führt konservativ zu `historyEffect=indeterminate`, `finalization=faulted` und weiterer Mutationssperre. Sicher belegter Text bleibt dabei applied, andernfalls indeterminate; unveränderter Text wird nach unbekanntem nativen Fehler nicht als History-Wirkungslosigkeit interpretiert.

Offene native Updates werden nicht durch Reparaturschleifen geschlossen. Der neue Coordinator ruft kein `EndUpdate` zur Reparatur auf. Der schon vor AP2.1 vorhandene einmalige Ausgleich eines im `UpdateStarted`-Guard abgewiesenen BeginUpdate bleibt bestehen; er ist keine neue allgemeine Finalisierungsstrategie. Der alte v1-UpdateEditor-Pfad samt bestehender Fehlerbehandlung bleibt ausserhalb dieses Grundpakets und darf nicht als vollständig auf D01 migriert gelten. Faulted besitzt hier keinen automatischen Recoverypfad.

## 5. Native Eingänge, Undo/Redo, Clipboard und IME

### 5.1 Native Historykorrektur nach U1

Die bisherigen zwei Löschungen aus `TextArea.DefaultInputHandler.CommandBindings` und die eigene direkte UndoStack-Delegation im WorkbenchTextEditor entfallen. Beide originalen nativen Undo-/Redo-Registrierungen und deren Ctrl+Z-/Ctrl+Y-Gesten bleiben erhalten.

`IRoutedCommandBindable.CommandBindings` der tatsächlich verwendeten WorkbenchTextArea vermittelt native Editingcommands einschliesslich Undo/Redo. Vor Ausführung wird der Coordinator geprüft. Ein privates Delegationsrecht wird beim ersten Zugriff auf die originale Bindingliste verbraucht, bevor native Handler oder ihre Callbacks laufen, und in `finally` entfernt. Reentrante Commands sehen wieder den Guard. Native CanExecute-Regeln werden weiter abgefragt; der eigentliche Ausführungsguard ist trotzdem verpflichtend.

Die native Bibliothek bleibt für UndoStack, Gruppen und Caret zuständig. Es gibt keine eigene UndoEngine und keinen allgemeinen KeyDown-Ersatz für Ctrl+Z/Y. Die enge Tab-Grenze betrifft nur den von TextArea unmittelbar ausgeführten Tab-/ShiftTab-Pfad. Das neue Historytestmodell benutzt beide verfügbaren Stackrichtungen während Releasing und prüft die **UndoStack-Eigenschaften**, nicht die absichtlich deaktivierten UI-CanExecute-Eigenschaften.

### 5.2 TextInput und verzögertes Clipboard

Routed TextInput wird vor Eintritt geprüft. Zusätzlich vermittelt `OnTextEntering` den öffentlichen nichtvirtuellen `PerformTextInput`-Pfad über einmalige native Wiedereinleitung innerhalb eines Tickets. Der äussere Aufruf wird danach unterdrückt: genau eine Textwirkung und genau ein TextEntering-/TextEntered-Zyklus. Diese zusätzliche Grenze verhindert einen Bypass durch die im Produkt und in bestehenden Tests verwendete direkte API.

Paste/Cut werden vor dem ersten await an Originaldokument, Bridgeidentität/-revision, Generation, Auswahlobjekt und Caret gebunden. Die echte Clipboard-API wird aufgerufen; nach Rückkehr ist erneut Zulassung erforderlich. Ein Dokumentwechsel, Fokusverlust, Drain oder zwischenzeitlicher Edit verwirft den alten Commit. Copy wird ebenfalls als nachverfolgter Clipboardvorgang beendet. Normale Plaintext-/Unicode-, Newline-, Tab-/ReadOnly- und WholeLine-Pfade sind berücksichtigt. Das interne native `OnTextPasted` wird nicht per Reflection aufgerufen; vollständige Clipboard-/Completionvarianten bleiben AP3-Nachweisaufgabe. Der einzige Reflection-Clipboardproxy liegt ausschliesslich im Testprojekt und verzögert die Plattformantwort, nicht die Coordinatorzulassung.

### 5.3 Permanente Konfiguration

Der WorkbenchTextEditor konstruiert die tatsächliche WorkbenchTextArea vor erstem Template/Fokus. Beide Typen erzwingen `InputMethod.SetIsInputMethodEnabled(false)` einschliesslich fest coercierter Metadaten; späteres Setzen auf true bleibt unwirksam. `EnableTextDragDrop=false` wird initial gesetzt und durch die frühe TextArea-Optionsvermittlung bei Optionsersatz oder späterem Änderungsversuch erhalten. StyleKeys bleiben die nativen TextEditor-/TextArea-Styles.

Es gibt keine Reaktivierung beim Übergang zu GUI, keinen WM_CHAR-Hook und keinen Unicodefilter. Mehrstufige CJK-Komposition ist gemäss Eigentümerentscheidung nicht unterstützt. Headless-Unicodeproben sind kein physischer Schweizer-Tastatur-, AltGr-, Dead-Key- oder CJK-Nativnachweis; diese Einschränkung wird nicht in einen fiktiven PASS umgedeutet.

## 6. Observerabdeckung und noch nicht migrierte Pfade

Produktiv neu vermittelt sind der Caret-Statuscallback und der TextEntered-Präsentationscallback der tatsächlichen CryptoScriptEditView. Die Bridge beobachtet weiterhin Originaldokument und Revision unabhängig davon; der Abschluss liest die Originalrevision nochmals gebunden. Dokumentwechsel erhält die frühe Metadatengrenze. Die übrigen kontrollierten Dokument-/Caret-/Selectioncallbacks in den neuen Tests werden einzeln über die produktive `Observe`-Schnittstelle registriert und prüfen reentrante Angriffe.

Dies ist **keine** Behauptung, dass alle 31 Observergruppen bereits produktiv umgestellt seien. TextEntering mit Completion-/Returnstart ist ein mutierender nativer Teilpfad; er wird nicht fälschlich als rein nichtschreibender Observer klassifiziert. Sidebar, DataGrid, Dialoge, Completioncommit, SearchPanel-eigene Replacebindings, Historyservice, sämtliche Properties, Save-/Passwort-/Return-/Shutdownfolgen und nicht vermittelte Fremdsubscriber brauchen AP3/AP4/AP5/AP6. Direkte beliebige UndoStack-/Dokumentmanipulationen durch noch nicht migrierten Hostcode sind nicht durch eine magische globale Capability abgesichert.

Gerade deshalb existiert keine produktive MCP-Owner-Aktivierung. Offene frühe GUI-/Observerpfade dürfen weder durch IsReadOnly kaschiert noch unter realem MCP-Besitz freigeschaltet werden. Der S1-Safetybeweis wird durch den lokalen Pendingzähler nicht zu einem Beweis des Gesamtprodukts.

## 7. Testresultate

Ausgeführt auf der bestehenden .NET-10-/Avalonia-11.3.20-/AvaloniaEdit-11.4.1-Basis, ohne Paketänderung. Rohartefakte im separaten Prüfbereich `results/`:

| Lauf | Ergebnis | Evidenz |
|---|---|---|
| Vollständige GUI-Suite, final | **457 PASS, 0 FAIL, 0 SKIP**: 434 vorhandene + 23 neue Fälle | `gui-final.trx`, `gui-final.log` |
| Neue Coordinator-Fälle separat | **23 PASS, 0 FAIL** | `ap21-fourth.trx`, `tests-fourth.log` |
| Vollständige Core-Suite | **7.667 PASS, 0 FAIL, 4 SKIP** | `core.trx`, `core.log` |
| Bestehende MCP-Suite | **89 PASS, 0 FAIL, 0 SKIP** | `mcp.trx`, `mcp.log` |
| Echte native CJK-Komposition | **OPEN**, nicht ausgeführt; akzeptiertes Restrisiko | Keine PASS-Behauptung |
| Neues sichtbares Windows-/physisches Tastaturprogramm | Nicht ausgeführt | U1-Nativnachweis bleibt historische Referenz, kein neu ausgeführter AP2.1-Nativlauf |

Die 23 neuen Fälle decken GUI-Unicode, genaue native Undo-/Redo-Gruppen/Gesten, frühe Ablehnung beider Historyrichtungen, reentrante Dokument-/Caret-/Selectionobserver, späte Observer, Identitätswechsel, alte Tickets, Callback-Sealing/Drain, spätes Callbackversagen, angewandten Text bei Fehler, offenen Updateabschluss, permanente IME-/Textdrag-Konfiguration, Unicode-Paste/Cut, verzögertes Clipboard, Detach/Attach, direkte PerformTextInput-Aufrufe, reine Historywirkung, unkontrollierten nativen Undo-Fehler, zweite Serviceinstanz und Ticketkapazität ab.

EC21-Produktcode und sämtliche bestehenden Testdateien bleiben unverändert. Insbesondere besteht `WholeEditorPrintIsInsertedOnceWithoutDuplicatingSource` weiterhin mit der bisherigen zusätzlichen `out:`-Quelltextmutation. Das ist **keine Erfüllung von D05**: Der genehmigte Zielvertrag ohne diese Mutation und der damit kollidierende historische Test bleiben ausdrücklich AP5. Die historische H4/U1-Zählung 433 PASS / 1 FAIL wird nicht geändert; deren isolierte Variante und dieser unveränderte produktive EC21-Pfad sind verschiedene Testgegenstände.

### Zwischenläufe und bekannte Testinstabilität

Die ersten Builds deckten die konkrete Avalonia-Typdeklaration der IME-Metadaten auf; sie wurde über den tatsächlichen StyledProperty-Typ angepasst. Der erste neue Testlauf war 6/17 erfolgreich: Der fehlende Headless-Style/Fokus verhinderte die meisten Tasteneingänge, und ein Test deckte eine zu späte Attachprüfung auf. Die neue Fixture verwendet nun die echten Fluent-/AvaloniaEdit-Styles und prüft Fokus ausdrücklich. Der Attachguard wurde vor die Registrierung verlegt; keine historische Assertion geändert.

Der nächste Lauf war 16/17 erfolgreich; die neue Drainprobe hatte UI-CanUndo mit tatsächlichem Stackinhalt verwechselt. Sie prüft nun unmittelbar beide UndoStack-Richtungen. Der erste vollständige GUI-Lauf zeigte ausserdem vier Regressionen beim Schreiben bereits vollständig abgelöster Dokumente. Die neue Hostbarriere wurde auf ihren registrierten beziehungsweise noch drainenden Originalbereich begrenzt. Danach bestanden 451/451 Fälle.

Nach sechs zusätzlichen Randfalltests ergab ein Zwischenlauf 456 PASS / 1 FAIL: `OpenNativeUpdateFaultsWithoutEndUpdateRepairLoop` scheiterte mit einer `NullReferenceException` in `Avalonia.Headless.HeadlessUnitTestSession.Dispose`, nicht mit einer Wirkungs-/Historyassertion. Der unveränderte gezielte 23-Fälle-Lauf und die anschliessende vollständige 457-Fälle-Suite bestanden. Die genaue Ursache des einmaligen Session-Abschlussfehlers ist nicht bewiesen behoben; `gui-third.trx` bleibt als Reviewbefund erhalten. Keine Wiederholungsergebnisse überschreiben diesen Lauf.

Compilerwarnungen bestehen, darunter vorhandene Nullable-Warnungen und obsolete Clipboard-/Headless-Kompatibilitäts-APIs in den neuen Tests beziehungsweise dem Clipboardadapter. Es gab in den finalen Testbuilds keine Compilerfehler. Diese Warnungen sind kein Nachweis nativer Plattformkompatibilität.

## 8. Abweichungen zu H4/U1 und erforderliche Folgeintegration

H4/U1 liefern technische Referenz für die frühe native Grenze und den einmaligen Historydispatch. Nicht übernommen wurden Labor-Acquire/Renew/Advance, synthetische Uhr, ReportIme, öffentliche Hooks, globale WeakTable-Registries, unbegrenzte Trace-Listen, ein zweiter Bridgebestand oder eine pauschale Übernahme aller H4-Adapter.

Stattdessen verwendet AP2.1 einen serviceeigenen Slot, typisierte Phasen und strukturierte interne Belege, explizite bounded Tickets und Producer-Sealing sowie Originalbindung aus der bestehenden Bridge. Zusätzlich deckt der TextEntering-Adapter direkte PerformTextInput-Aufrufe ab. Das Clipboard verwendet die vorhandene Plattform-API; es gibt keinen produktiven frei injizierbaren Clipboard-Laborhook.

| Paket | Verbleibende Pflicht; hier nicht automatisch umgesetzt |
|---|---|
| AP3 | Vollständige GUI-/Observerintegration entlang aller 51 Mutationspfade/31 Observergruppen; Sidebar, History, Search, Completion, weitere Property-/Fokus-/Dispatcherpfade; reale Eingabevarianten |
| AP4 | Save-/Dialog-/Passworttickets, Modal-/Intentregeln und gesamte Ressourcenfinalisierung; vollständiger Shutdown-/Callbackdrain |
| AP5 | EC21-Änderung ohne zusätzliche Ganzeditor-Quelltextausgabe und gesonderter Umgang mit kollidierendem historischen Test; Returnausgabe bleibt |
| AP6 | Striktes MCP-v2, Autorität/Lease, Admission-/Commitserialisierung mit Registry, D01-Transportprojektion, D02-Credential-/Epochen-/Speicher-/Timerintegration und echte Drainzertifikate |

Weiterhin bekannte Risiken: nicht vollständig vermittelte Host-/Fremdcallbacks, nicht instrumentierter Gesamt-Drain und Leaseprotokollspeicher, reale OS-/Suspend-/Credentialpfade, neue Windows-Nativabnahme ausstehend, einmaliger Headless-Session-Abschlussfehler. Kein Punkt wird durch den akzeptierten CJK-IME-Rest aufgehoben. Ein unabhängiger Reviewer sollte insbesondere Einmaldelegation, Originalbindung bei Detach/Clipboard, Receiptfinalisierung, unveränderte Legacy-Servicepfade und die Vollständigkeit der benannten offenen Pfade prüfen.

## 9. Abschlussstatus

`verification.json` dokumentiert HEAD-/Stagingerhalt, Originalkopien, autorisierte Dateideltas, unveränderte historische Evidenzen und die gesonderten Buildausgaben. `ap21-only.diff` enthält die Änderungen gegenüber dem vor diesem Auftrag vorhandenen uncommitteten Stand; der normale Git-Diff enthält zusätzlich die bereits bestehenden MCP-01.4-Arbeiten.

Ergebnis der Abschlussprüfung: **PASS**. Von 23.368 vorher vorhandenen Dateien bleiben 23.309 byteidentisch; sechs Dateien enthalten das autorisierte AP2.1-Delta, 53 bestehende Buildausgaben wurden durch die Testläufe erneuert. Hinzu kommen vier autorisierte neue Quelldokument-/Testdateien und 1.092 Buildausgaben unter bin/obj. Keine unerwartete Änderung, keine fehlende Originaldatei, keine unerwartete neue Datei. Sämtliche gesicherten uncommitteten Originalkopien stimmen mit der Vorherbaseline überein. HEAD und leeres Staging sind unverändert; H1–U2 und D02-S1 bleiben byteidentisch. Der finale Git-Status enthält weiterhin sechs modifizierte getrackte Dateien und nun 25 ungetrackte Dateien; AP2.1 wurde nicht gestaged.

Keine automatische Fortsetzung nach diesem Auftrag. Das Paket ist zur separaten unabhängigen Prüfung vorbereitet, nicht als abschliessend implementiert oder vollständig freigegeben erklärt.
