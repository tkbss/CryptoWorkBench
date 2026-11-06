# C01-F2 – Deterministic HTTP body limit test

Datum: 2026-11-04 (Europe/Zurich). Status: **READY FOR CI RETEST**.

## Basis und minimaler Diff

Branch `cleanup/remove-console-app`, unveränderter HEAD `95019f16856419a2f6860214b06aa97baa72ef9b`, Tree `9df67daa6e83b80994ed818e97927c3009d8b3dd`. F2 ersetzt den nicht abgenommenen F1-Testentwurf. [Der F1-Bericht](C01-F1-macos-arm64-http-body-test.md) und seine fehlgeschlagenen Rohbelege bleiben unverändert als historische Befunde erhalten; seine Bewertung beschreibt den damaligen Entwurf.

Zusätzlicher Quelldiff gegenüber dem abgenommenen C01-Commit: **45 eingefügte, 6 entfernte Zeilen**, ausschliesslich in `CryptoWorkBench.Mcp.Tests/TransportTests.cs`. Der vorhandene Test `HttpBodyLimitAndListenerDisposal` bleibt erhalten. F1s `ObservedBodyContent`, `FailingBodyStream`, Exception-Filter und zusätzlicher Klassifikationstest entfallen vollständig. Gegenüber C01 werden keine Testfälle entfernt oder hinzugefügt: Transport wieder 13 und MCP wieder 189 Fälle.

Ausserdem entsteht dieser F2-Bericht. Der bereits vor F2 unversionierte F1-Bericht bleibt bytegleich. Produktquellen, gemeinsame Testinfrastruktur, Projektdateien, Locks und CI-Dateien bleiben unverändert. Keine Testdeaktivierungen, zusätzlichen Ignore-/Explicit-Markierungen oder Skip-Bedingungen.

Vollständiger Quelldiff: [TransportTests.patch](../../artifacts/C01-F2/TransportTests.patch), SHA-256 `9b6a080de173787edc73d1df1e308b2c8d1898ab42e2fc8d5f52d3b16721ef00`.

```powershell
git diff --binary 95019f16856419a2f6860214b06aa97baa72ef9b -- CryptoWorkBench.Mcp.Tests/TransportTests.cs
```

## Geprüfte Invarianten

1. `AutomationContracts.MaxHttpBodyBytes` muss exakt **1.048.576** sein. Ein gültiger `workbench_get_status`-JSON-Request wird mit JSON-Whitespace auf genau diesen UTF-8-Byteumfang aufgefüllt. Der negative Request enthält ein weiteres Byte. Tatsächliche Bytezahlen und die von `StringContent` berechneten `Content-Length`-Header werden geprüft.
2. Der übergrosse Request verwendet explizit HTTP/1.1 und `Expect: 100-continue`. Die erwartete Antwort ist ausschliesslich **HTTP 413**. Danach muss `StatusCalls == 0` sein. Der Body ist bis auf die Grösse ein gültiger Toolaufruf; eine ungültige JSON-Nutzlast kann die Ablehnung nicht erklären.
3. `Expect100ContinueTimeout = Timeout.InfiniteTimeSpan` verhindert, dass nach einem kurzen Timer ungefragt doch ein laufender Upload beginnt. `HttpClient.Timeout = 10 Sekunden` begrenzt weiterhin den gesamten Request. Ein Timeout oder ein Socket-/HTTP-/I/O-Fehler lässt den Test fehlschlagen; es existiert kein Exception-Filter für den Upload. Der Wert wird von der verwendeten [.NET-10.0.12-Implementierung](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/SocketsHttpHandler.cs) unterstützt.
4. Nach der Ablehnung sendet ein **neuer HttpClient mit eigenem SocketsHttpHandler** den gültigen Request mit exakt 1.048.576 Bytes an denselben Listener. Verlangt werden HTTP 200, JSON-RPC-Korrelation `id == 1`, ein deserialisierbarer `Reply<WorkbenchStatus>`, die richtige Host-Instanz, die erwartete StateVersion und exakt **ein** Tooldispatch. Damit ist auch die inklusive obere Grenze und die weitere Funktionsfähigkeit des Listeners geprüft.
5. `DisposeAsync` läuft im `finally`. Anschliessend darf eine neue TCP-Verbindung nicht angenommen werden: innerhalb von fünf Sekunden wird ausdrücklich `SocketError.ConnectionRefused` erwartet. Timeout oder andere Socketfehler gelten nicht als Erfolg. Der Toolzähler muss weiterhin genau 1 sein.

