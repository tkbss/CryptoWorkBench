# MCP-B4-R3 – Independent Final Windows Security and Integration Review

**Gesamturteil: READY FOR PLATFORM VALIDATION** · 4. November 2026 · Windows x64

Die High-/Medium-Befunde aus R2 sind nachweislich geschlossen. Eigene Portübernahmeproben mit installiertem Codex, zusätzlichem CA-Vertrauen und Proxykonfiguration liefern keine vertraulichen HTTP-Daten an den Ersatzserver. Die echte Desktop-Abschlusskette bleibt bei öffentlichen CA-Dateilocks funktionsfähig und wartet weiter auf den ursprünglichen Coordinator-Drain. Eine tatsächlich angewendete Editoränderung wird nach verlorener HTTP-Antwort nicht erneut gesendet.

Es bleiben **zwei nicht blockierende Low-Befunde**: R2-03 ist auf der direkten HTTPS-Schnittstelle für Array-/Null-Parameter nicht vollständig behoben; ausserdem ist F2s Aussage über einen MRTR-freien `SendRequestAsync`-Pfad falsch. Letzteres wurde als zweiter Dispatch reproduziert, aber ohne zweite Text-, Revisions- oder History-Änderung. Der heutige Workbench-Server erzeugt die dafür injizierte MRTR-Antwort nicht. Beide Befunde werden unten begründet abgegrenzt. Kein Produktcode wurde korrigiert.

Die Entscheidung betrifft die Windows-Sicherheits- und Integrationsreife für die weitere Plattformvalidierung. Sie ist **keine plattformübergreifende MVP-Abnahme**; native Linux-/macOS-Nachweise fehlen weiterhin.

## 1. Unabhängigkeit, Kandidat und Integrität

Geprüft wurde `feature/mcp-ap3` bei HEAD `6806a34ebd75c97eab7fc17a4d4a5309a6d9f009`. Die sechs vorgegebenen B4-/R1-/F1-/R2-/F2-/Pairing-Dokumente wurden mit dem tatsächlichen Code, den Tests und den gespeicherten Belegen abgeglichen. B4/F1/F2 liegen als uncommitteter Kandidat auf der B3-Baseline; der gesamte Diff ist kein reiner F2-Diff.

Das eigene Eingangsinventar enthält **41 vorhandene geänderte/ungetrackte Dateien**. Alle stimmen byteweise mit `artifacts/b4-f2/final-source.sha256` überein. Auch alle 237 Publishdateien sowie alle 66 im F2-Evidenzinventar erfassten Dateien stimmen. Das Review verwendet eigene Prüfprogramme in `../b4-r3-independent`; im Zielworktree kommt ausschliesslich dieser Bericht hinzu. [Eingangsinventar](../../../b4-r3-independent/before.sha256) · [Abschlussinventar](../../../b4-r3-independent/after.sha256).

R3 ist eine neue adversarial Prüfung im selben Chat wie F2, keine personell unabhängige Zweitfreigabe. Die Prüfprogramme wurden aus dem ursprünglichen R2-Review übernommen und für R3 erweitert, nicht aus den F2-Testhelfern aufgebaut. Neue Gegenproben für echten Edit mit Antwortverlust, MRTR, STDIO, CNG-Fehler und Quellenzuordnung stehen in `Probes.cs`. Portangreifer und Codex-RPC-Client sind separate Reviewimplementierungen; produktive Host-, Service-, Editor- und Coordinator-Binaries bleiben unverändert. Interne Zugriffe erfolgen nur im Prüfharness über vorhandene Friend-Assembly-Grenzen beziehungsweise Reflection. Dies sind Prüfprivilegien, keine angenommenen Angreiferrechte. [Review.cs](../../../b4-r3-independent/Review.cs) · [Probes.cs](../../../b4-r3-independent/Probes.cs) · [Client.cs](../../../b4-r3-independent/Client.cs) · [Foreign.cs](../../../b4-r3-independent/Foreign.cs).

### Quell-/Binärzuordnung

Die portable PDB-GUID wurde jeweils gegen den CodeView-Eintrag der zugehörigen PE-Datei geprüft. Anschliessend wurden die PDB-Dokumentprüfsummen mit den tatsächlichen lokalen Quelldateien verglichen: **544 vorhandene Dokumente je Debug und Release, null Abweichungen**. Sieben virtuelle Avalonia-NameGenerator-Quellen werden nicht als fehlende Repositorydateien gezählt. Die erste zu strenge Prüfung hatte diese sieben virtuellen Pfade als fehlend gemeldet; das ist in `pdb-initial.log` erhalten.

