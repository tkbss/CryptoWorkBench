# C01-F1 – macOS ARM64 HTTP body rejection test

Datum: 2026-11-04 (Europe/Zurich). Status: **CHANGES REQUIRED**.

Der eng begrenzte Testentwurf besteht die gezielten Transportläufe, aber nicht die vollständigen MCP-Regressionen: Der normale übergrosse Upload scheitert beim **Lesen** mit `ConnectionAborted` (10053, Debug) beziehungsweise `ConnectionReset` (10054, Release). Diese zusätzlichen Fehlerbilder fallen nicht unter die freigegebene Alternative eines beim Body-Schreiben nachgewiesenen `ConnectionReset`. Der Filter wurde nicht erweitert; die fehlgeschlagenen vollständigen Läufe wurden nicht durch Retries ersetzt. Der Kandidat ist deshalb **nicht bereit für einen CI-Retest**.

## Basis und Git-Grenze

Branch `cleanup/remove-console-app`, HEAD `95019f16856419a2f6860214b06aa97baa72ef9b`, Tree `9df67daa6e83b80994ed818e97927c3009d8b3dd`. [PR #4](https://github.com/tkbss/CryptoWorkBench/pull/4) bleibt offen und ungemergt. Die Korrektur ist lokal und unstaged. Kein Commit, Push, Merge oder CI-Rerun wurde ausgeführt. Andere Worktrees bleiben unverändert.

## Originalbefund und Einordnung

Originaler [GitHub-Actions-Run 41870878162](https://github.com/tkbss/CryptoWorkBench/actions/runs/41870878162), Workflow-ID `73997315`, macOS-ARM64-Job [`126717268930`](https://github.com/tkbss/CryptoWorkBench/actions/runs/41870878162/job/126717268930). Ausgeführt wurde der synthetische PR-Testmerge `7433d58ebdc557fb7783103f1d960479fd9b44d6` mit exakt dem C01-Tree.

Die originalen Joblogs wurden erneut über die GitHub-API gelesen. Die originale `Mcp.trx` liegt im heruntergeladenen Artefakt `13344563891` (`test-results-osx-arm64`). SHA-256 des ZIP-Archivs: `2a631a85957ecb0e5800ae44de8b17b735595f84a325af9dad92d90368856210`; der lokale Hash stimmt mit dem von GitHub gemeldeten Digest überein.

Am `2026-11-04T16:53:27Z` scheiterte ausschliesslich `TransportTests.HttpBodyLimitAndListenerDisposal`. Die TRX zählt für MCP 170 Passed, 18 NotExecuted und 1 Failed. Der Fehler ist:

```text
HttpRequestException: Error while copying content to a stream.
  IOException: Unable to write data to the transport connection: Connection reset by peer.
    SocketException: ConnectionReset; NativeErrorCode 54
HttpContent.CopyToAsync -> HttpConnection.SendRequestContentAsync
TransportTests.cs, ursprüngliche Zeile 142: await http.SendAsync(request)
```

Damit ist der Fehler im Senden des Requestbodys verortet. Er ist kein nachgewiesener DNS-, Connect-, Timeout- oder Response-Parse-Fehler. Der ursprüngliche Test kam aufgrund der Exception nicht mehr zu seinen nachfolgenden Assertions; deren Erfüllung wird aus der alten TRX nicht behauptet. Die übrigen ARM64-Tests und die Publishprüfung bestanden.

Die Einordnung stützt sich auf folgende konkrete Belege:

1. [RFC 9110, Abschnitt 15.5.14](https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.14) erlaubt bei zu grossem Inhalt neben der 413-Antwort die Beendigung des Requests beziehungsweise das Schliessen der Verbindung.
2. [RFC 9112, Abschnitt 9.6](https://www.rfc-editor.org/rfc/rfc9112.html#section-9.6) beschreibt den TCP-Reset bei nachlaufenden Daten nach einem vollständigen Close und den möglichen Verlust der noch nicht gelesenen letzten HTTP-Antwort.
3. [Microsofts Kestrel-Dokumentation zum Request-Draining](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/request-draining?view=aspnetcore-10.0) erläutert, dass eine geschriebene Antwort vor einem Verbindungsabbruch nicht zwingend vom Client gelesen wird.
4. Der unveränderte lokale `SafeMcpParseErrors.ValidateInputAsync` setzt bei `ContentLength > MaxHttpBodyBytes` sofort 413 und kehrt vor SDK-/Tool-Dispatch zurück. Sowohl `TestListener` als auch `LocalMcpHost` konfigurieren zusätzlich Kestrels `MaxRequestBodySize` auf dieses Limit.
5. Die Kestrel-Quellen für `v10.0.12` erklären den relevanten Nachlauf: [`Http1ContentLengthMessageBody.OnReadStarting`](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Servers/Kestrel/Core/src/Internal/Http/Http1ContentLengthMessageBody.cs) deaktiviert Keep-alive und wirft bei überschrittener deklarierter Grösse. [`Http1MessageBody.OnConsumeAsync`](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Servers/Kestrel/Core/src/Internal/Http/Http1MessageBody.cs) behandelt diese Ablehnung auch beim Draining nach bereits geschriebener Antwort.

**Befund:** Die ausschliessliche Erwartung eines beim Client lesbaren HTTP-413-Status ist für diesen laufenden HTTP/1.1-Upload zu eng. Der im Original belegte Schreib-Reset ist mit der vorzeitigen Grössenablehnung vereinbar und darf unter den unten beschriebenen engen Bedingungen als Alternative gelten. Ein beliebiger Reset allein beweist keine Grössenablehnung. Eine Paketaufzeichnung des ursprünglichen Laufs liegt nicht vor; insbesondere wird kein tatsächlich vom Client empfangener 413-Status für den fehlgeschlagenen Request behauptet. Es gibt keinen nachgewiesenen Produktdefekt und keinen Anlass, Produktcode zu ändern.

## Exakter zusätzlicher Änderungsumfang

| Pfad | Änderung gegenüber `95019f1...` |
| --- | --- |
| `CryptoWorkBench.Mcp.Tests/TransportTests.cs` | 126 eingefügte, 6 entfernte Zeilen; vorhandener Bodylimit-/Disposal-Test verstärkt, ein neuer Kontrolltest und zwei private Testhilfsklassen |
| `docs/cleanup/C01-F1-macos-arm64-http-body-test.md` | Dieser neue Bericht |

Keine Projekt-, Paket-, Lock-, CI-, Testlistener-, gemeinsame Infrastruktur- oder Produktcodeänderung. Keine Tests entfernt, deaktiviert oder neu als Explicit/Ignore markiert. Der bisherige C01-Bericht bleibt unverändert.

Der vollständige zusätzliche Quelldiff liegt in [TransportTests.patch](../../artifacts/C01-F1/TransportTests.patch), SHA-256 `0ed2f80964adb992e45719b7951341c5b6cc6243bc747be8c3aaabba7f2e7e52`. Er ist reproduzierbar mit:

```powershell
git diff --binary 95019f16856419a2f6860214b06aa97baa72ef9b -- CryptoWorkBench.Mcp.Tests/TransportTests.cs
```

### Assertions und Begrenzung der Reset-Alternative

- Der Test verwendet einen gültigen `workbench_get_status`-Request, mit JSON-Whitespace auf exakt **1.048.576 UTF-8-Bytes** aufgefüllt. Der abzuweisende Request enthält genau **ein weiteres Byte**. Beide tatsächlichen Bytezahlen werden geprüft; das Nutzdatenformat allein kann die Ablehnung nicht erklären.
- Eine separate Vorprobe mit `Expect: 100-continue` verlangt weiterhin zwingend **HTTP 413**. Hier ist keine Exception-Alternative erlaubt. Der Toolzähler bleibt 0.
- Der normale Upload sendet denselben übergrossen Inhalt ohne `Expect: 100-continue` und explizit über HTTP/1.1. Eine erhaltene Antwort muss weiterhin genau **HTTP 413** sein.
- `ObservedBodyContent` beobachtet direkt `stream.WriteAsync` bei der HTTP-Serialisierung. Nur eine dort geworfene `IOException` mit unmittelbar innerer `SocketException.SocketErrorCode == ConnectionReset` wird gespeichert und unverändert weitergeworfen.
- Der äussere Catch akzeptiert ausschliesslich eine `HttpRequestException`, deren `InnerException` per **Referenzidentität** genau diese gespeicherte Schreibexception ist, und nur bei tatsächlich übergrossem Inhalt. Es gibt weder Nachrichtenvergleich noch pauschalen Catch beliebiger I/O-/HTTP-/Socketfehler, keine Stacktrace-Heuristik und keine Betriebssystemausnahme.
- Nach beiden Ablehnungen muss `StatusCalls == 0` sein. Danach muss derselbe gültige Aufruf an der exakten zulässigen Grenze auf einer **frischen Verbindung** mit HTTP 200 und einem deserialisierbaren `Reply<WorkbenchStatus>` der richtigen Host-Instanz erfolgreich sein. Der Toolzähler muss genau 1 betragen. Die neue Verbindung prüft den weiter funktionierenden Listener, ohne Wiederverwendbarkeit einer zulässigerweise geschlossenen Upload-Verbindung vorauszusetzen.
- Nach `DisposeAsync` muss ein neuer TCP-Verbindungsversuch innerhalb von fünf Sekunden mit **ConnectionRefused** scheitern. Timeout und andere Socketfehler gelten nicht als Erfolg. Der Toolzähler bleibt genau 1.
- `HttpBodyRejectionRequiresObservedConnectionResetDuringWrite` prüft die tatsächliche `HttpContent.CopyToAsync`-Exception-Kette mit einem kontrolliert fehlschlagenden Schreibstream: nur der beobachtete Schreib-Reset ist zulässig. Andere Exception-Instanzen, ein Reset ohne Body-Schreibbeobachtung, Inhalt innerhalb des Limits, allgemeine `IOException`, `ConnectionAborted`, `ConnectionRefused`, `TimedOut` und Cancellation werden nicht akzeptiert.

Der erste lokale Entwicklungsdurchlauf zeigte einen Reset beim Lesen der nachfolgenden Grenzwert-Anfrage auf einer wiederverwendeten Verbindung. Er wurde nicht akzeptiert oder in einen Erfolg umgedeutet. Die Funktionsprobe verwendet im endgültigen Kandidaten eine frische Verbindung. Das ursprüngliche Fehlerprotokoll und TRX bleiben als `targeted-initial-pool-reuse.log` bzw. `Transport-initial-pool-reuse.trx` erhalten.

Der anschliessende vollständige Debug-Lauf liefert einen davon getrennten Befund im normalen übergrossen Upload selbst (`TransportTests.cs:171`): `HttpRequestException` mit innerer `IOException` aus `HttpConnection.InitialFillAsync` und `SocketException.ConnectionAborted`, NativeErrorCode `10053`. Der Client liest dabei die Antwort; es ist kein durch `ObservedBodyContent` nachgewiesener Schreib-Reset. Die dokumentierte zulässige Originalalternative begründet **keine** pauschale Zulässigkeit dieses neuen Fehlerbilds. Die weitere Ursachenklärung dieses konkreten lokalen Abbruchs ist offen. Es wird weder als erwartete Ablehnung gewertet noch ein Produktdefekt daraus abgeleitet.

Der vollständige Release-Lauf scheitert an derselben Aufrufstelle mit `SocketException.ConnectionReset`, NativeErrorCode `10054`, ebenfalls im Antwort-Lesepfad `HttpConnection.InitialFillAsync`. Es fehlt die geforderte Referenzidentität zu einer beim Body-Schreiben beobachteten Exception. Auch dieser Fall bleibt fehlgeschlagen. Die nachfolgenden Positiv-/Disposal-Assertions sind nur durch die erfolgreichen gezielten Läufe belegt; aus den beiden abgebrochenen vollständigen Testfällen werden sie nicht als bestanden abgeleitet.

## Lokale Windows-Regression

Windows x64, .NET SDK `10.0.401` / Runtime `10.0.12`, unveränderte gepinnte Prism-Integration. Zunächst gezielter Lauf der vollständigen `TransportTests` in Debug und Release mit Locked Restore und Build. Anschliessend beide Solutions mit Locked Restore und Build und die vollständigen, ungefilterten Core-/GUI-/MCP-Suiten je Konfiguration über die bestehenden CI-Wrapper. Zusätzlich Windows-Publish und der vorhandene Artefaktvalidator. Kein GitHub-Actions-Lauf wurde gestartet oder wiederholt.

| Prüfung | Debug | Release |
| --- | --- | --- |
| Gezielte vollständige `TransportTests` | 14 Passed / 0 NotExecuted / 0 Failed | 14 Passed / 0 NotExecuted / 0 Failed |
| Locked Restore und Build `CryptoWorkBench.sln` | PASS | PASS |
| Locked Restore und Build `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.sln` | PASS | PASS |
| Vollständige Core-Suite | 11.559 Fälle: 11.532 Passed / 27 NotExecuted / 0 Failed | 11.559 Fälle: 11.532 Passed / 27 NotExecuted / 0 Failed |
| Vollständige GUI-Suite | 790 Fälle: 790 Passed / 0 NotExecuted / 0 Failed | 790 Fälle: 790 Passed / 0 NotExecuted / 0 Failed |
| Vollständige MCP-Suite | 190 Fälle: 176 Passed / 13 NotExecuted / **1 Failed** | 190 Fälle: 176 Passed / 13 NotExecuted / **1 Failed** |
| Locked RID Restore und Windows-Publish | nicht vorgesehen | PASS |
| Vorhandene Publish-Artefaktprüfung | nicht vorgesehen | PASS: Apphost, verwaltete Abhängigkeiten, 5 native Assets, 133 InfoDocs |

Beide vollständigen MCP-Läufe führten sämtliche 190 Fälle auf. Der einzige Fehler ist jeweils `HttpBodyLimitAndListenerDisposal`; der neue Kontrolltest besteht. Die ursprünglichen 27 Core-Ausnahmen und 13 MCP-Explicit-Fälle sind anhand ihrer Namen gegenüber den C01-TRX unverändert. Keine neue Ausnahme wurde eingeführt. Der ursprüngliche Entwicklungsfehler beim Connection-Pooling steht separat in den Belegen und wird nicht als finaler Kandidatenlauf gezählt.

Die neue Kontrollmethode erhöht die MCP-Fallzahl um genau einen Fall: 190 statt 189. Die 790 GUI-Fälle nach der C01-Bereinigung bleiben unverändert. Gezählt werden einzelne `UnitTestResult`-Einträge, einschliesslich bestehender `NotExecuted`-Fälle; Teilsuiten werden nicht zur Gesamtsuite addiert.

`artifacts/C01-F1/verify.ps1` vergleicht alle 14 abgenommenen C01-Pfade gegen ihre damaligen Git-Blob-IDs beziehungsweise Löschungen: **unverändert**. HEAD und HEAD-Tree entsprechen weiter exakt der oben genannten Arbeitsbasis. Der vollständige tracked Diff besteht ausschliesslich aus `TransportTests.cs`; das einzige neue nicht ignorierte Dokument ist dieser Bericht. Der Index ist leer, `git diff --check` besteht. Die vollständige TRX-Auswertung samt Fehlern und unveränderten Skip-Gründen steht in [test-matrix.json](../../artifacts/C01-F1/test-matrix.json).

Lokale Belege: [artifacts/C01-F1](../../artifacts/C01-F1/), einschliesslich gezielter und vollständiger TRX, Logs, Prüfskripte, Quelldiff und Matrix. Originale CI-Belege bleiben unter [artifacts/C01/ci](../../artifacts/C01/ci/) erhalten. Alle lokalen Testartefakte bleiben ignoriert und werden nicht gestaged.

## Grenze des Nachweises

Die lokale Windows-Prüfung belegt die Testlogik und ihre strikte Fehlerklassifikation. Sie ersetzt **keinen erneuten Lauf auf echter macOS-ARM64-Hardware**. Die Live-HTTP-Requests der gezielten Windows-Läufe lieferten 413; die Reset-Alternative wurde zusätzlich kontrolliert über den echten `HttpContent.CopyToAsync`-Schreibpfad geprüft. Ein erfolgreicher macOS-ARM64-Retest und anschliessende Integration dürfen erst nach erneuter Freigabe des korrigierten Kandidaten erfolgen.