Die in F1 beobachteten Resets beim Schreiben beziehungsweise Lesen eines bereits laufenden Uploads werden nicht mehr zum Testorakel gemacht. Sie bleiben in den F1-Belegen dokumentiert und werden nicht durch allgemeinere Exception-Filter zu Erfolgen umklassifiziert.

## Connection-Pooling und parallele Tests

Der negative Client/Handler wird vor der positiven Probe entsorgt. Der positive Client besitzt einen separaten Pool; er kann keine Verbindung des abgewiesenen Requests wiederverwenden. Beide Handler verwenden direktes Loopback ohne Proxy (`UseProxy = false`). Es gibt keine gemeinsam genutzten Clients, Handler, Cookies, variablen Portnummern oder Dispatchzähler zwischen Testfällen.

`TestListener.StartAsync` erstellt für jeden Aufruf eine eigene WebApplication, einen neuen Fake-Service mit eigenem Statuszähler und einen durch das Betriebssystem vergebenen Loopback-Port (`Listen(..., 0)`). Der hier benutzte gemeinsame Caller ist unveränderlich. Die Requests dieses Tests werden nacheinander abgewartet. Der Test benötigt keine Ergebnisse, Konfigurationsänderungen oder Reihenfolge anderer Tests. Es wurden weder globale Sperren noch neue `NonParallelizable`-Attribute eingeführt. Diese Isolationsbewertung beruht auf Quellprüfung und den gezielten sowie vollständigen Läufen; ein zusätzlicher Parallel-Stresstest wird nicht behauptet.

## Abdeckung und eigenständiger Streaming-Testbedarf

Dieser Test belegt die **frühe Ablehnung bei bekannter Content-Length** oberhalb der Grenze sowie die erfolgreiche Verarbeitung eines tatsächlich übertragenen Bodys exakt an der Grenze. Das entspricht dem bisherigen Testziel, mit einer zusätzlichen positiven Grenzprobe und ohne den TCP-Abbruch-Wettlauf. Die Bodygrösse wird nicht reduziert und HTTP 413 nicht optional gemacht.

Er belegt ausdrücklich **nicht** die Ablehnung eines übergrossen Bodys ohne bekannte Content-Length. Im unveränderten `SafeMcpParseErrors.ValidateInputAsync` sind dies verschiedene Pfade: zuerst die `ContentLength > limit`-Prüfung, danach die kumulative Prüfung `buffer.Length > limit - replay.Length` während des Lesens. Auch Kestrels eigene Begrenzung ist eine zusätzliche Schutzschicht; dieser Integrationstest isoliert nicht, welche Schicht ablehnt.

Die Suche in den MCP-Testquellen ergab keine separate gezielte Grenzprüfung für unbekannte Content-Length. Der vorhandene Chunked-Forwarding-Code in `B4F2SecurityTests` ist dafür kein Nachweis. **Eigenständiger Testbedarf:** Eine deterministische Streaming-Prüfung sollte `ContentLength = null` und mehrere Bodysegmente verwenden, die kumulativ exakt das Limit beziehungsweise Limit + 1 erreichen. Für den zu grossen Stream sind 413 und null nachfolgende Dispatches direkt am Middleware-/BodyReader-Vertrag zu prüfen; der erlaubte Grenzstream muss vollständig funktionieren. Ein ergänzender echter HTTP/1.1-Chunked-Test kann den Kestrel-Pfad prüfen, darf aber keine beliebigen Socketabbrüche als Grössennachweis akzeptieren. Diese separate Abdeckung wird in F2 dokumentiert, nicht implementiert und nicht als bestanden ausgegeben.

