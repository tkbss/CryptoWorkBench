# MCP-B4-F1 – Security and Lifecycle Corrections

> Historische F1-Evidenz. R2 widerlegt die exklusive Identitätsgarantie des direkten CA-Pfads bei zusätzlichem Vertrauen. Die aktuelle Anleitung ist das [gepinnte F2-STDIO-Pairing](MCP-pinned-client-setup.md); die folgenden F1-Resultate bleiben als historische Befunde erhalten.

**Abschlussstatus: READY FOR R2** · 3. November 2026 · Windows x64

## 1. Eingangsinventar und Umfang

Arbeitsbranch: `feature/mcp-ap3`. Unveränderter HEAD und abgenommene B3-Baseline: `6806a34ebd75c97eab7fc17a4d4a5309a6d9f009`. B4 lag als ungestagter Kandidat vor; F1 ergänzt diesen Arbeitsbaum. [B4-Bericht](MCP-B4-implementation-and-e2e-report.md) und [R1-Review](MCP-B4-R1-independent-review.md) bleiben historische, unveränderte Eingangsdokumente. Das [Eingangsinventar](../../artifacts/b4-f1/before.sha256) enthält die Byte-Hashes der 24 bei F1-Beginn vorhandenen Kandidat-/Reviewdateien. Es ist kein Hashinventar des gesamten B3-Repositories.

F1 ändert Transportidentität, Transportvalidierung, Desktop-Rückmeldung und deren Tests. CryptoScript, Coordinator-Zustandsautomat, Lease-Identität, Revisionen, Ausführungsreceipts, ursprüngliche POST-Bindung und AP0-D02 bleiben erhalten. Es gibt keine neuen Pakete oder Produktprojektverweise gegenüber B4. Der experimentelle STDIO-Adapter gehört nicht zum ausgelieferten Produkt. Keine Commits, Pushes, Merges oder Änderungen anderer Worktrees wurden vorgenommen.

## 2. Entscheidung zur Serveridentität

**Gewählt: Variante A, direktes HTTPS mit einer frischen sitzungsgebundenen CA und überprüfendem Codex-Client.** Der feste Endpunkt lautet nun `https://127.0.0.1:51837/mcp`. HTTP wird dort nicht angeboten. Der installierte Client meldet `Codex Desktop/0.166.0-alpha.5.1`. Für die Proben wurde sein echter `app-server --stdio` mit isoliertem `CODEX_HOME` und direkten MCP-Aufrufen verwendet; es wurden keine Modellaufrufe benötigt und keine Benutzerkonfiguration geändert.

| Variante | Tatsächlicher Nachweis | Bewertung |
|---|---|---|
| A: HTTPS, öffentlicher CA-Bundle über `CODEX_CA_CERTIFICATE`, Bearer über Clientumgebung | Discovery/Status, wiederholte Portübernahme mit demselben laufenden Codex sowie fehlender, fremder und abgelaufener Vertrauensanker getestet | Gewählt: Plattform-TLS, kein zusätzliches Protokoll oder Adapterprogramm |
| B: STDIO-Adapter mit TLS-Zertifikatsbindung zur Workbench | Experimenteller Adapter transportierte Discovery/Status; nach Serverwechsel wurden zwei Folgeaufträge verworfen | Technisch möglich, aber zusätzliche Prozess-, Framing- und Fehlerpfade ohne Vorteil für den belegten Angriff |

