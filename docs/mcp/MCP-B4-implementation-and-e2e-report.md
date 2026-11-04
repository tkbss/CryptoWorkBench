# MCP-B4 · Integration und End-to-End-Abnahme

> Historische B4-Evidenz. Die damalige direkte HTTP-Konfiguration ist durch das [gepinnte F2-STDIO-Pairing](MCP-pinned-client-setup.md) ersetzt; nicht als aktuelle Verbindungsanleitung verwenden.

3. November 2026 · **BLOCKED für die plattformübergreifende MVP-Abnahme.** Der installierte Codex-Client hat den sichtbaren Windows-Editor gelesen, exklusiv ergänzt, den AES-Test ausgeführt und denselben Editor mit funktionierender History und Save an die GUI zurückgegeben. Linux-Laufzeitregression und macOS-Build/CI für diesen uncommitteten Kandidaten stehen aus.

## Ausgangsbasis und Umfang

Der Worktree `mcp-ap3` stand vor der ersten Änderung auf `feature/mcp-ap3`, HEAD `6806a34ebd75c97eab7fc17a4d4a5309a6d9f009`. Index, getrackter und ungetrackter Worktree waren sauber. B1 stellt den sichtbaren Editor, B2 die Ausführungsregistry und die gemeinsame Engine, B3 die v2-Tools, Credentials und Lease-Zustandsmaschine. Auf der Baseline existierten ein isolierter v1-Status-Testlistener und die Registrierung der elf v2-Tools. Im Desktop fehlten HTTP-Listener, lokales Pairing und die Verbindung zwischen authentisiertem Request und B3-Credential. [B1](MCP-B1-N1-R-final-acceptance-addendum.md) · [B2](MCP-B2-N1-final-native-acceptance.md) · [B3](MCP-B3-final-acceptance.md) · [AP0](MCP-00-AP0-contract-finalization.md) · [Transport- und Sicherheitsvertrag, §§6–7](MCP-00-architecture.md)

## Implementierung und Bedienung

Der Windows-Desktop bietet **Connect Codex…** an. Erst **Allow access** aktiviert einmal je Workbench-Prozess die gemeinsame Automation-Service-Instanz. Der Prozess erzeugt 32 kryptografisch zufällige Bytes. Nach erfolgreicher Bindung zeigt ein lokaler Dialog Endpoint, Instanz-ID und das hexadezimale Sitzungstoken. Die Schaltfläche **Disconnect Codex** widerruft die Sitzung. Für neues Pairing ist ein neuer Workbench-Prozess erforderlich. Die Anzeige wird auch bei einem belegten Port nicht auf einen anderen Endpoint umgeleitet. [Desktop-Einstieg](../../CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs) · [HTTP-Host](../../CryptoWorkBench.Mcp/LocalMcpHost.cs)