## Windows-Verifikation

Windows x64, SDK `10.0.401`, Runtime `10.0.12`, unveränderte gepinnte Prism-Integration. Zuerst die gezielten Transporttests mit Locked Restore und Build je Konfiguration, danach beide Solutions und die vollständigen Core-/GUI-/MCP-Suiten in Debug und Release, abschliessend Windows-Publish mit RID-Locked-Restore und vorhandenem Artefaktvalidator.

| Prüfung | Debug | Release |
| --- | --- | --- |
| Gezielte vollständige Transporttests | 13 Passed / 0 NotExecuted / 0 Failed | 13 Passed / 0 NotExecuted / 0 Failed |
| Locked Restore und Build `CryptoWorkBench.sln` | PASS | PASS |
| Locked Restore und Build `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | PASS | PASS |
| Vollständige Core-Suite | 11.559 Fälle: 11.532 Passed / 27 NotExecuted / 0 Failed | 11.559 Fälle: 11.532 Passed / 27 NotExecuted / 0 Failed |
| Vollständige GUI-Suite | 790 Fälle: 790 Passed / 0 NotExecuted / 0 Failed | 790 Fälle: 790 Passed / 0 NotExecuted / 0 Failed |
| Vollständige MCP-Suite | 189 Fälle: 176 Passed / 13 NotExecuted / 0 Failed | 189 Fälle: 176 Passed / 13 NotExecuted / 0 Failed |
| RID-Locked-Restore und Windows-Publish | nicht vorgesehen | PASS |
| Vorhandene Artefaktprüfung | nicht vorgesehen | PASS: Apphost, verwaltete Abhängigkeiten, 5 native Assets und 133 InfoDocs |

`HttpBodyLimitAndListenerDisposal` besteht sowohl in beiden gezielten Läufen als auch in beiden vollständigen MCP-Läufen. Die 27 Core-Ausnahmen entsprechen weiterhin 18 fehlenden lokalen Windows-Symlinkprivilegien, 8 Darwin-Fällen und einem expliziten Hardwarebenchmark. Die 13 MCP-Ausnahmen sind die bisherigen Explicit-Fälle. Ihre Namen wurden je Konfiguration mit den ursprünglichen C01-TRX verglichen und sind identisch.

TRX-Einzelergebnisse sind die Zählgrundlage; bestehende Explicit- und Plattformausnahmen werden nicht als bestanden gezählt. Teilsuiten werden nicht zur Gesamtsuite addiert. Jeder F2-Lauf wird genau einmal ausgeführt; keine Wiederholung eines fehlgeschlagenen F2-Tests zum Erreichen eines grünen Ergebnisses.

Die abschliessende Prüfung in `artifacts/C01-F2/verify.ps1` bestätigt alle 14 abgenommenen C01-Pfade unverändert anhand ihrer Git-Blob-IDs beziehungsweise Löschungen. HEAD und HEAD-Tree sind unverändert. Die einzige Änderung an einer versionierten Datei ist `TransportTests.cs`; hinzu kommt dieser Bericht. Der bereits vorhandene F1-Bericht ist per SHA-256 unverändert. Der Index ist leer und `git diff --check` besteht. [test-matrix.json](../../artifacts/C01-F2/test-matrix.json) enthält die tatsächlichen Ergebnisse und alle bestehenden Ausnahmegründe; [verification.log](../../artifacts/C01-F2/verification.log) enthält den Integritätsnachweis.

Rohbelege, Skripte, Quelldiff und Ergebnis-/Integritätsnachweise: [artifacts/C01-F2](../../artifacts/C01-F2/). Alle Artefakte bleiben lokal und ignoriert. Keine Commits, Pushes, Merges oder CI-Reruns. Die lokale Windows-Verifikation ist kein neuer macOS-ARM64-Nachweis; dieser bleibt einem gesondert freizugebenden CI-Retest vorbehalten.
