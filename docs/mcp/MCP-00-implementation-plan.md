# MCP-00 – Implementierungsplan für MCP-01

Status: Vorschlag, noch nicht zur Implementierung freigegeben. Revision MCP-00-R1 (Reviewpunkte R1–R5), Stand: 22.10.2026. Basis: `027815e9ea6f2dab11bd03485500b819907f2862`, bestehender Worktree `feature/mcp-server`. Massgeblich sind die revidierten Verträge in [MCP-00-architecture.md](MCP-00-architecture.md).

## 1. Minimaler durchgängiger Umfang

MCP-01 verbindet einen lokalen Codex-Client mit der laufenden Avalonia-Anwendung über einen authentisierten Streamable-HTTP-Endpoint. Der Benutzer aktiviert den Zugang, erteilt begrenzte Rechte und bestätigt konkrete Ausführungen lokal. Der Agent liest Status/Editorrevision, fügt Script ein, führt genau diese Revision aus und bekommt ein strukturiertes Ergebnis. Die GUI zeigt denselben Variablenzustand und dieselben Diagnosen.

Zum MVP gehören die acht Tools des Architekturvertrags: Status, Editor lesen/ändern, Editor ausführen, Ergebnis lesen, Diagnostik lesen, Variablenmetadaten lesen und eigenen Lauf abbrechen. Insert/Append/Replace werden mit Revisionen und lokalem Schutz des ersetzten Inhalts umgesetzt. Angenommene Aufträge gehören dem Service und laufen nach HTTP-Disconnect weiter; die Ergebnisabfrage liest ihren Zustand ohne Replay. Ausdrücklicher Cancel, Service-Deadline, MCP-Deaktivierung und Desktop-Shutdown steuern den Abbruch. Das bleibt ein begrenzter In-Memory-Auftragsvertrag ohne persistentes Job-System oder MCP-Tasks-Erweiterung.

Die gemeinsame Ausführungspipeline muss bereits GUI-Eingabe, ScriptBook-Import und MCP bedienen. Die Migration der vorhandenen Host-Orchestrierung ist daher Voraussetzung des kleinsten belastbaren MVP. Lediglich einen MCP-Runner neben der GUI zu starten wäre kein zulässiger Zwischenerfolg.

Ausserhalb von MCP-01: externer `execute_text`-Toolzugang, allgemeine Datei-Upload-/Download-Tools, rohe Schlüssel-/Variablenwerte, Remote-Binding, OAuth-Server, persistente Credentials, Cloud-/Tunnelzugriff, mehrere isolierte Interpreter pro Prozess, zweiter Host, IPC, neue CryptoScript-Operationen und eine Sprach-/Dateisystem-Sandbox. Das hostneutrale `ExecuteTextAsync` wird intern für die vorhandene GUI-Ausführung benötigt.

## 2. Implementierungsschritte und Nachweise

### Schritt 0 – Freigabe und Ausgangsstand

Gemeinsam die R1–R5-Korrekturen prüfen: HTTP-unabhängige Auftragslebensdauer, Save-Revision/Commit-Sperre, Runner pro Einheit mit serviceweiter Passwort-Quarantäne, getrennte Tool-/Diagnosebewertung und SDK-Kandidat 2.3.0. Die vereinbarten Projektgrenzen, fail-fast Admission, Editor-ScriptBook-Semantik und Sicherheitsregeln bleiben bestehen. Erst nach Freigabe Produktivdateien ändern.

Im vorhandenen Worktree Status und Unterschiede zu weiterentwickeltem `master` neu prüfen. Keine automatischen Resets, Rebases oder Änderungen fremder Worktrees. Die obige Basis ist eine Momentaufnahme; zwischenzeitliche Änderungen an Interpreter-Gates, Snapshot-Funktionen und Diagnostik zuerst fachlich abgleichen. `.codex/config.toml` bleibt aus Änderungen und Commits ausgeschlossen. Commit/Push/Merge benötigen separate Freigabe.

### Schritt 1 – SDK-/Client-Kompatibilität und Vertragsbasis

- Den geprüften stabilen Kandidaten `ModelContextProtocol.AspNetCore` **2.3.0** nach Freigabe exakt pinnen; tatsächlichen Restore-/Buildgraph und Desktop-Frameworkbedarf prüfen. Release, Paketmetadaten und v2.3.0-Quellstand wurden in MCP-00-R1 ohne Installation geprüft; Quellen und relevante Release-Folgen stehen in Architekturabschnitt 6.1.
- Abstractions-Projekt und DTOs aus Abschnitt 4 anlegen. Serialisierungsregeln, Größenlimits, Nullable-Felder, Enumtexte und Fehlercodes als Vertragstests festlegen.
- MCP-Projekt zunächst gegen einen einfachen Test-Adapter prüfen: stateless Streamable HTTP `/mcp`, aktuelle Revision `2026-07-28` und älterer Handshake `2025-11-25`. Der Test-Adapter ersetzt nur die GUI in isolierten Transporttests; er ist kein Produktionsrunner.
- Mit der tatsächlich eingesetzten lokalen Codex-Version einmal tools/list und einen ungefährlichen Statusaufruf durchführen. Version/Protokoll/Paketpin dokumentieren. Zusätzlich Discovery-/HTTP-Fehler, einen stillen Handler bis nach Client-Warteabbruch, null-sicheres ServerInfo im Testclient und sichere HTTP-400-Parseantworten prüfen. Keine Änderung der projektlokalen `.codex/config.toml`; eine spätere Clientkonfiguration wird separat lokal eingerichtet.

