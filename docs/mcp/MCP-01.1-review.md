# MCP-01.1 – Independent Code Review

Stand: 22.10.2026. Entscheidung: **CHANGES REQUIRED**.

Geprüft: Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `5de3320dbe37b8fb29632194c12e2fcfeb9ed910` einschließlich der uncommitteten MCP-01.1-Änderungen. Grundlage sind MCP-00-architecture.md, MCP-00-implementation-plan.md und MCP-01.1-validation.md. Der Review verwendet zusätzliche Grenzfallproben und übernimmt den Abschlussbericht nicht als alleinigen Nachweis. Er ist keine Begutachtung durch eine zweite Person.

## Entscheidung und Findings

Critical: keine. High: 1. Medium: 1. Low: 1.

### H1 – Akzeptierter Endpoint-Alias umgeht die Parsefehler-Normalisierung

**Datei/Zeile:** `CryptoWorkBench.Mcp/SafeMcpParseErrors.cs:14`.

**Ursache:** Die Middleware schützt nur den exakten Pfad `/mcp`. ASP.NET-Routing akzeptiert auch `/mcp/`. Dort erreicht dieselbe Anfrage den MCP-SDK-Handler, ohne den Antwortfilter zu durchlaufen.

**Reproduktion:** POST an den isolierten Testlistener mit `Accept: application/json, text/event-stream`, `MCP-Protocol-Version: 2025-11-25` und Body `{"jsonrpc":"2.0","id":1,"method":"tools/list","params":Q}`. `/mcp` und `/MCP` liefern den festen Fehlertext. `/mcp/` und `/MCP/` liefern HTTP 400 mit dem SDK-Parserdetail, einschließlich des Eingabezeichens `Q`, JSON-Pfad und Byteposition.

**Auswirkung:** Die explizite MCP-00-R1-Schutzmaßnahme gegen eingabeabhängige Parsermeldungen ist für einen tatsächlich erreichbaren Pfad unwirksam. Nachgewiesen ist die Reflexion von Eingabeteilen und Parserdetails, kein vollständiger Schlüssel- oder Tokenabfluss. Der produktive Host ist noch nicht angebunden; die Lücke würde jedoch mit unveränderter Integration übernommen.

**Lösung:** Schutz und Endpoint auf dieselbe Routingentscheidung stützen, beispielsweise über Endpoint-Metadaten, oder alle nichtkanonischen Aliase vor dem SDK eindeutig ablehnen. Regressionstests für sämtliche akzeptierten Pfadvarianten und beide Zielrevisionen ergänzen. Die unmittelbare Weiterleitung erfolgreicher Streaming-Antworten erhalten.

### M1 – Cancellation-Testdouble akzeptiert falsche Ausführungs-IDs und terminale Aufträge

**Datei/Zeile:** `CryptoWorkBench.Mcp.Tests/FakeAutomationService.cs:54`, insbesondere Zeilen 57–61.

**Ursache:** CancelExecutionAsync verwendet eine einzige CancellationTokenSource ohne ID- oder Zustandsprüfung; GetExecutionAsync ignoriert ebenfalls die angefragte ID.

**Reproduktion:** Auftrag A annehmen und Abbruch für eine zufällige ID B anfordern: accepted=true und Auftrag A wird cancelled. Nach abgeschlossenem Auftrag liefert ein erneuter Abbruch ebenfalls accepted=true/cancellationRequested. GetExecutionAsync mit einer unbekannten ID liefert das Ergebnis von A.

**Auswirkung:** Der Testvertrag bildet den vereinbarten gezielten Abbruch nicht korrekt ab. MCP-00-R1 verlangt bei unbekannten Ausführungen NotFound und für terminale Aufträge accepted=false mit tatsächlichem Zustand. Die vorhandenen positiven Cancellation-Tests würden einen ungezielten Abbruch nicht erkennen. Dies ist ein Befund zur Testaussagekraft, keine nachgewiesene produktive Berechtigungslücke. Die Trennung zwischen Request-Token und dem angenommenen Auftrag funktioniert im getesteten Ein-Auftrag-Fall.

