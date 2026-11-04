# MCP-B3-F4 · Hostisolation, Workspace-Benachrichtigungen und HTTP-Abbruch

**Empfehlung zur Übergabe an das unabhängige B3-R5-Read-only-Review.** R4-01 bis R4-03 und ein gleichartiger Lockfehler im lokalen Legacy-Edit wurden im bestehenden SDK-Binding und Editor-Coordinator korrigiert. Die ursprünglichen R4-Prüfprogramme wurden unverändert gegen die neue Release-Ausgabe verwendet. Die Eigentümerabnahme und eine produktive MCP-Freigabe sind eigenständige Entscheidungen. [R4-Bericht](../../../b3-r4-independent/MCP-B3-R4-independent-rereview.md)

## Ausgangspunkt

Branch `feature/mcp-ap3`, HEAD `683f789324bd040b5e2f6e834b4c2f1ef0e8aa9f`, Index gleich HEAD. Beim Einstieg stimmten alle 41 Kandidatendateien mit dem unabhängigen R4-Endmanifest überein. SHA-256 der eingehenden Desktop-Assembly war `DC9819B26B3EF9655B6A004019576A77E519741BB7DEBCCA60C77BA0FEE00C4B`. Der fachliche Vergleich folgt R2/AP0, D02-S1 und den vorhandenen B1-/B2-Abnahmen. [Eingangshashes](../../artifacts/b3-f4/input.sha256) · [B3](MCP-B3-mcp-binding-lease-v2.md) · [Architektur](MCP-00-R2-consolidated-architecture-decision.md) · [AP0](MCP-00-AP0-contract-finalization.md) · [D02-S1](MCP-00-D02-S1-epoch-safety.md) · [B1](MCP-B1-N1-R-final-acceptance-addendum.md) · [B2](MCP-B2-N1-final-native-acceptance.md)

## Drei R4-Korrekturen

| Befund | Ursache und Entscheidung | Negativ- und Positivkontrolle |
|---|---|---|
| R4-01 · Tooltransplantation | Der Beleg bestätigte nur Bs Requestserver. Das eingesetzte Tool konnte As Resolverclosure tragen. Die Sessionbindung hält jetzt zusätzlich genau den ursprünglichen, privat erstellten Katalog dieser Registrierung. Das Binding weist sich mit seiner eigenen Objektreferenz und seinem festen Namen aus. | Beim Transplantat auf B bleiben beide Resolver- und Dienstzähler unverändert. Mit eigenem Tool erreicht B wieder ausschließlich B. Permanente Paarungen für Status, Mutation und Execute, parallele eigene Requests und unterschiedliche Instanzmarker. [Originalprobe](../../artifacts/b3-f4/HostIsolation.log) · [Tests](../../CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs) |
| R4-02 · View-Observer unter Registrylock | Attach und Activate riefen synchrone Workspace-Empfänger unter `sync` auf. Unter dem Monitor wird der jeweilige Übergang entschieden und die Veröffentlichung vorgemerkt. Der Dispatcher publiziert nach Freigabe des Monitors. | Bei Attach und Activate kehrt der externe L1-Worker innerhalb des Observers zurück. Ersetzung prüft die veröffentlichte Retirement-Phase und nachfolgende Original-/Nachfolgerbindung separat. Echter TextChanged hält die ursprüngliche Dispose-/Drainpflicht bis zur Callbackfreigabe. [Originalprobe](../../artifacts/b3-f4/EditorEdges.log) · [Tests](../../CryptoWorkBenchGuiTest/B3F4Tests.cs) |
| R4-03 · Erstdispatch nach Disconnect | Die Middleware entwertete unverbrauchte Belege erst im finally. Der Beleg erfasst jetzt den originalen HTTP-Abbruchtoken. Nur der erste atomare Anspruch kann Erfolg haben, und auch dieser muss unmittelbar danach den ursprünglichen Token aktiv vorfinden. | Bei serverseitig bestätigtem Abbruch vor Erstverbrauch bleiben Resolver und Dienst bei 0/0. Bei zuerst angenommenem Edit bleiben genau ein Dispatch und die ursprüngliche Applied-Receipt erhalten. Ein zweiter Aufruf und ein regulär beendeter unbenutzter Request werden abgewiesen. [Originalprobe](../../artifacts/b3-f4/ReceiptLifetime.log) · [Tests](../../CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs) |