Gate: .NET-10-Build und beide Protokollpfade nachgewiesen; keine implizite GUI- oder CryptoScript-Referenz im MCP-Projekt. Eine Abweichung bei neueren SDK-APIs wird vor Host-Migration geklärt.

### Schritt 2 – Gemeinsame Host-Ausführung herauslösen

Die vorhandene Logik aus `ExecuteLineAsync` und `ExecuteScriptBookCoreAsync` in einen Service-eigenen Zeilenkern und eine Buchschleife überführen. Runner weiterhin pro Ausführungseinheit erzeugen, pro Runner genau einen Passwort-Handler binden und abschliessend entfernen; kein Singleton-Runner und kein Pool. Unterschiedliche RunnerIds teilen denselben globalen Variablenbestand und denselben exklusiven Host-Scope. Den Parserreset, asynchrone Operationsdispatches, Fehlertransport und Print-Reihenfolge erhalten. Diagnose-DTOs vor dem Verwerfen der Einheit kopieren.

Alle konkurrierenden Mutationen verwenden dieselbe Admission-Grenze, einschliesslich des späteren GUI-Save-Commits. `InterpreterHostQueue` erhält einen nicht wartenden Eintritt. Ganze Bücher halten eine Reservation und einen Host-Scope; innere Zeilen nehmen keine zweite Queue. `VariableViewModel`-Änderungen und Book-Clear werden über service-interne Mutationen koordiniert und aktualisieren stateVersion. Der GUI-Save-Commit braucht Admission, aber keinen Interpreter-Scope und erhöht stateVersion nicht. Die vorhandenen öffentlichen synchronen Ausführungseinstiege werden aus produktiven Aufrufketten entfernt; Tests auf diese Einstiegspunkte werden auf die gemeinsame Async-Pipeline umgestellt. Keine synchronen Warte-Wrapper auf dem Dispatcher.

Gate: bestehende Einzelzeilen-/Buchsemantik bleibt erhalten; sequenzielle Runner pro Einheit durch denselben Service, keine parallel aktiven Runner oder separate MCP-Ausführungspipeline, keine verschachtelte RunAsync-/Queue-Nutzung. Die bewusst geänderte Präsentation von Teilzustand nach Fehler ist separat getestet.

### Schritt 3 – Snapshots, Ergebnisse und Cancellation

Tiefe DTO-Kopien unter `AccessState`, auch nach Fehler/Abbruch. Eine service-eigene executionId verbindet Zeilen; Runner-executionIds bleiben Diagnoseprovenienz. Collector für Print/Info mit Reihenfolge und bounded Transportprojektion ergänzen. GUI-Payload und externe Antwort getrennt projizieren, ohne die Print-Semantik zu ändern.

Status und Metadatensnapshot atomar veröffentlichen. Annahme und Ergebnisablage mit Principal/Aktivierungsepoche, acceptedAt/deadlineAt, cancellationReason, Retention, executionId-Deduplizierung und Tombstones hinzufügen. Service-eigene beobachtete Tasks und Execution-CancellationSources behalten die Aufträge auch nach Ende des HTTP-Scopes. Request-Cancellation beendet nach Annahme nur den Antwort-Wait; nur explizite Abbruchanforderung, Service-Deadline, lokale Benutzerentscheidung und Lifecycle-Ereignisse dürfen den Execution-Token abbrechen. Aktive Aufträge nicht aus dem Ergebnisregister verdrängen; Retention erst ab terminalem Abschluss.

Die Passwort-Quarantäne serviceweit umsetzen: Callback-Tasks und Antwortreferenzen pro Runner/Request registrieren, nach Ende von Runner und Callback mit dem vorhandenen idempotenten Dispose-Vertrag defensiv bereinigen, dann Subscription lösen und Admission freigeben. Kein Probeaufruf und kein Zugriff auf private Runner-Felder. Die nächste Buchzeile bzw. ein neuer Auftrag darf die Quarantäne nicht mit einem frischen Runner umgehen. Diagnose-Reihenfolge und Zustandskopie bleiben unter dem Host-Gate.

Gate: keine lebenden Dictionary-/SemanticError-/GUI-Objekte im DTO; get_status/get_variables bleiben während Passwortdialogen responsiv; alte Ergebnisse ändern sich nicht durch spätere Ausführung. HTTP-Disconnect erzeugt weder Ausführungsabbruch noch zweite Ausführung. Expliziter Cancel/Timeout hält Admission bis Unwind und Quarantäne-Ende.

### Schritt 4 – Editor-Adapter und GUI-Anbindung

Echten Editor bei Attach registrieren, bei Detach abmelden; editorId und monotone Revision verwalten. Alle Dokumentänderungen inklusive Undo/Redo, History, Ausgaben und Dokumenttausch berücksichtigen. Einfügen, Anhängen und Ersetzen in einer Undo-Gruppe implementieren; kein synthetisches Tastaturereignis und kein implizites Speichern/Ausführen/Clear.