**Lösung:** Im Fake mindestens aktuelle ID und Zustand prüfen; unbekannte IDs ablehnen und terminale Abbrüche korrekt quittieren. Tests für falsche ID, fehlenden Auftrag und Abbruch nach Abschluss ergänzen. Dafür sind weder produktives Ergebnisregister noch MCP-01.2-Ausführungskern erforderlich.

### L1 – Strukturprüfung erfolgt nach Dereferenzierung ungültiger DTO-Inhalte

**Datei/Zeile:** `CryptoWorkBench.Automation.Abstractions/AutomationJson.cs:70` und `:77`; die rekursive Nullprüfung folgt erst ab Zeile 93.

**Ursache/Reproduktion:** Deserialize eines ansonsten gültigen ExecutionResult mit `outputs:[null]` verursacht eine NullReferenceException in der Summierung. Serialize eines hostseitig erzeugten EditorSnapshot mit Text=null verursacht dieselbe Exception beim Zugriff auf Text.Length.

**Auswirkung:** Ungültige Vertragsdaten werden abgewiesen, aber mit einem Implementierungsfehler statt der vorgesehenen sicheren JsonException. Das erschwert verlässliches Fehlerhandling; der aktuell allein registrierte Statuspfad ist dadurch nicht als angreifbar nachgewiesen.

**Lösung:** Strukturelle Nichtnull-/Collection-Prüfung vor feldübergreifenden Berechnungen ausführen. Für beide Fälle gezielt den erwarteten sicheren Exceptiontyp prüfen; eine beliebige Exception darf den Test nicht erfüllen.

## Prüfung der Schwerpunkte 1–8

| Bereich | Ergebnis und Grenze |
| --- | --- |
| DTOs, Schemas, Serialisierung | Die neun Serviceoperationen, DTOs, Enums und Reply-Zweige sind vorhanden. Geschlossene Schemas, Required/Nullable, UUID/UTC, Revisionen, Größenlimits und immutable Snapshots werden geprüft. L1 betrifft ungültige verschachtelte Daten. |
| Architektur und Abhängigkeiten | Abstractions verwendet nur die .NET-BCL. Mcp referenziert Abstractions, ASP.NET Core und den exakt gepinnten MCP-SDK 2.3.0. Keine unerlaubten Core-/GUI-/Avalonia-/ViewModel-Abhängigkeiten festgestellt. |
| SDK und Ergebnismapping | Stateless ist ausdrücklich gesetzt; Legacy SSE deaktiviert. Nur workbench_get_status registriert. Text und structuredContent enthalten denselben Envelope. Reply.ok, Laufzustand und hasErrors sind getrennt; historische Ergebnisabfragen werden nicht allein wegen des historischen Fehlzustands zu Toolfehlern. |
| HTTP-400-Normalisierung | Begrenzte Pufferung nur für 400-Antworten; erfolgreiche Antworten werden direkt weitergeleitet. Die bisherigen kanonischen Fehler-/Logtests reichen wegen H1 nicht aus. |
| Cancellation | Request-Abbruch vor Annahme verhindert Arbeit; nach Annahme bleibt der simulierte Auftrag bestehen. HTTP-Timeouttest belegt Weiterlauf des Fake-Auftrags, jedoch keinen vollständigen Reconnect-/Ergebnisabruf und keinen produktiven Runner. M1 begrenzt den gezielten Abbruchnachweis. |
| Testisolation | Listener und Fake liegen im Testprojekt; dynamischer Loopbackport und Dispose. Desktop startet/referenziert den Transport nicht. Kein produktiver anonymer Listener eingeführt. |
| Testaussagekraft | Gute positive und negative Grundabdeckung, unabhängiger JSON-Schema-Evaluator. H1/M1/L1 fehlen in der bestehenden Suite. Rohe HTTP-Fehler-/Disconnecttests verwenden überwiegend 2025-11-25; moderne Revision ist insbesondere über den SDK-Erfolgspfad abgedeckt. Nullable-sicheres Lesen von ServerInfo ist kein eigener Test eines Servers ohne ServerInfo. |
| Solutions und CI | Alle drei neuen Projekte in beiden Solutions integriert. CI ergänzt Restore/Build/Test in der vorhandenen Plattformmatrix. Beide lokalen Release-Builds erfolgreich. Kein tatsächlicher Linux-/macOS-CI-Lauf durch diesen Review nachgewiesen. |

