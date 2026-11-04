# MCP-B3-F1 – Security and Lifecycle Corrections

Arbeitsstand auf `feature/mcp-ap3`, HEAD `683f789324bd040b5e2f6e834b4c2f1ef0e8aa9f`. Grundlage war der uncommittete B3-Kandidat. Vor dem ersten Eingriff stimmten alle 29 SHA-256-Werte mit dem externen R1-Inventar überein. Git meldete 18 bearbeitete und elf neue Dateien. Die eingehenden TRX enthielten je Konfiguration 745 bestandene GUI-Tests und 132 bestandene MCP-Tests sowie ein nicht ausgeführtes installiertes Client-Szenario. [B3-Bericht](MCP-B3-mcp-binding-lease-v2.md) · [R1-Review](../../../b3-r1-independent/MCP-B3-R1-independent-review.md)

## Änderungen und dauerhafte Gegenproben

| Finding | Ursache und Korrektur am Produktpfad | Permanente Regression und Gegenkontrolle |
|---|---|---|
| R1-01 · Save nach L1 | Der ReadOnly-Observer konnte die Epoche zwischen Erstprüfung und Commit widerrufen. Die abschließende Prüfung berücksichtigt Closing unter demselben Registrylock wie L1. Der asynchrone Save startet Delete oder öffnet die Append-Datei noch unter dieser Serialisierung. Er wartet außerhalb des Locks und hält Originalticket, Dokumentlock und Q bis zum Ende. Der synchrone Eingang prüft ebenfalls nach dem Propertycallback. Ordneranlage und Save-Picker starten nur unter einem erneut geprüften Datei-Intent. | `SaveRechecksL1AfterReadOnlyObservers` deckt beide Save-Eingänge ab. Kein Delete, keine neuen Dateiinhalte, kein verspäteter Providerstart, danach ist ein lokaler Save möglich. `DeleteBeforeL1CompletesOriginalWriteAndUnlock` löscht eine echte temporäre Datei vor L1 und prüft den ursprünglichen Write, Unlock und GUI-Zulassung. |
| R1-02 · Letztes Detach | Die Registrierungsanzahl konnte schon null sein, obwohl der Coordinator noch ein Originalticket hielt. Die Registry erfasst bei L1 die Referenz auf den bestehenden Coordinator. Sein Drain gehört unabhängig von Editoranzahl und Lease zum gemeinsamen Close-Ticket. | `DetachedCutWaitsForOriginalClipboardBeforeClose` blockiert den Provider eines echten Cut, meldet die letzte Registrierung ab und wiederholt Disable bzw. Dispose. Nach der Providerfreigabe bleiben Original und Nachfolger unverändert. Nach normalem Disable nimmt der Nachfolger wieder Eingabe an. `DetachedSealedBodyStillOwnsItsCallback` trennt Bodyende und letzte Callbackfreigabe. |
| R1-03 · Native Gruppe | Exit-Quieszenz wurde als L2 gewertet. Nach dem Service-, Callback- und Quieszenz-Wait prüft der Coordinator die Originalgruppe, aktuelle Gruppe, Dokumentlocks und ausstehende Arbeit. Bei offenem Zustand bleibt das Epoch-Ticket fehlerhaft und Closing gesetzt. Erst nach erfolgreicher Zertifizierung dürfen Lease und Closing abgeräumt werden. Der Desktop-Exit darf nach Quieszenz den spezifischen Fehler einer offenen nativen Verpflichtung tolerieren. Öffentliche Epoch-Waits behalten ihn. | `FaultedEpochDistinguishesOpenNativeGroupFromExitQuiescence` injiziert einen Fehler beim Gruppenstart im öffentlichen v2-Edit. Receipt und Gruppe bleiben unbestimmt/faulted bzw. offen. Die zweite Variante erreicht Faulted bei geschlossener Gruppe und erlaubt den Epochabschluss. Beide verweigern GUI-Schreiben und Reaktivierung. |
| R1-04 · Execute-Replay | Der Execute-Eingang projizierte bekannte IDs allein mit Run-Rechten. Für vorhandene IDs und Tombstones wird nun der Registry-Lookup mit aktueller Epoche, Owner und `execution.read` verwendet. Erstannahme verlangt weiterhin Control, Run, Lease und Zustimmung. Ein Fingerprintkonflikt wird erst nach erfolgreicher Leseberechtigung sichtbar. | `JobReadsRespectGrantsFromFirstReplyThroughTombstone` prüft Run-only-Erstannahme, laufendes und terminales Replay, Abruf nach Release, Zeitablauf, fremden Principal und L1. Ein berechtigter Read-only-Caller kann den eigenen historischen Job lesen. |
| R1-05 · Globale Metadaten | Der Job enthielt den publizierten Variablensnapshot des Interpreters. Die gemeinsame Projektion liefert ohne `variables.metadata.read` keine Einträge und kein informationshaltiges Truncation-Flag. Der leere Ersatzsnapshot nutzt ausschließlich Zeit und Version des anfragbaren Jobs und ist als stale gekennzeichnet. Der interne Snapshot bleibt für berechtigte Leser erhalten. Die bestehende Output-Opt-in-, Zustimmungs- und Grantprüfung bleibt wirksam. | Derselbe Test erzeugt eine lokale Markervariable und prüft initiales abgelehntes Ergebnis, laufendes und terminales Replay sowie GetExecution. Die unberechtigte Liste ist leer, der berechtigte Abruf sieht den Marker. |
| R1-06 · Doppelte JSON-Felder | Das typisierte SDK-Dictionary hatte frühere Feldwerte schon verworfen. Das Binding verwendet die ursprünglichen `JsonRpcRequest.Params` des gepinnten SDK und parst sie mit `AllowDuplicateProperties=false`, bevor Argumentmigration, Resolver oder Service laufen. | `RawDuplicateNeverReachesResolverEvenIfLastValueIsValid` sendet für contractVersion, editorId, leaseId und text jeweils beide Feldreihenfolgen als rohes HTTP. Null Resolver- und Serviceaufrufe. Ein anschließender gültiger Request erreicht beide genau einmal. |
| R1-07 · Ausführungsrisiko | Execute veröffentlicht jetzt `openWorldHint=true`, `destructiveHint=true`, `readOnlyHint=false` und `idempotentHint=false`. Der lokale Dialog nennt ausdrücklich Desktoprechte sowie das Lesen oder Exportieren von Dateien und Schlüsseln. Client, Originaleditor, Revision, vollständiges Script, Arbeitsverzeichnis, State-Version und Ausgabeoption bleiben sichtbar. | `ExecuteDiscoveryExposesDesktopFileEffects` prüft die SDK-Discovery. `LocalExecutionConsentRecordsActualDecision` bedient den realen Dialog für einen harmlosen Save in eine temporäre Datei. Nur beim Execute-Klick entsteht die Datei. Der Quelltext bleibt erhalten. |
| R1-08 · Bestätigung nicht verfügbar | Der frühere Vergleich `== true` fasste fehlende Verfügbarkeit und Benutzerablehnung zusammen. Fehlendes TopLevel oder verlorener Owner ergibt nun einen fehlgeschlagenen Job mit sicherem `ConfirmationUnavailable`, echter Cancel-Klick einen abgelehnten Job. Die Registry speichert den Grund für Replay. Epochwiderruf hat weiterhin Vorrang. | Die Dialogregression umfasst Zustimmung, Cancel-Klick, fehlendes TopLevel, Ownerverlust bei offenem Dialog und Widerruf. Sie vergleicht Ausgang, Dateiwirkung und Replay. |
| R1-09 · MCP-Fehlerflag | Der DTO-Typ allein unterschied Execute nicht vom Lookup. Die Ausführungszustände beeinflussen `isError` jetzt nur beim Execute-Auftrag. Ein eigener Fehler des Lookups bleibt ein Toolfehler. | `ExecutionStateOnlyMarksTheExecutionCall` prüft rejected, failed, cancelled und completed mit Fehlerdiagnose an beiden Wire-Eingängen. `DeniedLookupKeepsStructuredError` prüft NotFound und Forbidden samt strukturiertem Inhalt. |
| R1-10 · Migration | Das Binding erkennt die bekannte v1-Argumentform vor Dispatch und unterscheidet `UnsupportedContractVersion` und bei v2 ohne Lease `LeaseRequired`. Sonstige unlesbare Daten liefern den neutralen InvalidParams-Fehler. Die Argumentform wird ausschließlich zur Ablehnung ausgewertet. | `StableMigrationErrorsAreDistinctFromMalformedInput` prüft Edit und Execute mit v1 ohne Versionsfeld, explizitem v1, v2 ohne Lease und fehlerhafter UUID. Kein Resolveraufruf und keine Defaultlease. |
| R1-11 · Text nach wirksamem Edit | Beim angewandten Edit mit CallbackFailed trägt nur der Textblock die feste Meldung „Editor operation failed after an applied change; refresh state.“. Structured Content enthält weiter den vollständigen D01-Receipt und das Fehlerflag bleibt gesetzt. | `AppliedMutation` prüft exakten Text, alle Receiptfelder und `isError` im tatsächlichen HTTP-/SDK-Adapter. |