Revision und UTF-16-Offset auf dem Dispatcher prüfen. Für ersetzte Inhalte lokale Vorher/Nachher-Bestätigung, anschliessend erneut validieren. Konservative dirty/unknown-Erkennung ohne erfundene Save-Baseline.

Den verifizierten GUI-Pfad `SaveScriptBookCommand → GuardAsync → SidebarViewModel.SaveScriptBook → SaveFilePickerAsync → Überschreibdialog → DeleteAsync → CryptoScriptEditViewModel.SaveScriptBook` revisionsgebunden machen: SaveIntent vor dem ersten await erfassen; nach Auswahl/Bestätigung Admission nehmen und Revision/Editor/Lifecycle **vor Delete/Open** prüfen. Danach alle Editor-Mutationen kurz bis Write-Ende sperren und ausschliesslich den bestätigten Snapshot schreiben. Den Writer entsprechend als Snapshot-Empfänger anbinden. ASCII, Weglassen leerer/Whitespace-Zeilen, LF, Append und bestätigtes DeleteAsync vor Write bleiben erhalten. Keine Umstellung auf transaktionalen Replace oder die CreateNew-Semantik der Sprachfunktion Save. Einen begonnenen destruktiven Save-Commit bei Shutdown bis Erfolg/Fehler finalisieren; dirty nicht automatisch löschen.

Editor-Gesamtausführung erstellt einen unveränderlichen Snapshot und nutzt die ScriptBook-Pipeline. Die interaktive View behält Anchor-Einfügung der Print-Ausgabe; Gesamtausführung verändert ihren Quelltext nicht nochmals. Status-/Variablen-VM werden aus dem Serviceergebnis aktualisiert.

Gate: gleichzeitig tippen, Bestätigung, Viewwechsel, Speichern und Ausführen können keinen falschen Dokumentstand verwenden. Auch während DeleteAsync verhindert die Commit-Sperre alle Editor-Mutationen. IsExecuting/Busy ist für sämtliche GUI-Kommandos aus derselben Servicequelle abgeleitet; mutationBusy bildet auch Save ohne Scriptlauf ab.

### Schritt 5 – Sicherheit und eingebetteter HTTP-Host

Default-off-Konfiguration, Loopback-Binding, exakte Host-/Originprüfung und Bearer-Pairing ergänzen. Toolregistrierung explizit und minimal; Autorisierung pro Tool und pro Ergebnis. Privilegierte Contextdaten aus geprüften Credentials ableiten, niemals aus Toolargumenten. MCP-Ausführung benötigt lokale Bestätigung für Script, Zustand, Arbeitsverzeichnis und Ausgabeoption.

Prism-Serviceinstanz in ASP.NET-DI übernehmen. Webhost-Start/Stop kontrolliert vom Desktop, ohne zweiten blockierenden Run-Loop, ConsoleLifetime oder unerwartete URL-Overrides. Keine HTTP-Requestobjekte oder request-scoped Services in angenommene Aufträge übernehmen. Request-/Output-Limits, sichere Logs, sanitisierte Diagnoseprojektion und standardmässig unterdrückten Output implementieren. isError anhand der Tool-/Kontrollfluss-Tabelle in Architekturabschnitt 6.3 abbilden: completed bleibt auch mit hasErrors=true ein erfolgreicher Execute-Toolaufruf; erfolgreiche Ergebnisabfragen liefern isError=false unabhängig vom historischen Laufstatus.

Gate: Auth/Host/Origin-Fehler erreichen den Interpreter nicht; verlorene/abgelaufene Tokens können keine Resultate lesen; einschliesslich ausdrücklichem Print geheimen Materials kein unbestätigter MCP-Export.

### Schritt 6 – Lebenszyklus und plattformübergreifender Durchlauf

Geordneten Closing/ShutdownRequested-Ablauf implementieren; Exit/Closed bleiben idempotente Fallbacks. Bewusstes Disable von blossem HTTP-Disconnect unterscheiden und nur MCP-Aufträge mit mcpDisabled abbrechen. Beim Desktop-Shutdown Scriptaufträge mit shutdown abbrechen, Passwort-Quarantäne drainen und begonnenen GUI-Save-Commit abschliessen. Portkollision, Editor-unavailable, fehlenden Dialogowner, laufendes Passwortfenster und verzögerte synchrone Operation testen. ASP.NET-Runtime-Anforderung in Desktop-Publish und Installationshinweisen berücksichtigen.

Alle drei Produktionsprojekte und die Testgraphen gezielt bauen. Bestehende komplette Regressionstests und neue Tests auf der vorhandenen CI-Matrix ausführen; anschliessend lokaler Codex-Roundtrip mit dem echten Editor. Nur nach erfolgreichen Gates ist MCP-01 abgeschlossen.

## 3. Vorgesehene Projekt- und Dateiänderungen

Alle Pfade sind Vorschläge für MCP-01; MCP-00 legt sie nicht an.

