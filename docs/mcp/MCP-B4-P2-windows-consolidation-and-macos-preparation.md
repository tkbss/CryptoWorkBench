# MCP-B4-P2 – Windows Consolidation and macOS Preparation

**Gesamturteil: READY FOR MACOS CI.** 4. November 2026. Die fünf Linux-Testkorrekturen sind integriert; vollständige Windows-Debug-/Release-Regressionen sind ohne Fehler bestanden. Dies ist keine macOS-Abnahme und keine Freigabe zum Committen oder Veröffentlichen.

## 1. Auftrag, Basis und Abgrenzung

Branch `feature/mcp-ap3`, HEAD `6806a34ebd75c97eab7fc17a4d4a5309a6d9f009`, leerer Index. Vor der Integration lagen 21 getrackte Änderungen und 21 ungetrackte Kandidatendateien aus B4/F1/F2/R1–R3 vor. Alle **1.033** inventarisierten Quelldateien waren bytegleich mit dem ursprünglichen P1-Windows-Export. Der vollständige binäre Eingangsdiff wurde vor Änderungen gesichert; seine SHA-256 stimmt mit dem Linux-Rücktransfermanifest überein. Kein vorhandener Kandidateninhalt wurde verworfen.

Dies ist eine Testintegration mit Windows-Regression und statischer CI-Vorbereitung, **kein neuer Security-Review**. R3 bleibt `READY FOR PLATFORM VALIDATION`. `LINUX VALIDATED` ist der vom Auftraggeber übergebene P1-Status. Das Rücktransferpaket enthält Testquellen und eine Ergebniszusammenfassung, aber keinen vollständigen P1-Bericht und keine Linux-TRX-/GUI-Belege. P2 hat daher keine Linux-Abnahme neu durchgeführt oder deren vollständige Evidenz unabhängig erneut geprüft.

Eigene Evidenz liegt unter [`../../../b4-p2-preparation-20261104/`](../../../b4-p2-preparation-20261104/). Eingang: [Quellinventar](../../../b4-p2-preparation-20261104/before-source-inventory.json), [Git-Status](../../../b4-p2-preparation-20261104/before-status.txt), [binärer Diff](../../../b4-p2-preparation-20261104/before-tracked.patch).

## 2. Verifizierter Linux-Rücktransfer

Quelle: `C:\Users\stefa\Documents\CryptoWorkBench-B4-P1-linux-test-fixes.zip`, 217.411 Bytes.

| Nachweis | SHA-256 |
|---|---|
| Rücktransfer-ZIP, mit externer Prüfsumme verglichen | `c05c8459c5bb18bc7baaabd3973a300fc8f78376cff2094d0ff83bfb56ce70d6` |
| Manifest | `4cf5057275380b7eeb451507992724df12b7c2aa0b3c692eae6a73e4a96d60f3` |
| Korrekturpatch | `608764cd923eaae093dd644f009cfb732891cd8f6cfc142bf03b73eabd1f3e69` |
| Im Manifest gebundener ursprünglicher Kandidatendiff | `f563906aaeb371d3d809f655a14ef3f78e1d39a8ac8c62137a420d029545d94e` |
| Im Manifest gebundener ursprünglicher Transfer | `806bb69c2b2e1b56d3af36188f9d9d58f99e8b846b98603489836ed67639b41f` |

Alle 17 Einträge des Paketinventars, Dateiliste, Baseline, Before-/After-Hashes und Patchhash wurden geprüft. ZIP-Mitgliedspfade wurden vor dem Entpacken kontrolliert. Die zuerst unter Documents bereitgestellte ZIP war irrtümlich der ursprüngliche Windows-Export; aus ihr wurde nichts importiert. Nach Bereitstellung der richtigen Datei wurde ausschliesslich das oben identifizierte Paket verwendet.

Der gelesene Importer prüft zuerst alle fünf Ziele gegen die exakten Before-Bytes. Diese stimmen mit Windows überein; **kein Drei-Wege-Merge war nötig**. Erst danach wurden die fünf After-Dateien bytegenau übernommen und erneut gehasht. Die darin enthaltenen Zeilenenden wurden nicht pauschal normalisiert. [Manifest](../../../b4-p2-preparation-20261104/linux-test-fixes/manifest.json), [Patch](../../../b4-p2-preparation-20261104/linux-test-fixes/test-fixture-corrections.patch), [Preflight](../../../b4-p2-preparation-20261104/import-preflight.log), [Import](../../../b4-p2-preparation-20261104/import-apply.log).

