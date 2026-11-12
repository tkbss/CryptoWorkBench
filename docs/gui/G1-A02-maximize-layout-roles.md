# G1-A02 – Maximize, Containerrollen und Layoutintegrität

Stand: 09.11.2026, Windows x64, ausschließlich Worktree `artifacts/worktrees/gui-g1`,
Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.
Kein Commit, Push, Merge, L1-Import, Paketupgrade oder Dock-Upstream-Eingriff.

## 1. Ergebnis und Abnahmegrenze

Der in A01 nachgewiesene Fehlerpfad ist korrigiert: Legale Dokument-Seitendrops
erzeugen eigenständige strukturelle Container, statt zusätzliche Hauptanker zu
beanspruchen. Capture und Maximize akzeptieren diese Bäume. Haupt- und Pane-Menüs
(einschließlich nativer Floating-Panes) benutzen dieselbe kontrollierte
Layoutbefehlsgrenze. Die sechs fachlichen Pane-Objekte und die Editor-Session
werden weiterverwendet.

Zusätzliche Sequenztests fanden und beheben drei unmittelbar relevante Probleme:
eine veraltete Auswahl nach Promotion eines Split-Containers, verlorene
Editor-Präsentation beim Wiederverwenden eines Tab-Templates und veränderte
Split-Anteile nach gerendertem Maximize/Restore. Diese Befunde sind nicht als
bereits durch A01 nachgewiesene Ursachen des damaligen Windows-Absturzes ausgegeben.

**Empfehlung: CONDITIONAL GO für die gesonderte Eigentümerabnahme von A03.**
A02 liefert keine allgemeine Crashfreiheit, keine beliebige Dock-Rollbackgarantie
und keine neue Mouse-Redock-Abnahme. B4 bleibt A04; die offenen F03-Eingabeprüfungen
und die Eigentümerabnahme bleiben offen. A03 wurde nicht begonnen.

## 2. Ausgangsstand und Bestandsschutz

Vor Produktänderungen wurden vollständiger Git-Status, Branch, HEAD, der getrackte
Diff, Kopien der bereits veränderten/ungetrackten Arbeitsdateien und ein SHA-256-
Manifest von **1.117** bestehenden Quelldateien/Repository-Artefakten gesichert:

- `artifacts/g1-a02/baseline/{branch,head,status}.txt`
- `artifacts/g1-a02/baseline/tracked.patch`
- `artifacts/g1-a02/baseline/files.json` und `baseline/files/`
- `artifacts/g1-a02/baseline/a01-artifacts.json`
- `artifacts/g1-a02/baseline/binaries.json`

Die uncommitteten G1-/F03-Arbeiten sind die Implementierungsbaseline, nicht ein
zu bereinigender Fremddiff. Der Hauptcheckout wurde nicht bearbeitet. Die
Abschlussmanifeste vergleichen gegen diese Baseline, nicht nur gegen Git HEAD.

Verbindliche Grundlage ist `G1-A01-docking-architecture-review.md`. G1-01 bis
G1-08, F01, F2 und F03 samt Evidence bleiben historische Nachweise. Insbesondere
wird die unvollständige F03-Abnahme nicht rückwirkend umgeschrieben.

Die A01-Windows-Ereignisse 1026/1000 und die isolierten Diagnoseergebnisse wurden
geprüft. Der ursprüngliche Stack führt über `Capture` zur Pane-Menübehandlung;
die ursprüngliche Benutzer-Mausfolge und der damalige Live-Baum sind weiterhin
nicht vollständig rekonstruierbar.

Die lokale Dock-Referenz bleibt Paket **12.1.0.13**, Upstream-Commit
`78da173545501e796f37bc8528e9e55d9cbfd991`. Alle Paket-Locks und frühere Berichte/
Evidence werden im abschließenden Integritätsnachweis separat geprüft.