`CryptoWorkBench.Mcp`, GUI und Core im Publish-Paket sind bytegleich mit den geprüften Release-DLLs. Der Desktop-Apphost wird RID-spezifisch gebaut; seine Desktop-DLL/PDB sind daher nicht bytegleich mit dem RID-losen Build. Seine **eigene** Publish-PDB passt zur Publish-PE und bestätigt vier vorhandene Quelldokumente ohne Abweichung. [Debug](../../../b4-r3-independent/pdb-Debug.log) · [Release](../../../b4-r3-independent/pdb.log) · [Publish-Desktop](../../../b4-r3-independent/pdb-Publish.log).

## 2. Status aller R2-Findings

| R2-ID | Severity | R3-Status | Unabhängiger Nachweis |
|---|---|---|---|
| B4-R2-01 | High | **Geschlossen für den freigegebenen STDIO-/Pinning-Pfad** | Exakter Leaf-Pin ohne CA-Fallback; fremder Portbesitzer unter isoliertem Trust, `SSL_CERT_FILE`, Mehrfach-Bundle und Proxy: keine HTTP-/Bearer-/Canaryübertragung, keine Fake-Antwort. Neuer Workbench-Prozess mit anderer Identität wird von alter Paarung verworfen. |
| B4-R2-02 | Medium | **Geschlossen** | Echte Desktop-Stop-/Shell-Closing-Kette mit vorübergehendem und bis nach dem Close gehaltenem öffentlichen CA-Dateilock; Widerruf, Listenerende, privater Schlüsselabschluss und Originalticket separat geprüft. Kein dauerhaft vergifteter Stop-Task. |
| B4-R2-03 | Low | **Teilweise geschlossen; Rest B4-R3-01** | Normaler unbekannter Methodenaufruf ohne `params` beziehungsweise mit Objekt wird bereinigt. `params: []` und `params: null` passieren die neue Sonderbehandlung und spiegeln den Methodennamen über HTTPS. STDIO bereinigt auch diese Fälle. |
| B4-R2-04 | Low | **Geschlossen für die belegten behandelten Fehlerpfade** | Eigener Kindprozess mit fehlerhaftem TEMP/TMP: null neue CNG-Container, auch nach Exit. Öffentlicher Exportfehler, Fehler beim Hostaufbau und belegter Port: tatsächliche private Schlüsselressourcen nach Cleanup entfernt. |

## 3. Verbleibende Findings

| ID / Severity / Einordnung | Datei und Position | Konkreter Fehlerpfad und reproduzierbarer Beweis | Auswirkung und minimale Korrektur | Freigaberelevanz |
|---|---|---|---|---|
| **B4-R3-01 · Low** · historischer Rest von R2-03, unvollständige F2-Korrektur | `CryptoWorkBench.Mcp/SafeMcpParseErrors.cs:145–165`, insbesondere `:151`; HTTP-400-Normalisierung `:83` | Authentisierter POST `/mcp` mit `{"jsonrpc":"2.0","id":1,"method":"R2_PRIVATE_CANARY","params":[]}`. Die Sonderbehandlung verlangt abwesende oder objektförmige Parameter. Der Request erreicht den SDK-Methodenfehler; HTTP 200/SSE enthält `-32601` und den Canary-Methodennamen. `params:null` ergibt denselben Fehlerpfad. [Array-Rohantwort](../../../b4-r3-independent/wire-unknownArrayParams.txt), [Null-Rohantwort](../../../b4-r3-independent/wire-unknownNullParams.txt). | Der feste Fehlertextvertrag gilt nicht für alle Envelopeformen. Keine Serviceausführung, kein Beleg für Offenlegung serverseitiger Geheimnisse. Unbekannte Methoden unabhängig von dieser Parameterform mit festem Methodenfehler beantworten oder abweichende Formen vor SDK-Dispatch sicher als ungültig zurückweisen; permanente Array-/Null-Gegenfälle ergänzen. | **Nicht blockierend:** betrifft nur vom authentisierten Absender selbst gelieferten Text; kein Authority-/Pinning-Bypass. Der freigegebene STDIO-Ausgang normalisiert beide Antworten nachweislich. |
| **B4-R3-02 · Low** · neue belegte F2-Fehlannahme über SDK-Semantik | `CryptoWorkBench.Mcp/PinnedMcpStdio.cs:99–105`; F2-Bericht Abschnitt 2, Behauptung über den rohen SDK-Aufruf; `MCP-pinned-client-setup.md`, pauschale Aussage „Der Adapter selbst wiederholt keine Toolaufrufe“ | Das verwendete SDK 2.3.0 implementiert bereits **in `McpClientImpl.SendRequestAsync`** eine MRTR-Schleife. Ein gepinnter Prüfrelay ersetzt einmal nach echtem Editorcommit die Antwort durch `resultType:input_required` mit `requestState`. Derselbe installierte Codex/Adapter erzeugt zwei POSTs und zwei `UpdateEditorV2Async`-Dispatches; beim zweiten wird zusätzlich `requestState` übertragen. [Gegenprobe](../../../b4-r3-independent/mutation-mrtr.log), [SDK-Kopie Z. 678–766](../../../b4-r3-independent/sdk/McpClientImpl.cs). | Die behauptete Einzelsendegarantie folgt nicht aus dieser API-Wahl. Kommentar und Dokumentation müssen zwischen Transportfehler und MRTR-Fortsetzung unterscheiden. Wenn jede automatische Toolfortsetzung ausgeschlossen sein soll, MRTR vor einem erneuten POST explizit ablehnen beziehungsweise eine tatsächlich einmal sendende Ebene verwenden; permanente Gegenprobe ergänzen. | **Nicht blockierend für diesen Kandidaten:** die Probe verlangt eine absichtlich veränderte Antwort der gepinnten Gegenstelle. Der unveränderte Workbench-Toolhandler erzeugt kein `input_required`. Selbst unter Injektion verhindert die originale Revisionsprüfung einen zweiten Effekt: Text/Revision/History bleiben einmal geändert. Ein Netzabbruch allein löst keine Wiederholung aus. |

