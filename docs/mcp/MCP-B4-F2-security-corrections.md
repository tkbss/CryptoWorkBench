# MCP-B4-F2 – Server Identity, Shutdown Recovery and Security Corrections

**Empfehlung: READY FOR R3** · 4. November 2026 · Windows x64

Die vier belegten R2-Befunde sind im F2-Kandidaten korrigiert und mit permanenten Tests sowie dem installierten Codex nachgeprüft. Der ursprüngliche R2-Ersatzserver erhält über den neuen Verbindungspfad auch bei zusätzlichem CA-Vertrauen keine HTTP-Daten. Die native AES-Ausführung aus dem Windows-Publish-Paket, GUI-Rückgabe und anschliessende Portübernahmeabwehr desselben Codex-Prozesses bestehen. Ein öffentlicher CA-Dateilock blockiert den zertifizierten normalen Shutdown nicht mehr.

Dies ist der Implementierungs- und Verifikationsbericht, keine unabhängige R3-Freigabe. Die plattformübergreifende MVP-Abnahme bleibt wegen fehlender nativer Linux-/macOS-Nachweise **BLOCKED**.

## 1. Gegenstand und Abgrenzung

Branch `feature/mcp-ap3`, unveränderter HEAD `6806a34ebd75c97eab7fc17a4d4a5309a6d9f009`. Grundlage sind [B4-R2](MCP-B4-R2-independent-rereview.md), [F1](MCP-B4-F1-security-and-lifecycle-corrections.md) und die vorhandenen B4-/R1-Evidenzen. F2 baut auf den uncommitteten B4-/F1-Änderungen auf; der gesamte Arbeitsbaumdiff gegen HEAD ist deshalb nicht ausschliesslich F2.

Neue Produktdatei ist `CryptoWorkBench.Mcp/PinnedMcpStdio.cs`. Geändert wurden die Pairinganzeige und der Desktop-Einstiegspunkt, `LocalMcpIdentity`, die Initialisierungs-/Disposal-Grenze von `LocalMcpHost` sowie die Methodenfehlerprüfung in `SafeMcpParseErrors`. Neue permanente Tests liegen in `B4F2SecurityTests.cs` und `B4F2LifecycleTests.cs`; bestehende installierte Codex-Tests verwenden jetzt den gepinnten Adapter. Die GUI-Testreferenz auf MCP und ihre Debug-/Release-Locks wurden für das Laden der echten Desktop-Transportimplementierung ergänzt. Die B4-/F1-Berichte tragen einen historischen Hinweis auf die neue [Pairinganleitung](MCP-pinned-client-setup.md).

Editorarchitektur, Coordinator, Lease-Uhr, Execution-Registry und AP0-D02 wurden durch F2 nicht umgebaut. Keine Commits, Pushes, Merges oder master-Updates; Index leer. Andere Worktrees wurden nicht bearbeitet. Prüfartefakte liegen unter `artifacts/b4-f2`; die ursprünglichen R2-Prüfquellen ausserhalb des Worktrees wurden nur gelesen und in dieses Verzeichnis kopiert.

## 2. Architekturentscheidung

Der freigegebene Pfad lautet:

```text
installierter Codex
  → MCP über STDIO
  → derselbe Desktop-Apphost mit --mcp-stdio (ohne GUI)
  → HTTPS /mcp mit exklusivem SHA-256-Leaf-Pin
  → lokale Workbench / vorhandene Tools / Service / Coordinator
```

Ein zusätzliches CA-Bundle ist keine exklusive Serverbindung: R2 hat die Aufnahme fremden Vertrauens durch den tatsächlichen Codex gezeigt. F2 kontrolliert deshalb die TLS-Verbindung im kleinen integrierten .NET-Adapter. Dieser besitzt keine Editorinstanz, Credential-Ausstellung oder Execution-Registry. Er liest den tatsächlichen Toolkatalog des gepaarten Hosts und reicht Toolaufrufe weiter. Das vorhandene MCP-SDK übernimmt STDIO-Framing, Discovery, Antworten und Abbruchnachrichten; es gibt keine zweite Implementierung der Workbench-Geschäftslogik.