Abschlussvergleich: **9** bestehende Dateien gezielt geändert, **8** neue
Repository-Dateien, keine ursprüngliche Datei entfernt. Alle **24** historischen
Berichte/Evidence-Dateien und **5** Paket-Locks sind bytegleich; zusätzlich sind
**421** A01-Artefakte und **451** historische Roh-Evidence-Manifesteinträge
SHA-256-identisch. Der lokale Dock-Upstream ist sauber. Die Benutzerlayoutdatei
behält SHA-256 `A505774C3DC0A3AB96DCC70AABA48CEDE2BF994F351554B1EED20BC8821E039D`.
`artifacts/g1-a02/integrity-result.json`, `historical-raw-integrity.json` und
`final-source-manifest.json` enthalten die einzelnen Vergleiche.
Die finalen Assemblies sind in `final-binaries.json` erfasst; `binary-parity.json`
bestätigt für jede Konfiguration denselben GUI-DLL-Hash in Desktop, GUI-Tests
und nativer Probe. Es blieb kein G1ShellProbe-Prozess geöffnet.

## 3. Permanente rote Regression vor der Produktkorrektur

`DockLayoutRoleTests` wurde vor Produktänderungen ergänzt und gegen den alten
Produktcode ausgeführt. Die archivierte Testfassung steht in
`artifacts/g1-a02/red-final/DockLayoutRoleTests.cs`, die Ergebnisse in
`red-final/red.trx` und `red-final/results.json`.

| Alte Implementierung, Matrix aus 30 Fällen | Ergebnis |
| --- | --- |
| Capture nach Left/Right/Top/Bottom, Tab und DocumentDock-Gruppe | 8 FAIL mit exakt `System.IO.InvalidDataException : Invalid fixed-shell anchors.` |
| Pane-Menü-Maximize nach denselben Seitendrops | 8 FAIL mit derselben unbehandelten Fehlersignatur |
| Hauptmenü-Maximize nach denselben Seitendrops | 8 FAIL: vorhandener äußerer Catch unterdrückt den Fehler, `IsMaximized` bleibt false |
| Fill, Capture und Hauptmenü, jeweils Tab/Gruppe | 4 PASS |
| Fill, Pane-Menü, jeweils Tab/Gruppe | 2 FAIL beim anschließenden Menüzugriff: der Editor ist nach gerendertem Restore nicht mehr am visuellen Baum |

Damit **26 FAIL / 4 PASS**, darunter 16 direkte Nachweise der ursprünglichen
Exception. Die Tests erwarten keine beliebige Exception; sie verlangen die
erfolgreiche Operation. Kein Fail wurde durch Deaktivierung oder Entfernen einer
Assertion in ein PASS umgewandelt.

Der erste eingeschränkte Build blieb vor Ausgabe hängen; der exakt zugeordnete
Prozessbaum wurde beendet. Der Nachweis erfolgte danach mit `-m:1` außerhalb der
Sandbox. `red-run.log`, `sandbox-hang.json`, `red-unsandboxed.log` und beide
Rot-TRX bleiben erhalten. Ein zusätzlicher UI-Pump im Testaufbau löste den
Presenter-Befund nicht; erst die Produktkorrektur beseitigte ihn.

## 4. Gewählter Identitäts- und Rollenvertrag

| Kategorie | Vertrag |
| --- | --- |
| Fachliche Panes | Genau die sechs bekannten IDs; im Live-Baum außerdem exakt die vom Layout gehaltenen Pane-Objekte. Keine Neuregistrierung des Editors. |
| Hauptlayout | `dock:root` und der eindeutige proportionale `dock:main`; Rückkehrrollen `dock:documents`, `dock:tools`, `dock:diagnostics` werden ausdrücklich erzeugt. |
| Dynamische Struktur | Neue IDock-Objekte erhalten beim ersten `InitDockable` eine `dock:container:<GUID>`-ID. Ein von Dock kopierter String verleiht keine Ankerrolle. |
| Reparenting | Ein bereits registriertes Objekt behält seine Identität. Repeated Float/Split/Focus erzeugt keine neue Identität für dasselbe Objekt. |
| Ganze ausgefloatete Anker | Das bestehende Objekt behält seinen historischen Rollenstring; der Hauptbaum erhält seinen Rückkehranker. Die Rolle wird im Hauptbaum aufgelöst, nicht über alle Fenster global. Die bestehende F03-Collapse-Ausnahme bleibt eng auf diese Floating-Anker begrenzt. |
| Geladene Layouts | Validierte Schema-1-Strukturen werden vor der Initialisierung ausdrücklich registriert. Die Persistenz bekommt kein neues Rollenfeld und keine allgemeine Migration. |