Nichtblockierende Vertragspräzisierung: Das vereinbarte Revisionspattern `^[0-9]+$` akzeptiert im verwendeten Schema-Evaluator auch `1\n`, während AutomationJson ausschließlich Ziffern akzeptiert. Die Implementierung übernimmt hier das normative Pattern; deshalb kein eigenständiger Implementierungsbefund. Die gewünschte exakte Ziffernsemantik sollte bei einer ausdrücklichen Vertragspräzisierung in Schema und Validator identisch formuliert werden.

## Ausgeführte Verifikation

- Beide Solutions erneut Release gebaut: erfolgreich, inkrementell 0 Warnungen/0 Fehler.
- Reguläre MCP-Suite erneut ausgeführt: 64 bestanden, 0 fehlgeschlagen; Review-TRX unter `artifacts/test-results/MCP-01.1-review.trx`.
- Expliziter installierter Codex-Live-Test erneut ausgeführt: 1 bestanden.
- Zusätzliche isolierte Proben mit dem vorhandenen TestListener/FakeAutomationService reproduzieren H1, M1 und L1. Sie liegen außerhalb des geprüften Worktrees im Chat-Arbeitsordner unter `review-probes/`; keine Produktionsdatei wurde dafür geändert.
- `git diff --check`: erfolgreich.
- Die 7667 bestandenen Core-Tests mit 4 Skips und 239 GUI-Tests stammen aus der vorangegangenen Implementierungsvalidierung; in diesem Review nicht erneut ausgeführt. Sie sind kein zusätzlicher unabhängiger Reviewlauf.

## Claude-Code-Kompatibilität

### Offizielle Dokumentation und bekannte Clientprobleme