`PinnedMcpStdio.CreateHttpClient` akzeptiert nur HTTPS, `127.0.0.1`, den Pfad `/mcp` und keine Query, Fragmente oder Userinfo. Der TLS-Callback prüft die Gültigkeitszeit und vergleicht den SHA-256-Hash des vollständigen Leaf-Zertifikats konstantzeitlich mit dem Pin aus dem authentischen Pairingdialog. Eine erfolgreiche normale CA-Prüfung kann diese Entscheidung nicht erweitern. `UseProxy=false`, deaktivierte Redirects/Cookies und HTTP/1.1 mit `RequestVersionExact` verhindern zusätzliche Umleitungswege. Systemtrust, `SSL_CERT_FILE` und `CODEX_CA_CERTIFICATE` sind keine Eingaben der Pinentscheidung.

Der zufällige Bearer wird erst nach erfolgreicher TLS-Prüfung als HTTP-Header übertragen. Im Adapter wird die geerbte Token-Umgebungsvariable nach dem Einlesen entfernt. Der Token bleibt für die aktive Verbindung im Prozessspeicher; eine vollständige Löschung aller verwalteten Stringkopien wird nicht behauptet. Keine Secrets in Prozessargumenten oder TOML. stdout bleibt der MCP-Protokollkanal für die autorisierten Antworten; stderr und JSON-RPC-Fehler enthalten feste Diagnosen ohne reflektierte Eingaben. Autorisierte Scriptdaten und Ausgaben werden erwartungsgemäss an den gepaarten Client übertragen, aber nicht zusätzlich protokolliert.

Jeder Toolaufruf verwendet genau einen rohen SDK-`SendRequestAsync`-Aufruf. Der höherwertige MRTR-/Elicitation-Fortsetzungshelfer wird nicht verwendet. Standalone-GET, Wiederverbindungen und gepoolte Verbindungen sind deaktiviert (`MaxReconnectionAttempts=0`, `PooledConnectionLifetime=0`, `Connection: close`). Ein unklares Ergebnis führt zum Fehler; der Adapter startet keinen neuen Auftrag. Die Anleitung verlangt auch vom aufrufenden Client, Mutationen nicht automatisch zu wiederholen. STDIO-EOF beendet den Adapter und entsorgt Host, MCP-Client, Transport und HttpClient.

## 3. Vollständige Korrekturmatrix

| R2-ID / Severity | Korrektur und produktive Position | Permanenter Gegenbeweis | Ergebnis |
|---|---|---|---|
| **B4-R2-01 / High** | `PinnedMcpStdio.cs:35` exklusive TLS-Bindung; `:66` SDK-Bridge ohne Replay; `Program.cs:15` integrierter Einstieg; `DesktopMcpSession.cs` neues Pairing | Installierter Codex gegen fremde Identität unter isoliertem Trust, `SSL_CERT_FILE`, Mehrfach-Bundle und Proxy; fehlender/falscher/abgelaufener Pin; neue Workbench-Identität; verlorene Antwort nach Dispatch; zusätzlich unveränderter R2-Ersatzprozess | Behoben für den freigegebenen STDIO-Pfad; direkte CA-basierte HTTPS-Konfiguration ist nicht gleichwertig geschützt |
| **B4-R2-02 / Medium** | `LocalMcpIdentity.cs:67` private Schlüsselentsorgung vor öffentlicher Bereinigung; `:76` drei begrenzte Löschversuche; `LocalMcpHost.cs:119` wartet auf diese Bereinigung ohne öffentliche IO-Fault-Task | Tatsächlicher Windows-Dateilock mit und ohne Freigabe während Retry; echte Desktop-Stoproutine plus echte Shell-Closing-Routine und gehaltenes ursprüngliches Coordinator-Ticket | Transport-/Stop-Task bleibt erfolgreich; Fenster wartet weiterhin auf zertifizierten Drain und schliesst erst danach |
| **B4-R2-03 / Low** | `SafeMcpParseErrors.cs:154` unbekannte Methoden vor SDK-Dispatch; `PinnedMcpStdio.cs:83` feste ausgehende JSON-RPC-Fehler | Canary-Methode sowie `resources/read`, `tasks/get`, `logging/setLevel`; numerische, negative und String-ID; null Service-Dispatch; zusätzlich fehlerhafter Toolname als Objekt über tatsächliches STDIO | Fester Text `Method not found.`, Code `-32601`, zulässige ursprüngliche ID erhalten; kein Canary-Echo |
| **B4-R2-04 / Low** | `LocalMcpIdentity.cs:45` Cleanup-Grenze unmittelbar nach Import, einschliesslich Tempverzeichnis und öffentlichem Export; `LocalMcpHost.cs:39` partieller Hostaufbau mit Cleanup | Tatsächliche Windows-CNG-Dateicontainer: TEMP/TMP auf eigene reguläre Datei; injizierter Exportfehler nach Import; ungültiger Port beim Hostaufbau; tatsächlich belegter Listenerport | Keine neu importierten privaten Container bleiben nach den geprüften behandelten Fehlern zurück; keine fremden Container gelöscht |