`DockContainerIdentity` benutzt schwache Objektschlüssel, damit entfernte
Container nicht allein wegen der Identitätsregistrierung festgehalten werden.
Die Hauptanker werden beim Erzeugen registriert; neue Bibliothekscontainer
werden vor Veröffentlichung von Owner/Factory/Context in `InitDockable` benannt.
Capture repariert oder benennt keine fehlerhaften IDs nachträglich um.

Die Trennung bereitet Variante B mit der Verantwortungstrennung aus C vor:
Dock besitzt die Präsentationsstruktur, die Anwendung weiterhin fachliche
Identität und Admission. Es gibt keinen allgemeinen Operationsvertrag, keinen
Transaktionsmanager und keine neue Business-Autorität für Dock.

## 5. Gemeinsame Validierung und notwendige Kompatibilität

Die bestehende bounded Schema-1-Validierung bleibt erhalten: Typ-/Feld-Whitelist,
sechs Pane-IDs, erlaubte Hierarchie, aktive Auswahl, Split-Sequenzen, Proportionen,
Fensterinventar, geschlossene Tools, keine unerlaubten Zyklen oder verschachtelten
Floating-Fenster. Die vorhandenen Negativtests einschließlich `duplicate-anchor`
bleiben unverändert.

Ergänzt werden:

- Owner/Factory-Konsistenz des Live-Baums und Bindung von Floating-Root, Window,
  Host und Hauptroot; serialisierte Modelle besitzen absichtlich noch keine
  Laufzeit-Owner und durchlaufen zunächst die strukturelle Validierung.
- Trennung von Pane- und Container-IDs, eindeutige neue dynamische IDs und
  passende DocumentDock-/ToolDock-Typen für Ankerrollen.
- Ablehnung eines Ersatz-Panes mit derselben ID wie das registrierte Original.
- Begrenzte, wiederholt enumerierbare Baumtraversierung sowie ein begrenzter
  ActiveDockable-Pfad; auch ein Auswahlzyklus kann den UI-Thread nicht endlos
  durchlaufen lassen.

Die neue Identitätsklasse wird in Schema 1 gespeichert. Nur für diese expliziten
`dock:container:`-Objekte setzt die Projektion `IsCollapsable=true`; andernfalls
würden neu gespeicherte Floating-Strukturen nach Restart leere Hosts behalten.
Die fünf permanenten Restart-/Redock-Fälle belegen diese notwendige kleine
Kompatibilitätskorrektur. Die umfassende Bereinigung alter Collapse-/Persistenz-
rollen bleibt A03. Alte gültige Schema-1-Dateien bleiben lesbar; ungültige Dateien
fallen weiterhin kontrolliert auf Default zurück.

### Zusätzlicher nativer Sequenzbefund

`native-first/Release-layout-roles.json` dokumentiert einen weiteren
`Invalid selected tab` nach wechselnden Seitendrops und anschließendem Fill.
Dock `CleanupProportionalDockTree` ersetzt einen proportionalen Wrapper durch
sein einziges Kind, aktualisiert aber nicht die Auswahl des Großeltern-Docks.
`EditorDockFactory.InitDockable` überträgt die Auswahl ausschließlich dann,
wenn genau dieses ausgewählte, entfernte Wrapper-Objekt noch genau das gerade
promovierte Kind enthält. Unbeteiligte oder beliebige fehlerhafte Auswahlen
werden nicht normalisiert. Das ist eine lokale Kompatibilitätskorrektur dieses
Initialisierungsschritts, keine zusätzliche Zulassung einer Dockoperation.

