# MCP-B3 – MCP-Bindung und Lease v2

**Kandidat für unabhängiges Read-only-Review.** Die Desktop-DI stellt den v2-Vertrag am bestehenden WorkbenchAutomationService bereit. Der Toolkatalog lässt sich an einen authentisierten Host binden. Die Desktop-Anwendung startet dafür keinen Listener. Das Gate für epochengebundene Kapazitätswiederverwendung bleibt **OPEN**. B3 verwendet den konservativen AP0-D02-Rückfall.

## Ausgangspunkt und Coverage

Der Produktworktree `mcp-ap3` war auf `feature/mcp-ap3` bei `683f789324bd040b5e2f6e834b4c2f1ef0e8aa9f` sauber. Architekturentscheidungen folgen R2 und AP0. Für die Implementierung gilt der spätere, abgenommene B1/B2-Stand. B1-F1 bis F3-F1 und die zugehörigen Reviews und N1-Abnahmen verfolgen die ursprünglichen Findings. Der B1-Abnahmezusatz lässt F2R-10 für einen späteren Receipt-Verbraucher offen. B2 hat bereits EC21, die Return-Herkunft, Save-/Picker-Pinning und die Registry-Finalisierung integriert. [Architektur und Plan](MCP-00-implementation-plan.md) · [R2](MCP-00-R2-consolidated-architecture-decision.md) · [AP0](MCP-00-AP0-contract-finalization.md) · [AP3-Inventar](MCP-AP3-01-integration-assessment.md) · [AP3-02](MCP-AP3-02-final-acceptance.md) · [B1-Abnahme](MCP-B1-N1-R-final-acceptance-addendum.md) · [B2](MCP-B2-execution-lifecycle-integration.md) · [B2-N1](MCP-B2-N1-final-native-acceptance.md)

| Paket | Akzeptierter Bestand | B3-Implementierung | Noch nachzuweisen oder später zu implementieren |
|---|---|---|---|
| AP4 | Originalregistrierung, Edit-/Save-/Pickerpflichten, Shutdown und Quieszenz | Konkrete Dokumentadresse, eigener MCP-Tickettyp im bestehenden Coordinator, Revalidierung nach Replace-Dialog, L1 auch während lokaler Providerarbeit | Vollständige reale Provider-/OS-Fehlerkombinationen für ein wiederverwendbares L2-Zertifikat bleiben offen |
| AP5 | Gemeinsames Q, Variablenbestand, Registry, Snapshots, Rückgabeanker, Deadline und Retention | MCP-Execute durch dieselbe Registry und Pipeline, jobgebundene Zustimmung, zusätzlicher Lease-Fingerprint, berechtigte Ergebnisprojektion | Lokale Passwortquarantäne bleibt im B2-Testumfang. Der komplette Verlauf mit echtem Passwortdialog und widerrufenem externen Client gehört zum End-to-End-Gate |
| AP6 | Statusprobe und strikte v1-Abstractions | Elf v2-Mappings, Credential-Prüfung, Besitz, CAS, Reclaim, D01, Prozessbudget und koordinierter Abschluss | Keine Freigabe von L3. Reale Suspend-/Hibernate-Abnahme und unabhängiges Refinement offen |
| AP7 / B4 | B1/B2 liefern Desktop- und Headless-Evidenz für ihre Binärstände | Aktuelle GUI-/MCP-Suiten und isolierter nativer B3-Testhost | Vollständige E2E-Abnahme mit Benutzerinteraktion und Clientstörungen später |
| AP8 | Bestehender isolierter Status-Probehost | Host kann v2-Tools mit einem Resolver für den ursprünglichen authentisierten Request registrieren | Produktionstransport, Pairing, Credentialverteilung und Remoteaktivierung benötigen ein separates Paket |

Die B1-Grenzen für fremde rohe AvaloniaEdit-Handler, natives Clipboard und mehrstufige CJK-Eingabe gelten fort. Vollständige Tabs, Docking und Script-Book-Persistenz sind ausserhalb des Pakets.

## Öffentlicher Vertrag und Autoritäten