Die öffentliche Dateibereinigung versucht sofort sowie nach 100 und 200 ms erneut zu löschen. Sie fängt nur `IOException`/`UnauthorizedAccessException` ab, löscht nur die eigene Datei und nichtrekursiv das eigene leere Verzeichnis. Nach ausgeschöpften Versuchen bleibt ein öffentliches Residuum möglich; `PublicCleanupPending` und eine feste Trace-Warnung unterscheiden dies vom privaten Schlüsselabschluss. Es gibt keinen unbegrenzten Hintergrundreaper und keine Zusicherung späterer automatischer Löschung nach dem dritten Versuch. Private Schlüsselentsorgung, Listenerfehler und echter Drainfehler werden dadurch nicht zu Erfolg umklassifiziert.

## 4. Adversariale Ergebnisse

Installierter Client: **Codex Desktop/0.166.0-alpha.5.1**, Windows `10.0.26200`, x86_64. Die Prüfungen verwenden `app-server --stdio`, isolierte Profile und keine Modellaufrufe. Benutzerkonfiguration und OS-Truststore wurden nicht geändert.

| Gegenprobe | TCP | Serverseitig abgeschlossene TLS-Handshakes | Entschlüsseltes HTTP / Authorization / Body | Akzeptierte Fake-Antwort |
|---|---:|---:|---|---|
| Permanente Probe, isoliertes CA-Vertrauen | 3 | 3 | 0 / 0 / 0 | keine |
| Permanente Probe, fremde CA in `SSL_CERT_FILE` | 3 | 3 | 0 / 0 / 0 | keine |
| Permanente Probe, Bundle aus Workbench- und fremder CA | 3 | 3 | 0 / 0 / 0 | keine |
| Permanente Probe, HTTPS-/ALL-Proxy mit fremder CA | 3 am Ersatzserver | 3 | 0 / 0 / 0 | keine; Workbench-CONNECT zum Proxy: 0 |
| Ursprünglicher R2-Ersatzprozess, isoliert | 3 | 3 | 0 / 0 / 0 | keine |
| Ursprünglicher R2-Ersatzprozess, `SSL_CERT_FILE` | 3 | 3 | 0 / 0 / 0 | keine |
| Ursprünglicher R2-Ersatzprozess, Mehrfach-Bundle | 3 | 3 | 0 / 0 / 0 | keine |

**Serverseitiger TLS-Erfolg ist hier kein Beleg für clientseitige Akzeptanz.** Schannel kann am Ersatzserver den Handshake als abgeschlossen melden, bevor die Pinablehnung des Clients sichtbar wird. Die entscheidenden separat erhobenen Werte sind null entschlüsselte HTTP-Requests, null Bearer-/Bodyübertragung und keine akzeptierte Toolantwort. Frühe Testassertions verlangten irrtümlich null serverseitige TLS-Abschlüsse; sie wurden anhand dieser Beobachtung korrigiert. Der erste fehlschlagende Messlauf bleibt unter `codex-pinned-first*` erhalten.