Für B4-R3-02 wird kein von einem fremden lokalen Server ausführbarer Angriff behauptet: Ein solcher Server scheitert vor HTTP am Pin. Die Gegenprobe ersetzt bewusst die gepinnte Gegenstelle durch einen autorisierten Prüfrelay. Sie widerlegt eine Implementierungs-/Dokumentationsaussage und prüft die darunterliegenden Schutzmechanismen; sie belegt keinen zweiten erfolgreichen Editorcommit.

## 4. R3-S01/S02 – Identität, Portübernahme und Proxy

`PinnedMcpStdio.CreateHttpClient` akzeptiert ausschliesslich HTTPS, numerisches `127.0.0.1`, `/mcp`, keine Query/Fragmente/Userinfo sowie hexadezimale Pin-/Tokenwerte erwarteter Länge. Der TLS-Callback prüft NotBefore/NotAfter und vergleicht SHA-256 der vollständigen Leaf-DER-Daten konstantzeitlich. Er akzeptiert keinen alternativen positiven Chain-/Systemtrustentscheid. `UseProxy=false`, `AllowAutoRedirect=false` und deaktivierte Cookies verhindern zusätzliche Proxy-/Redirectpfade. Es gibt keinen Klartext-Fallback. Bearer und Toolbody werden erst nach der TLS-Peerprüfung geschrieben.

Die Paarung setzt einen authentischen lokalen Dialog und unveränderte lokale Programme voraus. Das Token wird über die Kindprozessumgebung übernommen und im Adapter aus seiner Umgebung entfernt; die aktive HTTP-Verbindung behält es im Speicher. Es ist kein Schutz gegen Speicherzugriff, Codeinjektion oder beliebig manipulierbare Programme/Umgebungen derselben Benutzeridentität. Diese Grenzen entsprechen dem bereits erklärten lokalen Angreifermodell.

**STDIO-Pinning und direkte HTTPS-Schnittstelle sind verschiedene Sicherheitszusagen.** Die Workbench bietet weiterhin HTTPS an, kann aber die Peerprüfung eines beliebigen externen Clients nicht erzwingen. Die aktuelle Pairinganleitung erklärt diesen Unterschied richtig. Der vorherige CA-only-Pfad wird nicht als gleichwertige sichere F2-Konfiguration angeboten. System-Truststores wurden für R3 nicht verändert; deren Irrelevanz für die Akzeptanzentscheidung folgt aus dem exklusiven Callback, ergänzt durch Zusatz-CA-/Bundle-Gegenproben.

Installierter Client in den eigenen Proben: **Codex Desktop/0.166.0-alpha.5.1**, Windows `10.0.26200`, x86_64. Verwendet wurden dessen `app-server --stdio`, isolierte Profile und das unveränderte veröffentlichte F2-Adapterprogramm; keine Modellaufrufe oder Änderungen an der Benutzerkonfiguration.

| Eigene Probe | TCP | Serverseitig erfolgreiche TLS-Handshakes | Entschlüsseltes HTTP | Authorization | Script-Canary | Akzeptierte Fake-Antwort |
|---|---:|---:|---:|---:|---:|---:|
| Fremder separater Portbesitzer, isoliertes CA-Vertrauen | 3 | 3 | 0 | 0 | 0 | 0 |
| Derselbe Angriff mit fremder CA in `SSL_CERT_FILE` | 3 | 3 | 0 | 0 | 0 | 0 |
| Derselbe Angriff mit Workbench-/Fremd-CA-Bundle | 3 | 3 | 0 | 0 | 0 | 0 |
| CONNECT-/TLS-Interception-Proxy, ohne Zusatz-CA | 5 allgemeine Kontakte | 0 | 0 | 0 | 0 | 0 |
| Proxy plus fremde CA in `SSL_CERT_FILE` | 5 allgemeine Kontakte | 0 | 0 | 0 | 0 | 0 |