## 3. Exakter Integrationsumfang

| Datei | Erforderliche Änderung und Wirkung |
|---|---|
| `CryptoWorkBenchGuiTest/B1EditorIntegrationTests.cs` | Der gemeinsame Test-Host setzt bei selbst erzeugtem Service eine konstante synthetische Lease-Uhr (`1_000UL`). Explizit übergebene Services behalten ihre eigene Uhr. Keine produktive Plattformfreischaltung. |
| `CryptoWorkBenchGuiTest/B3F1Tests.cs` | Ein explizit erzeugter Service im Grant-/Tombstone-Test erhält dieselbe Test-Lease-Uhr; die separate Registry-Uhr bleibt unverändert. |
| `CryptoWorkBenchGuiTest/B3F2Tests.cs` | Dasselbe für den parametrisierten Job-ID-/History-Berechtigungstest. |
| `CryptoWorkBenchGuiTest/LeaseV2Tests.cs` | Zusätzlicher Gegenfall mit ausdrücklich unverändertem Produktionsservice: Windows kann erwerben/freigeben; andere Plattformen liefern `TimerUnavailable`, halten keine Lease und lassen normale GUI-Eingabe zu. |
| `CryptoWorkBenchGuiTest/NativeEnterContinuationTests.cs` | Sieben hart kodierte CRLF-Erwartungen werden für Dokumente ohne bestehende Zeilentrenner durch `Environment.NewLine` ersetzt. Zwei zusätzliche Fälle prüfen ausdrücklich vorhandenes LF beziehungsweise CRLF samt Undo/Redo und abgeschlossenem Coordinator. |

Die neuen drei Testfälle erhöhen die Windows-GUI-Zahl von 797 auf 800. Bestehende gezielte Ablauf-/Expiry-Tests injizieren weiterhin ihre eigenen fortschaltbaren Uhren; die gemeinsame konstante Uhr ist kein Test realer Zeitabläufe. Die explizite Produktionsuhr-Gegenprobe verhindert, dass die Testkonfiguration als produktive Linux-Unterstützung missverstanden wird. Das Paket meldet für Linux je Konfiguration 799 GUI-Pass/1 Windows-Skip und 129 bestandene betroffene Fälle; diese Angaben sind **übergebene P1-Ergebnisse**, keine P2-Windows-Messwerte.

Zusätzlich wird ausschliesslich die aktuelle Anleitung `docs/mcp/MCP-pinned-client-setup.md` zur MRTR-/EOF-Grenze und zur Trennung von Desktop-Abnahme und MCP-Aktivierung präzisiert; dieser P2-Bericht kommt neu hinzu. Historische B4-/F1-/F2-/R1-/R2-/R3-Berichte bleiben bytegleich. Auch der bekannte unzutreffende MRTR-Kommentar im Produktcode wird in dieser reinen Test-/Dokumentationsintegration nicht geändert; die aktuelle Anleitung und Abschnitt 6 benennen seine Einschränkung ausdrücklich.

**Keine Produktdatei geändert:** Transport, Pinning, Credentials, Lease-Implementierung, Editor, Coordinator, Execution, Desktop-Aktivierung, Locks und CI-Workflow bleiben gegenüber dem P2-Eingang bytegleich. `DesktopMcpSession.Attach` macht den Button weiterhin nur unter Windows sichtbar; `WorkbenchLeaseV2.LeaseMilliseconds` verwendet weiterhin Windows `GetTickCount64` und wirft andernorts `PlatformNotSupportedException`. Die Testinjektion ist kein produktiver Linux-/macOS-Lease-Timer.

## 4. Windows-Builds und Regression

