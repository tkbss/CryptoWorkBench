# G1-A06 – Cross-Platform and Final Architecture Validation

Stand: 10.11.2026, Europe/Zurich. Abschlussentscheidung: **CONDITIONAL GO** für die Vorbereitung einer späteren Integration, vorbehaltlich der unten abgegrenzten Nachweise. Keine Integrationsfreigabe und keine Linux-/macOS-Produktfreigabe. Legacy bleibt Standard, G1 ausdrücklich `--dock-shell`.

Maschinenlesbarer Nachweis: [G1-A06-results.json](evidence/G1-A06-results.json). Neue Rohdaten ausschließlich unter [artifacts/g1-a06](../../artifacts/g1-a06). A06 verändert keinen Produkt- oder Testcode.

## 1. Verbindliche Baseline

Geprüft wurde die vollständige uncommittete A05-Arbeitskopie einschließlich M1 im Worktree `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Der HEAD allein enthält diese G1-Implementierung nicht. Vor dem ersten Lauf wurden 1.149 getrackte oder nicht ignorierte Dateien kopiert und SHA-256-inventarisiert, Git-Status und vorhandener Patch gesichert. Weitere 15.831 historische Artefaktdateien mit 2.557.534.574 Bytes wurden gehasht und unverändert geschützt. Ausgangszustand: 37 modifizierte und 113 ungetrackte Dateien.

Grundlagen sind G1-01–G1-08, F01/F2/F03, A01–A03, A04 einschließlich R1–R4 sowie [A05](G1-A05-native-chrome-input-acceptance.md) und [M1](G1-A05-M1-final-owner-acceptance.md). Die früheren roten Zwischenläufe bleiben historische Evidenz. M1 ergänzt die Eigentümerabnahme; es schreibt die damals offenen Aussagen in A05 nicht rückwirkend um.

Unveränderte Pins: Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock.Avalonia/Model/Serializer/Theme 12.1.0.13; offizieller Prism-Quellcommit `358118cd640d9a22ff8cf21c8ad197fa038b7990` mit 1.180 geprüften Dateien. Der lokale Dock-Reviewcheckout bleibt auf `78da173545501e796f37bc8528e9e55d9cbfd991`. Die vorhandenen konfigurations- und RID-spezifischen Locks wurden verwendet; kein Upgrade und keine Neugenerierung von Locks. Die befristete Prism-Quellintegration bleibt ein separat zu verwaltender Bestandsvertrag; A06 nimmt keinen Wechsel auf neue Pakete vor.

Umgebung: Windows x64, OS 10.0.26200, SDK 10.0.401, Runtime 10.0.12. [Umgebungsnachweise](../../artifacts/g1-a06/environment) erfassen tatsächliche OS-/Prozessarchitektur. Ein erster sandboxierter `dotnet --info`-Abruf konnte den Service Control Manager nicht lesen; der separat erhaltene autorisierte Abruf besteht. Dies ist eine Werkzeugberechtigung, kein Produktfehler. Builds und native Prozesse liefen mit der erforderlichen Sandbox-Freigabe.

## 2. Plattformmatrix und Aussagegrenzen

| Prüfung | Windows x64 | Linux x64 | macOS x64 | macOS ARM64 |
|---|---|---|---|---|
| Debug-/Release-Build | PASS / PASS | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| GUI Debug/Release | 1.108 / 1.108 PASS | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| MCP Debug/Release | 176 / 176 PASS; Explicit separat | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Core Debug/Release | 11.512 / 11.512 PASS | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| G1-01 Headless Debug/Release | 12 / 12 PASS | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Desktop-Start, native Fenster | PASS in Debug/Release | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Float/Redock und Host-Cleanup | PASS in Debug/Release; Maus siehe Abschnitt 5 | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Layoutpersistenz/Restart | PASS in Debug/Release | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Shutdown/Drain | PASS in Debug/Release | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Release-RID-Publish | PASS, win-x64 | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED |
| Produktive MCP-Lease-Uhr | Windows-Pfad geprüft | bekannte Plattformgrenze | bekannte Plattformgrenze | bekannte Plattformgrenze |

Es steht nur der Windows-Desktop zur Verfügung: WSL nicht installiert, Docker nicht vorhanden, kein autorisiertes Linux-/macOS-Ausführungssystem für diesen Arbeitsstand verfügbar. Keine CI auf einem anderen Commit wird als G1-Nachweis verbucht. Es wurden weder fremde Architekturen emuliert noch native Desktopergebnisse aus einem erfolgreichen Windows-Build abgeleitet. ARM64 erfordert einen tatsächlichen ARM64-Prozess; Rosetta/x64 reicht dafür nicht.

## 3. Vollständige Regression, Inventar und Builds

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|
| CryptoWorkBenchGuiTest | 1.108 | 1.108 | 0 | 0 |
| CryptoWorkBench.Mcp.Tests | 176 | 176 | 0 | 13 Explicit |
| CryptoScriptUnitTest | 11.512 | 11.512 | 0 | 26 Skip + 1 Explicit |
| G1DockFeasibility.Tests | 12 | 12 | 0 | 0 |

Alle acht vollständigen Suiten wurden neu ausgeführt. Der fallweise Vergleich mit A05 ergibt **keine hinzugefügten oder entfernten Tests, keine geänderten Ergebnisse und keine neuen Skips**. Nur dynamische UTC-Anzeigenamen der bekannten MCP-Contract-Testargumente werden zum Identitätsvergleich normalisiert. Namen und Gründe aller nicht ausgeführten Fälle bleiben in [test-inventory-difference.json](../../artifacts/g1-a06/test-inventory-difference.json) erhalten. Plattform-/Symlink-Skips und der explizite Hardwarefall sind keine PASS-Ergebnisse.

Zusätzlich bestehen je Debug/Release 16 kritische Fälle in einem getrennten Testprozess: Chrome, Popup-Retirement und A04-Partial-Failure. Diese Wiederholungen erhöhen nicht das Inventar von 1.108 GUI-Fällen. Alle projektbezogenen Locked-Restores bestehen; beide Solutions wurden zusätzlich über den unveränderten Prism-Buildwrapper geprüft: PASS in Debug und Release, beide Solutions jeweils Exit 0 und null Buildfehler. Die erste Solution meldet 85 Debug- beziehungsweise 91 Release-Warnungen, die zweite jeweils null; die vollständigen Meldungen bleiben in den Logs. Release `win-x64` wurde mit den vorhandenen RID-Locks veröffentlicht und mit dem vorhandenen Asset-/InfoDoc-Validator geprüft: PASS; Apphost, verwaltete Abhängigkeiten, fünf native Assets und 133 InfoDocs-Dateien bestätigt. Ein Publish-Inhaltscheck allein ist keine native UI-Abnahme des veröffentlichten Verzeichnisses; die Desktop-Lifecycle-Nachweise stammen aus den unten genannten nativen Prozessen desselben Quellstands.

Reproduktion: [run-regression.ps1](../../artifacts/g1-a06/run-regression.ps1), [run-additional.ps1](../../artifacts/g1-a06/run-additional.ps1); Befehle, Exitcodes, TRX und vollständige Logs bleiben erhalten. Vorhandene Compiler-/Restorewarnungen werden nicht als Fehler entfernt oder durch deaktivierte Prüfungen verdeckt.

## 4. Neue native Windows-Läufe

**72/72 erneut ausgeführte Bestandsprozesse bestehen**, je 36 in Debug und Release, jeweils Exit 0, null Timeouts und null fehlgeschlagene Checks. Ihre Haupt-Ergebnisdateien enthalten insgesamt **2.738 bestandene Checks** (1.369 je Konfiguration). Zusätzlich bestehen sechs Legacy-/Startfehlerprozesse mit 56 Checks; A06 insgesamt somit **78 native Prozesse und 2.794 Checks**. Checks sind keine zusätzlichen NUnit-Testfälle. [Native Einzelresultate](../../artifacts/g1-a06/native-results.json) enthalten Namen, PIDs, Konfiguration, Exitcodes und Rohdateipfade.

Der unveränderte Treiber `eng/gui/Validate-G1NativeAcceptance.ps1 -Configuration Both -IncludeHistorical` verwendet ein frisches Evidence-Verzeichnis. Je Konfiguration gehören zur historischen Matrix zwei Partial-Failure-Prozesse, Layoutrollen, Layout/Restart/Invalid, Redock, Editor, Integration, zwei Operation-Contract-Prozesse und zwanzig Cleanup-/Restore-Prozesse. Letztere prüfen sechs Szenarien jeweils mit Save, aktuellen und Legacy-Container-IDs sowie zweimal die echte Pre-A02-Datei. Hinzu kommen je Konfiguration zweimal `native-acceptance`, Tools, Output und Diagnostics.

Die Probes starten echte Avalonia-Desktopfenster. Sie prüfen unter anderem den vollständigen HWND-Bestand, zerstörte Quellhosts, entfernte Factory-/DockControl-Registrierungen, erhaltene zweite Floating-Fenster und das Ende der Editorregistrierung beim Shutdown. Programmgesteuerte DockManager-/Factory-Aktionen und simulierte Avalonia-Eingabe werden in den Ergebnisdateien ausdrücklich bezeichnet. Das ist keine neue externe Mausabnahme. Die Windows-HWND-Assertions dürfen nicht unverändert auf Linux/macOS ausgeführt werden.

Die zusätzlichen bestehenden Modi `legacy`, `fallback` und `startup-failure` bestehen in Debug und Release mit jeweils 10, 13 und 5 Checks. Sie bestätigen Standard-Legacy, strukturellen Fallback mit entferntem Vorschautheme/Hosts sowie den kontrollierten produktiven Startfehler ohne Ersatzregistrierung. Alle sechs Prozesse enden mit Exit 0; die bewusst provozierten Startfehler sind erwartete Prüfbedingungen.

## 5. Docking, Chrome und Eigentümerabnahme

Die sieben vom Eigentümer am **10.11.2026** bestätigten Windows-Mausabläufe gelten als angenommene Baseline: einzelnes Tool; vollständige Tool-Gruppe; CryptoScript-Dokument einschließlich Maximize/Restore; seitlich angedockter DocumentDock; verschachtelte Gruppen; zwei Floating Windows; Speichern und Wiederherstellen nach Neustart. Ihre Herkunft ist die ausdrückliche Eigentümerbestätigung in M1, kein neu erzeugtes A06-Automationsergebnis.

Die aktuelle native Matrix ergänzt diese Baseline durch wiederholte Modell-, Fenster- und Cleanup-Prüfungen. Der A05-Chrome-Vertrag bleibt erhalten: Nur die konkrete Dokument-Pane derselben Session begründet Dokument-Chrome; reine Tool-Hosts verwenden Tool-Chrome. Der bestehende Host reagiert auf Eintritt/Austritt des Dokuments und entfernt den `LayoutUpdated`-Handler beim Schließen. Die Spezialcaption ist Windows-gekapselt. Ihre Notwendigkeit auf anderen Betriebssystemen wird weder unterstellt noch aus Windows-Ergebnissen ausgeschlossen.

M1 bleibt verbindlich: `DockShellLayout.CanChange` verlangt `!IsMaximized`; `EditorDockFactory.Allowed` verlangt für neue normale Operationen `!FocusMode`. `IDockDropPolicy.CanDrop` und die Root-Capability-Policy verweigern entsprechende Ziele. Restore verwendet den eigenen zugelassenen Layoutpfad und entfernt die Fokusmodus-Sperre; Busy-/Lease-/Modal-/Fault-Gates bleiben bestehen. OS-Fenstermaximierung ist von diesem G1-Fokuszustand zu unterscheiden.

**Bedienregel: Zum Zurückdocken einer maximierten View zuerst `Restore Layout` ausführen.** Kein neuer Fehlerbefund aus dem akzeptierten Fokusmodus.

## 6. Editor, Eingabe und Präsentation

`DockEditorSession` hält eine autoritative View, einen Editor und dasselbe TextDocument samt UndoStack. `CreatePresenter` löst den alten Content, bevor dieselbe View neu angebunden wird. Relokation läuft unter der bestehenden Coordinator-Beobachtung und schließt hostgebundene Popups vor dem Reparenting. Sie registriert keinen zweiten Editor und erzeugt keine Interpreteroperation. Die aktuellen Tests prüfen Identität, WorkspaceGeneration, EditorHistory, Revision, SaveIntent sowie Undo/Redo.

Die native A05-Acceptance-Probe prüft erneut Auswahl/Clipboard, Paste, Undo/Redo, Completion und Insight im Hauptfenster, Floating-Editor und nach Redock, einschließlich Popup-PlacementTarget und wirkungsloser verspäteter Insertion. Tools/Output/Diagnostics ergänzen echte Clipboardzugriffe und Navigation. Die Eingabe in diesen A06-Läufen ist programmgesteuert; die externen A05-Nachweise für Windows-Tasten, Mausauswahl, Return/KP_Enter und Schweizer Unicode-/AltGr-Eingabe bleiben getrennte akzeptierte Vorgeschichte und wurden nicht als neue A06-Eingaben kopiert.

Output projiziert ausschließlich Ereignisse `Kind == "print"`, mit eindeutiger Präsentationsidentität und Quellbezug. Clear Output verändert keinen Editorinhalt. Diagnostics prüfen aktuelle Quellversionen und weisen veraltete Navigation zurück. Variables verwenden das vorhandene globale ViewModel und bleiben read-only; InfoDocs, Copy Example und Info-History verwenden dieselben bestehenden Dienste. Tool-Close/Show erzeugt keine zweite VariableState-Instanz. Auf macOS sind Modifier-/Clipboardkonventionen, auf Linux X11/Wayland-Fokus und Clipboard-Lifetime eigenständig abzunehmen.

## 7. MCP, Admission und Shutdown

`DockEditorSession.CanRelocate` verlangt unter anderem `Phase == Gui`, keine aktive/pending/Observer-Arbeit, keine Mutation-/Identity-Locks, kein Document-Update, keine Ausführung/Stopping/Busy und aktivierte Haupt-/Editorfenster. Die Factory prüft diese Zulassung für neue Operationen. Eine bereits laufende interne Cleanup-Fortsetzung unter `_mutationDepth` ist keine neue Editorberechtigung. MCP arbeitet weiterhin ausschließlich über die vorhandene registrierte GUI-Editor- und Interpreterserialisierung.

Die vollständigen Suiten sowie native Editor-/Integrationsprobes prüfen echten Transport, Lease-Sperren, Freigabe und GUI-Fortsetzung. `App.CoordinateClosing` erfasst das normale Layout vor `BeginShutdown`, stoppt den Transport und wartet `DrainForExitAsync` ab. Erst bestätigtes Schließen persistiert und disposed Session/Präsentation/Hosts. Ein abgebrochener oder noch drainender Close verliert keine laufende Arbeit.

`LayoutFaulted` bleibt die A04-R4-Grenze für einen nach Fehler inkonsistenten Layoutbaum. Capture/Persistenz und neue Layoutmutationen werden dann verweigert; fachliches Speichern des autoritativen Dokuments bleibt über seinen unabhängigen SaveIntent-/Editorvertrag möglich. Die bestehenden Partial-Failure-Tests prüfen diesen Unterschied erneut. Kein ungültiger Baum wird als normaler Snapshot gespeichert.

Die vorhandene Installation **codex-cli 0.167.0-alpha.3.2** wurde tatsächlich verwendet. Je Debug/Release bestehen **11/11 gezielt ausgewählte Explicit-Fälle**, ohne Skip: Discovery/Status, HTTPS-Portübernahme, drei nicht vertrauenswürdige/abgelaufene Serverfälle, vier fremde Portbesitzer-/Pinvarianten, kein Replay nach mehrdeutigem Dispatch und Ablehnung einer neuen Workbench-Identität trotz gleicher Credentials. Die unveränderten Tests verwenden einen isolierten temporären Kindprozess-Konfigurationspfad und selbst erzeugte Test-Listener; keine Benutzerkonfiguration und kein Modellaufruf werden benötigt. [Einzelfälle](../../artifacts/g1-a06/installed-codex-results.json). Diese Ergebnisse werden separat gezählt; die regulären MCP-TRX bleiben bei 176 PASS und 13 Explicit-NotExecuted. `InstalledCodexCompletesDesktopAes` und `InstalledCodexCompletesDesktopAesAndRejectsTakeover` sind **NOT VERIFIED**: gepaarter Wegwerf-Desktop, exakter Seed, lokaler Ausführungsentscheid und GUI-Disconnect fehlen. Assertions wurden nicht geändert.

Bekannte plattformspezifische Einschränkung: [WorkbenchLeaseV2.cs](../../CryptoWorkBenchAvalonia/Services/Automation/WorkbenchLeaseV2.cs#L30) verwendet produktiv `OperatingSystem.IsWindows() ? GetTickCount64() : throw new PlatformNotSupportedException()`. `ReadTime` schließt bei Uhrfehlern die Admission. Das ist reproduzierbar aus dem Code und bereits in G1-08 dokumentiert; injizierte Testuhren beweisen keine produktive Linux-/macOS-Lease. Keine neue G1-Regression, aber ein ausdrücklicher Blocker für eine solche Plattformfreigabe. Ein Ersatz benötigt gesonderte Freigabe und Sicherheitsnachweise für Suspend/Resume, Monotonie, Ablauf und Fail-closed-Verhalten.

## 8. Schema 1, Persistenz und plattformübergreifende Geometrie

Die Schema-1-Prüfung bleibt bei einer expliziten Allowlist, begrenzter Dateigröße, eindeutigen Pane-Identitäten, gültigen Rollen und Auswahlreferenzen. Hauptanker werden über ihre Rolle im Hauptbaum aufgelöst; Floating-Gruppen und dynamische Container erhalten keine unzulässige Ankerprivilegierung durch kopierte IDs. Aktuelle, Legacy- und historische Containeridentitäten werden in separaten echten Neustarts geprüft. Leere Quellhosts verschwinden über den bestehenden Dock-Lifecycle.

`DockLayoutStore` projiziert ausschließlich Präsentationsstruktur; Context, Editor-Text, Variablewerte, Credentials und laufende MCP-Leases werden nicht serialisiert. Speichern erfolgt über eine temporäre Datei im Zielverzeichnis, Flush, erneutes Decode und atomaren Replace/Move. Ungültige Dateien führen zum kontrollierten Default-Fallback. Während Maximize liefert Capture den gesicherten Normalbaum; Restore verwendet die vorherigen Referenzen und Proportionen.

`DockWindowGeometry.Clamp` behandelt Positionen als native Pixel, Größen als Avalonia-DIP und berücksichtigt die aktuelle Skalierung/WorkingArea. Nicht endliche oder unbrauchbare Werte werden begrenzt, ein nicht mehr vorhandener Bildschirm fällt auf einen verfügbaren Bildschirm zurück; ohne Screenliste gilt ein defensiver 800×600-Default. MainWindow und Floating-Geometrie werden vor Wiederherstellung angepasst. Damit ist die kontrollierte Behandlung im Code und in Windows-/Geometrietests belegt, **nicht** ein realer Windows→Linux/macOS-Layouttransfer. Für jede fehlende Plattform sind Monitorwechsel, HiDPI, negative Koordinaten, Maximierung und erreichbare Titelleisten mit derselben Schema-1-Datei nativ zu prüfen. Keine neue Formatversion.

## 9. Abschlussreview der Architektur

| Verantwortung | Befund |
|---|---|
| Dock.Avalonia | Bleibt Eigentümer der visuellen Struktur, Drag/Drop-Pipeline, Floating-Hosts und ihrer nativen Lebenszyklen. Kein Fork und keine Bibliotheksänderung. |
| CryptoWorkBench | Hält autoritative Editor-/MCP-Zulassung, Interpreterserialisierung, Präsentationsprojektionen, globale Variables, persistierbare Layoutprojektion und Fehlergrenze. |
| Zustandsführung | DockPaneState beschreibt Präsentation; Editor/Variables werden referenziert. Kein nachgewiesener zweiter fachlicher Zustand oder zusätzlicher Interpreter. |
| F01/F03-Anpassungen | Rollenbasierter Collapse, genaues Entfernen eines bereits leeren eigenen Hosts, Aufschub leerer Gruppenquellen bis zum Ende der Live-Enumeration, Auswahlkorrektur bei Promotion und Dokument-Grip haben weiterhin konkrete Vertragstests und nachvollziehbare Ursachen im gepinnten Dock. Keine nachgewiesene entbehrliche Umgehung. |
| Disposal/Events | Chrome-LayoutUpdated, Workspace-/History-/Completion-Abonnements, Presenterinhalt, RootChanged, Locators, Styles/Templates und native Trace-Hooks besitzen die geprüften Abbaupfade. Native Censuses und Registrierungsprüfungen decken die ausgeführten Zyklen ab; keine allgemeine Langzeit-Leakgarantie. |
| Plattformgrenzen | Windows-Chrome, optionales HWND-Tracing und byte-lock-basierte native Partial-Failure-Probe sind explizit gekapselt. Die bekannte Windows-Lease-Uhr bleibt ein separat benannter Plattformblocker. |
| Legacy | Ohne `--dock-shell` bleibt Legacy aktiv. Struktureller Shell-Fehler darf vor Editorstart zurückfallen; nach begonnenem produktivem Start wird sicher beendet, ohne zweiten Editor-Fallback. |

Gelesene zentrale Pfade: `DockShellStartup`, `DockShellState`, `DockShellLayout`/Persistence, `EditorDockFactory`, `DockEditorSession`/Files, `DockShellWindow`, `DockShellPresentation`, Output-/Diagnostics-/Variables-Präsentationen, `InfoCompletionController`, `App`, Desktop `Program` und `NativeDockWindowTrace`. Kein durch dieses Review nachgewiesener neuer Produktfehler. **NO REACHABLE B4 FOUND** bleibt bestehen. Die gezielte R4-Validierung nach tatsächlichen Fehlern ist keine neue generische Dock-Transaktionsarchitektur; A06 führt eine solche nicht ein.

## 10. Offene Risiken und Klassifikation

| ID | Klasse / Gewicht | Befund, Reproduzierbarkeit und Abschlussbedingung |
|---|---|---|
| A06-P1 | Fehlende native Voraussetzung / hoch für Plattformfreigabe | Linux x64, macOS x64 und ARM64 fehlen vollständig für diesen Arbeitsstand. Alle Matrixfelder dort NOT VERIFIED. Abnahme gemäß Abschnitt 11. |
| A06-P2 | Bekannte OS-Einschränkung / hoch für produktives MCP außerhalb Windows | Produktionsuhr lehnt Nicht-Windows ab; Codebedingung in Abschnitt 7. Bereits dokumentiertes Restrisiko, keine neue A06-Produktkorrektur. Separater Plattformauftrag erforderlich. |
| A06-M1 | Fehlende manuelle Voraussetzung / begrenzt | Zwei installierte-Codex-AES-Explicit-Fälle benötigen einen gepaarten Wegwerf-Desktop mit exaktem Seed, lokalem Ausführungsentscheid und anschließendem GUI-Disconnect. Diese Umgebung ist nicht vorhanden. |
| A06-I1 | Noch nicht ausgeführte Integration / mittel | G1+L1 wurde absichtlich nicht gebaut. Kein textueller Pfadoverlap ist keine semantische Kompatibilitätsgarantie. |
| A06-H1 | Akzeptierte historische Evidenzgrenze | A05-Maus-/Externeingabe und M1-Eigentümerabnahme bleiben ihre ursprünglichen Nachweise. Neue native A06-API-Probes werden nicht als wiederholte Handbedienung bezeichnet. |

Keine neue fehlgeschlagene Produktprüfung in A06. Die anfängliche eingeschränkte Umgebungsabfrage ist als behobene Testinfrastrukturgrenze erhalten (A06-T1); kein Test wurde deshalb umgeschrieben. Historische Fokus-/Pseudoconsole-/R3-Fehlversuche bleiben unverändert an ihrem ursprünglichen Ort und werden weder zu A06-PASS umetikettiert noch ohne neue Reproduktion als aktuelle Produktfehler ausgegeben.

## 11. Fehlende Abnahmen je Betriebssystem

Für **Linux x64** ist ein x64-Checkout des manifestidentischen G1-Stands, PowerShell 7, SDK 10.0.401, vorhandene Pins und eine echte angemeldete X11-/Wayland-Sitzung erforderlich. Beide Konfigurationen vollständig bauen/testen; den Linux-RID-Publish prüfen; Haupt-/Tool-/Dokumentfenster, Gruppen- und Tab-Drag, zwei Hosts, Fokus, Clipboard, Completion/Insight, Return/KP_Enter, Undo/Redo, Output/Diagnostics, Layouttransfer/Restart und Shutdown nativ protokollieren. Xvfb/Headless ersetzt diese Bedienabnahme nicht.

Für **macOS x64** dieselben Prüfungen auf einem tatsächlichen x64-Prozess mit angemeldetem Desktop und Xcode Command Line Tools. Den bestehenden `osx-x64`-Publish einschließlich nativer Shim-/Asset-Prüfung ausführen; Cocoa-Chrome, passende Modifier und Clipboard getrennt dokumentieren. Windows-HWND-/Dateisperrprobes nicht unverändert übernehmen.

Für **macOS ARM64** dieselben Schritte auf nativer ARM64-Hardware und ARM64-Runtime, einschließlich `osx-arm64`-Publish und nativer Ressourcenfreigabe. Ein erfolgreicher Intel-/Rosetta-Lauf erfüllt diese Zeile nicht. Der vorhandene macOS-Publish benötigt den Mac/Xcode-Pfad; ein Windows-Cross-Publish wäre kein gleichwertiger Nachweis.

Der bestehende `eng/gui/Validate-G1Platform.ps1 -Configuration Both -EvidenceDirectory <neuer lokaler Pfad> -Desktop` ist ein Ausgangspunkt für Build/Tests und den tatsächlichen Desktopstart. Er ersetzt nicht die ausgefüllte native Checkliste. OS, Architektur, Bildschirm/Skalierung, Tastaturlayout, Quellmanifest und Prüfer müssen in jeder Evidence stehen. Die produktive MCP-Lease ist auf diesen Plattformen erst nach separater Lösung/Zertifizierung von A06-P2 freigabefähig. Der bestehende Workflow `.github/workflows/dotnet.yml` enthält Debug-/Release-Builds und Release-Suiten/RID-Publishes, aber keine vollständige G1-Headless-/native Desktopabnahme; seine heutige Matrix reicht allein nicht.

## 12. Tatsächlicher Zuwachs gegenüber A05

A06 ändert keinen Test und kein Produktverhalten. Neu sind die vollständige erneute Regression und fallweise Inventur, frische native Bestandsläufe, Legacy-/Startfehler-Nachprüfung, Lösungsbuilds und Desktop-Publish, der Versuch mit vorhandenen installierten-Codex-Explicit-Tests, das abschließende Architekturreview und die read-only L1-Analyse. Die sieben Mausabläufe sind bereits in M1 akzeptiert; sie werden hier als solche übernommen. Linux/macOS bleiben offen, ebenso der echte plattformübergreifende Layouttransfer. Das neue A06-Paket behauptet keine zusätzliche externe Windows-Tastatur-/Mauskampagne.

## 13. Read-only Master-/L1-Analyse und Integrationsplan

Lokaler und über den verbundenen GitHub-Zugang lesend aufgelöster aktueller `master`: [`fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`](https://github.com/tkbss/CryptoWorkBench/commit/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb). Gemeinsame Ausgangsbasis ist `7ecac04`. Master enthält L1 samt Abschlussreview und Integration. Kein Fetch mit Ref-Änderung, Merge, Rebase oder L1-Import wurde durchgeführt.

[Pfadvergleich](../../artifacts/g1-a06/integration/paths.json): L1 ändert/ergänzt 35 Dateien; die vorhandene G1-Arbeitskopie 150. **Schnittmenge leer**, auch unter Einbeziehung ungetrackter G1-Dateien. Das ist eine Dateipfadprüfung, kein ausgeführter Merge. L1 ändert Grammatik/Parser, Assignment-/Initializer-Evaluatoren, Diagnostik-Enum, Info.Types und zugehörige Core-/GUI-Tests. G1 ergänzt daneben Dock-Shell, Präsentationen, Output-Quellbezug, GUI-Tests, Projektverweise und Locks. L1 ändert keine Projekt-/Paketdatei und keine MCP-Schnittstellendefinition.

Semantische Anschlussstellen bleiben wichtig: L1-Assignment ruft den bestehenden Funktionskonverter auf, an dem G1 den SourcePosition-Bezug ergänzt. Die optionale Source-Eigenschaft erhält bestehende AST-Konstruktoren. Beim später kombinierten Stand müssen Print-/Diagnostics-Quellen auch in Assignment-RHS und verschachtelten Aufrufen stimmen. L1s atomare Typprüfung/Initialisierung vor dem Variablen-Update, Fehlerredaktion und Fortsetzung dürfen durch G1-Präsentation nicht verändert werden. Neue L1-Diagnosecodes werden durch die generische Diagnostics-Projektion verarbeitet; dies ist ein Codebefund, noch kein Lauf des kombinierten Editors.

Die lokale L1-I1-Baseline dokumentiert Core 11.719, GUI 800 und MCP 176. Gegen die gemeinsame ursprüngliche Basis Core 11.508/GUI 797 sind das **211 Core- und 3 GUI-Fälle = 214 L1-Fälle**. G1 ergänzt seinerseits vier Core- und 311 GUI-Fälle. Bei unverändertem Inventar und gleichen lokalen Skip-Voraussetzungen ergibt sich die **ungeprüfte rechnerische Erwartung** Core **11.723**, GUI **1.111**, MCP **176**, Headless **12** je Konfiguration. Diese Zahlen sind kein PASS; der kombinierte TRX-Vergleich muss sie bestätigen oder jede Abweichung erklären. Alte L1-CI-Zahlen anderer Betriebssysteme zertifizieren nicht die uncommittete G1-Arbeitskopie.

Konkrete spätere Reihenfolge, ausschließlich nach gesonderter Freigabe:

1. Eigentümerabnahme von A06 und ausdrückliche Entscheidung über die abgegrenzten Plattform-/MCP-Gates einholen. Legacy als Standard und G1-Opt-in beibehalten.
2. Den vollständigen akzeptierten G1-Stand inklusive aller ungetrackten Produkt-/Testdateien, Projekte, Locks und Berichte anhand des Manifests sichern und auf einem separat autorisierten Integrationszweig versionieren. Kein Transfer bloß des alten HEAD.
3. Aktuellen Masterstand erneut feststellen. Auf einem neuen sauberen Integrationscheckout auf Master/L1 aufsetzen und genau die akzeptierten G1-Änderungen übernehmen; L1-Grammatik, Evaluatoren und Tests erhalten. Etwaige inzwischen entstandene Konflikte einzeln prüfen.
4. Debug-/Release-Locked-Builds, sämtliche vier Suiten und 214 L1-Fälle ausführen; Inventar gegen beide Vorgänger vergleichen. Print-Source, Assignment-Fortsetzung, Variablenidentität, Enter, authentifizierte MCP-Lease, Undo/SaveIntent und Diagnostics im gemeinsamen Editor kontrollieren. Kein Bestehen aus bloßer Summenbildung ableiten.
5. Windows-Nativmatrix, sieben Eigentümer-Mausabläufe und installierte-Codex-Prüfungen am kombinierten Stand wiederholen; Plattformgates aus Abschnitt 11 mit genau diesem Quellstand abschließen oder den Freigabeumfang ausdrücklich Windows-begrenzt halten.
6. Alle vier RID-Publishes und native Ressourcenfreigabe auf den tatsächlichen Zielsystemen prüfen. CI muss den freigegebenen vollständigen Quellstand und die echte Architektur ausweisen. Erst danach einen gesondert autorisierten Integrations-/PR-Schritt vorbereiten.

## 14. Dateiintegrität und vollständiger Git-Status

Der abschließende SHA-256-Vergleich bestätigt **1.149/1.149 Ausgangsdateien bytegleich**, keine entfernte oder geänderte Baseline-Datei, **15.831/15.831 historische Artefakte bytegleich** und alle 256 besonders geschützten Dateien unverändert. [integrity.json](../../artifacts/g1-a06/integrity.json) und [final-checks.json](../../artifacts/g1-a06/final-checks.json) enthalten die Einzelprüfungen. `git diff --check` besteht; zusätzlich werden die beiden neuen ungetrackten Ergebnisdateien mit `git diff --no-index --check` ohne Whitespace-Diagnose geprüft. Der Abschlussstatus umfasst 152 Zeilen: unverändert 37 modifizierte Bestandsdateien und nun 115 ungetrackte Dateien (113 Bestand + 2 A06-Berichte).

Neue nicht ignorierte Dateien sind ausschließlich dieser Bericht und `docs/gui/evidence/G1-A06-results.json`. Neue Raw-Evidence und Hilfsskripte liegen unter `artifacts/g1-a06`; ihr Dateiinventar mit Hashes wird separat erhalten. Baselinekopien, alte Berichte, Paket-Locks und frühere fehlgeschlagene Artefakte werden nicht überschrieben. Kein Commit, Push, Merge, Rebase, Cherry-Pick, Staging, Paketupgrade, Dock-Patch oder L1-Import. Der Hauptcheckout bleibt unverändert.

Vollständiger Abschlussstatus, einschließlich aller schon vor A06 vorhandenen Änderungen:

```text
 M CryptoScript/Model/AntlrToFunctionCallExpression.cs
 M CryptoScript/Model/Ast/FunctionCallExpressionNode.cs
 M CryptoScript/Model/FunctionCall.cs
 M CryptoScript/Model/FunctionCallEvaluator.cs
 M CryptoScript/Model/OperationInvocation.cs
 M CryptoScript/Operations/OutputOperations.cs
 M CryptoWorkBenchAvalonia.Desktop/DesktopMcpSession.cs
 M CryptoWorkBenchAvalonia.Desktop/Program.cs
 M CryptoWorkBenchAvalonia/App.axaml.cs
 M CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs
 M CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs
 M CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj
 M CryptoWorkBenchAvalonia/Models/DiagnosticDisplayItem.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchExecutionPipeline.cs
 M CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs
 M CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs
 M CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs
 M CryptoWorkBenchAvalonia/ViewModels/StatusViewModel.cs
 M CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs
 M CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj
 M CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs
 M CryptoWorkBenchGuiTest/WorkbenchReviewRegressionTests.cs
 M eng/prism/locks/Debug/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Debug/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Debug/CryptoWorkBenchGuiTest.lock.json
 M eng/prism/locks/Release/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/CryptoWorkBenchGuiTest.lock.json
 M eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/linux-x64/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/osx-arm64/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/osx-x64/CryptoWorkBenchAvalonia.lock.json
 M eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.Desktop.lock.json
 M eng/prism/locks/Release/rid/win-x64/CryptoWorkBenchAvalonia.lock.json