| Projekt / bestehender Pfad | Geplante Änderung |
| --- | --- |
| `CryptoWorkBench.Automation.Abstractions/` (neu) | `.csproj`, `IWorkbenchAutomationService.cs`, `AutomationContracts.cs`, `AutomationErrors.cs`; hostneutrale Vertragstypen. |
| `CryptoWorkBench.Mcp/` (neu) | `.csproj`, `McpHost.cs`, `McpOptions.cs`, `WorkbenchTools.cs`, `McpAuthentication.cs`, `LoopbackRequestPolicy.cs`; nur Transport, DI, Security und DTO-Mapping. |
| `CryptoWorkBenchAvalonia/Services/Automation/` (neu) | `WorkbenchAutomationService.cs`, `WorkbenchExecutionPipeline.cs`, `WorkbenchSnapshots.cs`, `ExecutionOutputCollector.cs`, `ExecutionResultStore.cs`, `EditorBridge.cs`, `AutomationPresentationAdapter.cs`, `AutomationApprovalService.cs`, `McpLifecycleCoordinator.cs`. Kleine Dateien können beim Implementieren sinnvoll zusammengefasst werden. |
| `CryptoWorkBenchAvalonia/Services/InterpreterHostQueue.cs` | Non-waiting Admission zur Queue, gesamter Book-Scope; Gate-Regeln beibehalten. |
| `CryptoWorkBenchAvalonia/Services/PasswordDialogHost.cs` | Offene Callback-Tasks/Antwortreferenzen pro Runner/Request für serviceweite Quarantäne verfügbar machen; ein Handler pro Einheit, sicherer Drain erst nach Runner-Ende, Antworten lokal. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | Gemeinsame Pipeline aufrufen, bisherigen eigenen Runner-/Parse-/Catch-Pfad entfernen, Busy/Resultate aus Service beziehen. SaveScriptBook-Writer auf bestätigten Snapshot umstellen, Dateisemantik beibehalten. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | Editor-Lifetime/Revision-Bridge anbinden, PendingExecution/Anchor-Verhalten bewahren. |
| `CryptoWorkBenchAvalonia/ViewModels/VariableViewModel.cs` | Mutationen unter gemeinsamer Admission; UI-Modelle bleiben lokal. |
| `CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs` | Ausführungsstatus und sichere Diagnosepräsentation aus Service; Quellreihenfolge behalten. |
| `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` und passende View | SaveIntent, Revisionsprüfung vor Delete/Open und Editor-Commit-Sperre über DeleteAsync/Write; gemeinsame Busy-Prüfung. Bewusstes MCP-Ein-/Ausschalten und Endpoint-/Pairinganzeige. |
| `CryptoWorkBenchAvalonia/App.axaml.cs` | Singleton-Registrierung, explizite Instanzbrücke zu Web-DI und awaitbarer Lifecycle-Owner. |
| `CryptoWorkBenchAvalonia.Desktop/Program.cs` | Nur falls für koordinierte Desktop-Lifetime erforderlich; Builder/Designer ohne Nebenwirkungen halten. |
| Avalonia-/Desktop-Projektdateien und Solution(s) | Referenzen, ASP.NET-Frameworkbedarf und neue Projekte integrieren; alle vorhandenen Solution-Dateien konsistent halten. |
| `CryptoWorkBench.Mcp.Tests/` (neu) | Transport-/Schema-/Auth-/Host-/Origin-Tests mit Fake-Service und wenigen echten Kestrel-Loopbacktests. |
| `CryptoWorkBenchGuiTest/` | `WorkbenchAutomationTests.cs`, `EditorAutomationTests.cs`, `McpLifecycleTests.cs` und notwendige Anpassungen bestehender Ausführungstests. |
| `.github/workflows/dotnet.yml`, `.github/scripts/Validate-DesktopPublish.ps1` | Neuen Testgraphen abdecken; Framework-/Runtime-/Publishlayout prüfen. |

`CryptoScript` benötigt nach diesem Vorschlag keine produktiven Änderungen. Falls eine präzise Diagnoseposition, harte I/O-Policy oder nichtkooperativer Abbruch zusätzliche Kern-APIs verlangen, ist dies vor Umsetzung eine Erweiterung des Umfangs. Es ist kein Grund, Gates oder Sprachoperationen stillschweigend umzubauen.

## 4. Durchgängiges AES-Szenario mit belegter Syntax

Der primäre Nachweis verwendet nur bereits vorhandene Sprache. Syntaxbelege: [GenerateKeyContractTests](../../CryptoScriptUnitTest/GenerateKeyContractTests.cs), [SymmetricAlgoCbcNistTests](../../CryptoScriptUnitTest/SymmetricAlgoCbcNistTests.cs), [AES-CBC-Dokumentation](../../CryptoScript/InfoDocs/Info.Mech.AES-CBC.md). Diese Kombination wurde in MCP-00 statisch abgeglichen, noch nicht als neuer Integrationstest ausgeführt.

```cryptoscript
KEY mcpKey = GenerateKey(AES-CBC, 128)
PARAM mcpParams = Parameters(#MECH:AES-CBC, #IV:0x(000102030405060708090A0B0C0D0E0F), #PAD:PKCS-7)
VAR mcpPlain = "MCP integration test"
VAR mcpCipher = Encrypt(mcpParams, mcpKey, mcpPlain)
VAR mcpRoundtrip = Decrypt(mcpParams, mcpKey, mcpCipher)
Print(mcpRoundtrip)
```