Alle elf Mappings verlangen `contractVersion: "2"`. Der Katalog veröffentlicht geschlossene Ein- und Ausgabeschemas. Der Decoder prüft Typ, Pflichtfelder, exakte Enumwerte, Version, UInt64-Dezimaldarstellung, zulässige Offset-Kombination und Text-/Zeitlimits. Der Service prüft Originalinstanz, Editor, Revision, UTF-16-Grenzen und für Execute zusätzlich stateVersion. Der Host muss die bestehende HTTP-Bodygrenze und den sicheren Parserfehlerpfad beim späteren Anschluss beibehalten. Die v1-Statusprobe bleibt separat. Der alte MCP-Schreibaufruf ohne Lease wird abgewiesen.

| Operation | Zusätzliche Bindung und Antwort |
|---|---|
| get_status | status.read. Phase, editorWriteBlocked, mutationBusy, Ziel-ID. Eigener Leasebeleg nur für den authentisierten Besitzer. Job-IDs sind auf lesbare eigene Jobs beschränkt |
| get_editor | editor.read, instanceId und konkrete registrierte editorId. Ein fehlendes, ersetztes oder geschlossenes Dokument ergibt EditorUnavailable. Lesen einer inaktiven Registrierung wechselt die Auswahl nicht |
| acquire_editor / renew_editor | editor.control und editor.write. Acquire übernimmt den adressierten Slot. Historischer Erwerbsbeleg beziehungsweise letzter CAS-Beleg ohne Text, Principal oder Credential |
| release_editor | editor.control, eigene ursprüngliche Bindung. Releasing bezeichnet entzogene Neuzulassung, released den abgeschlossenen Übergang |
| update_editor | editor.control, editor.write, gültige Lease und Originalrevision. D01 meldet Texteffekt, Historyeffekt, Abschluss, beobachtete Originalrevision, Katalogfehler, Refresh und noAutomaticRetry |
| execute_editor | editor.control und execution.run, Lease, Revisions-/State-Snapshot und konkrete lokale Zustimmung. Das Dialogmodell bindet Client, Script, Arbeitsverzeichnis und Outputoption. Standardtimeout 30 Sekunden ab Annahme einschliesslich Dialogzeit |
| get_execution / get_diagnostics / cancel_execution | execution.read, ursprüngliche Jobzuordnung und aktive Credential-Epoche. Registrierung, Cancel, Deadline, Quarantäne, Ablauf und Tombstones bleiben serverseitig in B2. Ein autorisierter identischer Retry kann das bestehende Ergebnis nach Leaseende abrufen |
| get_variables | variables.metadata.read. Kopierte Metadaten samt stale-Flag, keine Werte, Schlüsselbytes oder lebenden Modelle |

IncludeText verlangt output.read, ausdrückliches Opt-in und lokale Zustimmung zum konkreten Auftrag. Die Registry begrenzt freigegebenen Output nach UTF-8-Bytes. Ein nachfolgender Zugriff desselben Principals ohne Outputgrant erhält redigierte Einträge. Fehlercodes, messageKey und Diagnosen stammen aus statischen Projektionen. Ein abgebrochener Resultatwait veranlasst keine zweite Ausführung oder Editoränderung.

### Besitz, Uhr und Kapazität

Der lokale Host erzeugt die Epoche und gibt begrenzt unveränderliche Credential-Objekte aus. Der Service vergleicht für MCP aktive Epoche, Grant und die Referenz auf den ausgegebenen Kontext. Eine Record-Kopie mit identischen oder veränderten Feldern ist kein Credential. Der Resolver am MCP-Eingang erhält den konkreten Requestkontext. Es gibt keinen fernaufrufbaren Aktivierungs-, Reclaim- oder Renew-Defaultpfad.