### Registrierung, Request und Abbruch

`AddWorkbenchV2` erzeugt für genau eine Registrierung elf private Bindingobjekte mit ihren jeweiligen Dienst- und Resolverdelegaten. Die Prüfung verwendet den bei der Konfiguration festgehaltenen Originalkatalog, unabhängig von der später für die SDK-Auswahl modifizierbaren Toolcollection. Clientdaten können keine passende Objektreferenz herstellen. Der Rohkörperbeleg bleibt über den HttpContext und die identischen Optionen dieses stateless POST gebunden. Vor Authentisierung müssen konkrete Toolreferenz, der bei Toolerzeugung festgelegte Name, Name im validierten Rohkörper, Methode, Request-ID und Parameter übereinstimmen. Das Credential stammt allein vom bisherigen Resolver. Die schwache Tabelle verwendet weiterhin die tatsächlichen SDK-Serveroptionen als Schlüssel. Acht parallele inhaltsgleiche Anfragen dienten als unabhängige Kontrolle für getrennte Optionen, genau einmaligen Dispatch und Einsammelbarkeit nach Abschluss. [Registrierung](../../CryptoWorkBench.Mcp/WorkbenchToolsV2.cs) · [Beleg](../../CryptoWorkBench.Mcp/SafeMcpParseErrors.cs) · [Optionsprobe](../../artifacts/b3-f4/Options.log)

Beim ersten Aufruf beansprucht `Interlocked.Exchange` den Beleg gegenüber Wiederholung und regulärem Requestende. Das unmittelbar folgende Lesen des ursprünglichen, monotonen Abbruchsignals entscheidet über die Freigabe des beanspruchten Aufrufs. Ist der Abbruch bereits signalisiert, bleibt der Beleg verbraucht und der Resolver unerreicht. Erfolgt er erst nach dieser Entscheidung, behält die ursprüngliche Operation ihren Admission-/Cancellationvertrag. Dazu ist keine Cancellationregistrierung im Produkt nötig, auch kein Warten auf das Ausführen eines Abbruchcallbacks. Das Middleware-finally schließt auch unbenutzte Belege. Diese Prüfung verlangt weder einen Hostmonitor noch eine Principalhistorie. [Implementierung](../../CryptoWorkBench.Mcp/SafeMcpParseErrors.cs)

Die Wire-Tests halten einen vom echten SDK selektierten Aufruf vor dem Originaltool an, warten auf das serverseitige Disconnectsignal und übergeben absichtlich sogar CancellationToken.None an das Tool. Weitere Fälle trennen gewonnenen Erstverbrauch, Rennen der beiden Entscheidungen, Verbrauchswiederholung im noch offenen POST, reguläres Ende, Folgeanfrage sowie zwei gleichzeitig offene inhaltsgleiche POSTs, von denen einer abbricht. Die nach Annahme erhaltene D01-Wirkung wird am instrumentierten Dienst gemessen, die tatsächliche native Wirkung separat im GUI- und HWND-Pfad. [SDK-Regressionen](../../CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs)

### Viewwechsel und Empfänger

Das erste Attach, zusätzliche registrierte Views, Aktivierung und der Wechsel über ein bekanntes Editorobjekt benutzen dieselbe Bridge und denselben Coordinator. Bei einer Ersetzung ist der alte Editor zuerst im vorhandenen Drain in Releasing. Dieser Zustand wird außerhalb des Locks sichtbar, bevor die zweite Entscheidung fällt. Danach wird die alte Registrierung abgelöst, das konkrete Zertifikat geprüft und der aktuelle Zustand des eingehenden Dokuments unter dem Monitor erneut validiert. Ein Observer kann zwischen den Phasen die Dokumentidentität verändern oder Shutdown beginnen. Dann entsteht keine neue Zulassung. Ein bereits entschiedener Attach liefert seinen Handle nur bei weiterhin ausgewählter, eigener Registrierung und ohne Shutdown zurück. Das Handle erteilt keine Schreibberechtigung. [Service](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs) · [Bridge](../../CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs)