Der feste IV ist eine reproduzierbare Testeingabe. Der Integrationstest verwendet einen frischen Workbench-Testzustand und Testdaten. Er prüft intern die Gleichheit der entschlüsselten Bytes mit dem Klartext, Schlüsselmetadaten AES/128 und den sichtbaren GUI-Bestand. Schlüsselmaterial wird weder geloggt noch als MCP-Variable ausgegeben.

Ablauf:

1. Benutzer aktiviert MCP und richtet ein lokales Token mit den nötigen Rechten ein. Client fragt Status und Editor ab und merkt sich instanceId/editorId/revision/stateVersion.
2. Client überträgt das Script per revisionsgebundenem Update; für Ersetzen vorhandenen Inhalts erscheint die lokale Bestätigung. Die zurückgegebene neue Revision wird verwendet.
3. Client ruft execute_editor mit neuer executionId auf. Der Benutzer prüft/erlaubt die konkrete Ausführung. Für Print-Rückgabe wird includeText und das entsprechende Recht explizit gewählt; andernfalls genügt die Metadatenantwort.
4. Antwort enthält state=completed, hasErrors=false, Diagnostikliste und Variablenmetadaten. GUI zeigt mcpKey, mcpParams und die drei VARs. Kein KEY-Wert ist in der MCP-Antwort.
5. Benutzer führt in der GUI eine neue Zeile `VAR guiRoundtrip = Decrypt(mcpParams, mcpKey, mcpCipher)` aus. Der Erfolg beweist die Nutzung der zuvor per MCP erzeugten Objekte.
6. GUI legt `VAR guiInput = "from GUI"` an. Ein anschliessender MCP-Lauf verwendet `VAR fromGui = Encrypt(mcpParams, mcpKey, guiInput)` und beweist die Gegenrichtung.
7. Client trennt und verbindet sich neu. Variablen bleiben vorhanden; eine wiederholte executionId führt nicht erneut aus. In einer kontrolliert verzögerten Variante wird bereits nach Auftragsannahme getrennt: der Lauf endet trotz Disconnect, anschliessend liefert get_execution das Resultat derselben ID. Ein eigenständiger Test fordert dagegen cancel_execution an und prüft den kooperativen Abbruch.

Für einen belegten zusätzlichen Dateieffekt kann nach gesonderter lokaler Bestätigung ein Snapshot geschrieben werden:

```cryptoscript
PATH mcpExport = "mcp-cipher-result.json"
Save(mcpExport, mcpCipher)
```

Voraussetzung: pro Test neues, kontrolliertes Arbeitsverzeichnis und noch nicht vorhandene Zieldatei. `Save` verwendet CreateNew; der Test erhält einen bestehenden Sentinel unverändert. Dieses Beispiel exportiert die VAR als Snapshot-JSON. Es verschlüsselt **keine beliebige Eingabedatei** und exportiert bewusst keinen Schlüssel. [Save-Vertrag](../../CryptoScript/InfoDocs/Info.Func.Save.md).

Für den ursprünglichen späteren Anwendungsfall „eine vorhandene Datei mit AES-CBC verschlüsseln“ fehlt ein belegter allgemeiner Datei-Import/-Exportpfad. Auswahl/Format, I/O-Grenze und notwendige Sprach- oder Host-Erweiterung sind ein eigener Folgeauftrag nach MCP-01; keine erfundene `ReadFile`-/`EncryptFile`-Syntax im MVP.

## 5. Akzeptanzkriterien und Testmatrix