## 6. Ergebnisvertrag und Fehlergrenzen

`ExecuteLayout` ist der gemeinsame UI-Einstieg für Maximize, Restore und Reset.
Das Hauptmenü und jede Pane-Menüinstanz – auch im Floating-Host – verwenden ihn.
Die vorhandenen Bool-Methoden bleiben für interne Aufrufer und Bestandstests
erhalten. Recoverable Fehler werden mit Ursache und Stack in Trace erfasst;
der Benutzer erhält eine verständliche Statusmeldung.

| Ergebnis | Bedeutung und Zustand |
| --- | --- |
| Unavailable | Bestehendes Admission-Gate lehnt ab oder kein anwendbarer Pane; keine neue Operation. |
| Completed | Layoutbefehl vollständig angewandt. |
| Rejected | Validierung/Preflight scheitert vor der Strukturmutation; kein automatischer Reset oder erneuter Versuch. |
| Recovered | Mutation wurde begonnen und ist fehlgeschlagen; die befehlsspezifische Kompensation wurde anschließend überprüft. |
| Faulted | Kompensation konnte nicht vollständig bestätigt werden. Weitere Layoutänderungen und Persistenz werden gesperrt; Editor-/Save-/Shutdown-Verträge bleiben bestehen. Status fordert zum Speichern vor Neustart auf. |

Maximize erfasst vor der ersten Fokusänderung alle betroffenen Listen-/Auswahl-
Referenzen und sämtliche `Proportion`/`CollapsedProportion`-Werte. Bei einem
Notification-Fehler werden die gespeicherten Felder zurückgesetzt und die
Referenzen sowie der normale Capture geprüft. Restore bewahrt bei Fehlern die
zuvor fokussierte Darstellung samt normalem Snapshot. PropertyChanged kann
nach gesetztem Backing Field werfen; die Kompensation versucht deshalb jedes
gesicherte Fokusfeld einzeln und prüft den Endzustand.

Reset besitzt bereits die vollständige Präsentationsersetzung. Es bereitet
Default und die validierte vorherige normale Darstellung vor und bindet immer
dieselben sechs Pane-Objekte ein. Schlägt die Ersetzung fehl, wird die vorherige
Darstellung genau einmal kompensierend installiert. Native Floating-Hosts können
dabei neu entstehen; dies wird in der Benutzerstatusmeldung ausdrücklich genannt.
Es gibt keinen stillen fachlichen Reset und keine Wiederholung des fehlgeschlagenen
Reset-Befehls.

Eine dauerhaft scheiternde Präsentationsbenachrichtigung ist separat getestet:
Faulted sperrt Docking und Layoutpersistenz, der strukturelle Baum bleibt in
diesem geprüften Fehlerfall gültig, derselbe Editor bleibt registriert, und sein
Text kann über den bestehenden SaveIntent-/CommitSaveAsync-Weg gespeichert
werden. Das ist eine kontrollierte Begrenzung, **keine Garantie für beliebige
fremde Callbacks, Betriebssystemfehler oder Reentranzmutationen**.

## 7. Gerendertes Maximize/Restore

Der vorherige Fokusmechanismus sicherte nur Pfad-Proportionen. Dock aktualisiert
beim Rendern auch `CollapsedProportion`; diese Werte beeinflussen die nächste
Normierung. Nach wechselnden Side-Splits änderten sich dadurch beispielsweise
Anteile von 0,5 auf 2/3. `mixed-red/mixed-red.trx` und der erste native Lauf halten
den Befund fest. Die komplette Sicherung beider Größen verhindert diesen Drift.