Bei direktem Detach ist die Originalregistrierung vor dem Event aus der Bridge entfernt. Beginnt ein Empfänger während der Publikation einen neuen Workspaceübergang, werden die übrigen Empfänger aus dessen neuer Generation bedient. Die ältere Publikation endet am Generationsvergleich. Der Observer-Scope umfasst die gesamte Zustellung. Die VariableView invalidiert ihren Draft wie bisher über WorkspaceChanged. KeyMap, Status- und Editorpräsentation prüfen weiterhin ihre ursprünglichen Workspace-/Zielbindungen. In den positiven Tests sind die beiden Zustände einer Ersetzung je einmal sichtbar, bekanntes Attach auf die aktuelle View bleibt still, ein abgelöster Handle wird abgewiesen und nach sauberem Disable ist zulässige GUI-Eingabe wieder möglich. [Coordinator](../../CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs) · [UI-Tests](../../CryptoWorkBenchGuiTest/B3F4Tests.cs)

### Zusätzlicher bestätigter Lockpfad

Im lokalen v1-`UpdateEditorAsync` wartete ein UpdateStarted-Observer auf den Disable-Worker. Vor der zusätzlichen Korrektur maß die separate Probe `monitor=True l1ReturnedInObserver=False`. Auch dieser Eingang trennt jetzt nach Original-/Generationsprüfung `TryStartCommit` unter `sync` von `FinishCommit` außerhalb. No-op verwendet den aktuellen Snapshot. Text, History, bisherige lokale Fehlerprojektion und das Warten auf registrierte Originalcallbacks bleiben am ursprünglichen Ticket. Der korrigierte Lauf ergab `monitor=False l1ReturnedInObserver=True ok=True text=R2!`. Der bisherige v1-Pfad bleibt auf lokale GUI-Caller beschränkt. [Vorher](../../artifacts/b3-f4/legacy-before.log) · [Nachher](../../artifacts/b3-f4/LegacyObserver.log) · [Regression](../../CryptoWorkBenchGuiTest/B3F4Tests.cs)

Weitere geprüfte Stellen waren Save-Provider und Picker, Replace-/Execution-Zustimmung, laufende Job-/Passwortarbeit, Präsentation, Timer und Cancellation. Der Save nimmt einen konkreten Schritt an und startet den Provider außerhalb des Monitors. V2-Execution prüft unter `sync` die gebundene Absicht, publiziert ausstehende Workspaceänderungen und ruft erst danach den lokalen Zustimmungsprovider auf. Der Registryworker wird im Hostqueue per Task.Run gestartet. RequestCancel nutzt CancelAsync. In der Registry bleiben interne Intentprädikate, Zeitmessung und Test-Injektionspunkte. Im Produkt prüfen die hier verfolgten Prädikate konkrete Editor-/Jobidentitäten, bei nativer Eingabe auch Caret, Auswahl und Handled. Die injizierbaren Timer/Checkpoints sind Host-/Testinfrastruktur. Umfang der Messung sind die genannten Produktpfade, keine Garantie für beliebig implementierte externe Provider. [Save](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs) · [Execution](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionV2.cs) · [Registry](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs)

## Regressionserhalt und unabhängige Prüfprogramme

Die R4-Originale wurden byteidentisch in den Testlauf mit den neuen Release-Abhängigkeiten übernommen. `HostIsolation` war als Fehlerreproduktion geschrieben und liefert Exitcode 1, wenn der frühere Fremddispatch ausbleibt. Für F4 zählen seine tatsächlichen Zähler und Kontrollmarker. Bei den alten Wire/WireEdges/Ingress-Binärprogrammen ist MethodAccessException beim Aufruf der privaten historischen Toolfabrik die erwartete ABI-Grenze. Die bereits in R4 mit ihren Originalen verglichenen Migrationen liefen separat. Die R2-Adversarial-Originalprobe greift am Ende auf den inzwischen entfernten Timer zu. Die nullsichere Vergleichsfassung behält ihr Sicherheitsorakel. [Laufmatrix](../../artifacts/b3-f4/replay.log) · [R4-Quellvergleich](../../../b3-r4-independent/MCP-B3-R4-independent-rereview.md)