Der Proxy ist als CONNECT-/TLS-Interception-Gegenstelle vorbereitet. Der Adapter kontaktiert ihn wegen `UseProxy=false` für die Workbench überhaupt nicht. Unabhängige Codex-Startup-Verbindungen wurden beobachtet; sie zählen nicht als Workbench-CONNECT und werden vom Testproxy nicht weitergeleitet. Dies belegt Proxyumgehung für den Workbench-Verbindungspfad, keine Durchführung einer erfolgreichen transparenten Tunnelphase durch diesen Adapter und keine allgemeine Prüfung fremder Unternehmens-PAC-Systeme.

Die wiederverwendete R2-Datei `Foreign.cs` ist bytegleich mit dem ursprünglichen separaten Angreiferprozess (SHA-256 `C111EF06F7649B14A5B28CD9AF8CEBD73CAE5F43D0AE3CCA4DF28B5BEFAE872F`). In der kopierten Clientanbindung wurde die Konfiguration auf STDIO/Pin umgestellt. Der Treiber liest vor dem Angriff einen tatsächlichen Headless-Editor über den echten Service, widerruft und drainiert den echten Coordinator und startet dann den fremden Prozess am bisherigen Port. Derselbe laufende Codex sendet drei Edit-Canaries. Der Ersatzprozess kennt den Originalbearer nicht; alle drei Varianten bestätigen `noOriginalBearer=True`, `noDecryptedHttp=True`, `noForgedResponse=True`. [Proben](../../artifacts/b4-f2/r2-counterprobe/Review.cs) · [SSL-Ergebnis](../../artifacts/b4-f2/r2-counterprobe/attack-ssl.log) · [Bundle](../../artifacts/b4-f2/r2-counterprobe/attack-bundle.log).

Weitere Fälle:

- Fehlender Pin, ausgetauschtes Zertifikat und abgelaufenes, ansonsten korrekt gepinntes Zertifikat: keine entschlüsselten HTTP-Requests; jeweils Debug und Release.
- Neuer echter Host mit neuem Zertifikat am alten Port und absichtlich gleichem Bearer: alter Client scheitert vor Service-Dispatch. Zusätzlich startet die R2-Folgeprobe einen **anderen OS-Prozess** mit echtem Host/Service: neue Identität, alte Paarung verworfen, frische Paarung mit neuem Codex erfolgreich. Dies ist ein Headless-Prozessnachweis, kein weiterer sichtbarer GUI-Neustart. [Restart-Log](../../artifacts/b4-f2/r2-counterprobe/restart.log).
- Antwortverlust nach tatsächlichem Dispatch: gepinnter Testrelay verwirft die Toolantwort; `toolPOST=1`, `serviceCalls=1`, Fehler beim Client. Das verwendete Tool ist ein Statusread über einen Service-Probe; es ist kein separat nachgewiesener commitierter Editorwrite mit verlorener Antwort. Alle Tools verwenden denselben produktiven Weiterleitungspfad ohne Retry-Verzweigung.
- STDIO: Initialisierung, feste Fehler, erhaltene String-ID, fehlerhafter Methodenparameter und reguläres EOF-Ende innerhalb der Testfrist bestanden.

## 5. Build-, Test- und Packaging-Matrix

| Prüfung | Debug | Release |
|---|---:|---:|
| Vollständiger Solution-Build mit Prism-/Locked-Restore | bestanden | bestanden |
| Core | 11.532 Pass, 0 Fail, 27 NotExecuted | 11.532 Pass, 0 Fail, 27 NotExecuted |
| GUI | 797 Pass, 0 Fail | 797 Pass, 0 Fail |
| MCP regulär | 176 Pass, 0 Fail, 13 explizit NotExecuted | 176 Pass, 0 Fail, 13 explizit NotExecuted |
| Installierter Codex, explizite Security-/Kompatibilitätsmatrix | 11 Pass, 0 Fail | 11 Pass, 0 Fail |
| Native AES-/Disconnect-/Takeover-Gesamtprobe | nicht separat ausgeführt | 1 Pass, 0 Fail; veröffentlichtes win-x64-Paket |
| win-x64 RID Locked Restore, Publish, Inhaltsvalidator | nicht separat ausgeführt | bestanden |