?? CryptoScriptUnitTest/PrintSourceTests.cs
?? CryptoWorkBenchAvalonia.Desktop/NativeDockWindowTrace.cs
?? CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs
?? CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs
?? CryptoWorkBenchAvalonia/Shell/DockDiagnosticsView.cs
?? CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs
?? CryptoWorkBenchAvalonia/Shell/DockEditorSession.Files.cs
?? CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs
?? CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs
?? CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs
?? CryptoWorkBenchAvalonia/Shell/DockLifecycleTrace.cs
?? CryptoWorkBenchAvalonia/Shell/DockOutputService.cs
?? CryptoWorkBenchAvalonia/Shell/DockOutputView.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellStartup.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellState.cs
?? CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml
?? CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs
?? CryptoWorkBenchAvalonia/Shell/DockTraceOutput.cs
?? CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs
?? CryptoWorkBenchAvalonia/Shell/DockWindowGeometry.cs
?? CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs
?? CryptoWorkBenchAvalonia/Shell/SourceNavigationResult.cs
?? CryptoWorkBenchGuiTest/DockDiagnosticsNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockDiagnosticsTests.cs
?? CryptoWorkBenchGuiTest/DockEditorNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockEditorTests.cs
?? CryptoWorkBenchGuiTest/DockFinalIntegrationTests.cs
?? CryptoWorkBenchGuiTest/DockGroupDocumentRedockTests.cs
?? CryptoWorkBenchGuiTest/DockIntegrationNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs
?? CryptoWorkBenchGuiTest/DockLayoutNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs
?? CryptoWorkBenchGuiTest/DockLayoutTests.cs
?? CryptoWorkBenchGuiTest/DockMouseRedockTests.cs
?? CryptoWorkBenchGuiTest/DockNativeAcceptanceProbe.cs
?? CryptoWorkBenchGuiTest/DockNativeChromeTests.cs
?? CryptoWorkBenchGuiTest/DockOperationContractTests.cs
?? CryptoWorkBenchGuiTest/DockOperationNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockOutputNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockOutputTests.cs
?? CryptoWorkBenchGuiTest/DockPartialFailureNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockPartialFailureTests.cs
?? CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs
?? CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockRedockNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockShellNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockShellTests.cs
?? CryptoWorkBenchGuiTest/DockToolNativeProbe.cs
?? CryptoWorkBenchGuiTest/DockToolTests.cs
?? CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.G108.cs
?? CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json
?? docs/gui/G1-01-dock-feasibility.md
?? docs/gui/G1-02-shell-implementation.md
?? docs/gui/G1-03-editor-mcp-integration.md
?? docs/gui/G1-04-variables-info-integration.md
?? docs/gui/G1-05-output-pipeline.md
?? docs/gui/G1-06-diagnostics-navigation.md
?? docs/gui/G1-07-layout-persistence.md
?? docs/gui/G1-08-final-validation.md
?? docs/gui/G1-08-native-acceptance-checklist.md
?? docs/gui/G1-A01-docking-architecture-review.md
?? docs/gui/G1-A02-maximize-layout-roles.md
?? docs/gui/G1-A03-persistence-cleanup-roles.md
?? docs/gui/G1-A04-R1-actual-callback-audit.md
?? docs/gui/G1-A04-R2-test-cleanup.md
?? docs/gui/G1-A04-R3-partial-failure-audit.md
?? docs/gui/G1-A04-R4-partial-failure-correction.md
?? docs/gui/G1-A04-dock-operation-contract.md
?? docs/gui/G1-A05-M1-final-owner-acceptance.md
?? docs/gui/G1-A05-native-chrome-input-acceptance.md
?? docs/gui/G1-A06-cross-platform-final-validation.md
?? docs/gui/G1-N1-F01-F2-native-window-investigation.md
?? docs/gui/G1-N1-F01-mouse-redock-correction.md
?? docs/gui/G1-N1-F03-group-document-redock.md
?? docs/gui/evidence/G1-01-results.json
?? docs/gui/evidence/G1-02-results.json
?? docs/gui/evidence/G1-03-results.json
?? docs/gui/evidence/G1-04-results.json
?? docs/gui/evidence/G1-05-results.json
?? docs/gui/evidence/G1-06-results.json
?? docs/gui/evidence/G1-07-results.json
?? docs/gui/evidence/G1-08-results.json
?? docs/gui/evidence/G1-A02-results.json
?? docs/gui/evidence/G1-A03-results.json
?? docs/gui/evidence/G1-A04-R4-results.json
?? docs/gui/evidence/G1-A04-results.json
?? docs/gui/evidence/G1-A05-results.json
?? docs/gui/evidence/G1-A06-results.json
?? docs/gui/evidence/G1-N1-F01-F2-results.json
?? docs/gui/evidence/G1-N1-F01-results.json
?? docs/gui/evidence/G1-N1-F03-results.json
?? eng/gui/Test-DockNativeTrace.ps1
?? eng/gui/Validate-G1LayoutRoles.ps1
?? eng/gui/Validate-G1NativeAcceptance.ps1
?? eng/gui/Validate-G1Platform.ps1
?? eng/gui/Validate-G1Shell.ps1
?? eng/gui/native/G1ShellProbe.csproj
?? eng/gui/native/Program.cs
?? eng/gui/native/packages.lock.json
?? prototypes/G1DockFeasibility/G1DockFeasibility.csproj
?? prototypes/G1DockFeasibility/NativeProbe.cs
?? prototypes/G1DockFeasibility/ProbeWorkspace.cs
?? prototypes/G1DockFeasibility/Program.cs
?? prototypes/G1DockFeasibility/README.md
?? prototypes/G1DockFeasibility/Session.cs
?? prototypes/G1DockFeasibility/Tests/DockTests.cs
?? prototypes/G1DockFeasibility/Tests/G1DockFeasibility.Tests.csproj
?? prototypes/G1DockFeasibility/Tests/packages.lock.json
?? prototypes/G1DockFeasibility/Validate.ps1
?? prototypes/G1DockFeasibility/Views.cs
?? prototypes/G1DockFeasibility/packages.lock.json
```

## 15. Abschlussentscheidung

**CONDITIONAL GO**. Die belegten Windows-Verträge und das Architekturreview liefern die Grundlage für eine spätere Integrationsvorbereitung. Die Entscheidung bleibt an die ausdrücklich fehlenden Linux-/macOS-Nativnachweise, die bekannte produktive Lease-Uhrgrenze, die getrennten Explicit-Voraussetzungen und die noch ausstehende kombinierte L1/G1-Validierung gebunden. Kein neuer relevanter Produktfehler wurde durch die beschriebenen Prüfungen nachgewiesen; die offenen Punkte werden nicht als bestanden ausgegeben.

A06 endet mit diesem Bericht zur Eigentümerabnahme. Eine Integration in `master` wurde nicht begonnen.
