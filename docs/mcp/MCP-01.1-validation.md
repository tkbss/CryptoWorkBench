# MCP-01.1 – Abschlussbericht

Stand: 22.10.2026, nach Korrektur der Review-Findings H1/M1/L1. **READY FOR RE-REVIEW**. Kein Commit, Push oder Merge.

## 1. Ausgangszustand und Umfang

Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`.
Branch: `feature/mcp-server`. HEAD vor und nach Umsetzung: `5de3320dbe37b8fb29632194c12e2fcfeb9ed910`.
Der Worktree war vor Beginn sauber. Beide vollständigen MCP-00-R1-Dokumente, beide Solutions, Core-/GUI-Testprojekte und CI wurden geprüft.

`origin/master` und der mit `git ls-remote origin refs/heads/master` abgefragte Remote-Stand lagen bei `027815e9ea6f2dab11bd03485500b819907f2862`. Vor Umsetzung bestand der Unterschied ausschliesslich aus dem MCP-00-Dokumentationscommit; keine zwischenzeitlichen Codeänderungen erforderten eine Anpassung der Integrationspunkte. Kein Fetch mit Refänderung, Merge oder Rebase. Andere Worktrees wurden nicht verändert. Der anfänglich zugewiesene Chat-Ordner ist ein anderes, leeres Repository und enthält keine Implementierung.

MCP-01.1 registriert ausschliesslich `workbench_get_status`. Der einzige Listener liegt im Testprojekt, bindet dynamisch an `127.0.0.1` und wird gestoppt/entsorgt. Kein Desktop-Projekt referenziert oder startet den neuen Transport. Keine Änderung an CryptoScript, GUI-Ausführung, Dateifunktionen oder Sprachsemantik. Bestehende `.codex/config.toml` wurde weder gelesen noch verändert oder aufgenommen.

## 2. Neue und geänderte Dateien

Neu in `CryptoWorkBench.Automation.Abstractions/`:

- `CryptoWorkBench.Automation.Abstractions.csproj`: hostneutrales net10.0-Projekt ohne Paket-/Projekt-/ASP.NET-Referenzen.
- `IWorkbenchAutomationService.cs`: alle neun Methoden inklusive internem ExecuteTextAsync; dokumentierte Annahme-/Cancellation-Grenzen.
- `AutomationContracts.cs`: sämtliche DTOs, Zustandsenums, Caller-/Grantinformationen, Limits und UTF-16-Offsetprüfung.
- `AutomationErrors.cs`: spezifizierte Fehlercodes und sicherer Meldungskatalog.
- `AutomationJson.cs`: strikte Serialisierung, Required-/Nullable-, Enum-, UUID-/UTC-, Revisions-, Envelope- und Größenprüfung.
- `AutomationSchemas.cs`: geschlossene JSON-Schema-2020-12-Verträge, optionale Eingaben, Insert-Bedingung und Reply-Erfolgs-/Fehlerzweige.

Neu in `CryptoWorkBench.Mcp/`:

- `CryptoWorkBench.Mcp.csproj`, `packages.lock.json`: exakter SDK-Pin und aufgelöster Paketgraph.
- `StatusTransport.cs`: SDK-Toolregistrierung, explizit Stateless, Legacy SSE ausdrücklich deaktiviert; Caller kommt aus vertrauenswürdigem Hostkontext.
- `AutomationToolResults.cs`: identischer Text-/structuredContent-Envelope und getrenntes isError-Mapping.
- `SafeMcpParseErrors.cs`: begrenzte Normalisierung der HTTP-400-Parsefehlermeldungen; normale JSON-/SSE-Antworten werden unmittelbar durchgereicht.

Neu in `CryptoWorkBench.Mcp.Tests/`:

- `CryptoWorkBench.Mcp.Tests.csproj`.
- `ContractTests.cs`: DTO-/Schema-/Limit-/Snapshot-/Cancellation-/Fehlervertragsprüfungen.
- `TransportTests.cs`: SDK-Discovery, Status, HTTP-/JSON-RPC-Fehler, Warteabbruch, Limits, Dispose und SDK-Parsefehler-Reproduktion.
- `FakeAutomationService.cs`: ausschliesslich Testdouble mit simuliertem angenommenem Task; kein Interpreter oder GUI-Adapter.
- `TestListener.cs`, `CapturedLogs.cs`: kontrollierter Kestrel-Loopbacklistener und Prüfung der Logausgaben.
- `CodexCompatibilityTests.cs`: explizit anwählbarer Live-Test gegen ein lokal installiertes Codex-Binary.

Weitere Änderungen:

- `CryptoWorkBench.sln` und `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln`: drei neue Projekte; bestehende Any-CPU-Konfigurationen beibehalten.
- `.github/workflows/dotnet.yml`: Restore, Release-Build und Tests des neuen Projektgraphs auf allen bestehenden Matrixplattformen.
- `docs/mcp/MCP-01.1-validation.md`: dieser Bericht.

## 3. Tatsächliche Abhängigkeiten und Frameworkbedarf

| Projekt | Referenzen |
| --- | --- |
| Automation.Abstractions | ausschliesslich .NET-10-BCL; ImmutableArray/ImmutableHashSet und System.Text.Json |
| Mcp | Automation.Abstractions; FrameworkReference Microsoft.AspNetCore.App; ModelContextProtocol.AspNetCore exakt `[2.3.0]` |
| Mcp.Tests | Mcp; Microsoft.NET.Test.Sdk 18.9.0; NUnit 4.6.1; NUnit3TestAdapter 6.3.0; JsonSchema.Net 9.4.1 als unabhängiger Schema-Validator |

Transitive **Produktionspakete**: ModelContextProtocol 2.3.0, ModelContextProtocol.Core 2.3.0, Microsoft.Extensions.AI.Abstractions 10.11.0. Der vollständige Produktionsgraph steht in `CryptoWorkBench.Mcp/packages.lock.json`; Restore im Locked-Modus bestanden. Keine Referenz auf CryptoScript, Avalonia, Prism oder ViewModels; zusätzlich per Assembly-Test geprüft.

Die Testhost-Runtimekonfiguration verlangt Microsoft.NETCore.App 10.0 und Microsoft.AspNetCore.App 10.0. Lokal vorhanden: SDK 10.0.401, Runtime/ASP.NET Core 10.0.12, Windows x64. Bei späterer Desktop-Referenzierung wird ASP.NET Core zum zusätzlichen Shared-Frameworkbedarf des framework-dependent Desktop-Hosts. Die Desktop-Referenzen und dessen Publishmodell bleiben in diesem Teilschritt unverändert.

## 4. Validierung

| Prüfung | Ergebnis |
| --- | --- |
| Restore beider Solutions | erfolgreich |
| Release-Build beider Solutions | erfolgreich |
| Restore Mcp mit `--locked-mode` | erfolgreich |
| Reguläre Vertrag-/Transporttests nach Reviewkorrektur | 89 bestanden, 0 fehlgeschlagen; davon 25 zusätzliche Testfälle |
| Separater expliziter Codex-Live-Test | 1 bestanden |
| Vollständige CryptoScriptUnitTest-Suite | 7667 bestanden, 4 übersprungen, 0 fehlgeschlagen |
| Vollständige CryptoWorkBenchGuiTest-Suite | 239 bestanden, 0 fehlgeschlagen |
| `git diff --check` | erfolgreich |

Der erste vollständige Build zeigte sechs vorhandene Nullable-Warnungen in CryptoScript und acht in Avalonia. Neue Projekte sind warnungsfrei. Abschliessende inkrementelle Release-Builds meldeten 0 Warnungen/0 Fehler. Die übersprungenen Core-Fälle betreffen plattformspezifische Symlink-/macOS-ACL-Tests; der explizite Hardwarebenchmark ist kein regulärer Suitelauf.

TRX-Dateien liegen lokal im ignorierten Verzeichnis `artifacts/test-results/`: `CryptoWorkBench.Mcp.Tests.trx`, `CodexCompatibility.trx`, `CryptoScriptUnitTest.trx`, `CryptoWorkBenchGuiTest.trx`. CI für Linux/macOS ist erweitert, wurde hier aber nicht ausgeführt. Kein nativer GUI-/Publish-/Produktionshostnachweis wird aus den isolierten Tests abgeleitet.

Nachgewiesen werden insbesondere Required-Felder auch bei nullable Werten, exakte Enumtexte ohne Zahlen-/Case-Fallback, dezimale Revisionsstrings oberhalb der JavaScript-Präzision, UUID-D-Format, UTC-Zeitpunkte, unbekannte/doppelte Felder, verbotene explizite null-Optionen, UTF-16-/CRLF-/Surrogatgrenzen, Text-/Eintrags-/UTF-8-Output-/HTTP-Limits sowie konsistente Fehler-Envelopes. Snapshot-Listen sind immutable und enthalten ausschliesslich immutable DTOs. VariableInfo hat keine Wert-/Parameter-/Schlüsselmaterialfelder; valueIncluded ist im Schema auf false festgelegt und wird validiert.

`Reply.ok`, Laufzustand und `hasErrors` bleiben getrennt. completed mit semantischer Error-Diagnose erzeugt isError=false. Fehlgeschlagene Läufe erzeugen nur beim Execute-Tool isError=true; erfolgreiche historische Abfragen bleiben erfolgreich. Cancellation-Tests zeigen am Testdouble: vor Annahme keine Arbeit, nach Annahme nur Abbruch des Antwort-Waits, unveränderte Deadline, Weiterlauf und separater ausdrücklicher Execution-Abbruch. Die ergänzten Tests prüfen außerdem NotFound für fehlende/fremde IDs, unveränderten Weiterlauf des richtigen Auftrags bei fremdem Abbruch, accepted=false mit tatsächlichem Zustand nach Abschluss oder Cancellation sowie wiederholten Abbruch. Eine bereits abgebrochene Cancel-Anfrage setzt keinen Execution-Abbruch. Der kleine Fake nimmt genau einen Auftrag pro Instanz an; weitere Annahmen liefern Busy, ohne den bestehenden Auftrag zu ersetzen. Dies ist ein Vertragsnachweis, noch kein Nachweis einer produktiven Interpreterpipeline.

## 5. SDK- und Codex-Kompatibilität

SDK-Client 2.3.0: Discovery/tools-list und strukturierter Status über **2026-07-28** sowie **2025-11-25** bestanden; NegotiatedProtocolVersion wird jeweils explizit geprüft. ServerInfo wird nullable-sicher gelesen und nicht für Autorisierung verwendet. GET/DELETE liefern im Stateless-Modus 405; /sse ist nicht vorhanden; der ältere initialize-Handshake erstellt keine Session-ID. Für beide Revisionen überschreitet ein stiller Handler die verkürzte HTTP-Wartefrist von 300 ms; der dabei angenommene Fake-Auftrag läuft bis zum kontrollierten Abschluss weiter. Direkte GetExecutionAsync-Abfragen mit seiner ID zeigen danach Running und Completed. Das ist kein vollständiger Netzwerk-Reconnect-Test. Der rohe Request für 2026-07-28 enthält die vom SDK verlangten Methoden-/Namensheader und Protokoll-/Clientmetadaten.

Tatsächlicher lokaler Client: **Codex Desktop / codex-cli 0.163.0-alpha.11**, Windows 10.0.26200 x64. Das installierte Binary wurde als separater App-Server mit einem frisch angelegten temporären Konfigurationswurzelverzeichnis gestartet. Ausschliesslich dieser Kindprozess erhielt eine isolierte CODEX_HOME-Umgebung und den Endpoint als Kommandozeilenoverride. Der Test liest oder kopiert keine bestehenden Konfigurations- oder Anmeldedateien und schreibt keine MCP-Konfiguration; der Endpoint wird nur als Prozessargument übergeben. Eine kurzlebige Testsitzung und direkte `mcpServerStatus/list`-/`mcpServer/tool/call`-Aufrufe benötigten keinen Modellaufruf. Prozess, Listener und temporäres Clientverzeichnis wurden entsorgt.

Codex entdeckt das einzige Status-Tool und erhält ok=true mit strukturierter Antwort. Beobachtete HTTP-Protokollheader: initial leer bzw. 2024-11-05, anschliessend **2025-06-18**. Damit ist der lokale Codex-Statuspfad nachgewiesen, **nicht** die Nutzung der beiden neueren Revisionen durch diese Codex-Version. Deren Nachweis stammt separat vom SDK-Client. Produktive Authentisierung, Desktopaktivierung, weitere Tools und andere Clientplattformen bleiben ungetestet.

Reproduktion des optionalen Live-Tests (bestehende Konfiguration bleibt unberührt):

```powershell
$env:CRYPTOWORKBENCH_CODEX_PATH = (Get-Command codex).Source
dotnet test CryptoWorkBench.Mcp.Tests/CryptoWorkBench.Mcp.Tests.csproj -c Release --filter 'FullyQualifiedName~InstalledCodexDiscoversAndCallsStatus'
```

Die offizielle [Codex-MCP-Dokumentation](https://developers.openai.com/codex/mcp) beschreibt Streamable HTTP. Die Aussagen über die lokal installierte Version beruhen auf dem ausgeführten Test, nicht allein auf dieser Dokumentation.

## 6. Abweichungen und Präzisierungen gegenüber MCP-00-R1

Keine Änderung der Architektur oder SDK-Version. Zwei konkrete Kompatibilitätsbefunde:

1. **SDK-Parsefehlermeldungen:** Version 2.3.0 liefert bei ungültigem `params:Q` HTTP 400 mit einer Meldung, die das Eingabezeichen `'Q'` enthält. Reproduzierbar durch `Sdk230ParseDetailReproducerAndMitigation`. Der im R1 vorgesehene Schutz ist implementiert: `UseSafeMcpParseErrors` wird nach `UseRouting` installiert; `MapMcp("/mcp").WithMetadata(new McpParseErrorProtectionMetadata())` markiert den geschützten Endpoint. Die Middleware prüft ausschließlich diese Metadaten des tatsächlich ausgewählten Endpoints, keinen eigenen Pfadvergleich. SDK-Parsefehler werden durch einen festen JSON-RPC-Fehlertext ohne Quelltext/Parserpfade ersetzt. Damit erhalten sämtliche vom Routing auf diesen Endpoint aufgelösten Aliase denselben Schutz; getestet sind `/mcp`, `/MCP`, `/mcp/`, `/MCP/`, `/mCp` und `/%6dcp` für beide Zielrevisionen. Unmarkierte Endpoints werden nicht verändert. Gepuffert werden nur HTTP-400-Fehlerantworten bis 16 KiB, keine laufenden Status-/SSE-Antworten. Tests prüfen auch fehlende Geheimnismarker in Logs. Diese Middleware muss bei der späteren Hostintegration erhalten bleiben. Kein eigener Request-/Protokollparser und kein SDK-Versionswechsel.
2. **Lokale Codex-Revision:** Der reale Client nutzt 2025-06-18. Die Zielrevision 2026-07-28 und der verpflichtende zusätzliche Test 2025-11-25 funktionieren mit SDK 2.3.0, werden vom lokalen Client aber nicht ausgewählt. Eine aktualisierte Clientversion muss erneut separat geprüft werden.

Nicht im R1 wörtlich festgelegte C#-Details: CallerSource verwendet Gui/Mcp, Aktivierungsepoche ist eine Guid, Grants ein ordinaler ImmutableHashSet im Test. Sichere Meldungstexte und retryable-Katalogwerte sind in AutomationErrors zentralisiert; sie lösen keine automatischen Wiederholungen aus. Fehlendes outputPolicy/timeoutMs bleibt im Request optional; der spätere Service wendet metadataOnly/30000 ms an. Serialisierung und Eingangsvalidierung laufen über AutomationJson; zusätzliche zustandsabhängige Prüfungen bleiben Aufgabe des Hosts.

Die acht produktiven Tools werden gemäss Teilschrittgrenze noch nicht registriert. Testdouble und anonymer Loopback-Testlistener sind ausschliesslich Testinfrastruktur und dürfen nicht als Produktionshost verwendet werden.

## 7. Offen für MCP-01.2 und Folgeschritte

- Gemeinsamen Host-Ausführungskern mit fail-fast Admission und kurzlebigem Runner pro Parse-Einheit herauslösen; bestehende GUI-Semantik erhalten.
- Produktiven Automation-Service samt atomaren Snapshots, Rechteprojektion, monotonic Deadline, Ergebnisregister/Idempotenz und service-eigener Cancellation implementieren.
- Passwort-Quarantäne, Finalisierung und Race-Tests an echter Pipeline nachweisen.
- Editor-/Save-Revisionen und Commit-Sperre erst im freigegebenen Integrationsschritt anbinden.
- Anschliessend produktive Auth-/Host-/Originprüfung, Pairing, Default-off-Lifecycle und restliche Tools ergänzen; keine Desktopaktivierung durch diese Änderung.
- Desktop-Framework-/Publishbedarf, CI auf allen Plattformen und echte native GUI-/Codex-End-to-End-Szenarien nach Integration prüfen.

Hier endet MCP-01.1; keine vorgezogene Hostmigration.


## 8. Abschluss der Reviewkorrekturen H1/M1/L1

Entscheidung: **READY FOR RE-REVIEW**. Der ursprüngliche Review bleibt mit seinen Findings erhalten; die folgende Korrekturevidenz ersetzt keine erneute unabhängige Begutachtung.

### Korrekturen und geänderte Dateien in diesem Durchgang

- `CryptoWorkBench.Mcp/SafeMcpParseErrors.cs`: H1 – Endpoint-Metadaten statt separatem Pfadvergleich. Bestehende 16-KiB-Begrenzung und unmittelbare Weiterleitung erfolgreicher Antworten unverändert; kein eigener Request-Parser.
- `CryptoWorkBench.Mcp.Tests/TestListener.cs`: explizites UseRouting und Schutzmetadaten am SDK-Endpoint. Weiterhin nur isolierte Testinfrastruktur.
- `CryptoWorkBench.Mcp.Tests/FakeAutomationService.cs`: M1 – atomare ID-/Zustandsprüfung unter kurzem Lock, NotFound, terminale Ablehnung, getrennte Request-/Execution-Cancellation. Ein Testauftrag pro Fake-Instanz, kein produktives Register.
- `CryptoWorkBench.Automation.Abstractions/AutomationJson.cs`: L1 – rekursive Strukturprüfung vor feldübergreifenden Berechnungen; auch nichtinitialisierte ImmutableArray-Werte kontrolliert als JsonException abweisen. Bestehende Regeln für gültige DTOs und Schemas bleiben unverändert.
- `CryptoWorkBench.Mcp.Tests/ReviewRegressionTests.cs`: 24 neue Testfälle. Zwölf Kombinationen aus Pfadalias und Protokollrevision prüfen jeweils mehrere fehlerhafte Requests mit Geheimnismarker, Antwort/Logs und anschließend erfolgreiche SDK-Discovery samt Statusaufruf. Zwei Streamingtests prüfen die Sichtbarkeit von JSON-/SSE-Chunks vor Handlerabschluss; ein Überlauftest prüft die kleine feste Fehlerantwort. Sechs Cancellation-/Identitätstests und drei DTO-Testfälle decken die negativen Vertragsfälle ab.
- `CryptoWorkBench.Mcp.Tests/TransportTests.cs`: bestehender Disconnect-/HTTP-Timeouttest auf beide Revisionen erweitert (ein zusätzlicher Testfall), plus Zwischen-/Endstandsabfrage über die korrekte Ausführungs-ID. Vorzeitige SDK-Ablehnungen werden ausdrücklich als solche gemeldet.
- `docs/mcp/MCP-01.1-validation.md`: aktualisierte Nachweise und dieser Abschlussbericht.
- `docs/mcp/MCP-01.1-review.md`: ausschließlich ergänzter Korrekturstatus; ursprüngliche Findings und Claude-Vergleichsmatrix bleiben unverändert.

### Abschließende Ergebnisse dieses Durchgangs

| Prüfung | Ergebnis |
| --- | --- |
| H1/M1/L1-Regressionsfälle | Alle 24 neuen Fälle bestanden, enthalten in der vollständigen Suite; zusätzlich zweiter Disconnect-Protokollfall bestanden |
| Vollständige reguläre MCP-Suite | 89 bestanden, 0 fehlgeschlagen, 0 reguläre Skips |
| Separater expliziter Codex-Live-Test | 1 bestanden, 0 fehlgeschlagen; vorhandener Client, temporäre isolierte Konfiguration, kein Modellauftrag |
| Vollständige Core-Suite | 7667 bestanden, 4 plattformbedingte Skips, 0 fehlgeschlagen; expliziter Hardwarebenchmark nicht Bestandteil des regulären Laufs |
| Vollständige GUI-Suite | 239 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| CryptoWorkBench.sln, Release | Erfolgreich, 0 Warnungen / 0 Fehler im finalen inkrementellen Build |
| CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln, Release | Erfolgreich, 0 Warnungen / 0 Fehler im finalen inkrementellen Build |
| Projekt-/Paketabhängigkeiten | Unverändert; BCL-only Abstractions, Mcp nur Abstractions + ASP.NET Core + gepinntes SDK 2.3.0; kein Core-/GUI-Bezug |
| git diff --check | Erfolgreich; zusätzliche Prüfung der betroffenen noch unversionierten Dateien ohne Whitespacefehler |

TRX-Nachweise im ignorierten `artifacts/test-results/`: `MCP-01.1-fixes-mcp.trx`, `MCP-01.1-fixes-codex.trx`, `MCP-01.1-fixes-core.trx`, `MCP-01.1-fixes-gui.trx`. Buildlogs: `MCP-01.1-fixes-build-main.log` und `MCP-01.1-fixes-build-avalonia.log`. Der frühe gezielte Lauf mit 23 Fällen liegt separat unter `MCP-01.1-fixes-targeted.trx`; maßgeblich ist der abschließende vollständige Lauf mit 89 Fällen.

Bei der Testentwicklung schlug der neu hinzugefügte rohe 2026-07-28-Disconnect-Fall zunächst vor Annahme fehl: Er benötigte zusätzlich zu den HTTP-Headern die aktuellen Protokoll-/Clientmetadaten. Diese Testanfrage wurde korrigiert; der finale Lauf ist vollständig grün. Dafür wurde keine Serverlogik oder Protokollbehandlung erweitert.

Verbleibende Grenzen: keine produktive Ausführungspipeline, kein produktiver Auth-/Pairingnachweis, kein Claude-Live-Test, keine neuen Linux-/macOS-CI- oder Publishnachweise. JSON-/SSE-Durchleitung wird unmittelbar am Middleware-Stream geprüft, der echte SDK-Statuspfad zusätzlich über HTTP. Ein späterer Host muss Endpoint-Markierung und Middlewareposition nach UseRouting gemeinsam übernehmen. Die Claude-Kompatibilitätsmatrix bleibt mangels neuer Claude-Nachweise unverändert. Keine zusätzlichen Tools, keine Refactorings außerhalb der drei Findings, keine Änderung an `.codex/config.toml`, kein Commit/Push/Merge und kein Beginn von MCP-01.2. Hier endet der Korrekturauftrag.