Claude Code dokumentiert Remote-MCP über HTTP, statische HTTP-Header einschließlich Bearer-Authorization und Discovery über tools/list. Die aktuelle Dokumentation unterscheidet SDK-Generationen; v2 ergänzt MCP 2026-07-28. Sie beschreibt außerdem Schemaaufbereitung für Tool-Eingaben sowie Wiederverbindung und begrenzte Discovery-Retries. Daraus folgt keine Garantie, dass jede ältere installierte Version dieselbe Revision oder vollständige Output-Schema-Validierung unterstützt. [Offizielle MCP-Dokumentation](https://code.claude.com/docs/en/mcp)

Die Umgebungsvariablen dokumentieren Protokollnegotiation, SDK-Generation, Tool-/HTTP-/Idle-Timeouts und ein isolierbares Konfigurationsverzeichnis. Ein Timeout des Clients ist kein Nachweis, dass ein bereits angenommener fachlicher Auftrag serverseitig abgebrochen wird. Die exakte Übertragung von Cancellation und ihr Verhalten mit diesem Server wurden nicht live nachgewiesen. [Offizielle Umgebungsvariablen](https://code.claude.com/docs/en/env-vars)

Die offiziellen Release Notes nennen structuredContent-Unterstützung seit 2.0.21 sowie Funktionalität der Revision 2025-11-25. Sie dokumentieren in 2.1.307 eine Korrektur doppelter HTTP-Toolaufrufe nach Disconnect: Wiederholungen werden auf als readOnly/idempotent gekennzeichnete Tools beschränkt. Der Status-Toolvertrag passt dazu; daraus wird keine Sicherheit späterer Execute-Tools abgeleitet. [Offizielles Changelog](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)

Ein gemeldeter Windows-Fehler in Claude Code 2.1.114 betrifft fehlendes Authorization bei tatsächlichen Tool-POSTs trotz erfolgreicher Verbindungsanzeige. Der geschlossene Issue ist kein Beleg für die Behebung in allen Versionen. Dies ist ein berichteter Clientfehler, kein hier reproduzierter Serverfehler. Ein späterer Auth-Test muss Headerankunft bei Discovery **und** tatsächlichem Toolaufruf prüfen, ohne den Headerwert zu protokollieren. [Issue 50464](https://github.com/anthropics/claude-code/issues/50464)

Ein anderer Header-/Verbindungsbericht wurde nach einem Fix für fehlerhafte Protokollprobe-Antworten in 2.1.232 geschlossen; der Maintainer berichtet erfolgreichen lokalen Header-Auth-Test mit 2.1.233. Dieser Befund darf nicht pauschal als Lösung des vorgenannten Windows-Problems ausgegeben werden. [Issue 76459](https://github.com/anthropics/claude-code/issues/76459)

### Lokale Prüfung und Testgrenzen

`Get-Command claude` fand keine ausführbare Installation. Auch die üblichen Pfade unter `.local/bin`, `AppData/Roaming/npm`, `AppData/Local/Programs/Claude` und `.bun/bin` waren nicht vorhanden. Damit konnte keine lokale Claude-Code-Version erfasst werden. Dies ist keine vollständige Suche über sämtliche Datenträger.

Claude Code wurde nicht installiert. Keine Anmeldung und kein kostenpflichtiger Modellaufruf wurden ausgelöst. Keine vorhandene Claude-Konfiguration wurde geändert und keine Tokens wurden erfasst. Verbindung, Discovery, tatsächlicher Statusaufruf, strukturierte Ergebnisverarbeitung, fehlerhafte Antworten, Disconnect und Authorization konnten mit Claude Code deshalb **nicht live getestet** werden. SDK-Tests werden nicht als Claude-Live-Nachweis ausgewiesen.

### Vergleichsmatrix

| Eigenschaft | Codex | Claude Code |
| --- | --- | --- |
| Clientversion | **Durch Live-Test nachgewiesen:** Desktop/codex-cli 0.163.0-alpha.11, Windows x64 | **Nicht getestet:** ausführbare Installation nicht gefunden; Version unbekannt |
| Streamable HTTP | **Durch Live-Test nachgewiesen:** isolierter Loopback-Endpunkt /mcp | **Nur dokumentiert:** Remote-HTTP-Transport |
| Protokollrevision | **Durch Live-Test nachgewiesen:** beobachtete Header nach Start 2025-06-18; initial leer/2024-11-05. **Durch SDK-Test nachgewiesen:** Server mit 2026-07-28 und 2025-11-25, unabhängig von Codex | **Nur dokumentiert:** v2 unterstützt 2026-07-28; Release Notes beschreiben 2025-11-25-Funktionalität. Tatsächliche Negotiation **nicht getestet** |
| Tool-Discovery | **Durch Live-Test nachgewiesen:** workbench_get_status sichtbar | **Nur dokumentiert:** tools/list. Dieser Server **nicht getestet** |
| Status-Toolaufruf | **Durch Live-Test nachgewiesen:** ok=true | **Nicht getestet** |
| JSON-/Schema-Verarbeitung | **Durch Live-Test nachgewiesen:** strukturiertes Statusergebnis. **Durch SDK-Test nachgewiesen:** Schema-/Vertragsprüfungen; kein Beleg vollständiger clientseitiger Schemaerzwingung | **Nur dokumentiert:** structuredContent und Input-Schemaverarbeitung. Tatsächlicher Status-Envelope und Output-Schema-Prüfung **nicht getestet** |
| Authorization-Header | **Nicht getestet:** Live-Test verwendet anonymen Testlistener | **Nur dokumentiert:** konfigurierbare Bearer-Header; Übertragung in diesem Setup **nicht getestet**, versionsabhängige Fehlerberichte beachten |
| Cancellation / Reconnect | Client-Liveverhalten **nicht getestet**. **Durch SDK-Test nachgewiesen:** serverseitige Request-/Auftragstrennung im Fake, ergänzt durch rohe HTTP-Tests; Einschränkung M1. Reconnect **nicht getestet** | **Nur dokumentiert:** Timeout-/Reconnectmechanismen. Cancellation-Wireverhalten und Zusammenspiel mit diesem Server **nicht getestet** |

Bewertung: Keine grundlegende Claude-Protokollinkompatibilität nachgewiesen. Der vorhandene standardbasierte Streamable-HTTP-Endpunkt und das Status-Tool enthalten keine Codex-spezifische CryptoScript-Ausführung. Dokumentation und SDK-Tests sprechen für Clientunabhängigkeit, ersetzen aber den fehlenden Claude-Live-Test nicht. Dessen Ausfall ist **kein Commitblocker**. H1 liegt im serverseitigen Antwortschutz und betrifft beide Clients; die genannten Authorization-/Retry-Berichte betreffen bestimmte Claude-Versionen. Es wurden keine clientspezifischen Workarounds implementiert. Produktives Pairing, Berechtigungen und Authentisierung bleiben MCP-01.5/MCP-01.6.

## Prüfung des Abschlussberichts

MCP-01.1-validation.md grenzt produktiven Service, Interpreterpipeline, Hostmigration, Authentisierung, weitere Tools und Plattformnachweise im Wesentlichen korrekt ab. Die Trennung zwischen live beobachteter Codex-Revision und den neueren SDK-Testrevisionen ist korrekt. Spätere MCP-01-Schritte werden nicht als abgeschlossen ausgegeben.

Korrekturbedarf besteht bei der uneingeschränkten Aussage zum implementierten Parsefehlerschutz in Abschnitt 6, insbesondere Zeile 102: H1 widerlegt sie für akzeptierte Pfadvarianten. Der Cancellation-Vertragsnachweis in Abschnitt 4 ist auf die tatsächlich geprüften IDs/Zustände einzuschränken oder nach M1 um negative Tests zu ergänzen. Nullable-sicherer Zugriff auf ServerInfo ist nur Codeeigenschaft, kein nachgewiesener Missing-ServerInfo-Test. Erfolgreiche Suitezahlen allein decken die hier gefundenen Grenzfälle nicht ab.

## Abschluss

**CHANGES REQUIRED** wegen H1 und des unzureichenden Abbruch-Testvertrags M1. L1 ebenfalls gezielt korrigieren und nachprüfen. Anschließend Abschlussbericht auf die tatsächliche Evidenz aktualisieren. Fehlender Claude-Live-Test ist ausdrücklich kein eigenständiger Blocker.

Dieser Review ändert ausschließlich diesen Bericht im geprüften Worktree. Keine Refactorings, keine Änderung an `.codex/config.toml`, kein Commit, Push oder Merge und kein Beginn von MCP-01.2.


## Nachtrag: Korrekturstatus vom 22.10.2026

Die ursprüngliche Entscheidung **CHANGES REQUIRED**, die drei Findings und die obige Claude-Kompatibilitätsmatrix bleiben als Reviewhistorie unverändert. Nach gezielten Korrekturen lautet der Implementierungsstatus **READY FOR RE-REVIEW**, nicht bereits READY FOR COMMIT.

| Finding | Korrektur | Neue Evidenz |
| --- | --- | --- |
| H1 | Schutz anhand der Metadaten des von ASP.NET ausgewählten Endpoints; UseRouting vor der Middleware, Markierung direkt an MapMcp | Sechs akzeptierte Pfadvarianten × zwei Protokollrevisionen mit mehreren fehlerhaften Requests, Geheimnismarkerprüfung in Antwort/Logs und erfolgreichem Statusaufruf; sofortige JSON-/SSE-Durchleitung und begrenzte Fehlerantwort zusätzlich geprüft |
| M1 | Ein Auftrag pro Fake-Instanz, synchronisierte ID-/Zustandsprüfungen, NotFound bei unbekannten IDs, accepted=false mit tatsächlichem terminalem Zustand | Fehlender/fremder Auftrag, wiederholter/terminaler Abbruch, bereits abgebrochener Cancel-Request und Ablehnung einer zweiten Annahme; HTTP-Warteabbruch nach Annahme für beide Revisionen lässt den Auftrag bis zum Abschluss bestehen |
| L1 | Rekursive Null-/Collection-Prüfung vor Berechnungen | Explizite JsonException-Assertions für outputs:[null], diagnostics:[null], EditorSnapshot.Text=null, fehlende Variables, null-Variableneinträge und default Outputs |

Abschließend: 89 reguläre MCP-Tests bestanden; separater Codex-Live-Test 1 bestanden; Core 7667 bestanden/4 plattformbedingte Skips; GUI 239 bestanden. Beide Release-Solutions mit 0 Warnungen/0 Fehlern im finalen inkrementellen Build. Projektabhängigkeiten unverändert, git diff --check erfolgreich. Die neuen Testfälle und Grenzen sowie lokale TRX-/Buildnachweise sind in [MCP-01.1-validation.md](MCP-01.1-validation.md), Abschnitt 8, dokumentiert.

Dieser Nachtrag dokumentiert die Implementierungskorrektur und deren Tests, keine zweite unabhängige Reviewfreigabe. Keine neuen Claude-Nachweise, keine produktive Authentisierung und keine vorgezogenen MCP-01.2-Arbeiten. Kein Commit, Push oder Merge.


## Final Re-Review – 22.10.2026

Erneute Prüfung der tatsächlichen Implementierung auf `feature/mcp-server` anhand der MCP-00-R1-Architektur, des Implementierungsplans und der beiden MCP-01.1-Berichte. Dieser Durchgang hat ausschließlich diesen Abschnitt ergänzt; keine Quellcode-, Test- oder Konfigurationsänderung. Er ist eine erneute sachliche Prüfung mit Testausführung, keine Begutachtung durch eine andere Person.

### H1 – VERIFIED FIXED

**Quellcodereferenzen:** `CryptoWorkBench.Mcp.Tests/TestListener.cs:34`–36; `CryptoWorkBench.Mcp/SafeMcpParseErrors.cs:16`–39 und :57–69. Tests: `CryptoWorkBench.Mcp.Tests/ReviewRegressionTests.cs:18`, :58 und :81.

UseRouting ist ausdrücklich vor UseSafeMcpParseErrors angeordnet. Die Schutzmetadaten werden direkt am von MapMcp gelieferten Endpoint-Builder registriert. Die Schutzmiddleware liest den bereits ausgewählten Endpoint über GetEndpoint; sie trifft keine zweite Pfadentscheidung. MapMcp registriert hier den Endpoint, führt ihn nicht bereits an dieser Stelle aus. Damit können Groß-/Kleinschreibung, abschließender Slash oder andere vom Routing auf denselben Endpoint aufgelöste Schreibweisen den Filter nicht durch einen abweichenden Stringvergleich umgehen.

Erneut bestanden: zwölf Kombinationen aus sechs Pfadvarianten und den Revisionen 2025-11-25/2026-07-28, jeweils mit drei fehlerhaften Eingaben, festem Antworttext und Geheimnismarkerprüfung in Antwort und Logs sowie anschließender SDK-Discovery und Statusaufruf. Die Pfadproben verwenden HttpClient/System.Uri; sie sind keine erschöpfende Prüfung sämtlicher roher HTTP-Request-Target-Kodierungen. Die darüber hinausgehende Absicherung folgt aus der geprüften gemeinsamen Endpointentscheidung und nicht aus der Behauptung einer vollständigen Aliasliste.

Die Capture-Methode speichert höchstens 16384 Fehlerbytes; Überlauf führt zum festen kleinen Fehler-Envelope. Erfolgreiche Writes und Flushes gehen unmittelbar an den ursprünglichen Stream. Zwei Tests prüfen JSON-/SSE-Chunk-Sichtbarkeit schon vor Handlerabschluss sowie unveränderte Bytes; der Überlauftest prüft die feste kleine Antwort. Alle drei erneut bestanden. Kein eigener MCP-Requestparser hinzugefügt.

**Verbleibendes Risiko:** Bei späterer Hostintegration müssen UseRouting, Schutzmiddleware und Endpoint-Metadaten zusammen übernommen werden. Unmarkierte Endpoints werden bewusst nicht geschützt. Der Nachweis gilt für den gegenwärtigen SDK-2.3.0-Testhost; ein produktiver Host und dessen weitere Middleware sind nicht Bestandteil von MCP-01.1. Kein verbleibender Blocker aus H1.

### M1 – VERIFIED FIXED

**Quellcodereferenzen:** `CryptoWorkBench.Mcp.Tests/FakeAutomationService.cs:39`–92. Tests: `CryptoWorkBench.Mcp.Tests/ReviewRegressionTests.cs:105`, :131, :158 und :172; `CryptoWorkBench.Mcp.Tests/TransportTests.cs:91`.

Annahme, Abschluss, GetExecutionAsync und CancelExecutionAsync verwenden denselben executionGate-Lock. Vor Cancel und Get wird die angefragte ID mit dem gespeicherten Auftrag verglichen; fehlende/fremde IDs liefern NotFound, ohne die CancellationSource zu verändern. Der Terminalzustand wird vor dem Setzen einer Abbruchanforderung geprüft. Ein bereits abgeschlossener Auftrag liefert accepted=false und seinen gespeicherten Zustand. Bei einem Rennen zwischen Abschluss und Abbruch entscheidet derselbe Lock; ein veröffentlichter Terminalzustand wird nicht wieder cancellationRequested.

Der angenommene Auftrag besitzt weiterhin die service-eigene CancellationSource. Request-Cancellation wird vor Annahme geprüft und nach Annahme ausschließlich für WaitAsync verwendet. Der Fake behält genau einen Auftrag pro Instanz; weitere Annahmen überschreiben weder ID noch CancellationSource. Es wurde kein produktives Ergebnisregister eingeführt.

Erneut bestanden: sechs neue Fälle zu unbekannten/fremden IDs, wiederholtem und terminalem Abbruch, abgebrochener Cancel-Anfrage und zweiter Annahme; außerdem beide bisherigen direkten Request-/Execution-Cancellationtests und beide HTTP-Warteabbruchtests. Die HTTP-Tests zeigen nach Annahme Running ohne CancellationReason und anschließend kontrollierten Abschluss mit Completed, auch über GetExecutionAsync mit korrekter ID.

**Verbleibendes Risiko:** Der bewusste Ein-Auftrag-Fake ist kein Beleg für produktive Retention, Idempotenz, Berechtigungen oder eine echte Interpreterpipeline. Wiederholter Abbruch darf je nach bereits erfolgtem Abschluss entweder die aktive Anforderung oder accepted=false/cancelled melden; der Test berücksichtigt dieses zulässige Rennen. Keine umfassende Race-Stressprüfung und kein Netzwerk-Reconnect-Ergebnisabruf behauptet. Kein verbleibender Blocker aus M1.

### L1 – VERIFIED FIXED

**Quellcodereferenzen:** `CryptoWorkBench.Automation.Abstractions/AutomationJson.cs:33`–44 und :62–124. Tests: `CryptoWorkBench.Mcp.Tests/ReviewRegressionTests.cs:188` und :196; `CryptoWorkBench.Mcp.Tests/ContractTests.cs:49`.

Serialize validiert vor der Serialisierung; Deserialize validiert nach dem typisierten Einlesen. Nichtnull-Prüfung, rekursive Prüfung verschachtelter DTOs und Collection-Elemente erfolgen vor dem abschließenden Switch mit Text.Length und Output-Summierung. Die IsDefault-Prüfung eines ImmutableArray erfolgt vor seiner Enumeration. Deshalb erreichen die dokumentierten ungültigen Inhalte diese Dereferenzierungen nicht mehr. Optionale Nullwerte behalten ihre bisherige Behandlung; Schemaerzeugung und Serializeroptionen wurden durch die Korrektur nicht verändert.

Erneut bestanden: drei gezielte DTO-Testfälle, darunter Deserialisierung von outputs:[null] und diagnostics:[null] sowie Serialisierung mit null-Text, null-Elementen, fehlenden Variables und default Outputs. Die Assertions verlangen ausdrücklich JsonException. Zusätzlich bestehen alle 16 RoundtripAndSchema-Fälle für gültige DTOs und Reply-Envelopes gegen den unabhängigen Schema-Evaluator.

**Verbleibendes Risiko:** Dies ist Validierung der vereinbarten DTO-Typen, keine Zusicherung für beliebige fremde Objektgraphen. Es wurde kein verbleibender NullReferenceException-Pfad für die geprüften ungültigen Vertragsstrukturen gefunden. Kein verbleibender Blocker aus L1.

### Regression, Scope und Dokumentation

Der gezielte Release-Testlauf ergibt **44 bestanden, 0 fehlgeschlagen, 0 übersprungen**: 24 neue Finding-Regressionsfälle, zwei HTTP-Abbruchfälle, zwei bestehende direkte Cancellationfälle und 16 DTO-/Schema-Roundtrips. Nachweis: `artifacts/test-results/MCP-01.1-final-rereview.trx`. Keine zusätzlichen vollständigen Suiten oder Live-Clientläufe waren mangels Quellcodeänderung oder neuem Befund erforderlich.

Die vorhandenen TRX-Dateien der Korrekturrunde bestätigen separat 89 reguläre MCP-Tests, einen Codex-Live-Test, 7667 bestandene Core-Tests und 239 bestandene GUI-Tests. Vier plattformbedingte Core-Skips und der nicht regulär ausgeführte explizite Hardwarebenchmark bleiben wie dokumentiert. Diese Werte sind frühere Korrekturvalidierung, nicht neue vollständige Re-Review-Läufe.

Projektdateien, Referenzen und Integrationsdiff geprüft: SDK-Pin weiterhin exakt [2.3.0], Abstractions ohne zusätzliche Framework-/Paketabhängigkeit, Mcp ohne Core-/GUI-Abhängigkeit. Beide Solutions enthalten die drei Projekte samt Debug-/Release-Konfigurationen. CI restauriert, baut und testet den MCP-Testgraphen in der vorhandenen Matrix. Keine produktive Desktop-Referenz/Serveraktivierung, keine Änderung an CryptoScript oder dessen Semantik und kein MCP-01.2-Ausführungskern festgestellt. git diff --check erfolgreich. Die Testaussagen des aktualisierten Validierungsberichts stimmen mit den geprüften Pfaden und Grenzen überein.

Die ursprüngliche Reviewhistorie einschließlich Claude-Code-Kompatibilitätsmatrix bleibt unverändert. Fehlender Claude-Live-Test ist kein Blocker; neue Claude-Kompatibilitätsnachweise werden nicht behauptet. Keine neuen blockierenden Findings (Critical/High/Medium/Low) im geprüften Korrekturumfang.

**Abschlussentscheidung: READY FOR COMMIT.**

Kein Commit, Push oder Merge durchgeführt. `.codex/config.toml` weder gelesen noch verändert. MCP-01.2 nicht begonnen.