Außerdem kann Dock das normale Tab-Template zwischenspeichern, während die
Fokusdarstellung einen zweiten Presenter erzeugt. Der alte Presenter hatte
seinen sessioneigenen Editor bereits freigegeben. `DockShellPresentation` bindet
den bestehenden Inhalt deshalb beim erneuten visuellen Anhängen des Pane-
Presenters an. Ein nur erzeugtes, noch nicht angehängtes Template entzieht dem
sichtbaren Pane den Inhalt nicht. Editor, TextDocument und ViewModels bleiben
dieselben Instanzen; Session-/Coordinator-Code wird dafür nicht verändert.

Die permanenten Tests decken alle sechs Panes, mehrere DocumentDocks,
wechselnde Seiten, ganze ursprüngliche Anker, Floating-Document, Floating-
Toolgruppe, weitere Hosts, geschlossenes Status-Tool, mehrfach verschobenes
Output, History, Undo/Redo, registrierte Editor-ID und WorkspaceGeneration ab.
Die Vergleichsprüfungen verwenden sowohl Modellreferenzen/JSON als auch den
wirklich angehängten Editor und native HWNDs.

## 8. Native Windows-Ergebnisse

Die vorhandene `G1ShellProbe` wurde um `layout-roles` erweitert.
`eng/gui/Validate-G1LayoutRoles.ps1` baut dieselbe Probe mit Locked-Restore,
protokolliert PIDs/Exitcodes und setzt pro Prozess eine 180-Sekunden-Grenze.
Ein Timeout wäre ein dokumentierter FAIL, kein Wiederholungsversuch.
Alle Layoutdateien liegen neben der jeweiligen Evidence, nicht im Benutzerprofil.

| Modus | Debug | Release | Aussage |
| --- | ---: | ---: | --- |
| layout-roles | 194 PASS | 194 PASS | Tab, Gruppe, ganzer Anker × Fill/4 Seiten; beide Menüpfade; komplexe Hosts; kontrollierter Fehler; Reset; Shutdown |
| layout | 48 PASS | 48 PASS | Bestehende G1-07-Lifecycle-/Layoutprobe |
| layout-restart | 56 PASS | 56 PASS | Persistenz/Restart |
| layout-invalid | 49 PASS | 49 PASS | Ungültige gespeicherte Datei |
| redock | 134 PASS | 134 PASS | Bestehende F01/F03-Host-/Gruppenprüfungen |
| editor | 54 PASS | 54 PASS | Bestehende native Editor-/MCP-Schutzprüfung |
| integration | 28 PASS | 28 PASS | Bestehende Gesamtintegration einschließlich Save/Open und Shutdown |

Quelle: `artifacts/g1-a02/native-final/commands.json` und die jeweils benannten
Ergebnisdateien. Jeder dieser 14 Prozesse endete mit **Exitcode 0**, kein Timeout.
Die neue Probe prüft zerstörte Source-HWNDs, fortbestehende andere HWNDs,
Host-/DockControl-Registrierungen, denselben Editor und die vollständige Freigabe
beim Shutdown. Die Window-Journale bleiben als JSONL erhalten.

**Eingabeart:** programmatische DockManager-Operationen und geroutete MenuItem-
Click-Ereignisse im echten nativen Prozess. Das sind keine ausgeführten
Cross-Window-Mausgesten. Die programmatischen PASS ersetzen keine M2–M8-Abnahme.

## 9. Vollständige Regression und unabhängige Wiederholung

Die verbindlichen Abschlusszahlen und zugehörigen TRX-Pfade stehen in
`evidence/G1-A02-results.json`. Die vollständige Matrix verwendet pro
Konfiguration einen Locked-Restore; sämtliche vorhandenen Assertions bleiben
erhalten. Neue GUI-Tests erhöhen die historische Zahl von 984 auf 1.043.

| Suite | Debug | Release |
| --- | ---: | ---: |
| GUI, einschließlich 59 neuer A02-Fälle | 1.043 PASS | 1.043 PASS |
| MCP | 176 PASS | 176 PASS |
| Core | 11.512 PASS | 11.512 PASS |
| G1-01 Headless | 12 PASS | 12 PASS |