Die öffentliche Codex-Dokumentation beschreibt den Custom-CA-Bundle und die HTTP-/STDIO-MCP-Anbindung. Entscheidend für die Auswahl waren zusätzlich die lokalen Tests des installierten Clients. [Custom CA bundles](https://learn.chatgpt.com/docs/auth#custom-ca-bundles) · [MCP-Konfiguration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)

Die Probe für B ist in [stdio-variant-final.trx](../../artifacts/b4-f1/stdio-variant-final.trx) und den eingefrorenen `*-tested.cs` unter [identity-probe](../../artifacts/b4-f1/identity-probe/) dokumentiert. Sie ist Architekturvergleich, kein Test des gewählten Produktpfads. Die finale A-Probe befindet sich in [installed-codex-Debug.trx](../../artifacts/b4-f1/installed-codex-Debug.trx) und [installed-codex-Release.trx](../../artifacts/b4-f1/installed-codex-Release.trx).

### Identität, Geheimnisse und Bedrohungsmodell

[LocalMcpIdentity](../../CryptoWorkBench.Mcp/LocalMcpIdentity.cs) erzeugt je Aktivierung eine zufällige CA und ein Serverzertifikat mit SAN `127.0.0.1`, Server-Auth-EKU, SHA-256/RSA-2048 und acht Stunden Gültigkeit. Der CA-Signierschlüssel wird nach Ausstellung verworfen. Nur das öffentliche CA-Zertifikat wird in einem zufälligen temporären Verzeichnis exportiert. Ein offener Dateihandle verbietet unter Windows bis zum Sitzungsende das Überschreiben oder Löschen dieser Datei. Es erfolgt keine Installation in den Betriebssystem-Truststore. Der Pairingdialog benennt Endpunkt, CA-Datei, Ablaufzeit und Bearer-Umgebungsvariable. Eine neue Workbench benötigt neues Pairing und einen neu gestarteten Client mit dem neuen Bundle.

Windows-Schannel unterstützt den zunächst getesteten rein ephemeren Zertifikatsschlüssel nicht. Die permanente TLS-Plattformprobe schützt deshalb den funktionierenden Import über `X509CertificateLoader` mit `UserKeySet`, ohne `PersistKeySet` oder `Exportable`. Der PFX-Zwischenpuffer existiert nur im Speicher und wird genullt. Schannel verwendet einen temporären benutzergebundenen Schlüsselcontainer; dessen reguläre Lebensdauer endet mit dem Zertifikats-Dispose. Ein harter Prozessabbruch ist kein Nachweis garantierter Bereinigung zurückbleibender OS-Schlüsselcontainer. Dies wird ausdrücklich nicht als rein speicherbasierte Schlüsselhaltung dargestellt.

Angreiferannahme: Ein anderer lokaler Prozess darf Loopback-Verbindungen öffnen, den freigewordenen Port binden, öffentliche CA-Dateien lesen und eigene Zertifikate/Antworten erzeugen. Er hat keinen Zugriff auf Workbench-/Codex-Prozessspeicher, den privaten OS-Schlüsselcontainer oder veränderbare Clientkonfiguration/Vertrauensanker. Administratorrechte, Codeinjektion und beliebige Manipulation unter derselben Benutzeridentität liegen ausserhalb dieser Garantie. Insbesondere schafft TLS keine neue Isolation zwischen uneingeschränkt gleichberechtigten Prozessen desselben Windows-Benutzers. Innerhalb der genannten Rechte erhält ein Portbesitzer weder den HTTP-Authorization-Header noch den Toolbody: Die Gegenprobe zählt sämtliche entschlüsselten HTTP-Requests vor jeder eigenen Authprüfung, nicht nur erfolgreiche Toolaufrufe.

Der bisherige zufällige 256-Bit-Bearer, konstanter Vergleich und Registry-Widerruf bleiben bestehen. Ablauf wird auf jedem HTTP-Request geprüft, zusätzlich widerruft ein Timer den Host und veranlasst Desktop-Disconnect. Das schützt auch eine bereits bestehende TLS-Verbindung nach Ablauf. Alte Credentials erhalten keine neue Epoche. Neue CA und neuer Bearer werden nicht aus alten Pairings übernommen. Produktlogging schreibt keine Tokens, Scripttexte oder Outputs. Tests enthalten nur synthetische Canaries und den bekannten AES-Testvektor.

## 3. Korrekturen und permanente Gegenproben

| F1 / R1 | Änderung und produktiver Pfad | Permanente Gegenprobe und Resultat |
|---|---|---|
| F1-01 / B4-R1-01 · High | `LocalMcpIdentity`, `LocalMcpHost` und `DesktopMcpSession`: überprüfbare HTTPS-Instanzidentität vor HTTP, Sitzungslimit und CA-Pairing | `CertificateCanAuthenticateOnThisPlatform`, `ReplacementCertificateIsRejectedBeforeAnyHttpData`, `ExpirationRevokesAnAlreadyEstablishedClient`, `SessionTrustFileIsPublicLockedAndRemovedOnDispose`; echte Codex-Proben für zwei spätere Edits und drei negative Vertrauensfälle bestanden |
| F1-02 / B4-R1-02 · Medium | `DisableMcpAndDrainAsync` erfasst unter dem Registry-Lock die ursprüngliche Epochenabschluss-Task. `McpDisconnect` beobachtet Transport und Drain gemeinsam. Desktop meldet Revoking, Waiting, Disconnected/GUI released oder Safe fault. Zusätzliche GUI-Zertifizierung verhindert, dass eine quieszente native Fault-Phase als erfolgreiche Rückgabe erscheint | Gehaltenes echtes Originalticket blockiert Abschluss und GUI; separates Exception-Injektat wird nach Ticketabschluss beobachtet. Zusätzlich echter Cleanup-Fault und bereits vorhandene Coordinator-Fault-Phase geprüft. Kein erzwungenes Reclaim |
| F1-03 / B4-R1-03 · Low | `IHttpRequestFeature.RawTarget` muss ordinal exakt `/mcp` sein; exakte numerische Host-/Portprüfung und Origin-Ausschluss bleiben | Echte rohe TLS/HTTP-Requestzeilen: `/mcp` 200/Dispatch; `/MCP`, `/%6dcp`, `/x/../mcp`, `/mcp?`, `/mcp?a=1` und vier Hostvarianten 403, null Dispatch |
| F1-04 / B4-R1-04 · Low, historischer Binding-Restpunkt | `SafeMcpParseErrors` prüft Versions-, Methoden- und Namenssignale vor SDK-Routing. Widersprüche werden auch bei fehlendem Versionsheader abgewiesen; moderne Bodymetadaten ohne Versionsheader sind nicht Legacy | Identischer widersprüchlicher moderner Request mit und ohne Versionsheader jeweils 400/null Dispatch; eindeutiges Legacy ohne Header und konsistenter moderner Request je erfolgreich. Single-use-Receipt und Original-POST-Abbruchprüfung unverändert |
| F1-05 / B4-R1-05 · Low | Feste Transportfehlermeldung; unbekannte Tools werden anhand des tatsächlich registrierten Toolkatalogs vor dem SDK mit `-32602`/`Unknown tool.` beantwortet | Unbekannte Version und widersprüchlicher Toolheader: 400/-32600; unbekanntes Tool: 200/-32602. Canary fehlt in den Antworten; kein Dispatch. Zulässige Toolkorrelations-ID bleibt erhalten |
| F1-06 / B4-R1-06 · Low | Kestrel: 32 Verbindungen, fünf Sekunden TLS-Handshake-/Requestheaderfrist, 15 Sekunden Keepalive; vorhandene 1-MiB-Bodygrenze bleibt | 32 tatsächliche TLS-Clients senden nur eine unvollständige Requestzeile. Nach sieben Sekunden lässt der Host wieder einen vollständigen authentisierten Request durch |
| F1-07 / B4-R1-07 · Low, historischer Shell-Restpunkt | `App.CoordinateClosing` zeigt nach fünf Sekunden einen Wartehinweis im Fenstertitel, bei Fehler festen Safe-Fault-Text. Timer wird beim Abschluss abgebrochen und beobachtet | Gehaltenes Originalticket: nach 5,2 Sekunden Hinweis, Fenster offen, GUI gesperrt; nach Freigabe ordnungsgemäss geschlossen. Injizierter Transportfehler: Fenster bleibt offen, kein Canary im Text, Wiederholung möglich |

Quellen: [Transporttests](../../CryptoWorkBench.Mcp.Tests/B4F1SecurityTests.cs), [Lifecycle-Tests](../../CryptoWorkBenchGuiTest/B4F1LifecycleTests.cs), [Desktop-Sitzung](../../CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs), [Disconnect](../../CryptoWorkBenchAvalonia/Services/Automation/McpDisconnect.cs), [Registry](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs), [Parserbindung](../../CryptoWorkBench.Mcp/SafeMcpParseErrors.cs), [Toolkatalog](../../CryptoWorkBench.Mcp/WorkbenchToolsV2.cs), [Shell](../../CryptoWorkBenchAvalonia/App.axaml.cs).

F1-02 ändert keine B3-Admission- oder Abschlussautorität. Ein ursprüngliches Ticket bleibt massgeblich; `Task.WhenAll` lässt einen offenen Drain nicht verschwinden, auch wenn der Transport scheitert. Shell-Exit verwendet weiterhin seine eigene `DrainForExitAsync`-Zertifizierung, einschliesslich des akzeptierten Sonderfalls nachgewiesener Quieszenz bei nativer Fault-Phase. Ein strikter Disconnect-Fault darf diesen erlaubten Exit nicht dauerhaft verhindern. Die UI-Texte erteilen selbst keinerlei Schreibberechtigung.

## 4. Testmatrix und Aussagekraft

| Prüfung | Debug | Release |
|---|---:|---:|
| Beide Solutions, gepinnte offizielle Prism-Quelle, Locked Restore und Build | PASS | PASS |
| Core / `CryptoScriptUnitTest` | 11.532 PASS, 27 NotExecuted, 0 Fehler | 11.532 PASS, 27 NotExecuted, 0 Fehler |
| GUI / `CryptoWorkBenchGuiTest` | 796 PASS, 0 Fehler | 796 PASS, 0 Fehler |
| Reguläre MCP-Suite | 168 PASS, 0 Fehler | 168 PASS, 0 Fehler |
| Explizit installierter Codex: Discovery, Übernahme, missing/different/expired | 5 PASS | 5 PASS |
| Native AES-Ausführung und anschliessende Portübernahme im selben laufenden Codex | Nicht separat nativ ausgeführt | 1 PASS |
| Windows RID Locked Restore, Publish und Artefaktprüfung | Nicht anwendbar | PASS, win-x64 |

Die 27 nicht ausgeführten Core-Fälle sind die bisherigen 26 Plattform-/Dateisystemfälle und ein expliziter Hardwarebenchmark. Sieben explizite installierte-Client-Fälle werden in der normalen MCP-Suite nicht ausgeführt; deren Ergebnisse stehen separat. Fünf wurden je Konfiguration aufgerufen. Die kombinierte native Release-Probe verwendet dieselbe AES-Prüfroutine wie die erhaltene ursprüngliche AES-Methode; diese ursprüngliche Methode wurde in F1 nicht zusätzlich separat gestartet. Der sichtbare Release-Test wird nicht als zusätzlicher Debug-Native-Nachweis gezählt.

Der [Windows-Publish](../../artifacts/b4-f1/publish-win-x64.log) wurde mit RID-Locked-Restore nach `artifacts/b4-f1/publish/win-x64` erstellt. Der bestehende Prüfer bestätigte Apphost, verwaltete Abhängigkeiten, fünf native Assets und 133 InfoDocs-Dateien. Der Publish-Prüfer startet keine GUI; die native E2E-Evidenz unten stammt ausdrücklich aus dem normalen Release-Buildverzeichnis, nicht aus einem zweiten Start des Publish-Ordners. Die Desktop-/MCP-Lockdateien sind gegenüber dem B4-Eingang unverändert.

Belege: [Debug-Build](../../artifacts/b4-f1/final-build-Debug.log), [Release-Build](../../artifacts/b4-f1/final-build-Release.log); [Core Debug](../../artifacts/b4-f1/CryptoScriptUnitTest-Debug.trx), [Core Release](../../artifacts/b4-f1/CryptoScriptUnitTest-Release.trx); [GUI Debug](../../artifacts/b4-f1/CryptoWorkBenchGuiTest-Debug.trx), [GUI Release](../../artifacts/b4-f1/CryptoWorkBenchGuiTest-Release.trx); [MCP Debug](../../artifacts/b4-f1/CryptoWorkBench.Mcp.Tests-Debug.trx), [MCP Release](../../artifacts/b4-f1/CryptoWorkBench.Mcp.Tests-Release.trx).

Die Wire-Tests benutzen echte Kestrel-/TLS-Sockets. Einige positive Transporttests zählen einen Proxy-Serviceaufruf und beweisen damit Routing, nicht fachliche Ausführung. Die Lifecycle-Tests arbeiten mit echtem Service und Coordinator in der Headless-GUI; Faults werden gezielt injiziert. Die zusätzliche native Probe verwendet dagegen den produktiven Desktop-Service und sichtbaren Editor. Die Tests ersetzen weder Linux-/macOS-Nachweise noch eine personell unabhängige Sicherheitsfreigabe.

Frühe Fehlversuche sind nicht als bestanden gewertet: Schannel wies ephemere Schlüssel zurück; eine Test-Assertion verwendete unpassendes File-Sharing für die absichtlich gesperrte CA-Datei; eine synchrone NUnit-Async-Assertion blockierte den Test-Dispatcher und wurde durch tatsächlich asynchrones Await ersetzt. Ein isoliert gestarteter VSTest-Prozess erreichte seinen Testhost nicht. Diese Test-/Umgebungsprobleme wurden vor den jeweils erfolgreichen Läufen korrigiert. Keine dieser Beobachtungen wird als bestandener nativer Produkttest umgedeutet.

## 5. Reproduktion

Im Zielworktree mit dem vorhandenen .NET SDK 10.0.401:

```powershell
./eng/prism/Build.ps1 -Configuration Debug
./eng/prism/Build.ps1 -Configuration Release
dotnet test CryptoScriptUnitTest -c Debug --no-build
dotnet test CryptoWorkBenchGuiTest -c Debug --no-build
dotnet test CryptoWorkBench.Mcp.Tests -c Debug --no-build
# Die drei Testbefehle auch mit -c Release ausführen.
$env:CRYPTOWORKBENCH_CODEX_PATH = (Get-Command codex).Source
dotnet test CryptoWorkBench.Mcp.Tests -c Release --no-build --filter 'FullyQualifiedName~InstalledCodexRefusesUntrustedOrExpiredServer|FullyQualifiedName~InstalledCodexRejectsPortTakeoverOverHttps|FullyQualifiedName~InstalledCodexDiscoversAndCallsStatus'
```

Die explizite native Probe `InstalledCodexCompletesDesktopAesAndRejectsTakeover` benötigt eine frische sichtbare Workbench mit exakt `KEY k=GenerateKey(AES-CBC,0x(2b7e151628aed2a6abf7158809cf4f3c))` im Editor. Nach lokaler Freigabe die beiden im Pairingdialog genannten Umgebungsvariablen nur im Testprozess setzen. Zusätzlich `CRYPTOWORKBENCH_F1_READY_FILE` auf einen neuen lokalen Signaldateipfad setzen. Die Probe verbindet den echten Codex, prüft elf Tools, Status, Editor-ID, Revision, Acquire, Append/History-Receipt, Konflikte, bestätigte AES-Ausführung, Diagnosen, Release und Ablehnung des alten Lease. Sobald die Signaldatei erscheint, GUI-Undo/Redo/Save durchführen und innerhalb von fünf Minuten Disconnect klicken. Der Test wartet auf das Ende des echten HTTPS-Hosts, bindet danach den Ersatzlistener an denselben Port und schickt zwei weitere Editoraufträge über dieselbe unverändert laufende Codex-Sitzung. Null entschlüsselte HTTP-Requests und keine akzeptierte Fake-Erfolgsantwort sind zwingende Assertions.

## 6. Native Evidenz

Die [kombinierte native TRX](../../artifacts/b4-f1/desktop-aes-takeover.trx) enthält einen bestandenen Test vom 3. November 2026, 21:41:39–21:45:52 CET. Workbench-Instanz: `8ac6c374-6d37-4c55-809c-8762bbb749e1`; sichtbarer Editor: `a241a707-4c49-4e3b-8a62-d7fe2ce54441`; Revision 1→2; Ausführungs-ID: `1ffe3f56-dc3a-48bf-8354-1163c422450a`.

1. Ein frischer produktiver Release-Prozess zeigte den einzeiligen AES-Seed. Der Benutzer erteilte die lokale Freigabe. Der Pairingdialog wurde ohne Tokenausgabe ausgelesen; die bestätigte Vorschau zeigte genau diesen Editor, Revision 2, die fünf bekannten AES-Zeilen und `IncludeText`. [Ausführungsvorschau](../../artifacts/b4-f1/execution-confirmation.png)
2. Der echte installierte Codex entdeckte exakt elf Tools und las Status sowie sichtbaren Editor. Der Test bestätigte Acquire, `editorOwned` beim zweiten Erwerb, vollständigen Text-/History-/Finalisierungsreceipt und `revisionConflict` beim alten Revisionswert. Der bestätigte Job endete ohne Fehler, mit finalen Diagnosen und exakt `out: 0x(7649abac8119b246cee98e9b12e9197d)`.
3. Nach Release stimmte der gesamte Editorinhalt. Ein weiterer Edit mit der alten Lease lieferte `leaseRevoked`, ohne Text-/Historyeffekt und ohne Revisionsänderung. Status war `gui`. [Freigegebener Editor](../../artifacts/b4-f1/after-release.png)
4. Natives Ctrl+Z entfernte den kompletten MCP-Anhang in einem Schritt und zeigte nur die Seed-Zeile. Ctrl+Y stellte die fünf Zeilen wieder her. Diese Schritte sind Bedienbeobachtungen mit Bildbelegen, keine simulierten NUnit-Editoraufrufe. [Undo](../../artifacts/b4-f1/undo.png) · [Redo](../../artifacts/b4-f1/redo.png)
5. Wegen wiederholter Automationsprobleme im Windows-Dateidialog führte der Benutzer Save und Disconnect aus. Die GUI bestätigte den tatsächlich verwendeten Standardpfad `C:\Users\stefa\Documents\CryptoScript\ScriptBook.sbk`. Die Datei wurde read-only gegen alle fünf erwarteten Zeilen geprüft und unverändert als [F1-AES.sbk](../../artifacts/b4-f1/F1-AES.sbk) in die Evidenz kopiert. SHA-256: `3F67C54DAD03AFEE61F6ECCBC153E6C17378C9E930611DB6EF38903D11FDE6D0`, identisch zum bekannten B4-Buch. Der angefragte Artefaktpfad war somit nicht das tatsächliche GUI-Speicherziel; die Evidenzkopie wird nicht als zweiter GUI-Save ausgegeben.
6. Der Test hielt denselben Codex-Prozess und Thread über diese Bedienfolge hinweg offen. Nach echtem GUI-Disconnect und Ende des produktiven Hosts startete er den fremden HTTPS-Listener am selben Port 51837. Zwei weitere `workbench_update_editor`-Aufträge mit synthetischem Canary wurden abgelehnt. Der Ersatzlistener zählte **0 entschlüsselte HTTP-Requests**; somit erreichten ihn weder Bearer noch Toolbody. Keine Fake-Erfolgsantwort wurde akzeptiert. Das sind Assertions der bestandenen nativen TRX, keine Ableitung aus dem Proxy-Status-Test.
7. Der native Desktop zeigte `Disconnected — GUI released`. Danach schloss er regulär per Alt+F4; PID war beendet, kein Listener auf 51837 vorhanden. [Speichern und Disconnect](../../artifacts/b4-f1/saved-disconnected.png) · [UI-Text](../../artifacts/b4-f1/saved-disconnected.txt). Umgeleitete [stdout](../../artifacts/b4-f1/native-takeover.stdout) und [stderr](../../artifacts/b4-f1/native-takeover.stderr) waren jeweils 0 Bytes.

Die Pairingfreigabe und die manuellen Save-/Disconnect-Schritte wurden vom Benutzer durchgeführt. Der Testagent bediente die konkrete synthetische Ausführungsvorschau sowie Undo/Redo. Fokus-/Geometrieprobleme der Windows-Automation erforderten zuvor neue Wegwerffenster, ohne produktiven Testcode zu ändern. Ein erster isolierter Teststarter scheiterte an der VSTest-Loopbackverbindung, bevor der Editorauftrag begann. Für den erfolgreichen Start wurden die Pairingdaten einmal in einer benutzergebundenen DPAPI-Datei verschlüsselt übergeben, vor dem Test entschlüsselt und die Datei sofort gelöscht. Kein Klartexttoken wurde als Datei, Befehlsargument oder Bericht ausgegeben; der Test verwendet nur die Kindprozessumgebung. Die öffentliche CA-Datei wurde mit dem Host entsorgt.

Die F1-AES-Assertion prüft den MCP-Output des produktiven Runners. Ein erneuter unabhängiger GUI-Rechenlauf in einem frischen Interpreter ist in F1 nicht enthalten und wird nicht behauptet. B4s dokumentierter GUI-Wertevergleich bleibt historische ergänzende Evidenz.

## 7. Verbleibende Risiken und Plattformbedingungen

* Bösartige lokale Prozesse können den Port vor Start besetzen oder Ressourcen fortgesetzt erschöpfen. Die kleine Timeout-/Verbindungsgrenze räumt stille Verbindungen auf; sie garantiert keine Verfügbarkeit gegen dauerhafte lokale Floods. Eine Portkollision führt weiterhin zu sicherem Startfehler.
* Prozessspeicher, Benutzer-Schlüsselcontainer, vertrauenswürdige Clientkonfiguration und Root-Trust sind Voraussetzungen des Modells. Harter Prozessabbruch kann öffentliche CA-Dateien und temporäre OS-Schlüsselartefakte hinterlassen; die achtstündige Gültigkeit und neue Prozessidentität ersetzen keine Bereinigungsgarantie.
* Die unterstützten Protokollversionen sind bewusst auf die lokal geprüfte SDK-Kompatibilität begrenzt. Eine zukünftige Version wird fest abgewiesen, bis sie überprüft und aufgenommen wird. F1-05 deckt die drei geforderten inputabhängigen Fehlerpfade ab; daraus folgt keine pauschale Zusicherung für beliebige zukünftige SDK-Diagnosen.
* Der Operator muss CA-Bundle und Bearer des angezeigten authentischen Workbench-Prozesses übernehmen. Eine neue Sitzung ist ein neues Vertrauensverhältnis. Der öffentliche Bundle ist kein Bearerersatz.
* Linux und macOS bleiben eigenständige Validierungsbedingungen. Produktive MCP-Aktivierung und der akzeptierte Lease-Uhrpfad bleiben Windows-only. Nicht-Windows-Desktopauslieferung benötigt wegen des vorhandenen B4-Projektverweises weiterhin das ASP.NET-Core-Shared-Framework; ein ausgeblendeter Button entfernt keine Runtime-Abhängigkeit. Keine native Nicht-Windows-Abnahme wird aus Windows-Tests abgeleitet.
* AP0-D02 bleibt pro Prozess bestehen. Disconnect eröffnet keine zweite Aktivierung, keine neue Lease-Epoche und keinen Quotenreset. Nicht zertifiziertes Ende erzeugt keine erzwungene GUI-Rückgabe.

## 8. Empfehlung

**READY FOR R2.** Für alle sieben technischen R1-Findings liegt eine Korrektur mit permanenter Regression vor. Debug und Release bestehen die vollständigen vorhandenen Suiten; die expliziten Codex-Gegenproben sowie der native AES-/Übernahmetest sind bestanden. Die dokumentierte Serveridentitätsgarantie gilt unter dem ausdrücklich genannten lokalen Angreifermodell. R2 sollte insbesondere dieses Modell, die Schannel-Schlüsselhaltung, den ursprünglichen Drainpfad und die native Übernahme-TRX unabhängig bewerten.

Dies ist eine Implementierungsentscheidung für den nächsten Review, keine unabhängige Sicherheitsfreigabe. Die plattformübergreifende MVP-Abnahme bleibt **BLOCKED**, solange die separaten Linux-/macOS-Nachweise fehlen. Der Kandidat bleibt uncommitted; HEAD und Index wurden nicht verändert. [Finales Quellinventar](../../artifacts/b4-f1/final-source.sha256) · [Evidenzinventar](../../artifacts/b4-f1/evidence.sha256).