Die 27 Core-Ausnahmen enthalten 26 Plattform-/Dateisystemfälle und einen expliziten Hardwarebenchmark. Die 13 MCP-Explicit-Fälle sind im regulären Lauf korrekt als nicht ausgeführt ausgewiesen: elf wurden separat in beiden Konfigurationen gestartet, der kombinierte native Fall zusätzlich in Release; der separate, kürzere native AES-Test wurde nicht nochmals zusätzlich zum umfassenderen Kombinationsfall ausgeführt. Testzahlen beruhen auf den tatsächlichen `UnitTestResult`-Outcomes der TRX-Dateien, nicht allein auf Console-Skips.

F2 ergänzt acht reguläre MCP-Testfälle und einen GUI-Fall. Die TLS-/Methodenproben isolieren Service-Dispatch mit einem Probe; sie ersetzen keine Editorprüfung. Diese erfolgt in den vorhandenen B1–B4-Suiten, im R2-Folgetreiber mit realem Service/Editor und nativ. Der neue GUI-Test lädt die tatsächliche Desktop-Stoproutine und hält ein echtes ursprüngliches Coordinator-Ticket; er ersetzt den produktiven Stop nicht durch einen umschaltbaren Erfolgsdelegate. Vorhandene echte Drain-/Transportfault- und Late-Callback-Gegenfälle bleiben in der vollständigen Regression enthalten.

Bei der ersten Methoden-Allowlist fehlte `server/discover`; sieben bestehende Tests zur modernen Protokollvariante deckten dies auf. Die Methode ist nun enthalten, die vollständigen MCP-Suiten wurden grün wiederholt. Die echte Desktop-Lifecycle-Probe erforderte die passende MCP-/DI-Projektreferenz statt einer nur per Reflection geladenen inkompatiblen Dependency-Version. Nach der späteren Änderung ausschliesslich des manuellen Testwartelimits wurden die MCP-Tests erneut in Debug/Release gebaut und vollständig ausgeführt; Produktcode und Publish-Paket blieben dabei unverändert.

Die Lösung verwendet weiterhin den gepinnten Prism-Stand `358118cd640d9a22ff8cf21c8ad197fa038b7990`. Das frameworkabhängige win-x64-Paket enthält Apphost, verwaltete Abhängigkeiten, fünf native Assets und 133 InfoDocs-Dateien. Es verlangt weiterhin `Microsoft.NETCore.App` und `Microsoft.AspNetCore.App` 10.0; F2 fügt keine zusätzliche Runtime oder separaten Adapter-Apphost hinzu. Der Inhaltsvalidator ist kein GUI-Test; die native Prüfung unten wurde deshalb direkt aus diesem Paket durchgeführt. [Build-/Testskript](../../artifacts/b4-f2/Verify.ps1) · [Publishprüfung](../../artifacts/b4-f2/publish-validation.log).

Linux/macOS wurden in F2 nicht nativ gebaut, gestartet oder abgenommen. Bestehende RID-Locks sind kein Plattformnachweis. Die Desktop-MCP-Aktivierung bleibt dort wegen der bisher nur für Windows zertifizierten Lease-Uhr deaktiviert. Daraus wird kein neuer Linux-/macOS-Produktdefekt abgeleitet.

## 6. Native Codex-E2E-Evidenz

GUI und Adapter wurden beide aus `artifacts/b4-f2/publish/win-x64` gestartet. Der Benutzer erteilte die lokale Zugriffsfreigabe. Pin und Token wurden aus dem Pairingdialog übernommen, ohne Token in Chat, Prozessargumenten oder Testlogs auszugeben. Die kurzzeitige lokale Übergabedatei war mit Windows-DPAPI geschützt und wurde beim Teststart entfernt.