Die 13 bestehenden MCP-Explicit-Fälle bleiben separat ausgeschlossen; Core
behält 26 Skips und einen nicht ausgewählten Explicit-Fall. Sie werden nicht
als neue PASS gezählt. Die beiden neuen Testklassen werden in eigenständigen
Testprozessen zusätzlich wiederholt; native A02-Proben laufen ebenfalls in
frischen Prozessen. Es gibt keine pauschalen Retries oder kaschierenden Delays.
UI-/Fenster-Wartepunkte sind an überprüfbare Zustände gebunden.

Die Rohdaten behalten auch frühere Zwischenstände: ein Debug-Build mit
Release-Restore-Artefakten scheiterte an `AttachDeveloperTools`; ein früher
Release-Gesamtlauf hatte ausschließlich die damals noch rote neue Mischfolge
als Fehler. Diese Läufe werden nicht als Abschluss-PASS ausgegeben.

Ein später ergänzter künstlicher Auswahlzyklus blockierte zunächst bereits
Dock's Live-Setter im Testaufbau, bevor der geprüfte Menüpfad erreicht wurde.
Dieser Prozess wurde gezielt beendet (`selection-test-hang.json`). Die finale
Testfassung injiziert die fehlerhafte Metadatenreferenz ohne aktive Templates
und ohne Factory-Callbacks während der Injektion, aktiviert danach wieder die
Factory und prüft die begrenzte Auswahltraversierung. Die vollständigen finalen
GUI-Nachweise stehen deshalb unter `independent/GUI-{Debug,Release}.trx`, die
zusätzlichen 59er-Wiederholungen unter `independent/A02-repeat-*.trx`.
MCP/Core/Prototyp verwenden die vollständigen Läufe unter `regression-final/`.
Die native A02-Wiederholung des finalen Quellstands steht unter `native-repeat/`.

## 10. Unverändert offene F03-Eingabeabnahme

- `completion-in-floating-host`: historischer F03-FAIL, weiterhin OPEN.
- `completion-retired-on-relocation`: historischer F03-FAIL, weiterhin OPEN.
- Externe Clipboard-/Output-/Diagnostics-/Layout-Eingabematrix: weiterhin
  unvollständig; alte Timeouts/Unterbrechungen werden nicht überschrieben.
- Eigentümerabnahme des korrigierten normalen Release-Mausworkflows bleibt offen.
- F2-M2-PASS gilt nur in seinem dokumentierten Umfang; M3–M8 bleiben OPEN.

A02 ergänzt erfolgreiche automatische native Release-Läufe. Das erfüllt einen
automatischen Teil der früher fehlenden Release-Prüfung, ohne die alte F03-
Evidence oder ihre damals zutreffende Bewertung zu ändern.

## 11. Eigenständiger Abschlussreview

| Schweregrad | Befund / Bewertung |
| --- | --- |
| Hoch, A02 korrigiert | Kopierte DocumentDock-ID führte nach legalem Side-Drop zu Capture-/Maximize-Fehler und unbehandelter Pane-Menüexception. Rot-/Grün- und native Nachweise vorhanden. |
| Mittel, A02 korrigiert | Gerendertes Restore verlor Presenter-Zuordnung und Split-Anteile; Mischfolgen hinterließen eine ungültige Promotion-Auswahl. Zusätzliche Nachweise und eng begrenzte Korrekturen vorhanden. |
| Mittel, im Review korrigiert | Unbegrenzter ActiveDockable-Pfad und Wiederverwendung eines Traversierungs-Enumerables; neue Tests prüfen zyklische Auswahl und erneute Enumeration. Ersatz-Pane mit gleicher ID wird ebenfalls abgelehnt. |
| Hoch, bestehend / A04 | `_mutationDepth` erlaubt weiterhin unabhängige Callback-Mutationen innerhalb einer zugelassenen Dockoperation (B4). A02 erweitert den Bypass nicht; der neue Faulted-Zustand schränkt ihn zusätzlich ein. |
| Mittel, bestehend / A03 | Umfassende Legacy-Collapse-/Cleanup-/Persistenzrollen sind noch nicht konsolidiert. Nur neue explizite dynamische Container erhalten die erforderliche Roundtrip-Behandlung. |
| Mittel, bestehend | F03-Floating-Completion und externe Eingabeabnahme bleiben offen. |
| Begrenzung | Befehlsspezifische Fokus-/Reset-Kompensation ersetzt keinen allgemeinen Operationsvertrag. Bei nicht bestätigter Kompensation wird kontrolliert gesperrt, keine angebliche vollständige Reparatur gemeldet. |