EditorBridge löst die ursprüngliche Registrierung auf. EditorCoordinator serialisiert GUI- und MCP-Schreibrecht, Generation und Editor-Tickets. Bei Acquire hält Q nur die Übernahmeprüfung. Eine einzelne Änderung oder Ausführung reserviert Q erneut fail-fast. Die Lease läuft nach 600 Sekunden ab. Windows liefert die verstrichenen Millisekunden über GetTickCount64, UTC liefert nur die Anzeigedaten. Rücksprung, drohender Überlauf und fehlende Plattformunterstützung sperren neue Leaseannahmen. Der Dispatcher-Timer prüft beim Aufwachen den aktuellen absoluten Termin. Eine verspätete Zustellung kann eine Frist weder verlängern noch eine vorzeitig noch gültige Nachfolgerlease entziehen. Die Windows-API zählt laut Plattformvertrag auch Sleep und Hibernate. [Microsoft Windows Time](https://learn.microsoft.com/windows/win32/sysinfo/windows-time)

Ein einziges Prozess-Ledger hat 4.096 Slots und höchstens 256 Einträge je Principal. Es hält auch endgültige gültige Ablehnungen. Instanz, Epoche, Principal und Acquisition-ID bilden den Schlüssel. Editor und kanonische Revision bilden mit der geprüften Instanz und Vertragsversion den Fingerprint. Die nie wiederverwendete Slotnummer erzeugt eine innerhalb des Prozesses eindeutige Lease-ID. Ein terminaler Slot hat keine Referenz auf Host, Ticket, Timer oder Job. Der Service hält pro aktiver Lease genau einen Renew-Beleg. Die Quoten gelten im Rückfall über die gesamte Prozesslebensdauer. Das separate 10.000er Execution-ID-Budget bleibt bestehen.

### D01 und F2R-10

Bei der historischen Reproduktion verwirft der Completion-Controller während UpdateStarted die Einfügung. Die optionale Caret-Aktion kann CanUndo vorübergehend setzen. Der Coordinator erfasst den ausführbaren Zustand beider Stacks vor und nach der eigenen Gruppe. Bei beidseitig leerem Ausgang und Abschluss belegt eine transiente Benachrichtigung keine Historywirkung. Die Regression kontrolliert unveränderten Text und Revision, beide Stacks, SizeLimit, geschlossene Gruppe, Ticketzahl und None/Complete. Die benachbarten Fälle prüfen erhaltenes vorheriges Redo sowie erfolgreiche Completion mit ausführbarem Undo und Redo. Der v2-Fehlerfall prüft bereits angewandten Text bei entkommenem Callbackfehler, indeterminate/faulted und die Sperre weiterer Arbeit. [Historische Reproduktion](MCP-B1-F3-F1-final-correction.md)

## Sicherheitsmodell und Refinement

B3 reproduziert das bereits für AP0 §5.9 erstellte endliche Transitionssystem in einem separaten Artefaktverzeichnis. Seine volle BFS prüft alle erreichbaren Zustände und Kanten bis zur leeren Frontier. Es modelliert zwei Principals und Epochkontexte, Kapazität 2/1, konkurrierende IDs, CAS-Versionen, Credentialwiderruf, offene Jobs/Callbacks, Producer-Sealing, Hängen und Abschluss. Die Zeitkomponente exploriert Ablauf und veraltete Timer getrennt. Die Modellannahmen und der Induktionsschritt über beliebig viele sichere Rotationen sind in D02-S1 §§3–5 definiert. [D02-S1](MCP-00-D02-S1-epoch-safety.md) · [B3-Modellquellen und Ergebnisse](../../artifacts/b3/model/)

| Erneut ausgeführtes Modell | Zustände | Kanten | Frontier |
|---|---:|---:|---|
| Epochenmodell | 578.952 | 36.261.115 | erschöpft |
| Prozessbudget | 323.124 | 14.006.390 | erschöpft |
| Zeit/CAS | 268 | 12.166 | erschöpft |

Alle zwölf gerichteten Modellproben liefern PASS_MODEL_ONLY. Elf absichtlich fehlerhafte Varianten liefern die erwarteten Gegenbeispiele, etwa Rebinding eines alten Callers, Löschen eines Tombstones, fehlende Commitfence, Abschluss ohne Callback oder Sealing, alte Lease-ID und fristverlängernder CAS-Replay. Die vollständigen Gegenbeispielzustände stehen neben den BFS-Ergebnissen. [Proben und Gegenbeispiele](../../artifacts/b3/model/results/probes.json)

| Invariante | Abbildung auf B3 und Beweisgrenze |
|---|---|
| I01, I06 – eine Autorität | `ActivateMcp` prüft und setzt `activationUsed` unter dem Registry-Lock. L1 entfernt den aktiven Kontext und alle Credentialreferenzen. Kein Übergang setzt activationUsed zurück. Deshalb entsteht in derselben Hostinstanz nach L1 keine zweite Epoche |
| I02, I07 – originale Wirkung | Authentisierter Caller und Requests bleiben über Dispatcher- und Dialog-Awaits gebunden. V2Access, CheckBinding, Editor-Ticketcommit sowie Job-Admissible/Fence prüfen die Originalbindung. GUI-Neuzulassung erfolgt am bestehenden Coordinator. Ein angenommener Save darf seinen ursprünglichen Write nach Delete abschliessen |
| I03, I04, I10 – Pflichten | B2 veröffentlicht Job, editCompletion, saveCompletion und fileDialogCompletion vor ihren Wirkungen. Der Coordinator hält Ticket, ObserverDepth, Callbackzählung und Sealing. FinishEpochCloseAsync wartet auf den Service-Finalizer, die Lease-Cancellation und das ursprüngliche Quieszenzsignal. Ein hängender Provider behält den Abschluss. Ein erkannter Cleanupfehler verhindert bestätigten Übergang. Quieszenz eines faulted Editors erlaubt keine GUI-Schreibfreigabe |
| I05 – Induktion über Acquires | Anfangs ist used=0. Nur Reserve fügt unter dem Ledgerlock nach beiden Quotentests einen Slot hinzu. Replay und Konflikt verändern used nicht. Release, Expiry, Dispose, Disable und lokale Aktivierung haben keinen Löschpfad. Damit kann kein bereits verbrauchter Schlüssel erneut erwerben. Slotnummern wachsen höchstens bis 4.096 |
| I08 – Induktion über Renew | Nur CAS mit der aktuellen Version erhöht sie um eins. Der direkte Vorgänger liest das eingefrorene Ergebnis, alle anderen Versionen ergeben Konflikt. Es gibt keinen Wrap. EndLease setzt den terminalen Grund vor der Einleitung des Drains |
| I09 – Speicher | Feste Slotzahl, begrenzte Principal-/Revisionsstrings, maximal 64 aktive Credentialobjekte mit je maximal 16 begrenzten Grants und ein Timer pro aktivem Workspace. Der Kapazitätstest misst die verwalteten Allokationen einschliesslich Hilfsobjekten beim Füllen aller 4.096 Slots. Ein zweiter Test misst die maximal zugelassene Credentialmenge und eine aktive Lease. Sie prüfen getrennte Obergrenzen von 3 MiB und 1 MiB |

Das modellspezifische L3-Zertifikat erfordert eine vollständige Zuordnung sämtlicher realer Producer, Callback- und Fehlerpfade. Die Integrationstests unten belegen bestimmte Ordnungen, keine allgemeine Äquivalenz aller Plattform- und Schedulerabläufe. B3 implementiert daher **keinen** Kapazitätsreset nach L2. Auch wiederholte Disable-/Aktivierungsversuche öffnen kein neues Kontingent. Bereits ein lokales Disable beendet die einzige Aktivierung dieses Hosts. Ein neuer Prozess benötigt neue Instanz und lokale Autorisierung.

## E01–E12 im Produkt

| Fall | Produktnachweis und Disposition |
|---|---|
| E01 | Kopierter Caller und nach L1 pausierter ursprünglicher Caller werden abgewiesen. Alter Acquire und wartendes Edit/Execute verlieren ihre Autorität. E+1 im selben Host ist durch den Rückfall gesperrt |
| E02 | Widerruf am Registry-Checkpoint vor Veröffentlichung lässt keinen Job und keine Zustimmung zurück. Widerruf während Replace- oder Run-Zustimmung verhindert den Commit. Bereits angenommene Resultatfinalisierung wird abgewartet |
| E03 | 256 IDs eines Principals und 4.096 insgesamt mit maximal langen Identitätsstrings geprüft. Die Tabelle hat keinen vorzeitigen Byte-/Kompaktierungspfad. Replay, Konflikte und terminale Entscheidungen bleiben adressierbar |
| E04 | Rückfall geprüft. Wiederholtes Aktivieren wird abgewiesen, alte Credentials bleiben ungültig. Frisches Kontingent nach L2 **gesperrt** |
| E05 | Lokaler Picker, separat offen gehaltener Dispatcher-Callback, wartendes Replace, Jobzustimmung und verzögerter Save-Provider verzögern die Freigabe. B2 prüft auch späte Passwortantwort und Q bis zur finalen Präsentation. Kombinierter nativer Passwort-/Widerrufsfall **OPEN** |
| E06 | RegisterCallback nach Ticketversiegelung wird abgewiesen. Ein realer Editor-Callback hält den Epochabschluss nach Picker-Rückkehr offen. Vollständiges producerübergreifendes Refinement für L3 **OPEN** |
| E07 | Wiederholter Widerruf erzeugt keinen neuen Epochkontext. Abschlusspfade behalten bei Fehlern die Sperre. Vollständige produktnahe Fehlerinjektion für Timer-, Dispose- und Resultatkopiefehler **OPEN** |
| E08 | Ein blockierter Save-Provider verhindert während wiederholter Aktivierungsversuche die GUI-Wiederzulassung. Die Probe gibt ihn gezielt frei und prüft danach Originaldatei und Drain. Im Modell darf der Provider dauerhaft hängen |
| E09 | Allokationsmessungen für das lebende Protokollbudget im Rückfall. Kein Epochenarchiv, kein Execution-Budgetreset. Heap-Stresstest über viele reale Rotationen **nicht anwendbar**, da L3 gesperrt ist |
| E10 | Parallele CAS-Requests liefern einmal eine Verlängerung und sonst denselben Beleg. Die ältere Version scheitert nach weiterem CAS. Ein alter Weckzeitpunkt entzieht die verlängerte Lease nicht |
| E11 | Fake elapsed clock vor, an und nach der Grenze sowie bei Rücksprung und Überlauf. Die native Probe verwendet die reale Windows-Uhr. Physischer Sleep/Hibernate mit Resume vor und nach 600 Sekunden **OPEN** |
| E12 | Shutdown widerruft und schliesst die Wiedereingabe im nativen Test. Die Prozesskonstruktion verhindert Wrap des Erwerbsordinales. Crash zu allen L1/L2/L3-Zeitpunkten, erzwungener UInt64-Renew-Überlauf und echtes Prozess-Restart-Pairing **OPEN** |

## Build, Tests und native Evidenz

| Konfiguration | Suite | Total laut TRX | Passed | Failed | NotExecuted |
|---|---|---:|---:|---:|---:|
| Debug | GUI | 745 | 745 | 0 | 0 |
| Debug | MCP | 133 | 132 | 0 | 1 |
| Release | GUI | 745 | 745 | 0 | 0 |
| Release | MCP | 133 | 132 | 0 | 1 |

Die vier XML-Dateien wurden über ResultSummary/Counters und die Outcomes aller UnitTestResult-Einträge abgeglichen. In jeder Konfiguration ist `InstalledCodexDiscoversAndCallsStatus` der einzige nicht ausgeführte Test. Beide finalen Desktop-Solution-Builds meldeten null Fehler und sechs bestehende Warnungen. Die Release-DLL aus dem Desktop-Output und die im nativen Testhost geladene Kopie von `CryptoWorkBenchAvalonia.dll` haben denselben SHA-256-Wert `5B71D7B446C6C79EB8602A0887C4295CB82A1ED257C9AE430B1EB1FFC76DA527`.

Die Release-Messung ergab 2.598.200 zugewiesene Bytes für das vollständige Ledger samt Testeingaben und 255.144 Bytes für die Credential-/Acquire-Probe. Debug ergab 2.598.200 und 255.224 Bytes. Die Messwerte gelten für die protokollbezogenen Testpfade der gepinnten .NET-Laufzeit. Queue, Interpreter, Jobresultate und Transport besitzen eigene Budgets. Die Messung ersetzt kein Heap-Profil einer hypothetischen Epochenrotation.

Die verwendete .NET-SDK-Version ist 10.0.401. Vor jedem Konfigurationsbuild lief der gesperrte Restore mit NuGet.Config und den Repository-Prism-Hooks. Die Präparation prüfte Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` und 1.180 gepinnte Dateien. Das erste Entwicklungsbuild ohne Prism-Hooks schlug fehl. Seine acht generierten NuGet-Pakete wurden in den ignorierten B3-Artefaktordner verschoben. Kein Prism-Quellfile wurde angepasst. [Lokale Buildlogs und TRX](../../artifacts/b3/)

**Windows, echte native Oberfläche.** Der separate STA-Testhost lädt die Release-Bibliothek über Avalonia Desktop und fordert ein echtes Windows-HWND. Er gibt programmatisch Text an dieselbe TextArea, prüft die Sperre während MCP-Besitz, verifiziert die sichtbare Dokumentmutation nach Release und schliesst bei erneutem Besitz mit Shutdown. Das ist ein automatisierter nativer Backendtest. Physische Tastatur, Sichtprüfung durch einen Benutzer und tatsächlicher Standby wurden in diesem Lauf nicht beobachtet. Die Headless-Suiten benutzen einen separaten Dispatcher und werden oben separat gezählt. [Native Probe](../../eng/mcp/native/Program.cs) · [Native Ergebnisdatei](../../artifacts/b3/native.log)

Für eine Wiederholung in einer vorbereiteten PowerShell im Produktworktree:

```powershell
. ./eng/prism/Enter-Prism.ps1
dotnet restore eng/mcp/native/B3NativeProbe.csproj -p:Configuration=Release --configfile NuGet.Config
dotnet build eng/mcp/native/B3NativeProbe.csproj -c Release --no-restore -m:1
dotnet eng/mcp/native/bin/Release/net10.0/B3NativeProbe.dll
dotnet eng/mcp/native/bin/Release/net10.0/B3NativeProbe.dll --manual-expiry
```

Für den letzten Aufruf das Testsystem nach dem Besitzwechsel über zehn Minuten suspendieren. Nach Resume die Konsoleneingabe bestätigen. Die Probe erwartet den ursprünglichen Ablaufgrund und danach erfolgreiche GUI-Eingabe. Für die noch offene Gegenkontrolle unter zehn Minuten Zeitstempel, Restfrist und anschliessenden Ablauf erfassen. Ein normaler Zehn-Minuten-Wait liefert keinen physischen Standbybeleg.

## Dateiinventar und Reviewentscheidung

- Bearbeitet · `CryptoWorkBench.Automation.Abstractions/AutomationContracts.cs`
- Bearbeitet · `CryptoWorkBench.Automation.Abstractions/AutomationErrors.cs`
- Bearbeitet · `CryptoWorkBench.Automation.Abstractions/AutomationJson.cs`
- Bearbeitet · `CryptoWorkBench.Automation.Abstractions/AutomationSchemas.cs`
- Bearbeitet · `CryptoWorkBench.Mcp.Tests/ContractTests.cs`
- Bearbeitet · `CryptoWorkBench.Mcp.Tests/TestListener.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/App.axaml.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs`
- Bearbeitet · `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs`
- Bearbeitet · `CryptoWorkBenchGuiTest/B1EditorIntegrationTests.cs`
- Bearbeitet · `CryptoWorkBenchGuiTest/B1F3CorrectionTests.cs`
- Bearbeitet · `CryptoWorkBenchGuiTest/EditorAutomationTests.cs`
- Bearbeitet · `CryptoWorkBenchGuiTest/WorkbenchRegistryTests.cs`
- Neu · `CryptoWorkBench.Automation.Abstractions/AutomationV2.cs`
- Neu · `CryptoWorkBench.Mcp.Tests/BindingV2Tests.cs`
- Neu · `CryptoWorkBench.Mcp/WorkbenchToolsV2.cs`
- Neu · `CryptoWorkBenchAvalonia/Services/Automation/ProcessAcquisitions.cs`
- Neu · `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchBindingV2.cs`
- Neu · `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionV2.cs`
- Neu · `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs`
- Neu · `CryptoWorkBenchGuiTest/LeaseV2Tests.cs`
- Neu · `eng/mcp/native/B3NativeProbe.csproj`
- Neu · `eng/mcp/native/Program.cs`
- Neu · `docs/mcp/MCP-B3-mcp-binding-lease-v2.md`

Die Binär-, Modelllauf- und TRX-Artefakte liegen getrennt in `artifacts/b3`. Der Abschlussstatus umfasst 18 bearbeitete und 11 neue Dateien. Der Index ist leer und HEAD entspricht der Baseline. Der Diff-Check ist ohne Befund. Für das unabhängige Review sind insbesondere die Commitgrenze nach Dialogen, die Reihenfolge von Ressourcenfreigabe und GUI-Zulassung, der Speicherumfang sowie die Abgrenzung zwischen Modell und Produkt zu prüfen. **Empfehlung: Read-only-Review des konservativen Kandidaten.** Für Epochenrotation, AP7/B4 oder produktive Remoteaktivierung steht die Abnahme aus.