| ID | Konkreter Nachweis |
| --- | --- |
| A01 – Off/On | Frischer Start öffnet keinen MCP-Port. Explizite Aktivierung liefert authentisierten Status. Deaktivierung schliesst Listener und widerruft Credentials, ohne Variablen zu löschen. |
| A02 – Client | Reale lokale Codex-Version entdeckt acht Tools, ruft Status auf, schreibt Editor, startet freigegebenen Lauf und liest strukturiertes Resultat. Versionen und Revision protokolliert. |
| A03 – Architektur | MCP-Assembly referenziert weder Avalonia/Prism/ViewModels noch CryptoScript; CryptoScript referenziert kein Automation-/MCP-Paket. Prism und Web-DI verwenden dieselbe Serviceinstanz. |
| A04 – Editor | Insert an UTF-16-Offset, exaktes Append, Replace mit Undo-Gruppe. Veraltete Revision, anderer Editor, invalides Offset und abgelehnte Bestätigung bewirken keinerlei Edit. Multibyte/Surrogate, CRLF, Undo/Redo, History und Save-Baseline berücksichtigen. |
| A05 – TOCTOU | Während Replace-/Run-Bestätigung manuell editiertes Dokument führt zu RevisionConflict. Zwischen Update und Execute geänderter Interpreterzustand führt zu StateConflict. Kein Script startet auf Basis einer alten Zustimmung. |
| A06 – Gemeinsamer Zustand | AES-Szenario und beide GUI↔MCP-Richtungen gelingen. HTTP-Reconnect und MCP-Neustart erhalten Variablen. Keine pro Client angelegte Dictionary-Kopie. |
| A07 – Semantik/Toolerfolg | `submission` und `scriptBook` bewahren Parse-/Stop-Regeln, out:-Skipping, Leerzeilen und Buchzeilen. `KEY missing=Check(0x(AA)) VAR continued=0x(01)` läuft weiter: state=completed, hasErrors=true, effects=possible, error=null und isError=false. Warnungen ebenfalls kein Toolfehler. Syntax-/geworfener semantischer Abbruch: Execute isError=true; erfolgreiche get_execution/get_diagnostics-Abfrage desselben Fehlers isError=false. |
| A08 – Teilfehler | Script mit früher Zustandsänderung und späterem Fehler behält die erste Änderung. MCP-Resultat und GUI zeigen denselben Teilzustand; keine Rollbackbehauptung. Keine Diagnose der vorigen Runnerausführung bei neuem Syntaxfehler. |
| A09 – Konkurrenz | GUI-Lauf gegen MCP-Lauf, zwei MCP-Clients, Book gegen GUI-Variablenedit/Clear/Save-Commit: genau eine Mutation zugelassen, andere sofort Busy. Kein Interleaving zwischen Buchzeilen oder Umgehen durch neue Runner; kein UI-Deadlock, Status-Lesen funktioniert. |
| A10 – Reentrancy | Passwort-/Ausgabe-Callback versucht Execute/Parse/Dictionaryzugriff: Gates weisen zurück. Kindtask mit geerbtem ExecutionContext erhält keine zusätzliche Zustandsberechtigung. |
| A11 – Snapshots | Nachträgliche Änderung eines SemanticError oder einer Variable verändert bereits publizierte DTOs nicht. laufender GetVariables liefert letzten fertigen Snapshot mit stale=true. Keine lebenden Collections, Bytes oder GUI-Modelle im Serializer. |
| A12 – Ausgaben | Mehrere Print/Info-Ereignisse behalten Reihenfolge. GUI-Zeilenausgabe erscheint genau einmal, Book-/Editorlauf verdoppeln den Text nicht. Truncation ist sichtbar und begrenzt nur Transportdaten. |
| A13 – Expliziter Abbruch/Deadline | Vor Annahme beobachteter Request-Abbruch: kein Auftrag/Effekt. Nach Annahme Cancel-Tool, GUI-Cancel, serviceeigene Deadline, UserCancel, Disable und Shutdown separat prüfen. Abbruch während Bestätigung/Passwortdialog, zwischen Statements und während synchroner Operation: passende cancellationReason, keine Folgeoperation nach wirksamem Abbruch; Gate bis Unwind/Drain gesperrt. Deadline wird durch Reconnect/Retry nicht verlängert. Bereits abgeschlossenes letztes Statement bleibt completed. |
| A14 – Passwort/Runner-Lebensdauer | Pro Einheit neuer Runner und genau ein Handler, keine Wiederverwendung; gleiche Variablen trotz verschiedener RunnerIds, getrennte Diagnose-IDs. Fehlende/mehrfache Handler und falsche RequestId ergeben sichere Hostfehler. Verzögerte Antwort oder Fault nach Cancel: neuer GUI-/MCP-Auftrag bleibt Busy bis Callback-Ende und defensiver Antwortentsorgung. Rennen mit interner Late-Response-Beobachtung beweist idempotentes Dispose ohne vorzeitige Pufferlöschung. Kein zweiter Dialog, keine Passwortbytes in Antworten/Logs/History. |
| A15 – Disconnect/Idempotenz | Nach atomarer Annahme Request-/SDK-Scope entsorgen bzw. HTTP-Verbindung trennen: Auftrag und Dialog laufen weiter, kein cancellationReason gesetzt. Reconnect/get_execution liefert Zwischen-/Endstand; Effekte genau einmal. Zwei simultane gleiche IDs erzeugen einen Auftrag; Wiederholung führt weder zu neuer Bestätigung noch neuer Deadline. Gleiche ID + andere Argumente abgewiesen. Annahmerennen mit verlorener Antwort und vorläufigem NotFound nur mit gleicher ID wiederholen. Aktive Resultate nicht verdrängen; Retention startet am Ende. Abgelaufene ID ergibt ResultExpired, alter instanceId nach Neustart wird abgewiesen. |
| A16 – Netzwerkgrenze | Fremder Host/Port, feindlicher Origin, Origin null, falsches/fehlendes/widerrufenes Token, Wildcard-Konfiguration und URL-Override greifen nicht auf Tools zu. IPv4/optional IPv6 positiv testen. GET/DELETE/Handshake verhalten sich revisionsgerecht. |
| A17 – Datenabfluss | Geheimnis-Testmarker in KEY, VAR, PATH, PARAM, Syntaxfehler, Exception und Print: Standardantwort/Logs enthalten sie nicht. Explizit freigegebener Print wird übertragen; die unveränderte lokale Print-Funktion bleibt funktionsfähig. |
| A18 – Lebenszyklus | Start ohne Editor, Detach, Portkollision, Disable während GUI- bzw. MCP-Lauf, Closing während Dialog/Dateioperation, wiederholtes Dispose: definierte Zustände, keine neue Arbeit während Stopping, kein falscher Idle-Status. HTTP-Ende stoppt keinen Auftrag; bewusstes Disable stoppt nur MCP-Aufträge, Desktop-Shutdown alle Scriptaufträge. Begonnener Save-Commit wird abgeschlossen, nicht zwischen Delete und Write abgebrochen. |
| A19 – Dateieffekt | Save-Szenario legt nur die Testdatei an; vorhandene Datei bleibt unverändert. Keine Behauptung beliebiger Dateiverschlüsselung oder Pfadsandbox. |
| A20 – Limits | Body-/Text-/Output-/Diagnose-/Resultatlimits werden geprüft; keine unbeschränkte Queue. Snapshot-Capture-Fehler markiert Zustand stale/faulted, statt alte Daten als aktuell zu liefern. |
| A21 – GUI-Save-Revision | Kontrollierte Pausen in Ordnerzugriff, Picker und Überschreibdialog; Benutzer-, Undo/Redo-, History- oder MCP-Änderung danach ergibt RevisionConflict vor Ziel-Delete/Open, bestehender Sentinel unverändert. Abgelehnte/abgebrochene Auswahl, neuer Editor, Shutdown oder Busy vor Commit ebenfalls ohne Zielschreiben. Vorschlagsordner darf wie bisher bereits angelegt sein. |
| A22 – Save-Commit/Dateisemantik | Nach Bestätigung vor Delete Sperre aktivieren; während verzögertem DeleteAsync sämtliche Editor-Mutationswege und Dokumenttausch abweisen. Writer verwendet bestätigten Snapshot, danach Sperre zuverlässig gelöst. ASCII-Fallback, Whitespace-Zeilenfilter, LF, direktes Append und bestätigtes Delete-vor-Write gegen bisheriges Verhalten vergleichen. Fehler nach Delete meldet Fehlschlag ohne Rollback-/Clean-Behauptung; kein Deadlock bei Shutdown. |
| A23 – SDK 2.3.0 | Exakten Paketgraph/TFMs bauen, beide Protokollrevisionen testen. Discovery-/HTTP-Fehler bleiben korrekt, fehlendes optionales ServerInfo verursacht keinen Fehler im Testclient. Stiller Handler überschreitet HTTP-Wartefrist ohne Execution-Abbruch. Manipulierte JSON-Bodies mit Geheimnis-Markern ergeben sichere Parse-Fehlerantworten und Logs. |