Unverändert bleiben EditorCoordinator, EditorBridge, WorkbenchAutomationService,
InterpreterHostQueue, Interpreter, Tickets, Leases, Pending-/Observer-Verträge,
SaveIntent und Shutdown/Drain. Es gibt keine neue Busy-Ausnahme, keinen Replay
und keine automatische Session-Neuregistrierung. F01/F03-Bestandstests und die
native Redock-Probe prüfen ihre bisherigen Schutz- und Cleanup-Eigenschaften.

Die neue Container-ID-Konvention ist mit mehreren DocumentDocks vereinbar. Sie
führt aber noch keine weiteren fachlichen Dokumente ein: die sechs erlaubten
Pane-IDs und der eine registrierte CryptoScript-Editor bleiben absichtlich
begrenzt. Eine spätere Multi-Document-Implementierung muss die fachliche
Inventar-/Session-Architektur ausdrücklich erweitern.

## 12. Vollständige Änderungsliste

Geänderte Baseline-Dateien:

1. `CryptoWorkBenchAvalonia/Shell/DockLayoutStore.cs`
2. `CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs`
3. `CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs`
4. `CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs`
5. `CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs`
6. `CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs`
7. `CryptoWorkBenchGuiTest/DockShellNativeProbe.cs`
8. `CryptoWorkBenchGuiTest/DockToolTests.cs` – ausschließlich optionaler Startup-
   Fixturepfad; vorhandene Tests/Assertions unverändert.
9. `eng/gui/native/Program.cs`

Neue Repository-Dateien:

10. `CryptoWorkBenchAvalonia/Shell/DockContainerIdentity.cs`
11. `CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs`
12. `CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs`
13. `CryptoWorkBenchGuiTest/DockLayoutFailureTests.cs`
14. `CryptoWorkBenchGuiTest/DockLayoutRoleNativeProbe.cs`
15. `eng/gui/Validate-G1LayoutRoles.ps1`
16. `docs/gui/G1-A02-maximize-layout-roles.md`
17. `docs/gui/evidence/G1-A02-results.json`

Neue lokale Build-/Test-/Diagnoseartefakte liegen ausschließlich unter
`artifacts/g1-a02/` sowie den üblichen Build-Ausgabeverzeichnissen des G1-
Worktrees. Ihre SHA-256-Manifeste, finaler Git-Status und der gegen die
Arbeitsbaseline abgegrenzte Diff sind dort referenziert. `git diff --check`
und eine zusätzliche Whitespace-Prüfung der neuen Dateien gehören zum Abschluss.

## 13. Restrisiken und Übergabe

CONDITIONAL GO bedeutet: A03 kann nach ausdrücklicher Eigentümerabnahme auf
diesem Stand aufbauen; A02 gibt weder A03 noch A04 automatisch frei.
Die offenen Punkte sind die historische Eingabe-/Mausabnahme, B4 und die
umfassende Legacy-Rollenbereinigung. Die überprüften Fehlerfälle belegen einen
kontrollierten Prozess und erhaltene fachliche Arbeit, keine pauschale
Fehlerfreiheit der gesamten Dock-Bibliothek.

Nach Erstellung und Prüfung dieser Evidence endet die Arbeit an A02.