Windows x64, .NET SDK **10.0.401**, Host und .NET-/ASP.NET-Core-Runtime **10.0.12**, PowerShell 7.6.5. Die unveränderten Repositoryskripte bauen beide Solutions (`CryptoWorkBenchAvalonia.sln`, `CryptoWorkBench.sln`) mit `--locked-mode`, danach `--no-restore`. Prism bleibt auf dem offiziellen Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`; die vorhandene Integritätsprüfung bestätigt 1.180 gepinnte Dateien. Keine Lockregeneration oder Ersatzpakete.

| Prüfung | Debug | Release |
|---|---:|---:|
| Locked Restore beider Solutions | bestanden | bestanden |
| Build beider Solutions | bestanden | bestanden |
| Core, vollständig | 11.532 Pass / 0 Fail / 27 NotExecuted | 11.532 Pass / 0 Fail / 27 NotExecuted |
| GUI, vollständig | 800 Pass / 0 Fail / 0 NotExecuted | 800 Pass / 0 Fail / 0 NotExecuted |
| MCP, vollständiger regulärer Lauf | 176 Pass / 0 Fail / 13 NotExecuted | 176 Pass / 0 Fail / 13 NotExecuted |

Die TRX-Ergebnisse sind massgeblich: Core enthält 11.559 Fälle, davon sieben Darwin-ABI-Tests, einen Darwin-ACL-Test, 18 mangels Windows-Symlinkprivileg nicht ausgeführte Fälle und einen expliziten Hardwarebenchmark. Die Konsolensumme zählt den expliziten Benchmark nicht mit. MCP enthält 189 Fälle, davon 13 explizite installierter-Codex-/native-Desktop-Proben. Diese wurden in P2 nicht gesondert angefordert oder ausgeführt und werden nicht als Pass dargestellt. Vorhandene R3/F2-native Belege bleiben historisch; P2 behauptet keine neue native Windows-E2E-Abnahme.

Der erste Debug-Aufruf in der Sandbox scheiterte vor Tests am Restore; `dotnet --info` zeigte dort zusätzlich verweigerten Service-Control-Manager-Zugriff. Dieser Versuch bleibt als [Build-sandbox-attempt.log](../../../b4-p2-preparation-20261104/results/Debug/Build-sandbox-attempt.log) erhalten und wird nicht als Produkt-Testfehler gezählt. Die erfolgreichen Prüfungen laufen nach automatischer Freigabe ausserhalb der Sandbox. Keine Systeminstallation oder Produktkorrektur war dafür nötig. Beide GUI-Solution-Builds melden sechs vorhandene Warnungen (vier CS8618, eine AVA2001, eine CS8619 in unveränderten Dateien); die zweite Solution jeweils null Warnungen, beide null Fehler.

Reproduktion: [`Run-WindowsRegression.ps1`](../../../b4-p2-preparation-20261104/Run-WindowsRegression.ps1) mit `-Configuration Debug` beziehungsweise `Release`. Das Skript bewahrt die vollständigen Logs/TRX je Konfiguration getrennt und zählt keine gezielten Teilsuiten zusätzlich als neue Tests. [Debug](../../../b4-p2-preparation-20261104/results/Debug/), [Release](../../../b4-p2-preparation-20261104/results/Release/), [ausgewertete Matrix](../../../b4-p2-preparation-20261104/test-matrix.json).

## 5. macOS-CI: vorhandene Gates und Grenzen

Geprüfte Dateien: `.github/workflows/dotnet.yml`, `.github/scripts/Invoke-PrismCi.ps1`, `Test-F01Native.ps1`, `Validate-DesktopPublish.ps1`, `eng/prism/{Build,Enter-Prism,Prepare-Prism}.ps1`, `Lock.props`, `PrismSecurity.targets`, `CryptoScript/Native/FilePublication.targets` und die RID-Locks.

| Bereich | Vorhandener Prüfpfad / Bewertung |
|---|---|
| Runner | `macos-26` → `osx-arm64`; `macos-26-intel` → `osx-x64`. Beide sind offiziell verfügbare native Runnerlabels. ARM64-Gate verlangt zusätzlich echte ARM64-PowerShell und ARM64-Testhost, nicht Rosetta. |
| SDK/Frameworks | `actions/setup-dotnet@v6` installiert exakt 10.0.401; `global.json` verbietet Roll-forward. Desktop verlangt .NET 10 und ASP.NET Core 10. Tatsächliche Runner-/SDK-/Runtimeversionen im macOS-Lauf erfassen. |
| Restore/Prism | Beide Solutions Debug/Release mit Locked Restore. Unveränderter offizieller Prism-Pin, Source-/Compile-Input-Prüfung und getrennte Konfigurationslocks. Pro macOS-RID sind neun Release-Locks einschliesslich MCP und Desktop vorhanden. |
| Tests | Vollständige **Release**-Core-/GUI-/MCP-Suiten, zusätzlich Coordinator/F1/C1. Die Pipeline baut Debug, führt dessen Suiten jedoch derzeit nicht aus. Die zusätzlichen Gates überlappen vollständige Suiten; ihre Zahlen nicht addieren. |
| Native Shim | Echter macOS-Host und Xcode-CLI erforderlich. `clang` baut beide Slices (`x86_64`, `arm64`), Deploymentziel 11.0, ad-hoc signiert; erst nach Erfolg wird das finale dylib gesetzt. Cross-Publish von Windows/Linux ist explizit abgewiesen. |
| Native Gate | FileEncryption-/FilePublicationNativeTests; exakter Containerreferenzvektor muss bestehen. Unter macOS müssen sämtliche entdeckten Darwin-ABI-Fälle bestehen (mindestens sieben). ARM64 hat explizite Architekturassertions. |
| Publish | Release, frameworkabhängig, `UseAppHost=true`, RID-spezifischer Locked Restore und `--no-restore`-Publish. |
| Validator | Apphost ausführbar; verwaltete/native Assets aus deps.json vollständig; beide Shared Frameworks 10.0; InfoDocs inhaltlich bytegleich. Shim: beide Architekturen, gültige Signatur, erlaubte Abhängigkeiten je Slice, echtes Laden aus Publishverzeichnis und beide Exporte. |
| Diagnose | `fail-fast: false`, keine Fehlerunterdrückung. Suiten können nach anderer Suite weiterlaufen. 30 Minuten Joblimit, fünf Minuten natives Gate. Logs/TRX werden pro RID hochgeladen. Publish-Binaries sind **nicht** Bestandteil des bestehenden Uploads. |
| GUI-Abnahme | Headless-GUI-Tests und Publishvalidator starten keine sichtbare macOS-Anwendung. Eine spätere native Desktopprüfung bleibt gesondert erforderlich; CI-Erfolg allein ist keine vollständige macOS-Plattformabnahme. |

Die Build-/Suite-Aufrufe im YAML übergeben keinen RID und protokollieren deshalb den Skriptdefault `win-x64`. Diese Stufen verwenden den Parameter nur für Lognamen, nicht für Restore/Build/Test; ausgeführt wird der native Host. Native-/Publish-Aufrufe übergeben ausdrücklich den Matrix-RID. Das ist eine bestehende Diagnosebenennung, kein Windows-Crossbuild auf macOS.

Erwartete macOS-Ausnahmen ergeben sich aus bestehenden `Assert.Ignore`-/`Explicit`-Bedingungen: Windows-CNG/Sharing/Apphost-Proben im MCP-Projekt, der Windows-Desktop-Dateilock-Fall in GUI, Windows-Pfad-/Sharing-Verträge in Core und der explizite Benchmark. Weitere Skips etwa bei fehlendem System-CCM-Provider oder Dateisystemberechtigungen müssen einzeln anhand der tatsächlichen TRX erklärt werden. **Darwin-ABI-Skips sind im nativen Gate nicht akzeptabel.** Installierter-Windows-Codex-Tests sind kein macOS-Nachweis. Es werden keine macOS-Pass-/Skip-Zahlen vorhergesagt.

Runnerzuordnung am 4. November anhand [GitHub Runner Images](https://github.com/actions/runner-images) und [GitHub-Ankündigung macOS 26](https://github.blog/changelog/2026-02-26-macos-26-is-now-generally-available-for-github-hosted-runners/) geprüft. Die Pipeline selbst bleibt unverändert; kein Workflow wurde gestartet.

## 6. Akzeptierte R3-Low-Restbefunde

| ID | Präzise weiterhin geltende Grenze |
|---|---|
| **B4-R3-01 · Low** | Authentisiertes direktes HTTPS kann bei unbekannter Methode mit Array-/Null-`params` den Methodennamen in der Fehlerantwort spiegeln. Der freigegebene STDIO-Ausgang bereinigt diese Fälle. Keine neue unauthentisierte Offenlegung wird behauptet. |
| **B4-R3-02 · Low** | Das SDK 2.3.0 enthält schon in `McpClientImpl.SendRequestAsync` eine automatische MRTR-Schleife. R3s autorisierter gepinnter Prüfrelay bewirkte nach echtem Commit einen zweiten POST/Dispatch; CAS verhinderte den zweiten Text-/Revisions-/History-Effekt. Der heutige Workbench-Handler erzeugt kein `input_required`. Gewöhnlicher Antwortverlust wurde separat ohne Replay beobachtet. Daraus folgt **keine pauschale Einzelsendegarantie** für SDK-/Toolaufrufe. |

Die aktuelle Pairing-Anleitung korrigiert die pauschale Wiederholungsfreiheit. Historische F2-Aussagen bleiben als damaliger Stand lesbar und werden durch [R3](MCP-B4-R3-independent-review.md), diesen Bericht und die aktuelle Anleitung eingeordnet. EOF-Nachlauf bis zum Originalabschluss/Timeout ist eine weitere bereits akzeptierte Grenze: EOF bedeutet weder sofortige Cancellation noch Credential-Widerruf. P2 erweitert weder die Security-Abnahme noch die unterstützten Clients/Protokollantworten.

## 7. Exakter Kandidat und nächste Schritte

Die abschliessende Integritätskontrolle ergibt exakt fünf geänderte Testdateien, die aktuelle Pairing-Anleitung und diesen neuen Bericht gegenüber dem P2-Eingang. Alle übrigen Dateien einschliesslich historischer Berichte sind unverändert. `git diff --check` ist erfolgreich; Dateimenge und alle fünf importierten After-Hashes stimmen. Der Gesamtkandidat umfasst 1.034 Quelldateien, 26 geänderte getrackte Dateien und 22 ungetrackte Dateien bei unverändertem HEAD und leerem Index.

Vollständiges Bundle, binärer Gesamtdiff, ungetrackte Dateien, vollständiger Quellsnapshot und SHA-/kanonisches Git-Blob-Inventar stehen unter `b4-p2-preparation-20261104/candidate-package`. Die unabhängige Wiederherstellung prüft alle Quelldateien byteweise sowie den kanonischen Patch vor und nach dem expliziten Rohbytesnapshot. ZIP-Prüfsumme, Umfang und Abschlussnachweis stehen ausserhalb des ZIP, um eine zyklische Selbstprüfsumme zu vermeiden. Das Paket enthält keine GitHub-Mutation und keine neuen Commitobjekte. [Kandidatenmanifest](../../../b4-p2-preparation-20261104/candidate-package/transfer-manifest.json), [Abschlussnachweis](../../../b4-p2-preparation-20261104/final-verification.json), [Übergabeanleitung](../../../b4-p2-preparation-20261104/candidate-tools/MACOS-HANDOFF.md).

Für die nächste, **gesondert zu beauftragende** Phase:

1. Den abgegrenzten Kandidaten und die Prüfnachweise übernehmen; Herkunft der fünf Dateien anhand des Rücktransfermanifests bewahren. Nach einer späteren Commitbildung muss der gesamte Git-Tree zum kanonischen Quellinventar passen. Lokale Konfigurationen, Secrets, Caches und Artefakte nicht committen.
2. Einen Prüfbranch mit diesem Kandidaten einschliesslich der lokal noch unveröffentlichten B3-Historie veröffentlichen. Kein Update von `master` ist dafür erforderlich. Commit/Push sind in P2 ausdrücklich **nicht ausgeführt**.
3. Die unveränderte Pipeline reagiert ausschliesslich auf Push nach `master` oder Pull Requests mit Ziel `master`. Ein Prüfbranch-Push allein genügt nicht; `workflow_dispatch` fehlt. Daher später PR nach `master` eröffnen, ohne Merge. Das betrifft auch einen Draft-PR, solange die Repositoryregeln die Ausführung zulassen.
4. GitHub-PR-Workflows testen standardmässig den synthetischen Merge-Commit. Head-/Base-/Merge-SHA und tatsächlichen Quellbaum festhalten. Nur wenn der getestete Tree dem Kandidateninventar entspricht, ist der exakte Kandidat validiert. Bei Abweichung Integrationsprüfung ausdrücklich gesondert ausweisen oder einen separat autorisierten Workflow mit festem Head-Checkout vorbereiten. [GitHub-Ereignissemantik](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows).
5. Beide macOS-Jobs vollständig auswerten, TRX-Pass/Fail/NotExecuted und konkrete Ausnahmegründe dokumentieren; native Architektur, Prism-Pin, Locked Restore und Publishvalidator prüfen. Ein Fehler bleibt sichtbar, keine spontane Lockregeneration, Testdeaktivierung oder Produktkorrektur.
6. Nach CI sichtbare native macOS-Desktopabnahme planen. Für eine Weitergabe des CI-Publishpakets ist ein zusätzlich beauftragter Artefakt-Upload nötig. Keine Linux-/macOS-MCP-Aktivierung im Rahmen dieser Schritte.

**READY FOR MACOS CI.** Die belegten Testkorrekturen verursachen keine Windows-Testregression. Es bleiben die ausdrücklich akzeptierten R3-Low-Grenzen und die noch ausstehende macOS-Ausführung. Keine Commits, Pushes, Merges, Branch-/master-Updates oder GitHub-Mutationen durchgeführt.