Bestehende Regressionen gezielt erhalten: `AsyncPasswordInfrastructureTests`, `AsyncPasswordGuiTests`, `CryptoScriptExecutionPresentationTests`, `SemanticDiagnosticPresentationTests`, `SemanticDiagnosticExecutionTests`, `SyntaxErrorTests`, `GenerateKeyContractTests`, `SymmetricAlgoCbcNistTests`, `AesCbcDefaultPaddingTests`, `SaveFunctionTests`, `LoadFunctionTests`, `SecureSaveTests`, `SecureLoadTests` und `DeleteTests`. Anschliessend die vorhandenen vollständigen Kern-/GUI-Suites ausführen. Tests für gemeinsam genutzte statische Interpreterzustände laufen nicht parallel im selben Prozess.

### Plattformen und Testebenen

Die bestehende [CI](../../.github/workflows/dotnet.yml) baut/testet/publiziert auf `windows-latest` / win-x64, `ubuntu-latest` / linux-x64, `macos-26` / osx-arm64 und `macos-26-intel` / osx-x64. `global.json` fixiert .NET SDK 10.0.401 mit latestPatch. `CryptoWorkBenchGuiTest` verwendet Avalonia.Headless; dies ist kein Nachweis echter nativer Dialog-/Shutdown-Ereignisse.

- Vertragstests und Fake-Service-Transporttests auf allen vier Matrixzeilen; die revidierten acceptedAt/deadlineAt/cancellationReason-/mutationBusy-Felder sowie die isError-Mappingtabelle explizit prüfen.
- Host-/Dispatcher-/Passwort-Tests im vorhandenen Headless-Harness, einschliesslich gemeinsamer Pipeline und Abschlussbarriere.
- Mindestens ein echter Kestrel-Loopbacktest pro Plattform für Header, Auth, Portbelegung, echten Disconnect nach Annahme, explizite Cancellation, Deadline und Listener-Freigabe; Testport dynamisch wählen, Produktionskonfiguration fest und sichtbar halten. Disposable Request-Scopes im Test aktiv beenden, damit versehentliche Scope-Abhängigkeiten auffallen.
- Vollständiger Core-/GUI-Regressionlauf und neuer MCP-Testprojektlauf pro Matrixzeile. CI-Restoregraph explizit erweitern, da er heute konkrete Projektwurzeln statt nur der Solution verwendet.
- Desktop-Publish für alle vier RIDs; neue ASP.NET-Shared-Framework-Abhängigkeit bei framework-dependent Veröffentlichung prüfen. Eine bisher ausreichende .NET-Runtime allein ist gegebenenfalls nicht mehr genug; Installation oder später bewusst self-contained Deployment dokumentieren.
- Nativer manueller Smoke-Test auf Windows, Linux und macOS für Owner-Dialog, Closing, tatsächlichen Editor, Tastatureingabe, Save-Picker/Überschreibbestätigung und Editor-Sperre während Dateizugriff; realer Codex-E2E mindestens auf der verwendeten Entwicklungsplattform. Weitere Clientplattformen erst nach ihrem eigenen Nachweis als getestet ausweisen.