Vor jeder Portübernahme funktionieren Discovery, echter Status und Lesen des tatsächlichen Headless-Editors. Danach erfolgen tatsächlicher Widerruf, zertifizierter Drain und Hostende. Der separate Ersatzprozess bindet den alten Port; **derselbe Codex-Prozess** sendet drei Edit-Canaries. Der Ersatzprozess kennt den Originalbearer nicht. Die Zähler erfassen Daten vor jeder eigenen Authentisierung. [Isoliert](../../../b4-r3-independent/attack-isolated.log) · [Zusatz-CA](../../../b4-r3-independent/attack-ssl.log) · [Bundle](../../../b4-r3-independent/attack-bundle.log).

Die serverseitigen TLS-Abschlüsse bedeuten nicht, dass der Client den Peer akzeptiert hat: Schannel kann vor der clientseitigen Pinablehnung Erfolg melden. Null entschlüsselte HTTP-Daten und null akzeptierte Fake-Antworten sind die entscheidenden zusätzlichen Messwerte. Die Proxy-TCP-Zahlen enthalten unabhängige Codex-Startupkontakte; der eigene Proxy verwirft Nicht-Loopback-Ziele und leitet sie nicht weiter. **Workbench-CONNECT ist in beiden Fällen null**. Die Interception-Gegenstelle wird zwar scharf geschaltet, der Adapter umgeht sie vollständig. Dies ist kein Test einer erfolgreichen Tunnelphase durch den Adapter oder eines Unternehmens-PAC-Systems. [Proxy](../../../b4-r3-independent/proxy.log) · [Proxy mit Zusatz-CA](../../../b4-r3-independent/proxy-extra-ca.log).

Fehlender, falscher und abgelaufener Pin verhindern Discovery/Status. In einer weiteren Probe startet ein neuer OS-Prozess einen echten Host/Service am alten Port mit neuer Identität und absichtlich demselben Bearer: alte Paarung abgewiesen, neue Paarung mit frischem Codex erfolgreich. Der gleiche Bearer isoliert die Identitätsprüfung von einem blossen 401. [Negative Pins](../../../b4-r3-independent/pins.log) · [Neuer Prozess](../../../b4-r3-independent/restart.log).

## 5. R3-S03 – STDIO, Korrelation, Retry und EOF

Die Bridge enthält keine eigene Editor-, Credential- oder Execution-Autorität. Sie übernimmt den tatsächlichen Katalog und reicht typisierte Parameter/Resultate über das SDK weiter. Der Desktop-Einstieg behandelt `--mcp-stdio` vor GUI-Initialisierung und Console-Unterdrückung. Die eigene direkte STDIO-Probe bestätigt Initialisierung, **exakt elf Toolnamen**, negative/String-Korrelations-IDs und parallele Aufrufe mit korrekter Zuordnung. Zwei Statusresultate stimmen semantisch mit der direkten Antwort desselben echten Service überein. Unbekannte Methoden einschliesslich Array-/Null-`params` werden am STDIO-Ausgang bereinigt. Eine explizite `notifications/cancelled`-Nachricht erreicht den ursprünglichen HTTP-Dispatch als Cancellation; stderr enthält weder Token noch Fehler-Canary. [STDIO-Probe](../../../b4-r3-independent/adapter.log).

### Tatsächlich angewendete Mutation, anschliessend verlorene Antwort

Ein eigener gepinnter Kestrel-Prüfrelay leitet genau den originalen Toolbody an den echten `LocalMcpHost` weiter. Ein transparenter Service-Wrapper zählt tatsächliche `UpdateEditorV2Async`-Aufrufe. Erst nach Empfang des erfolgreichen produktiven Receipts wird die Antwortverbindung abgebrochen. Codex erhält einen Fehler. Danach werden Editor und History direkt beziehungsweise über einen neuen Read desselben Clients geprüft:

- ein HTTP-POST und ein tatsächlicher Edit-Dispatch;
- Text um genau eine synthetische Zeile ergänzt;
- Revision genau um eins erhöht;
- verlorener Receipt bestätigt `textEffect=applied`, `historyEffect=applied`;
- ein Undo stellt exakt den Seed her, danach kein weiterer Undo-Eintrag; Redo stellt genau die eine Änderung wieder her.

Es wurde kein unsicherer Client-Retry gestartet. [Antwortverlustprobe](../../../b4-r3-independent/mutation.log). Der ergänzende MRTR-Fall ergibt zwei Dispatches, aber weiterhin dieselben einmaligen Effekte; siehe B4-R3-02.

### SDK-Abgleich und Grenzen