| Historische Findings | Wiederholte Orakel am F4-Kandidaten |
|---|---|
| R1-01, R1-02, R2-01, R2-02 | Save vor/nach L1, gebundener Delete/Append, gehaltene Providerpflicht, Q und Abbruch eines Einzelwaiters. Echtes verzögertes Clipboard, letztes Detach, alter und neu erfasster Drain. [Save](../../artifacts/b3-f4/Save.log) · [Held](../../artifacts/b3-f4/Held.log) · [Cut](../../artifacts/b3-f4/R1Proof.log) |
| R1-03, R2-06, R3-02, R3-06 | Unabhängiger Callback trotz werfendem Replace-Cancellationprovider, strikt fehlerhaftes L2 bei offener Nativegruppe, entsorgter Originaltimer und Lifetime. Fault-Observer vor/nach Dispose bei beiden Gruppenarten, unsicheres Cleanup hält das Fenster offen. [Lifecycle](../../artifacts/b3-f4/Lifecycle.log) · [Fault- und Shellkontrolle](../../artifacts/b3-f4/FaultEdges.log) |
| R1-04, R1-05, R2-03, R3-04 | Leserechte beim Replay, Metadata-/Outputgrants, gleiche UUID in verschiedenen Owner-Historien, Cancel/Retention/Deadline. Beide SDK-Statusadapter trennen Active und Last auch bei beschränkten Grants. [Owner](../../artifacts/b3-f4/Owners.log) · [Statusadapter](../../artifacts/b3-f4/StatusWire.log) · [Grantkontrollen](../../artifacts/b3-f4/Controls.log) |
| R1-06, R1-09 bis R1-11, R2-04, R2-05, R3-03, R3-05 | Elf Mappings, isError, Refresh bei wirksamem Edit, v1-v2-Migrationsfehler, Envelope-/Argumentduplikate und Bytegrenzen. Teilregistrierung bleibt geschlossen, Transplantation und Disconnect sind jetzt auch mit gültigem Requestserver abgedeckt. [Wire](../../artifacts/b3-f4/WireSupported.log) · [WireEdges](../../artifacts/b3-f4/WireEdgesSupported.log) · [Ingress](../../artifacts/b3-f4/IngressSupported.log) |
| R1-07, R1-08 | OpenWorld-Discovery und lokaler Dialog mit Originaltext. Nur echte Zustimmung erzeugt die Kontrolldatei. Fehlender Owner bleibt ConfirmationUnavailable auch beim Replay. [Dialoge](../../artifacts/b3-f4/AdversarialNullSafe.log) |
| R3-01 und B1/B2 | Editannahme vor Nativeffekt, Disable während Originalarbeit, Revocation vor Zustimmung, No-op/RevisionConflict und D01-Fehlerpaare. F2R-10, Undo/Redo, Return, Completion, Script Book und EC21 im vollständigen GUI-Lauf. [Editor](../../artifacts/b3-f4/EditorEdges.log) · [No-op](../../artifacts/b3-f4/NoOp.log) · [GUI](../../artifacts/b3-f4/Release-CryptoWorkBenchGuiTest.trx) |

Die separat ausgeführte Empfängerprobe verwendete eine tatsächlich an einen Window-VisualTree gebundene VariableView. Ihr ViewModel war zunächst aktuell, nach Attach veraltet, nach Refresh wieder aktuell und nach Activate erneut veraltet. Beide PropertyChanged-Benachrichtigungen liefen außerhalb des Registrylocks. [Empfängerprobe](../../artifacts/b3-f4/VariableRecipient.log)

## Tests, Binärstand und Inventar

| Vollständig ausgeführte Konfiguration | GUI bestanden | MCP bestanden | Fehlgeschlagen | Nicht ausgeführt |
|---|---:|---:|---:|---:|
| Debug | 790 | 159 | 0 | 1 |
| Release | 790 | 159 | 0 | 1 |