Der erfolgreiche abschliessende Lauf verwendete Workbench-Instanz `44e2d851-a22c-4892-95c3-6b8ca24a4abb`, sichtbaren Editor `01786cea-abc1-46cf-95cf-7ceeced24390`, Revision 1 → 2 und Job `9ad6b2b1-8f37-4cb0-8ec7-804093053eaa`. Installierter Codex entdeckte die elf Tools, übernahm den Editor, schrieb das synthetische AES-CBC-Script und erhielt den History-Receipt. Revisionskonflikt, Ausführungsvorschau mit lokaler Bestätigung, Ausführungsstatus/Diagnostik, Release, ungültige alte Lease und GUI-Rückgabe bestanden. Der Ciphertext entsprach `7649abac8119b246cee98e9b12e9197d`.

Danach wurde der sichtbare Button `Disconnect Codex` betätigt. Derselbe weiterhin laufende Codex versuchte zwei Edits gegen den Ersatzserver: beide abgewiesen, null entschlüsselte HTTP-Requests. Die GUI zeigte `Disconnected — GUI released`. Das Fenster wurde anschliessend regulär geschlossen; abschliessend waren null Paketprozesse und null Listener auf 51837 vorhanden. [TRX](../../artifacts/b4-f2/native-aes-takeover.trx) · [Vorschau](../../artifacts/b4-f2/execution-preview-final.jpg) · [Freigegebener Editor](../../artifacts/b4-f2/after-release-final.jpg) · [Disconnect](../../artifacts/b4-f2/disconnected-final.jpg) · [Abschlusskontrolle](../../artifacts/b4-f2/final-cleanup.json).

Undo/Redo und Save wurden im ersten nativen F2-Durchlauf aus demselben Publish-Paket geprüft: Undo stellte den Seedzustand wieder her, Redo die fünf AES-Zeilen. Der native Speicherdialog erforderte Benutzerbedienung. Die tatsächlich gespeicherte Datei lag unter `C:\Users\stefa\Documents\CryptoScript\ScriptBook.sbk`; sie wurde nach Inhaltsprüfung als [F2-AES.sbk](../../artifacts/b4-f2/F2-AES.sbk) in die Evidenz kopiert. SHA-256: `3F67C54DAD03AFEE61F6ECCBC153E6C17378C9E930611DB6EF38903D11FDE6D0`. [Undo](../../artifacts/b4-f2/undo.jpg) · [Redo](../../artifacts/b4-f2/redo.jpg) · [Speicherpfad und Disconnect](../../artifacts/b4-f2/saved-disconnected.jpg).

Dieser erste Lauf bestand AES und die GUI-Schritte, überschritt aber vor dem Takeoverteil die ursprünglichen fünf Minuten für manuelle Bedienung. Er ist **kein bestandener Gesamtfall** und bleibt als `native-first-manual-timeout.trx/.log` erhalten. Danach wurde nur das Testwartelimit für den manuellen Disconnect auf 30 Minuten erhöht; Produkt-, Lease- und Toolfristen blieben unverändert. Der anschliessende durchgehende Lauf bestand in 5 min 35 s. Undo/Redo/Save gehören zur ersten, der erfolgreiche kombinierte Takeovernachweis zur zweiten Sitzung; eine einzige lückenlose Sitzung mit sämtlichen manuellen Schritten wird nicht behauptet.

## 7. Sicherheitsmodell und verbleibende Grenzen

Die Bindung setzt den authentischen Pairingdialog und einen unveränderten lokalen Adapter voraus. Angreifer mit Kontrolle über den Benutzerprozess, dessen Speicher, Umgebungsvariablen, ausführbare Dateien oder private Schlüsselcontainer liegen weiterhin ausserhalb des Modells. Lokale Portbelegung/Floods können Verfügbarkeit beeinträchtigen. Ein harter Prozessabbruch kann wie in R2 beschrieben öffentliche Dateien und Windows-Schlüsselressourcen zurücklassen; F2 schliesst die belegten behandelten Initialisierungs- und normalen Shutdownfehler, verspricht aber keine Bereinigung nach beliebigem Crash.

Die Workbench bleibt einzige Autorität für Credential-Widerruf, Rechte, Editoridentität/-revision, Leases und Execution-Receipts. Achtstündige sitzungsgebundene Transportidentität und prozessgebundene AP0-D02-Aktivierung bleiben erhalten. Disconnect eröffnet im selben Prozess keine neue Paarung und setzt keine Erwerbsquoten zurück. Der Adapter erteilt keine lokale Ausführungsbestätigung. Echte Transport- oder Drainfehler lassen den Editor geschützt; nur öffentliche Aufräumfehler sind vom kritischen Abschluss getrennt.