MCP-00 einschliesslich R1 führte keine Builds, Paketinstallationen oder neuen Laufzeittests aus. Save-Aufrufpfad, PasswordResponse-Dispose-Vertrag und SDK-2.3.0-Quellen wurden lesend geprüft. Die Testliste ist ein verbindlicher Vorschlag für MCP-01, kein bereits bestandener Testbericht.

## 6. Risiken, offene Entscheidungen und Bereitschaft

| Punkt | Behandlung / Entscheidung vor Abnahme |
| --- | --- |
| Parallele master-Entwicklung | Vor MCP-01 Aufrufketten und Commitbasis neu prüfen; Integration nur im eigenen Branch. |
| Globale CryptoScript-Zustände | Ein Prozess = ein Workbench-Zustand. Multi-Workspace/Parallelrunner ausdrücklich nicht zusagen. |
| SDK-/Clientrevision | Stabiler Kandidat 2.3.0 anhand Release, Paketmetadaten und getaggter Quellen bestätigt; exakter Restore-Pin, Buildgraph und realer Codex-Roundtrip folgen erst nach Freigabe. Explizite SDK-Konfiguration bleibt Pflicht. |
| GUI-Nebenwege | Alle produktiven synchronen und asynchronen Einstiegspunkte, Variablenänderungen und Clear erfassen; sonst versagt der Exklusivitätsvertrag. |
| Fehlerdarstellung | Bewusste Aktualisierung der GUI nach Teilfehler fachlich freigeben; entsprechende bisherige Tests erwarten aktuell keinen Refresh. |
| Auftrags-/HTTP-Lebensdauer | Angenommene Aufträge sind schon im MVP HTTP-unabhängig, begrenzt durch Service-Deadline und explizite Lifecycle-/Cancel-Steuerung. Kein harter Zeit-/Ressourcen-Sandboxvertrag; Unwind/Quarantäne kann länger dauern. Persistente oder unbegrenzt lange Jobs bleiben späterer Umfang. |
| Runner-/Passwort-Ownership | Runner pro Einheit ist entschieden; serviceweite Quarantäne und defensives idempotentes Dispose nach Runner-/Callback-Ende müssen durch Race-Tests belegt werden. Keine neue Kern-API vorausgesetzt. |
| GUI-Speichern | Revision plus Commit-Sperre schützt den Inhalt; alle Mutationswege müssen teilnehmen. Bestehende nichttransaktionale Delete-/Append-Semantik und externe Dateirennen bleiben bestehen. Eine spätere Änderung bedarf eigener Freigabe. |
| Toolerfolg | completed plus fortsetzbare Error-Diagnose bleibt isError=false; keine automatische Wiederholung anhand hasErrors. Historische Fehler machen einen erfolgreichen Leseaufruf nicht fehlerhaft. |
| Freigaben und Dateirechte | Vorgeschlagen ist lokale Einzelauftragsbestätigung mit vollen Prozessrechten. Unbeaufsichtigte oder pfadbeschränkte Ausführung ist eine spätere Entscheidung. |
| Schlüssel-/Outputrechte | Metadaten als Default, Text nur nach zusätzlicher Freigabe. Editorlesen ist ebenfalls sensibel. Diese Produktentscheidung vor MCP-01 bestätigen. |
| Port und Pairing | 51837 ist ein konfigurierbarer Vorschlag, keine reservierte Portzuteilung. Flüchtige Tokens vermeiden zunächst Secret-Store-Infrastruktur. |
| Ausführungs-/Snapshotgrenzen | Resultatlimit und ID-Tombstone-Cap sind bewusst begrenzt; langfristiger Betrieb mit dauerhaftem Jobarchiv ist nicht der MVP. |
| Beliebige Dateiverschlüsselung | Kein belegter allgemeiner Sprachpfad; separate Spezifikation notwendig, keine Erweiterung unter dem Deckmantel von MCP-01. |

Selbstprüfung R1–R5: Request-Token erreicht nach Annahme keinen Execution-Token; Save-Revision wird vor Delete/Open geprüft und bis zum Schreiben geschützt; kurzlebige Runner umgehen weder gemeinsame Gates noch Passwort-Quarantäne; hasErrors ist kein isError-Prädikat; 2.3.0 ist ein quellengeprüfter Kandidat, kein behaupteter Build-/Clientnachweis. Zielprojekte, Sicherheitsregeln und CryptoScript-Semantik bleiben erhalten. Offen sind die Implementierungsnachweise der Race-/Native-UI-/Transporttests, keine zusätzliche Architekturentscheidung zugunsten eines Singleton-Runners oder eines HTTP-gekoppelten Auftrags. Der Vorschlag ist **bereit zur erneuten Architekturprüfung**. MCP-01 beginnt erst nach deren Freigabe.