Die neuen GUI-Tests nutzen den bestehenden Headless-Dispatcher mit produktivem Service, TextArea, Editor und Dialog. Für Clipboard und Zeitablauf werden der vorhandene Provideradapter und die vorhandene Testuhr verwendet. Die separaten Wire-Tests verwenden den echten SDK-/HTTP-Pfad und einen zählenden Service-Testdouble. [GUI-Regressionen](../../CryptoWorkBenchGuiTest/B3F1Tests.cs) · [Wire-Regressionen](../../CryptoWorkBench.Mcp.Tests/B3F1WireTests.cs)

## Verifikation und Abnahmegrenzen


.NET SDK 10.0.401. Für Debug und Release lief je ein `dotnet restore --locked-mode` der Desktop-Solution mit `NuGet.Config`, anschließend der Build mit `--no-restore`. Die Repository-Hooks aus `eng/prism/Enter-Prism.ps1` prüften Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` und 1.180 gepinnte Dateien. Beide finalen Solution-Builds hatten null Fehler und sechs Bestandswarnungen. Die vollständigen GUI- und MCP-Suites liefen jeweils gegen den gebauten Stand.

| Konfiguration | Suite | TRX-Einzelresultate | Passed | Failed | NotExecuted |
|---|---|---:|---:|---:|---:|
| Debug | GUI | 759 | 759 | 0 | 0 |
| Debug | MCP | 147 | 146 | 0 | 1 |
| Release | GUI | 759 | 759 | 0 | 0 |
| Release | MCP | 147 | 146 | 0 | 1 |

Die vier TRX wurden nach den tatsächlichen UnitTestResult-Outcomes ausgewertet und mit den Summen sowie den Testmethoden der Baseline abgeglichen. Je Suite sind 14 permanente Testfälle hinzugekommen. In beiden MCP-Läufen ist ausschließlich `InstalledCodexDiscoversAndCallsStatus` nicht ausgeführt. Die separate Auswahl aus F1, F2R-10, Coordinator, Registry-/Lease-Lifecycle, Return und Datei-/Passwortprovidern ergab 184 bestandene Release-Testfälle. Diese sind in der GUI-Gesamtzahl bereits enthalten. [TRX-Abgleich](../../artifacts/b3-f1/trx-audit.txt) · [Fokussierter Release-Lauf](../../artifacts/b3-f1/focus-final-release.log) · [Build Debug](../../artifacts/b3-f1/build-Debug.log) · [Build Release](../../artifacts/b3-f1/build-Release.log)

Der separat mit Prism gebaute Release-STA-Host bestätigte ein echtes Windows-HWND, blockierte Eingabe während MCP-Besitz, erlaubte Eingabe nach Release und gesperrte Wiedereingabe nach Shutdown. Die native Eingabe erfolgte programmatisch. Die Release-DLL `CryptoWorkBenchAvalonia.dll` im Desktop-Output und im nativen Host hatte byteidentisch SHA-256 `6F1D670FAEB8DC13D084CE9CBE368B2926BD11FED9420CCAF70F7A1048616B46`. [Native Probe](../../artifacts/b3-f1/native.log) · [Binärbindung](../../artifacts/b3-f1/native.sha256)

### Unveränderte R1-Prüfprogramme gegen Release

| R1-Beobachtung vor F1 | Verhalten nach F1 |
|---|---|
| Save meldete nach L1 Erfolg und begann Delete/Write. | `deleteCalledAfterL1=False`, `wrote=False`, `RevisionConflict`. |
| Drain war bei gezähltem Callback mit Pending=2 oder echtem Cut mit Pending=1 erfolgreich. | Beide Epoch-Waits blieben offen. Erst nach Freigabe sank Pending auf 0. Der verspätete Cut ließ den Originaltext unverändert. |
| Native Gruppe blieb offen, der Drain meldete Erfolg. | Mutation meldete `indeterminate/indeterminate/faulted`. Der Epoch-Task war `Faulted`, die Gruppe weiterhin offen und GUI-Schreiben gesperrt. |
| Ohne Read-Grant war der Job über Execute-Replay lesbar. | Regulärer Lookup und Replay lieferten `Forbidden`, fremder leseberechtigter Principal `NotFound`, alter Caller nach L1 `Forbidden`. |
| Lokaler Marker `R1Meta` erschien trotz verweigerter Metadatenabfrage im abgelehnten Job. Fehlender Owner erschien als Benutzerablehnung. | Der lokale Seed enthielt den Marker. Der externe Job enthielt keinen Marker. Sein historischer Zustand war `Failed` mit `ConfirmationUnavailable`. |
| Doppeltes Versionsfeld erreichte den Resolver, Discovery beschrieb Execute als geschlossen, historischer Lookup eines abgelehnten Jobs hatte ein Fehlerflag. | Die unveränderte R1-Wire-Probe erhielt für das Duplikat InvalidParams, las `openWorld=True` und erhielt den abgelehnten historischen Job als erfolgreiche Abfrage. Ihre gültigen Mappings erreichten weiterhin alle elf Servicemethoden. |

Die ursprünglichen Prüf-DLLs wurden aus dem externen R1-Ordner kopiert und mit F1-Release-Dependencies ausgeführt. Eigene Logs liegen im ignorierten F1-Artefaktordner. Die R1-Originale und der ursprüngliche B3-Bericht wurden nur gelesen. Die unveränderten Controls bestätigten zusätzlich F2R-10, echte Undo-/Redo-Folgefähigkeit, ursprüngliche Registrierung, CAS, verzögerte Timer, Rücknahme während der Registry-Vorbereitung, Ausgabegrants und Save-/Callback-Finalisierung. [R1 vor F1](../../../b3-r1-independent/proof.log) · [R1 nach F1](../../artifacts/b3-f1/r1-R1Proof.log) · [Kontrollen](../../artifacts/b3-f1/r1-Controls.log) · [Wire-Lauf](../../artifacts/b3-f1/r1-wire.log)

### D02 und verbleibende Gates

Der externe Lastversuch füllte das Prozess-Ledger mit einer aktiven Lease und 4.095 gespeicherten Ablehnungen bei insgesamt 64 ausgegebenen Credentials. Bei vollem Ledger erhielt ein neuer Erwerb `CapacityExceeded`. Renew erreichte Version 2, Release gelang, und der alte Erwerb blieb `AcquisitionRetired`. Eine zweite Aktivierung derselben Hostinstanz wurde verweigert. Eine weitere konstruierte Serviceinstanz sah dasselbe erschöpfte Prozessbudget. Die bestehenden Grenzen von 4.096 Slots und 256 IDs je Principal sowie der serverseitige Credentialkontext bleiben bestehen. Die gemessene Differenz des verwalteten Prozessheaps nach erzwungener GC betrug in dieser Probe 1.062.488 Byte. Ein einzelner Messpunkt belegt keine universelle 4-MiB-Spitzengrenze für alle gleichzeitig lebenden Ressourcen. [R1-Speicher- und Kapazitätsprobe](../../artifacts/b3-f1/r1-Memory.log)

Der sichere Rückfall benötigt weiterhin keinen L3-Übergang. Physischer Suspend/Resume, produktives Pairing und HTTP, ein vollständiger Lauf mit installiertem Client, sämtliche realen Providerfehler und ein vollständiger Nachweis sicherer Epochenrotation bleiben separate Gates. Insbesondere darf die für den Desktop-Exit zulässige Quieszenz einer offenen Fault-Gruppe künftig weder für einen Budgetreset noch als Epochenzertifikat verwendet werden.

## Dateiliste und Git-Abschluss

Der vollständige Status gegenüber HEAD umfasst 20 bearbeitete und 14 unversionierte Dateien. Von diesen 34 Kandidatendateien hat F1 13 geändert oder hinzugefügt. Die Spalte B3 bezeichnet gegenüber dem Eingang unveränderte Dateien. Der Index entspricht HEAD. Branch und HEAD entsprechen dem Eingang. [Status vor F1](../../artifacts/b3-f1/input-status.txt) · [Status nach F1](../../artifacts/b3-f1/final-status.txt)

| Git | Stand | Repository-Pfad |
|---|---|---|
| `M` | B3 | `CryptoWorkBench.Automation.Abstractions/AutomationContracts.cs` |
| `M` | B3 | `CryptoWorkBench.Automation.Abstractions/AutomationErrors.cs` |
| `M` | B3 | `CryptoWorkBench.Automation.Abstractions/AutomationJson.cs` |
| `M` | B3 | `CryptoWorkBench.Automation.Abstractions/AutomationSchemas.cs` |
| `M` | B3 | `CryptoWorkBench.Mcp.Tests/ContractTests.cs` |
| `M` | B3 | `CryptoWorkBench.Mcp.Tests/TestListener.cs` |
| `M` | F1 | `CryptoWorkBenchAvalonia/App.axaml.cs` |
| `M` | B3 | `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` |
| `M` | B3 | `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs` |
| `M` | F1 | `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` |
| `M` | F1 | `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs` |
| `M` | F1 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` |
| `M` | B3 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs` |
| `M` | B3 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs` |
| `M` | F1 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` |
| `M` | F1 | `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` |
| `M` | B3 | `CryptoWorkBenchGuiTest/B1EditorIntegrationTests.cs` |
| `M` | B3 | `CryptoWorkBenchGuiTest/B1F3CorrectionTests.cs` |
| `M` | B3 | `CryptoWorkBenchGuiTest/EditorAutomationTests.cs` |
| `M` | F1 | `CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs` |
| `??` | B3 | `CryptoWorkBench.Automation.Abstractions/AutomationV2.cs` |
| `??` | F1 | `CryptoWorkBench.Mcp.Tests/B3F1WireTests.cs` |
| `??` | B3 | `CryptoWorkBench.Mcp.Tests/BindingV2Tests.cs` |
| `??` | F1 | `CryptoWorkBench.Mcp/WorkbenchToolsV2.cs` |
| `??` | B3 | `CryptoWorkBenchAvalonia/Services/Automation/ProcessAcquisitions.cs` |
| `??` | B3 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchBindingV2.cs` |
| `??` | F1 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionV2.cs` |
| `??` | F1 | `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs` |
| `??` | F1 | `CryptoWorkBenchGuiTest/B3F1Tests.cs` |
| `??` | B3 | `CryptoWorkBenchGuiTest/LeaseV2Tests.cs` |
| `??` | F1 | `docs/mcp/MCP-B3-F1-corrections.md` |
| `??` | B3 | `docs/mcp/MCP-B3-mcp-binding-lease-v2.md` |
| `??` | B3 | `eng/mcp/native/B3NativeProbe.csproj` |
| `??` | B3 | `eng/mcp/native/Program.cs` |

## Empfehlung

B3-F1 ist bereit für ein unabhängiges Read-only-Rereview des uncommitteten Kandidaten. Der Rereview sollte die L1-Reihenfolgen, das strikte L2-Zertifikat sowie die Rechte- und Fehlerprojektionen gegen die hier gebundene Release-DLL erneut prüfen.