Das lokale Paket nennt SDK-Commit `9a327829ef066c44a0d737c58bf3c7617d7c701c`. Die Quellen dieser exakten offiziellen Revision wurden separat gelesen. Der Streamable-HTTP-Pfad sendet den POST einmal; seine SSE-GET-Wiederaufnahme wird hier durch `MaxReconnectionAttempts=0` unterbunden. OAuth-Reauth ist nicht konfiguriert. Davon getrennt ist die belegte MRTR-Schleife in `McpClientImpl.SendRequestAsync`; die Wahl dieser API umgeht sie nicht. [Offizieller Transport](https://github.com/modelcontextprotocol/csharp-sdk/blob/9a327829ef066c44a0d737c58bf3c7617d7c701c/src/ModelContextProtocol.Core/Client/StreamableHttpClientSessionTransport.cs) · [Offizielle SendRequest-Implementierung](https://github.com/modelcontextprotocol/csharp-sdk/blob/9a327829ef066c44a0d737c58bf3c7617d7c701c/src/ModelContextProtocol.Core/Client/McpClientImpl.cs).

**EOF ist bei laufendem Aufruf kein sofortiges Cancel.** Die eigene Probe mit gehaltenem Dispatch überschritt acht Sekunden und musste vom Prüfharness beendet werden; beide Beobachtungslogs bleiben erhalten. Der SDK-Sessionabschluss wartet zunächst auf laufende Handler; erst danach fordert der Hosted Service den Hoststopp an. Die konfigurierten fünf Sekunden `ShutdownTimeout` begrenzen diese vorgelagerte Wartephase nicht. [Sessionhandler](https://github.com/modelcontextprotocol/csharp-sdk/blob/9a327829ef066c44a0d737c58bf3c7617d7c701c/src/ModelContextProtocol.Core/McpSessionHandler.cs) · [Hosted Service](../../../b4-r3-independent/sdk/SingleSessionMcpServerHostedService.cs).

Eine zweite, produktnähere EOF-Probe verwendete einen echten angenommenen Execution-Job mit ausstehender lokaler Bestätigung und 2,5 Sekunden Produktfrist. Nach EOF lief der Adapter zunächst weiter, der Editor blieb geschützt. Die ursprüngliche Frist beendete den Job als `TimedOut`, kein unbestätigtes Script wurde ausgeführt, und der Adapter beendete sich anschliessend regulär mit Exitcode 0. [Echte Execution bei EOF](../../../b4-r3-independent/eof-execution.log). Idle-EOF und EOF nach explizitem Cancel enden regulär innerhalb der achtsekündigen Prüffrist.

Diese begrenzte Nachlaufsemantik wird als Restrisiko akzeptiert, nicht als unbegrenzte Shutdownblockade oder unerlaubte GUI-Freigabe. Sie darf weder als sofortiger Jobabbruch noch als Credential-Widerruf beschrieben werden. Massgeblich bleiben Originaljob/Coordinator, Produktfrist und gegebenenfalls der 150-Sekunden-HTTP-Timeout. Zukünftige Clients sollten vor STDIO-Ende explizit abbrechen; ein verbindlicher sofortiger EOF-Abbruch bräuchte eine zusätzliche Brückenregel und einen permanenten Test mit laufender Arbeit.

## 6. R3-L01 – Lifecycle, CNG und Recovery

Die eigene öffentliche-Dateilock-Probe verwendet die **echte** `DesktopMcpSession.StopTransportAsync`-Implementierung und `App.CoordinateClosing`, kein austauschbares Stop-Mock. Beide Varianten halten ein originales Coordinator-Ticket:

| Bedingung | Beobachtetes Ergebnis |
|---|---|
| Leserhandle ohne Delete-Sharing, Freigabe während Retry | Privater Schlüssel weg; Listener gestoppt; Credential verweigert; öffentlicher CA-Pfad anschliessend weg |
| Leserhandle über alle drei Versuche und über den Window-Close hinaus gehalten | Dieselben kritischen Abschlüsse erfolgreich; öffentliches Residuum vorhanden; kein dauerhaft fehlerhafter Stop-Task |
| Wiederholter echter Desktop-Stop | Identische, erfolgreich abgeschlossene Task |
| Originalticket noch offen | Fenster sichtbar, keine neue GUI-Admission trotz erfolgreichem Transportstopp |
| Originalticket freigegeben | Zertifizierter Close, auch bei weiterhin gesperrter öffentlicher CA-Datei |

Öffentliche Bereinigung erfolgt nur für die eigene Datei und das eigene leere Verzeichnis, begrenzt auf drei Versuche mit 100/200 ms Abstand. Ein übrig gebliebener öffentlicher Pfad ist diagnostiziert und kein Beleg für verbleibenden privaten Schlüssel. Die Probe entfernt nach Schliessen ihres Handles ausschliesslich ihr eigenes Residuum. [Dateilock-/Shutdownprobe](../../../b4-r3-independent/r3-cleanup.log).

Unabhängig wiederholte Lifecycle-Fälle decken gehaltenen Transport, Transportfault, tatsächlichen Registry-Cleanup-Fault, kombinierte Faults, Shutdown während Disconnect, wiederholtes Disconnect und verspätete Meldungen ab. Offene Originaltickets verhindern Abschluss; Drain-/Shutdown-Faults lassen den Editor geschützt. Ein erneuter zertifizierter Shellabschluss funktioniert; nach dem Close folgt keine verspätete Wartebenachrichtigung. Diese zusätzlichen Fehlerproben verwenden kontrollierte Fault-Injektionen und ersetzen nicht den oben separat geprüften echten Desktop-Transportabschluss. [Lifecycle](../../../b4-r3-independent/lifecycle.log).

CNG wurde über tatsächliche Windows-Schlüsseldateien geprüft: ein vorhandener privater nicht exportierbarer Sitzungsschlüssel wird bei normalem Dispose entfernt; die öffentliche Datei enthält keinen privaten Schlüssel und lässt sich während der Sitzung nicht überschreiben. Die beobachtete ACL gewährt Everyone/Authenticated Users/Builtin Users kein Schreibrecht. Eigener TEMP/TMP-Fehlerprozess: `IOException`, null neue Container, null Überlebende nach normalem Exit. Injektion innerhalb der öffentlichen Exportgrenze nach nachgewiesenem Import, ungültiger Port bei Hostkonstruktion und tatsächlich belegter Port bei Listenerstart: eigene private und öffentliche Ressourcen entfernt. [Schlüssel](../../../b4-r3-independent/keys.log) · [Tempfehler](../../../b4-r3-independent/constructor-fault.log) · [Initialisierungsfehler](../../../b4-r3-independent/init-failures.log).

Der harte Prozessabbruch wurde nicht nochmals erzwungen. Der vorhandene unabhängige R2-Nachweis über mögliche CNG-/öffentliche Residuen bleibt einschlägig: normale Disposals sind bei einem Kill nicht garantiert. Das ist innerhalb des dokumentierten Modells kein Beweis einer Portübernahme ohne Zugriff auf den privaten Schlüssel. Keine fremden Container wurden gelöscht; es wird kein allgemeiner Crash-Reaper zugesichert.

## 7. R3-P01 – HTTP-/Protokollschutz

Die eigene Raw-TLS-Matrix bestätigt Hostauthentisierung und Revocation: fehlender Bearer und widerrufener Bearer jeweils 401, `Origin:null` 403. `/mcp%2f`, `//mcp`, absolute-form und `/mcp/` werden mit 403 abgewiesen. Doppelte Versions-/Methodenheader, widersprüchliche Methode ohne Versionsheader, unbekannte Bodyversion und moderne Bodyversion mit altem Header ergeben 400 ohne Canary-Echo. Unbekannte Tools mit Integer-/String-ID und gewöhnliche unbekannte Methoden werden bereinigt. Null-/gebrochene numerische IDs werden sicher mit 400 verworfen. Die belegte Array-/Null-Parameter-Ausnahme ist B4-R3-01. [Wire-Matrix](../../../b4-r3-independent/wire.log).

32 lokale Verbindungen ohne TLS-Beginn werden nach der fünfsekündigen Handshakefrist plus Toleranz freigegeben; nach 6,5 Sekunden funktioniert ein authentisierter Request wieder. Klartext-HTTP am TLS-Port erhält keine HTTP-Antwort. Die vollständige MCP-Regression enthält zusätzlich die bestehenden langsamen Header-, Original-POST-, Katalog-/Optionsbindung- und Metadatenfälle. Keine Erweiterung des produktiven Protokollumfangs wurde vorgenommen. [Slow-Probe](../../../b4-r3-independent/slow.log).

## 8. Regression und vorhandene native Evidenz

| Ebene | Debug | Release | R3-Einordnung |
|---|---:|---:|---|
| F2 Core-TRX | 11.532 Pass / 27 NotExecuted | gleich | gelesen, Hash unverändert |
| F2 GUI-TRX | 797 Pass | gleich | gelesen, Hash unverändert |
| F2 MCP regulär | 176 Pass / 13 Explicit-NotExecuted | gleich | gelesen, Hash unverändert |
| F2 explizite Codex-Matrix | 11 Pass | 11 Pass | vorhandene Evidenz, durch eigene andere Gegenproben ergänzt |
| **R3 vollständige Core-Wiederholung** | nicht wiederholt | **11.532 Pass / 27 NotExecuted / 0 Fail** | isolierte Kopie |
| **R3 vollständige GUI-Wiederholung** | nicht wiederholt | **797 Pass / 0 Fail** | isolierte Kopie |
| **R3 vollständige MCP-Wiederholung** | nicht wiederholt | **176 Pass / 13 Explicit-NotExecuted / 0 Fail** | isolierte Kopie |
| F2 native Kombination | nicht separat vorhanden | 1 Pass | Code/Log/Bilder/Artefakte geprüft, nicht nochmals nativ bedient |

Die 27 Core-Ausnahmen umfassen 26 Plattform-/Dateisystemfälle und einen expliziten Benchmark. Die 13 MCP-Explicit-Fälle bleiben im regulären Lauf nicht ausgeführt; F2 startete elf gesondert je Konfiguration sowie einen nativen Kombinationsfall in Release. Der separate kürzere native AES-Fall ist dadurch nicht zusätzlich ausgeführt. R3 deaktivierte keine Tests. Die eigenen Codex-Gegenproben sind Konsolenprogramme mit expliziten Bedingungen, keine zusätzlichen NUnit-Zahlen. [Ausgewertete Matrix](../../../b4-r3-independent/matrix.json) · [R3-Regressionsskript](../../../b4-r3-independent/Regression.ps1) · [R3-TRX](../../../b4-r3-independent/results/).

Die native F2-Evidenz ist nachvollziehbar:

- `Run-NativeAes.ps1` wählt den veröffentlichten win-x64-Apphost als Adapter; gespeicherte Beobachtungen belegen dasselbe Paket für die sichtbare GUI. Paketinhalt/Hashes wurden erneut geprüft. Der veröffentlichte Adapter wurde in R3 tatsächlich erneut verwendet.
- Die native Testmethode verlangt exakt elf Tools und den erwarteten synthetischen Seed, prüft exklusiven Erwerb, History-/Text-Receipt, Revisionskonflikt, AES-Abschluss und den Ciphertext `7649abac8119b246cee98e9b12e9197d`, finalisierte Diagnosen, Release, verworfene alte Lease und GUI-Phase.
- Das native Release-TRX nennt Editor `01786cea-abc1-46cf-95cf-7ceeced24390`, Revision 1 → 2, Job `9ad6b2b1-8f37-4cb0-8ec7-804093053eaa`. Der gespeicherte Ausführungsvorschau-Screenshot zeigt diesen Editor, Revision 2 und das passende Script. Anschliessende zwei Edits desselben Codex gegen den Ersatzserver werden verweigert, HTTP-Zähler null.
- Gespeicherte Undo-/Redo-Bilder zeigen erst den einzelnen Seed, danach wieder die fünf AES-Zeilen. Das Save-/Disconnect-Bild zeigt den tatsächlichen Speicherpfad `C:\Users\stefa\Documents\CryptoScript\ScriptBook.sbk` und `Disconnected — GUI released`. Die anschliessend in die Evidenz kopierte `F2-AES.sbk` enthält genau diese fünf Zeilen und stimmt mit dem dokumentierten SHA-256 überein.
- Undo/Redo/Save stammen aus der **ersten** F2-Sitzung; ihr Gesamttest überschritt die fünfminütige Bedienungsfrist und bleibt als **Failed**-TRX erhalten. Der erfolgreiche durchgehende native Takeoverlauf stammt aus der zweiten Sitzung mit erweitertem ausschliesslich manuellem Wartelimit. Es ist keine einzelne durchgehende Aufnahme aller Schritte.
- Die damalige Abschlusskontrolle dokumentiert null veröffentlichte Workbench-Prozesse, null Listener auf 51837 und null PFX-/DPAPI-Dateien. R3 behauptet hierfür keine neue native Bedienbeobachtung; R3 bestätigt eigene reguläre Adapter-/Testprozessabschlüsse separat.

[Native Assertions](../../CryptoWorkBench.Mcp.Tests/CodexCompatibilityTests.cs) · [Native TRX](../../artifacts/b4-f2/native-aes-takeover.trx) · [Ausführungsvorschau](../../artifacts/b4-f2/execution-preview-final.jpg) · [Undo](../../artifacts/b4-f2/undo.jpg) · [Redo](../../artifacts/b4-f2/redo.jpg) · [Save/Disconnect](../../artifacts/b4-f2/saved-disconnected.jpg) · [F2-Prozessabschluss](../../artifacts/b4-f2/final-cleanup.json).

## 9. Build, Plattformen und akzeptierte Grenzen

Die vollständigen Debug-/Release-Builds und Locked Restores aus F2 wurden anhand ihrer Logs/Artefakte geprüft; R3 hat den Kandidaten nicht erneut gebaut. Der Publishvalidator wurde erneut erfolgreich ausgeführt: Apphost, verwaltete Abhängigkeiten, fünf native Assets und 133 InfoDocs. Das frameworkabhängige Paket verlangt korrekt `Microsoft.NETCore.App` und `Microsoft.AspNetCore.App` 10.0. Der Desktopprojektverweis auf MCP erklärt die ASP.NET-Abhängigkeit; der integrierte Adapter erfordert kein zweites Paket/Apphost. [R3-Publishprüfung](../../../b4-r3-independent/publish-validation.log).

Statisch bleibt normaler Linux-/macOS-Desktopstart möglich, ohne MCP zu aktivieren: `DesktopMcpSession.Attach` versteckt den Aktivierungsbutton ausserhalb Windows; der normale Einstieg startet keinen Listener. `GetTickCount64` wird nur im Windows-Zweig der Lease-Uhr aufgerufen. Eine eigene Probe mit werfender nicht unterstützter Lease-Uhr und ohne MCP-Aktivierung erlaubt weiterhin normale GUI-Admission. Dies ersetzt keinen nativen Plattformstart. Linux-/macOS-RID-Locks allein sind kein Laufzeitnachweis; native Plattformvalidierung bleibt offen.

Akzeptierte Restrisiken sind die zwei begründeten Low-Findings, EOF-Nachlauf bis zum originalen Abschluss/Timeout, öffentliche Residuen nach ausgeschöpfter Bereinigung, Crashresiduen, lokale Verfügbarkeitsangriffe durch Portbelegung/Floods und die erklärten Grenzen gegenüber gleichberechtigtem Prozess-/Schlüsselzugriff. AP0-D02 bleibt prozessgebunden: keine neue Paarung oder Quotenrücksetzung im selben Workbench-Prozess. B4/F2 schwächen im geprüften Pfad weder Originalticketbindung noch CAS-Revisionen, Execution-ID-/Fingerprintprüfung oder zertifizierte GUI-Rückgabe.

## 10. Reproduktion und Abschluss

Die Reviewprogramme sind keine Produktänderungen. `Prepare.ps1` kopiert Kandidaten-Binaries ins eigene Prüfverzeichnis; `Compile.ps1` verwendet die auf diesem Rechner vorhandenen .NET-10-Referenzpfade. Aus `b4-r3-independent`:

```powershell
./Prepare.ps1 -Candidate ../mcp-ap3
$env:R2_CODEX = (Get-Command codex).Source
$env:R3_ADAPTER = (Resolve-Path '../mcp-ap3/artifacts/b4-f2/publish/win-x64/CryptoWorkBenchAvalonia.Desktop.exe').Path
dotnet probe/CryptoWorkBenchGuiTest.dll attack isolated
dotnet probe/CryptoWorkBenchGuiTest.dll attack ssl
dotnet probe/CryptoWorkBenchGuiTest.dll attack bundle
dotnet probe/CryptoWorkBenchGuiTest.dll proxy extra
dotnet probe/CryptoWorkBenchGuiTest.dll pins
dotnet probe/CryptoWorkBenchGuiTest.dll restart
dotnet probe/CryptoWorkBenchGuiTest.dll mutation
dotnet probe/CryptoWorkBenchGuiTest.dll mutation-mrtr
dotnet probe/CryptoWorkBenchGuiTest.dll adapter
dotnet probe/CryptoWorkBenchGuiTest.dll eof-execution
dotnet probe/CryptoWorkBenchGuiTest.dll r3-cleanup
dotnet probe/CryptoWorkBenchGuiTest.dll init-failures
dotnet probe/CryptoWorkBenchGuiTest.dll constructor-fault
dotnet probe/CryptoWorkBenchGuiTest.dll lifecycle
dotnet probe/CryptoWorkBenchGuiTest.dll wire
dotnet probe/CryptoWorkBenchGuiTest.dll slow
dotnet probe/CryptoWorkBenchGuiTest.dll keys
./Regression.ps1
```

`wire` protokolliert ausdrücklich auch positive Canary-Echos; sein Exitcode allein bedeutet keine Fehlerfreiheit. `mutation-mrtr` bestätigt die erwartete Gegenbeobachtung von zwei Dispatches und gleichzeitig nur einem Effekt. `adapter-active` ist die zusätzliche absichtlich festgehaltene Dispatch-Probe: ihre Achtsekundenannahme schlägt fehl und ist keine bestandene EOF-Abbruchprüfung. Der reale begrenzte Execution-Fall wird getrennt ausgewertet. Die erste Execution-Probe hatte mangels OutputRead-Grant einen falschen Testumfang (`IncludeText`) und erhielt korrekt `Forbidden`; mit `MetadataOnly` läuft die oben beschriebene Prüfung. Diese Prüfaufbaufehler wurden nicht als Produktregressionen gezählt und ihre Logs nicht als erfolgreiche Ergebnisse ausgegeben.

Bei Abschluss stimmen alle 41 Eingangshashes weiterhin; Branch, HEAD und leerer Index sind unverändert. Alle eigenen privaten Zertifikatsdateien sind entfernt; keine eigenen Review-/Adapterprozesse oder Testlistener bleiben zurück. [Abschlusskontrolle](../../../b4-r3-independent/final-cleanup.json) · [Evidenzinventar](../../../b4-r3-independent/evidence.sha256). Keine Commits, Pushes, Merges oder master-Updates.

**READY FOR PLATFORM VALIDATION.** Die belegten Sicherheits- und Shutdownblocker sind geschlossen. Die zwei Low-Restpunkte und die dokumentierten Grenzen rechtfertigen unter dem aktuellen Workbench-Protokoll und Angreifermodell keine Windows-Sperre; sie bleiben sichtbar für gezielte spätere Korrektur und weitere Client-/SDK-Entwicklung.