Andere MCP-Clients können den Standard-STDIO-Einstieg verwenden, benötigen aber eigene Kompatibilitätsnachweise. Ein direkter HTTPS-Client muss vor HTTP eine gleichwertige exklusive Pinprüfung implementieren. Die vorhandene HTTP-Schnittstelle kann nicht erzwingen, dass ein beliebiger externer Client seinen Server sicher authentisiert. Die neue Anleitung bietet den früheren CA-only-Pfad deshalb nicht als gleichwertige Alternative an. Eine spätere Protokoll-/SDK-Erweiterung muss die expliziten Methoden- und Fehlerpfade erneut prüfen.

## 8. Reproduktion und Evidenzintegrität

Im Zielworktree, mit installierter .NET-/Prism-Voraussetzung und installiertem Codex:

```powershell
./artifacts/b4-f2/Verify.ps1
```

Dieses Skript dokumentiert vollständige Builds, Suiten, elf explizite Codex-Fälle pro Konfiguration und den Windows-Publish. Die native Probe benötigt zusätzlich eine frisch freigegebene sichtbare Workbench, deren Pin/Token ausschliesslich in die Kindprozessumgebung gelangen. Der permanente Testfilter lautet `FullyQualifiedName~InstalledCodexCompletesDesktopAesAndRejectsTakeover`; Adapterpfad muss auf den geprüften Publish-Apphost zeigen. Lokale Freigabe, AES-Bestätigung, GUI-Editierbarkeit und Disconnect sind beobachtbare Schritte, keine aus einem Mock abgeleiteten Erfolge. `Run-NativeAes.ps1` dokumentiert den hier verwendeten DPAPI-Handoff; es ist ohne frische lokale Paarung nicht unbeaufsichtigt wiederholbar.

Die separate R2-Gegenprobe lässt sich aus dem Worktree wie folgt vorbereiten; `Compile.ps1` nennt die auf diesem Rechner verwendeten SDK-/Referenzpfade und muss bei anderer SDK-Installation entsprechend aufgelöst werden:

```powershell
$candidate = (Get-Location).Path
./artifacts/b4-f2/r2-counterprobe/Prepare.ps1 -Candidate $candidate
$env:R2_CODEX = (Get-Command codex).Source
$env:F2_ADAPTER = Join-Path $candidate 'CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.exe'
Push-Location artifacts/b4-f2/r2-counterprobe
try {
    dotnet probe/CryptoWorkBenchGuiTest.dll attack isolated
    dotnet probe/CryptoWorkBenchGuiTest.dll attack ssl
    dotnet probe/CryptoWorkBenchGuiTest.dll attack bundle
    dotnet probe/CryptoWorkBenchGuiTest.dll restart
} finally { Pop-Location }
```

Die Probe verwendet nur eigene synthetische Daten und eigene kurzlebige Identitäten. Gegenproben sind nach den expliziten Bool-/Zählerbedingungen auszuwerten, nicht nur nach Prozessstart. Sämtliche finalen Testresultate und ausgewählte Belege werden in [evidence.sha256](../../artifacts/b4-f2/evidence.sha256) erfasst, der Kandidat in [final-source.sha256](../../artifacts/b4-f2/final-source.sha256), das verwendete Paket in [publish.sha256](../../artifacts/b4-f2/publish.sha256). Der bytegleiche R2-Angreifer ist zusätzlich separat bestätigt. Abschliessende Kontrolle: keine `.pfx`-/DPAPI-Handoff-Dateien im F2-Artefaktbaum; keine eigenen veröffentlichten Workbench-Prozesse oder Listener mehr. `git diff --check` ist ohne Fehler abgeschlossen.

**READY FOR R3** gilt für die unabhängige erneute Sicherheitsprüfung dieses Windows-Kandidaten. Es ersetzt weder R3 noch die weiterhin ausstehende plattformübergreifende Abnahme.