Der einzige nicht ausgeführte Fall ist `InstalledCodexDiscoversAndCallsStatus`. Gegenüber F3 sind neun GUI- und acht MCP-Einzelresultate hinzugekommen. Der überlappende Release-Fokus bestand mit 174 GUI- und 45 MCP-Fällen. Die Prüfung vergleicht die Multimenge aus Testklasse, parametrisiertem Namen und Outcome in den Original-TRX. Lediglich variable UTC-Zeitstempel im Namen werden normalisiert. [TRX-Audit](../../artifacts/b3-f4/trx-audit.txt) · [Debug GUI](../../artifacts/b3-f4/Debug-CryptoWorkBenchGuiTest.trx) · [Debug MCP](../../artifacts/b3-f4/Debug-CryptoWorkBench.Mcp.Tests.trx) · [Release GUI](../../artifacts/b3-f4/Release-CryptoWorkBenchGuiTest.trx) · [Release MCP](../../artifacts/b3-f4/Release-CryptoWorkBench.Mcp.Tests.trx)

Locked Restore lief für beide Konfigurationen mit den Repository-Prism-Hooks. In beiden vollständigen Solution-Builds wurden 1.180 gepinnte Prism-Dateien am Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` geprüft. Beide Builds hatten sechs Warnungen und null Fehler. Der native STA-Prozess prüfte ein echtes, von null verschiedenes HWND, programmgesteuerte Eingabe bei MCP-Besitz, Rückgabe an die GUI und Shutdown mit widerrufenem Credential. [Restore Debug](../../artifacts/b3-f4/restore-Debug.log) · [Restore Release](../../artifacts/b3-f4/restore-Release.log) · [Build Debug](../../artifacts/b3-f4/build-Debug.log) · [Build Release](../../artifacts/b3-f4/build-Release.log) · [HWND-Probe](../../artifacts/b3-f4/native.log)

SHA-256 der F4-Release-Desktopassembly: **A4541ECEC80BDC8F03F8F0C17DA413F18A886E4986D3B37DE6C7F6C110D8165C**. MCP: **6C42A8F8A0188DEFDAB25C82FFD6E467F88357EA7B61DD11D1901C8B2E05C1AF**. Nativer Probehost: **151FA21977BEC183E9D6322220FB3E5A2C0DC5E13FA9FFE772DF16E1C5182731**. Die Desktop-, GUI-Test- und nativen Kopien der Produktassembly werden im Binärmanifest einzeln ausgewiesen. [Binärmanifest](../../artifacts/b3-f4/binary.sha256)

Alle sechs geprüften Release-Assemblies passen über CodeView-GUID, Stamp und Alter zu ihren portablen PDBs. Der separate Quellprüfer hat die Bytes aller 38 C#-Kandidatendateien den Release-Symbolen zugeordnet. [PE/PDB](../../artifacts/b3-f4/Symbols.log) · [Quellbindung](../../artifacts/b3-f4/PdbCheck.log)

Für R5 empfehlen sich zwei vollständig registrierte Hosts mit vertauschten Status-, Schreib- und Execute-Tools, ein noch offener unverbrauchter POST bei bereits signalisiertem Abbruch sowie der reentrante Austausch einer View während der Retirement-Benachrichtigung. Die jeweiligen Gegenrichtungen sollten die eigene Registrierung, die vor dem Abbruch angenommene Mutation und die weitere Eingabe am gültigen Nachfolger prüfen.

### Genehmigter Rückfall und offene Entscheidungen

D02 bleibt auf 4.096 lebenslange Acquisition-Slots pro Prozess und 256 je Principal begrenzt. Der separate Prozesslauf schöpfte das Budget mit einer aktiven Lease, 4.095 gespeicherten Ablehnungen und 64 Credentials aus. Renew und Release waren weiter möglich, alte Credentials blieben widerrufen, die zweite Aktivierung gesperrt und eine zweite Dienstinstanz teilte dasselbe erschöpfte Budget. Die sichere L3-Rotation bleibt ein späteres Gate. [Prozessmessung](../../artifacts/b3-f4/Memory.log)

Der Picker-Pfadkonflikt ist reproduzierbar. Wird ein zunächst freier Pfad vor dem Open von einem anderen Erzeuger angelegt, hängt der Legacy-Provider den Originalsnapshot an dessen Inhalt an. AP0 §9.3 erhält Append. Ob der Pickerfall „neuer Pfad“ exklusive Erzeugung verlangen soll, braucht eine eigene Eigentümerentscheidung. Dieser Befund ist von MCP-L1 und Epochdrain getrennt. Physischer Suspend/Resume, produktives HTTP/Pairing, installierter Client-End-to-End und umfassende reale OS-/Providerfehlerkombinationen bleiben separate Gates. [AP0](MCP-00-AP0-contract-finalization.md) · [Dateikontrolle](../../artifacts/b3-f4/Save.log)

### Git-Status des uncommitteten Kandidaten

Gegenüber dem R4-Eingang wurden sieben bestehende Kandidatendateien bearbeitet und zwei permanente Testdateien sowie dieser Bericht ergänzt. 34 Eingangspfade sind byteidentisch. Insgesamt 44 Kandidatendateien, davon 23 bearbeitet und 21 unversioniert. HEAD- und Indexprüfung erfolgen separat.

Porcelain-v1-Inventar gegenüber HEAD, einschließlich aller unversionierten Produktdateien. Der Prüfordner unter artifacts enthält ignorierte Protokolle und Binärnachweise. Die vollständige Hashliste und die Zuordnung neu/geändert/identisch gegenüber R4 stehen im Inventar. [SHA-256 je Datei](../../artifacts/b3-f4/final.sha256) · [Inventar mit Eingangsvergleich](../../artifacts/b3-f4/inventory.tsv) · [Git-Prüfung](../../artifacts/b3-f4/preservation.txt)
~~~text
 M CryptoWorkBench.Automation.Abstractions/AutomationContracts.cs
 M CryptoWorkBench.Automation.Abstractions/AutomationErrors.cs
 M CryptoWorkBench.Automation.Abstractions/AutomationJson.cs
 M CryptoWorkBench.Automation.Abstractions/AutomationSchemas.cs
 M CryptoWorkBench.Mcp.Tests/ContractTests.cs
 M CryptoWorkBench.Mcp.Tests/TestListener.cs
 M CryptoWorkBench.Mcp/SafeMcpParseErrors.cs
 M CryptoWorkBenchAvalonia/App.axaml.cs
 M CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs
 M CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs
 M CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs
 M CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs
 M CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs
 M CryptoWorkBenchGuiTest/B1EditorIntegrationTests.cs
 M CryptoWorkBenchGuiTest/B1F2CorrectionTests.cs
 M CryptoWorkBenchGuiTest/B1F3CorrectionTests.cs
 M CryptoWorkBenchGuiTest/EditorAutomationTests.cs
 M CryptoWorkBenchGuiTest/WorkbenchRegistryCorrectionTests.cs
 M CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs
?? CryptoWorkBench.Automation.Abstractions/AutomationV2.cs
?? CryptoWorkBench.Mcp.Tests/B3F1WireTests.cs
?? CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs
?? CryptoWorkBench.Mcp.Tests/BindingV2Tests.cs
?? CryptoWorkBench.Mcp/WorkbenchToolsV2.cs
?? CryptoWorkBenchAvalonia/Services/Automation/ProcessAcquisitions.cs
?? CryptoWorkBenchAvalonia/Services/Automation/WorkbenchBindingV2.cs
?? CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionV2.cs
?? CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs
?? CryptoWorkBenchGuiTest/B3F1Tests.cs
?? CryptoWorkBenchGuiTest/B3F2Tests.cs
?? CryptoWorkBenchGuiTest/B3F3Tests.cs
?? CryptoWorkBenchGuiTest/B3F4Tests.cs
?? CryptoWorkBenchGuiTest/LeaseV2Tests.cs
?? docs/mcp/MCP-B3-F1-corrections.md
?? docs/mcp/MCP-B3-F2-corrections.md
?? docs/mcp/MCP-B3-F3-corrections.md
?? docs/mcp/MCP-B3-F4-corrections.md
?? docs/mcp/MCP-B3-mcp-binding-lease-v2.md
?? eng/mcp/native/B3NativeProbe.csproj
?? eng/mcp/native/Program.cs
~~~