Für den Client genügt folgende Serverkonfiguration. Das Token gehört als `CRYPTOWORKBENCH_MCP_TOKEN` in die Umgebung des für die Sitzung gestarteten Clients. Der Wert gehört weder in TOML noch in Kommandozeile, Script Book oder Chat. Das frameworkabhängige Desktop-Paket benötigt jetzt die .NET-10- und ASP.NET-Core-10-Laufzeiten. Die geprüften Restore-Locks decken den neuen Desktop-Projektverweis und den MCP-Graphen ab. [Konfiguration in OpenAI Docs](https://developers.openai.com/codex/mcp/) · [Publish-Prüfung](../../.github/scripts/Validate-DesktopPublish.ps1)

```toml
[mcp_servers.cryptoworkbench]
url = "http://127.0.0.1:51837/mcp"
bearer_token_env_var = "CRYPTOWORKBENCH_MCP_TOKEN"
tool_timeout_sec = 150
```

Der lokale Principal erhält Status, Editor-Lesen/-Schreiben/-Kontrolle, Variablenmetadaten, Ausführung und Lesen eigener Ergebnisse. Roh-Ausgabe erfordert zusätzlich die Output-Option und die lokale Bestätigung. Ein Replace mit Inhalt und jede neue Ausführung durchlaufen die vorhandenen lokalen Dialoge. Der Desktop injiziert Transportstart und -stopp in seinen bestehenden Shell-Lifecycle. Coordinator, Dokumentregistrierung, Undo-Stack, Save-Pfad und Interpreter bleiben die bestehenden Besitzer. Der Windows-Desktop schliesst zusätzlich seine geerbten Textausgaben für Anwendungsdaten, da CryptoScript auch bei vorhandenem Event-Abnehmer `Print` nach stdout schreibt.

## Windows-Evidenz

Windows 10.0.26200 x64, .NET SDK 10.0.401, MCP-SDK 2.3.0, installierter Codex `0.166.0-alpha.5.1`. Der Client lief als echter `codex app-server` mit temporärer Konfiguration und ohne Modellaufruf. Der erste native Release-Prozess war PID 41704. Die lokale Sitzung wurde vom Benutzer ausdrücklich für den synthetischen Test freigegeben. Die Eingaben und Dialogbeobachtungen erfolgten per Windows-Automation. Es gab in B4 keinen physischen Schweizer Tastaturtest.

| Fall | Art | Tatsächliche Beobachtung |
|---|---|---|
| E2E-01 | Installierter Codex, HTTP, isolierter Status-Testhost | Die zuvor explizit ausgelassene Probe `InstalledCodexDiscoversAndCallsStatus` entdeckte das Tool und rief es genau einmal auf. Beobachtete HTTP-Protokollheader enthielten `2024-11-05` und `2025-06-18`, einzelne Requests hatten keinen Header. Diese Probe verwendet den Fake-Service. Der folgende Durchlauf verwendet den Desktop. |
| E2E-02 | Installierter Codex, echter Desktop | Der lokal eingegebene 63-Zeichen-AES-Schlüsselbefehl war unter Editor `67884231-1dda-4e8e-858c-13438faf8079` auf Revision 1 lesbar. Acquire übernahm diesen Editor. Ein zweiter Acquire wurde abgewiesen. Ein Append fügte vier sichtbare Anweisungen hinzu, Receipt `Applied/Applied/Complete`, Revision 2. Ein anschliessender Edit mit Revision 1 wurde verworfen. |
| E2E-03 | Installierter Codex mit lokaler Bestätigung, anschliessend GUI-Vergleich | Job `ca5af59c-d5a1-4c8b-96c0-15edbca8f960` verschlüsselte den ersten AES-128-CBC-Block des Repository-Testvektors ohne Padding. Er endete mit `completed`, ohne Fehler, mit finaler Diagnose und dem Ciphertext `7649abac8119b246cee98e9b12e9197d`. Der reguläre Script-Book-Import meldete alle Zeilen geparst. Interaktives `Print(ct)` zeigte dieselben Bytes. |
| E2E-04 | HTTP-Release, native GUI, Dateiprüfung | Die Probe verglich nach Release den vollständigen Quelltext und prüfte `phase=gui`. Ein GUI-Undo nahm den gesamten Append zurück, Revision 3, Länge 63. Redo stellte ihn wieder her. Die synthetische Unicode-Eingabe `ä€ê` ergab Revision 5, Länge 228. Nach Undo speicherte die normale GUI das neue Buch mit genau den fünf erwarteten Zeilen. SHA-256 `3F67C54DAD03AFEE61F6ECCBC153E6C17378C9E930611DB6EF38903D11FDE6D0`. |
| E2E-05 | Authentisierte HTTP-Requests an denselben sichtbaren Desktop | Ein neuer Erwerb für `Print(` und eine lokal bestätigte Ausführung ergaben für Job `52061b2b-b0a4-4773-96e1-3e4c046cf13f` den Zustand `syntaxError` und eine finale Diagnose. Release gelang. Ein weiterer Erwerb und bestätigtes SecureSave erreichten `waitingForUser`. Nach Schliessen der ursprünglichen HTTP-Verbindung war genau dieser Job weiterhin in diesem Zustand abfragbar. Ein separater Cancel beendete Job `167cc360-d41f-41cb-b234-73c51cd1c8e8` mit `cancelled/explicit`. Kein Passwort eingegeben, Zieldatei fehlte, Release ergab `released`, Status `gui` und `mutationBusy=false`. |
| Widerruf und Shutdown | GUI und separater HTTP-Client | Disconnect deaktivierte die Sitzungssteuerung. Der vorher authentisierte Statusclient bekam am geschlossenen Port „connection refused“. Der anschliessende normale Fensterschluss beendete den Prozess. Shutdown bei laufenden ursprünglichen Produzenten, verspätete Callbacks und fehlgeschlagener Drain haben separate automatisierte Abdeckung. |

Die AES-Probe prüft vor jeder Übernahme den exakten Wegwerf-Seed. Ein früher Pairingversuch endete beim Statusaufruf mit Codex-RPC-Fehler vor dem Erwerb. Der erfolgreiche Lauf nutzte ein verifiziertes Pairing. Es gab keine automatische Wiederholung eines Edits mit unbekanntem Ausgang. Die gespeicherten Belegbilder enthalten ausschliesslich synthetische Scriptdaten. [Codex-Proben](../../CryptoWorkBench.Mcp.Tests/CodexCompatibilityTests.cs) · [AES-TRX](../../artifacts/b4/desktop-aes.trx) · [Bestätigung](../../artifacts/b4/execution-confirmation.png) · [Nach Release](../../artifacts/b4/after-release.png) · [GUI-Vergleich](../../artifacts/b4/gui-comparison.png) · [Gespeichertes Buch](../../artifacts/b4/B4-AES-67884231.sbk)

**Gegenlauf am finalen Release.** Neuer Prozess 33892, frisches Pairing und der tatsächlich installierte Codex. Die Probe verglich alle elf entdeckten Toolnamen, prüfte `EditorOwned` beim zweiten Erwerb, `RevisionConflict` beim veralteten Edit und denselben AES-Ausgabewert. Editor `9cedd9d0-dbda-428f-956f-bea8abd899c7`, Revision 1→2, Job `ef4885c1-3643-4c66-b1aa-843f53b17779`. Nach Release wurde ein Edit mit der verbrauchten Lease als `LeaseRevoked` mit `None/None` und unveränderter Revision abgewiesen. Ein neuer Acquire gelang auf demselben Editor. Reguläres Alt+F4 beendete den Prozess mit dieser aktiven Lease. Die während des gesamten Durchlaufs umgeleiteten Desktop-Streams stdout und stderr waren beide 0 Bytes lang. SHA-256 der geprüften Desktop-DLL `31F77B468D03CCC6A1EC4CD5F5688040BAA0EDDBB40B4BC200EA895A3AC7C032`, der MCP-DLL `FEBDD2340C3CDED9705D5B9CCAB23C40976327F3557AD40C0A74DC65C72FEA76`. [Finale AES-TRX](../../artifacts/b4/desktop-aes-final.trx)

## Security und Lifecycle

* Der Host startet ohne geerbte Kestrel-Konfiguration und ohne Desktop-Commandline. Er bindet IPv4-Loopback, prüft numerischen Host samt Port, exakten MCP-Pfad und fehlende Query, weist vorhandene Origin-Header einschliesslich `null` ab und prüft den Bearer vor dem SDK-Routing. Discovery und Status unterliegen demselben Schutz. Der Vergleich erfolgt konstantzeitlich. Requestbody maximal 1 MiB, gleichzeitig maximal 32 Verbindungen. Der Transport hat keinen Log-Provider für Header, Payload oder Antwortinhalt.
* Stateless Streamable HTTP stellt elf `workbench_`-Tools bereit. Status, Variablenmetadaten und Editor lesen; Editor erwerben, verlängern, freigeben und ändern; Editor ausführen, Ausführung und Diagnosen lesen sowie Ausführung abbrechen. Der Resolver gibt ausschliesslich das für diesen Listener ausgestellte, hostgebundene Credential zurück. Die B3-Prüfung für Original-POST, Toolkatalog, aktive Epoche, Grants, Dokument, Lease und Revision bleibt aktiv. [v2-Bindung](../../CryptoWorkBench.Mcp/WorkbenchToolsV2.cs)
* Der HTTP-Test prüft fehlenden und falschen Bearer, unzulässigen Host, Origin, erfolgreichen Dispatch und sofortige Ablehnung nach Widerruf. Vor Authentisierung und nach Widerruf erreicht kein Testaufruf den Resolverpfad zum Service. Der Beleg für verlorene Verbindungen und tatsächlichen Jobabbruch steht in E2E-05. [Hostregression](../../CryptoWorkBench.Mcp.Tests/LocalMcpHostTests.cs) · [HTTP-Races](../../CryptoWorkBench.Mcp.Tests/B3F4WireTests.cs)
* AP0-D02 bleibt prozessgebunden. Die GUI stellt höchstens ein Pairing pro Dienst aus, die alten Credentials und Acquisition-Tombstones bleiben nach Widerruf unbrauchbar. B3 behält 4.096 Prozessslots, höchstens 256 je Principal. Der App-Shutdown widerruft über den Service, schliesst den Listener und wartet auf Quieszenz vor dem finalen Close. Der Transport autorisiert weder eine Epochenrotation noch eine vorzeitige GUI-Wiederzulassung. [D02-Abnahme](MCP-B3-final-acceptance.md) · [Lease-/Drain-Tests](../../CryptoWorkBenchGuiTest/LeaseV2Tests.cs)

## Altbefunde und technische Gegenprüfung

| Finding | Disposition |
|---|---|
| F2R-10 | In B3 behoben. `FinishCommit` prüft die ausführbaren Stacks vor und nach dem Commit. Eine transiente Benachrichtigung bei beidseitig leerer History bestätigt keinen Effekt. B4 übernimmt die vorhandenen Regressionen und prüft den erfolgreichen Receipt gegen echtes Undo/Redo. [B3, D01](MCP-B3-mcp-binding-lease-v2.md) |
| IR-N1-01 / AP3-P8/AP5 | B2 bindet die Fortsetzung an das originale native Newline-Ergebnis und dessen Generation. `PrivateReturnDeadlineRejectsBothJobAndNewline` und `RegistrationExceptionRejectsNewlineAndAllowsNextReturn` sind Teil der vollständigen GUI-Suite. |
| AP3-P6 | Die gepaarte UpdateStarted/UpdateFinished-Sperre unterdrückt den synthetischen Finish einer verworfenen Gruppe. `RejectedUpdateDoesNotRecursivelyDeliverFinished` prüft beide History-Fälle. Die akzeptierte Grenze fremder früher Raw-Handler bleibt bestehen. [B1](MCP-B1-editor-integration.md) |
| N1-F02 | Die ausgelieferte Info-Dokumentation hatte beide Modifikatoren vertauscht. Korrigiert auf Ctrl+PageUp/PageDown für Info und Alt+PageUp/PageDown für Editorzeilen. Die GUI-Regression prüft beide Paare im vorgesehenen Empfänger. Das neue Windows-Fenster zeigte den korrigierten Text. |
| Dokumentidentität, IME | Keine neue Dokumentauswahl und kein zweiter Editorbesitzer. Originalregistrierung, History und Sperren laufen durch die bestehenden B1–B3-Tests. Die B1-Evidenz für physische Schweizer Eingabe bleibt historisch, die akzeptierte CJK-IME-Grenze unverändert. |

Die Eigenprüfung verfolgte den Produktionspfad vom Start über HTTP-Middleware, v2-Admission, Snapshot-Bestätigung und Freigabe bis zum Shutdown. Sie verglich die tatsächlichen Publish-Abhängigkeiten mit dem CI-Prüfer. Der Desktop benötigt durch das eingebettete Kestrel beide Shared Frameworks. Der Publish-Prüfer kontrolliert nun beide und die MCP-Assembly. Dieser Bericht ist die technische Ausführungsrunde vor dem unabhängigen Review.

## Regression und Plattformen

Beide Repository-Lösungen wurden mit dem gepinnten Prism-Checkout `358118cd640d9a22ff8cf21c8ad197fa038b7990` und Locked Restore gebaut. Die Tabelle zählt die einzelnen TRX-Resultate. Die NUnit-Konsolensumme lässt explizite Fälle im normalen Lauf teilweise weg. [Debug-Build](../../artifacts/b4/final-build-Debug.log) · [Release-Build](../../artifacts/b4/final-build-Release.log)

| Windows-Suite | Debug bestanden / nicht ausgeführt | Release bestanden / nicht ausgeführt | Fehler |
|---|---:|---:|---:|
| CryptoScriptUnitTest | 11.532 / 27 | 11.532 / 27 | 0 |
| CryptoWorkBenchGuiTest | 790 / 0 | 790 / 0 | 0 |
| CryptoWorkBench.Mcp.Tests | 161 / 2 | 161 / 2 | 0 |

Die 27 Kernfälle umfassen 26 Plattform-/Dateisystemfälle und den expliziten Hardware-Benchmark. Die zwei expliziten MCP-Fälle sind Discovery/Status und der interaktive Desktop-AES-Test. Die separat ausgeführten Clientproben bleiben ausserhalb der regulären Summen. Ein erster Release-Statuslauf bestand die fachlichen Assertions, scheiterte jedoch beim Löschen des temporären Clientverzeichnisses. Der Test wartet jetzt nach EOF auf den Prozessabschluss und wiederholt ausschliesslich die Bereinigung bei kurz gesperrten Dateien. Danach bestanden je drei Einzelaufrufe in Debug und Release. [GUI Debug](../../artifacts/b4/CryptoWorkBenchGuiTest-Debug.trx) · [GUI Release](../../artifacts/b4/CryptoWorkBenchGuiTest-Release.trx) · [MCP Debug](../../artifacts/b4/CryptoWorkBench.Mcp.Tests-Debug.trx) · [MCP Release](../../artifacts/b4/CryptoWorkBench.Mcp.Tests-Release.trx) · [Core Debug](../../artifacts/b4/CryptoScriptUnitTest-Debug.trx) · [Core Release](../../artifacts/b4/CryptoScriptUnitTest-Release.trx) · [Codex Debug](../../artifacts/b4/codex-status-Debug-3.trx) · [Codex Release](../../artifacts/b4/codex-status-Release-3.trx)

Die native Probe lässt sich mit einem neuen Wegwerffenster wiederholen. Als Ausgangstext genau die erste Zeile des gespeicherten B4-Buchs ohne Zeilenumbruch eingeben. Pairing lokal erlauben, Token ausschliesslich dem Testprozess bereitstellen, `CRYPTOWORKBENCH_CODEX_PATH` auf den installierten Client setzen und bei laufendem Desktop folgenden Test einzeln auswählen. Vor **Execute** Script, Revision, Arbeitsverzeichnis und Ausgabemodus im Dialog prüfen.

```powershell
dotnet test CryptoWorkBench.Mcp.Tests -c Release --no-build --filter FullyQualifiedName~InstalledCodexCompletesDesktopAes
```

| Plattform | Evidenz und Grenze |
|---|---|
| Windows x64 | Beide Builds und vollständige Suiten, echter installierter Client, sichtbare GUI, lokales Pairing, Vergleichsausführung, Dateispeicherung, Widerruf und regulärer Prozessschluss. Locked RID-Restore und frameworkabhängiges Publish bestanden. Der CI-Prüfer validierte Apphost, verwaltete Abhängigkeiten, fünf native Assets und 133 InfoDocs. Die Schweizer Hardwareeingabe ist nur in der verlinkten B1-Abnahme physisch bestätigt. [Publish-Prüfung](../../artifacts/b4/validate-win-x64.log) |
| Linux x64 | Locked RID-Restore und Cross-Publish auf Windows bestanden. Eine statische Kontrolle prüfte x86-64-ELF, RID-Manifest, zwei native Assets und alle 133 InfoDocs mit Hashvergleich. Auf dem Windows-Rechner fehlt die WSL-Laufzeit, ein Docker-, Podman- oder VM-Runner stand ebenfalls nicht zur Verfügung. Linux-Dateirechte, Prozesssignale, Socket-Lifecycle und GUI-Eingabe sind auf Linux ungeprüft. Build und Regression auf einem Linux-Host bleiben offen. [Cross-Publish](../../artifacts/b4/publish-linux-x64.log) · [Statische Kontrolle](../../artifacts/b4/validate-linux-cross.log) |
| macOS arm64 / x64 | Beide RID-Graphen wurden auf Windows mit den aktualisierten Release-Locks wiederhergestellt. Es gibt hier keinen macOS-/Xcode-Runner. Der native Dateisystem-Shim, seine Signatur und die macOS-Suiten erfordern den macOS-Build. Die vorhandene CI-Matrix ist für diesen uncommitteten Stand nicht gelaufen. [CI-Matrix](../../.github/workflows/dotnet.yml) |

## Risiko und Entscheidung

Die Produktaktivierung ist auf Windows beschränkt. B3 verwendet produktiv `GetTickCount64`; andere Plattformen liefern im Lease-Pfad `TimerUnavailable`. B4 zeigt dort den Aktivierungsbutton nicht an. Eine suspendfeste Zeitquelle erfordert einen eigenen Plattformnachweis. Der stärkste verbleibende Nachweisbedarf liegt bei Linux-Prozess-/Dateisystemverhalten und macOS-Build/CI. Der lokale Bearer ist bis zum Widerruf eine Berechtigung für jeden Prozess, der ihn besitzt. Deshalb erfolgt die Übergabe lokal und flüchtig, und wegen der vollen CryptoScript-Fähigkeiten bleibt jede neue Ausführung an eine sichtbare Snapshot-Bestätigung gebunden.

**Empfehlung: BLOCKED.** Der Windows-Ablauf ist für die fachliche Gegenprüfung dokumentiert. Für die plattformübergreifende Abnahme fehlen die oben genannten Runner-Ergebnisse. Unabhängiger Gesamt-Review und abschliessende Eigentümerfreigabe stehen aus. Sämtliche B4-Änderungen liegen ungestagt im Zielworktree. TRX, Bilder, Buildlogs und Wegwerf-Buch unter `artifacts/b4` sind lokale, von Git ignorierte Evidenz.

Die abschliessende Windows-Prozess- und Socketprüfung fand weder PID 41704 oder 33892 noch einen Listener auf Port 51837. HEAD entsprach weiter der Eingangsbasis, der Index war leer, `git diff --check` und die lokale Linkprüfung des Berichts bestanden.
