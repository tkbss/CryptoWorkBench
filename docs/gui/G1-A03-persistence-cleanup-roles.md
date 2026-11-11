# G1-A03 – Persistenz- und Cleanup-Rollen

Arbeitsstand: 09.11.2026, `feature/gui-g1`, HEAD
`7ecac04483c10722c4cb4b459e3f6cb5157c6af1`, Worktree `artifacts/worktrees/gui-g1`.

**Abschluss: CONDITIONAL GO für A04 nach ausdrücklicher Eigentümerabnahme.**
A03 ist implementiert; vollständige automatisierte Regression, unabhängige
Wiederholung und finale native Debug-/Release-Matrix sind grün. B4 und die
bereits offenen F03-/Maus-/Plattformnachweise bleiben abgegrenzt. A04 wurde
nicht begonnen.

## Vor Produktänderungen: Rollen- und Lebenszyklusmatrix

Diese Matrix wurde vor der ersten A03-Produktänderung erstellt. Sie beschreibt
den angestrebten Rollenvertrag; Testresultate folgen nach der Rot-/Grün-Prüfung.
Baseline: 1.125 Dateien einschließlich aller uncommitteten G1-/A02-Arbeiten,
gesichert unter `artifacts/g1-a03/baseline/`. A01-/A02-Artefakte wurden zusätzlich
vollständig gehasht. Frühere Berichte, Evidence und Locks werden nicht geändert.

| Containertyp / Position | Rolle | Persistierte Identität | A02-Rekonstruktion | A03-Soll / letztes Kind | Host-Lebenszyklus |
| --- | --- | --- | --- | --- | --- |
| RootDock der Haupt-Shell | Hauptroot | `dock:root` | nicht kollabierbar | nicht kollabierbar; Lebensdauer der Shell | Hauptfenster nur über regulären Shutdown |
| ProportionalDock `dock:main` im Hauptbaum | Hauptstruktur | `dock:main` | nicht kollabierbar | erhalten; Träger der Rückkehrbereiche | kein eigener Host |
| DocumentDock unter Hauptstruktur | Dokument-Rückkehranker | `dock:documents` | nicht kollabierbar | bei Leerstand erhalten | kein eigener Host |
| ToolDock unter Hauptstruktur | Tool-/Diagnostics-Rückkehranker | `dock:tools` / `dock:diagnostics` | nicht kollabierbar | bei Leerstand erhalten | kein eigener Host |
| Sonstiger DocumentDock/ToolDock im Hauptbaum | dynamischer Container | A02-GUID oder Legacy-ID | nur GUID kollabierbar | bei letztem entfernten Kind regulär kollabieren | kein eigener Host |
| Sonstiger ProportionalDock im Hauptbaum | dynamische Split-Struktur | A02-GUID oder Legacy-ID, historisch auch leer | nur GUID kollabierbar; leere ID abgelehnt | reguläre Collapse-/Promotion-Regeln | kein eigener Host |
| Floating-RootDock | Floating-Root | A02-GUID, Legacy-ID oder historisch leer | nur GUID kollabierbar; leere ID abgelehnt | kollabierbar, wenn sein letzter Inhalt entfernt wurde | eigenes Quellfenster wird synchron zerstört und deregistriert |
| DocumentDock/ToolDock im Floating-Root | Floating-Inhaltsgruppe | GUID, Legacy-ID oder historischer Ankerstring | GUID kollabierbar; bekannter Anker nur per F03-Ausnahme | kollabierbar unabhängig vom historischen Ankerstring | Root-Collapse setzt normale RemoveWindow-Kette fort |
| ProportionalDock im Floating-Root | temporäre Floating-Split-Struktur | GUID / Legacy / leer | nur GUID kollabierbar | regulär kollabierbar; Composite-Aufzählung synchron schützen | Quellhost nach vollständigem Leeren freigeben |
| Document / Tool | fachlicher Pane, kein Container | sechs stabile Pane-IDs | Originalobjekte werden wiedergebunden | keine Instanzduplikation, kein neuer Editor | Dokumenthost bleibt gegen manuelles Schließen geschützt |

Ausnahmen von normalem Collapse sind ausschließlich Hauptroot, Hauptstruktur
und echte Rückkehranker im Hauptbaum. Typ, tatsächlicher Strukturpfad und
Root-Zugehörigkeit müssen zusammenpassen. Derselbe historische Ankerstring in
einem Floating-Baum verleiht dort keine Hauptlayoutrolle.

## Ausgangsanalyse

`Encode` validiert den Live-Baum, projiziert ausschließlich Präsentationsdaten
und prüft das serialisierte Ergebnis erneut über `Decode`. `Decode` prüft
JSON/Typen/Felder/Größen, deserialisiert, validiert und projiziert erneut.
`Project` setzt seit A02 nur Docks mit `dock:container:` auf kollabierbar.
Alle anderen unterstützten Strukturcontainer bleiben dadurch fixiert.

`Install` registriert validierte Containeridentitäten, bindet die sechs
vorhandenen Panes ein, beendet vorherige Hosts über `ExitWindows` und
initialisiert die neue Darstellung mit derselben Factory/Session.
`DockContainerIdentity` unterscheidet bekannte Objekte von neuen, deren kopierte
IDs bei `InitDockable` ersetzt werden. Diese Identitätsfunktion bleibt erhalten.

`CollapseDock` besitzt noch eine ID-basierte F03-Ausnahme für leere, ausgekoppelte
Originalanker. Der Composite-Scope verzögert die Entfernung leerer Quelldocks
bis nach DockManagers laufender Gruppenaufzählung. `RemoveWindow` markiert nur
den tatsächlich leeren, eigenen Quellhost innerhalb der bereits zugelassenen
Mutation; `DocumentSafeHostWindow.OnClosing` lässt dessen notwendiges Schließen
zu, schützt aber unbeteiligte oder dokumenthaltige Hosts weiter.

`EnsureReturnAnchor` erzeugt einen fehlenden Hauptanker nach Whole-Anchor-Float.
`CloseEmptyHosts` ist bisher eine zusätzliche globale Fenstersuche nach den
direkten Shell-Move-/Close-Aufrufen. A03 soll deren Notwendigkeit durch normale
rollenrichtige Collapse-Lebenszyklen beseitigen, nicht die Suche ausweiten.

Upstream `RootDock.ExitWindows` speichert und beendet die Hosts seines
Fensterinventars. `Factory.RemoveWindow` beendet den einzelnen Host, respektiert
einen abgelehnten Close und löst danach Root-/Window-/Layout-Verknüpfungen.
Diese vorhandenen Lebenszyklen bleiben die Grundlage; keine zeitgesteuerte
Reparatur, kein Dock-Operationsmanager und keine zusätzliche Busy-Ausnahme.

## Historische Schema-1-Datei

Die unveränderte Datei
`artifacts/g1-07/native-Release-layout.json.layout.json` vom G1-07-Stand enthält
ein Floating-Info-ToolDock mit `dock:tools`, einen Floating-Root mit leerer ID,
die drei Hauptanker und ein geschlossenes Status-Tool. Leere Struktur-IDs wurden
damals regulär gespeichert; sie sind kein Beleg für einen weiteren Hauptanker.
Die permanente Fixture wird bytegleich übernommen. A02 lehnt diese Datei wegen
der leeren Floating-Root-ID ab. Zusätzlich werden Legacy-IDs ohne A02-Präfix
isoliert geprüft, damit die Rekonstruktions- und Cleanup-Fehler getrennt sichtbar
werden.

Die konservative Kompatibilitätsregel versieht nur namenlose unterstützte
Nicht-Hauptroot-Container mit neuen strukturellen IDs. Fehlende
Hauptrollen, doppelte Hauptanker oder fehlerhafte Pane-IDs werden nicht geraten
oder repariert. Referenzstruktur und anschließende vollständige Validierung
bleiben maßgeblich. Schema 1 benötigt dafür keine neue Formatversion.

## Nachgewiesene Ausgangsfehler und Umsetzung

Der permanente Rotlauf gegen den unveränderten A02-Produktstand liegt in
`artifacts/g1-a03/red/`: **14 Failed, 12 Passed**. Zwölf Legacy-Sequenzen
(sechs Quellstrukturen, jeweils Factory und DockManager) nehmen den Move/Drop
an, lassen jedoch den vollständig geleerten Quellhost sichtbar. Die echte
G1-07-Datei wird wegen ihrer leeren Floating-Root-ID abgelehnt. Ein weiterer
Test weist die falschen rekonstruierten Floating-Rollen direkt nach. Die zwölf
entsprechenden neuen GUID-Sequenzen bestehen bereits auf A02. Die archivierte
Testdatei und die vor Implementierung gesicherte Matrix trennen diesen Nachweis
von den später hinzugefügten präventiven Tests.

`DockContainerRoles` ist die gemeinsame, rein strukturelle Rollenbestimmung
für `Project` und `EditorDockFactory.InitDockable`. Geschützt sind ausschließlich
Hauptroot, die `dock:main`-Proportionalstruktur im Hauptbaum und typgerechte
Rückkehranker darunter. Floating-Bäume werden bei dieser Suche nicht traversiert.
Alle anderen unterstützten Docks sind kollabierbar. Die A02-Identitätsregistry
vergibt weiterhin neue GUIDs nur für bisher unbekannte Containerobjekte;
bekannte GUIDs und nichtleere historische IDs bleiben erhalten.

Dock ruft `InitDockable` bei Add/Insert **vor** dem Einfügen in die Kinderliste
auf. Der neue Rückkehranker benötigt deshalb bereits dort seinen Schutz:
typgerechter Anker, tatsächlicher Ziel-Owner unter der Hauptstruktur und noch
kein vorhandener Anker derselben Rolle. Ein bloßer bekannter ID-String oder
eine Floating-Herkunft reicht nicht. Der erste Implementierungsversuch ohne
diese Vor-Einfüge-Regel erzeugte bei Whole-Anchor-Float ungültige Proportionen;
`green-1` dokumentiert 15 Fehler. Die Korrektur ergibt in `green-2` **163/163**
bestehende und neue gezielte Regressionen. Keine bestehende Assertion wurde
dafür abgeschwächt.

Die eng begrenzte Schema-1-Normalisierung benennt namenlose Nicht-Hauptroot-
IDocks einmalig vor der vollständigen Validierung. Der begrenzte Baumlauf
verhindert auch dabei Zyklen und übergroße Bäume. Pane-IDs, nichtleere IDs,
Whitespace-IDs, Typen, Referenzen und Hauptrollen werden nicht repariert.
Nach dem ersten normalen Speichern bleiben die neuen Identitäten über weitere
Starts stabil. Leere Splitter-IDs sind weiterhin zulässig, da Splitter keine
IDocks sind. Historische Herkunft ist kein dauerhafter Collapse-Schutz.

Die bisher erlaubte Abwesenheit eines Rückkehrankers wird nicht zu einer
erfundenen Rolle umgedeutet: der vorhandene `EnsureReturnAnchor`-/`Anchor`-Pfad
erzeugt bei Bedarf einen echten Hauptanker. Fehlende Hauptstruktur, doppelte
Hauptanker und ungültige Pane-Inventare bleiben Ablehnungsgründe. Die
Nicht-Hauptroot-Normalisierung ist die einzige neue Kompatibilitätsöffnung;
Schema, Whitelists, Größenlimits, Privacy-Projektion, atomarer Replace und
Default-Fallback bleiben bestehen.

## Cleanup und Startup-Lebenszyklus

Die globale `CloseEmptyHosts`-Suche samt ihren Aufrufen wurde entfernt.
Shell-Move/Close, Factory und DockManager benutzen nun dieselbe reguläre
Collapse-Kette. Eine nichtleere Gruppe bleibt bestehen; das letzte entfernte
Kind löst Eltern-Collapse und schließlich `RemoveWindow` aus. Ein anderer
Floating-Host wird weder durchsucht noch verändert. Die ID-basierte temporäre
F03-Freischaltung in `CollapseDock` ist durch die dauerhaft korrekte Floating-
Rolle ersetzt. Die Whole-Anchor- und Document-Side-Drop-Regressionen aus A02/F03
sowie die neue Restore-Matrix sichern die Gleichwertigkeit.

Unverändert bleiben F01s genaue Prüfung des eigenen, tatsächlich leeren
Quellfensters innerhalb einer zugelassenen Mutation, der manuelle
Dokumenthost-Schutz, der synchrone F03-Composite-Scope und `RemoveWindow` selbst.
Der Composite-Scope darf leere Quelldocks bis zum Ende der laufenden
Gruppenaufzählung zurückhalten; das ist eine begrenzte synchrone Ausnahme,
kein Timer und kein weiterer Operationszulassungsweg.

Die neue native Save/Restart-Matrix fand zusätzlich einen vorhandenen
Startup-Lifecycle-Fehler: `Install(..., present:false)` erzeugt Hosts, aber
der produktive DockControl hat `InitializeLayout=false` und zeigt diese
anschließend nicht an. Die alte Headless-Fixture führte `ShowWindows` selbst
aus; der alte native Restart prüfte Geometrie und Modell, nicht explizit die
Sichtbarkeit jedes wiederhergestellten Hosts. `native-first` enthält die
Timeouts und eine unabhängige HWND-Zählung mit erzeugten, unsichtbaren Fenstern.
Die Shell führt jetzt beim tatsächlichen `Opened` einmal das vorhandene
`Root.ShowWindows` aus, nachdem der Owner verfügbar ist. Die Hilfestellung
wurde aus der Fixture entfernt und eine explizite produktive Startup-Regression
ergänzt. Kein nachträglicher Reparaturtimer und keine erneute Editorregistrierung.

## Sicherheits- und Reviewgrenzen

Die Geschäftsverträge, Coordinator-/Observer-/Lease-Zustände, SaveIntent und
Drain bleiben unverändert. Die neuen Sequenzen prüfen ursprüngliche Pane-
Objekte, Editor, TextDocument, UndoStack, Snapshot einschließlich Revision und
Editor-ID, WorkspaceGeneration, History und ViewModel-/Output-/Diagnostics-
Objekte. Wiederholter Reset bleibt eine Präsentationsersetzung innerhalb
derselben Session. Ein neuer nativer Prozess hat erwartungsgemäß eine neue
Editor-Session; die Layoutdatei transportiert weder Quelltext noch Fachzustand.

Ungültige persistierte Bäume werden vor Installation und Host-Präsentation
abgelehnt. Die neue Normalisierung lockert weder Pane-Eindeutigkeit noch
Cycle-/Typ-/Feld-/Hierarchieprüfung. Zusätzliche negative Legacy-Fixtures
prüfen leeren Hauptroot, fehlende Hauptstruktur, leere/doppelte Panes,
doppelte Hauptanker/GUIDs, Whitespace, Zyklen, Context und falschen Ankertyp.
Zwei Fehler in der ersten neuen Negativtest-Fixture (gemeinsam verwendeter
JsonNode und verwaiste ActiveDockable-Referenz) sind in `green-3` erhalten und
wurden in der Fixture korrigiert, ohne die erwartete Ablehnung abzuschwächen.

**A04-Abhängigkeit:** Ein beliebig werfender Dock-/PropertyChanged-Callback
kann eine bereits begonnene allgemeine Dock-Mutation teilweise ausführen.
A03 verspricht dafür keinen Rollback. A02s eng begrenzte Focus-/Reset-
Kompensation bleibt bestehen. Ebenso bleibt B4s Zulassung unabhängiger
reentranter Mutationen bei `_mutationDepth > 0` ein offener Befund hoher
Priorität für A04. A03 erweitert diesen Pfad nicht und führt keine neuen
Busy-Ausnahmen, keinen Operationsmanager und keine automatische Wiederholung
fehlgeschlagener Dock-Aktionen ein.

## Testumfang und native Evidenz

Die 44 neuen permanenten A03-Fälle umfassen die 26 ursprünglichen Rot-/Grün-
Fälle, zehn negative Legacy-Varianten, drei Starts mit atomarer Speicherung,
zwei partielle Gruppen-/Reset-Sequenzen, drei Admission-Zustände, einen
Default-Fallback mit anschließendem Redock und den produktiven Startup-Pfad.
Die partiellen Gruppenfälle bewahren auch tatsächlich erzeugte Print-Ausgabe,
Diagnostics-Einträge, Filter und Präsentationsfeedback über Cleanup und drei
Reset-/Float-/Redock-Zyklen. Bereits vorhandene Tests decken Whitelist,
Dateigröße, atomare Schreibfehler, MCP-Leases, Host-Busy/Modal, Focus-Fehler,
Document-Side-Drops, F01 und F03 zusätzlich ab.

`eng/gui/Validate-G1LayoutRoles.ps1 -PersistenceCleanup` erweitert den vorhandenen
G1ShellProbe um sechs Save-Prozesse und je zwei unabhängige Restart-Prozesse
(aktuelle IDs / synthetische Legacy-IDs), dazu zwei Starts derselben echten
historischen Fixture. Alle Dateien liegen im angegebenen neuen Evidence-
Verzeichnis. Die unveränderte Eingabedatei wird zusätzlich als
`*.input-layout.json` archiviert, weil normaler Shutdown den regulären
isolierten Store aktualisiert. PID und Exitcode stehen in `commands.json`.

Die native Prüfung erfasst neben Factory-/Root-Registrierungen eigenständig
`IsWindow` und `EnumWindows`, Win32-Owner und WS_CHILD, Model-/Host-/Root-
Verknüpfungen und unveränderte HWNDs anderer Hosts. Bei mehreren Kindern
muss das Quellfenster bis zum letzten Kind erhalten bleiben. Maximize,
Restore, Reset, erneutes Float/Redock und finaler Shutdown folgen im selben
Prozess. Window-Journale sind reine Beobachtung; ihr bestehender Sampling-
Timer führt keinerlei Cleanup aus. DockManager-/Factory-Aufrufe sind ausdrücklich
programmgesteuert und **keine Mausabnahme**.

Der zweite Release-Gegenlauf (`native-second`) besteht in allen 20 neuen
A03-Prozessen mit insgesamt 488 Prüfungen. Die alte A02-Rollenprobe scheitert
in den beiden Zwischenläufen separat an `main active`, nachdem Drop,
Quellfreigabe und erhaltener Zweithost bestanden haben. Ein probeweises Warten
auf den Dispatcher-Render änderte daran nichts und wurde wieder entfernt.
Eine lesende Desktopdiagnose zeigte eine andere Vordergrundanwendung; es
wurden keine fremden Eingaben injiziert. Dieser Zwischenbefund wird nicht als
Dock-Cleanup-Fehler oder als erfolgreiche Fokusabnahme umgedeutet.

### Finale native Runde nach Fortsetzung

Der zwischenzeitliche Lauf `native-final` wurde nach dem physischen Escape-
Stopp von Computer Use beendet; `user-stop.json` hält die ausschließlich
beendeten Testprozess-IDs fest. In diesem Zwischenlauf wurde genau ein
Debug-Integrationsfenster über Computer Use aktiviert, ohne Dock-Mausgesten.
Nach der Rückmeldung „Entsperrt, keine parallele Bedienung“ wurde die gesamte
Matrix in `native-resumed` in neuen Prozessen ausgeführt. **Keine weiteren
Computer-Use-Eingaben und keine Produktänderungen** waren dafür nötig.
Alle bisherigen Fokusprüfungen bleiben unverändert und bestehen jetzt.

| Finale native Modi | Prozesse je Konfiguration | Debug Passed | Release Passed | Failed |
| --- | ---: | ---: | ---: | ---: |
| A03: sechs Save- und zwölf Current-/Legacy-Restart-Prozesse, zwei echte historische Starts | 20 | 488 | 488 | 0 |
| A02 Layoutrollen | 1 | 194 | 194 | 0 |
| Layout / Restart / Invalid | 3 | 153 | 153 | 0 |
| F01/F03 Redock | 1 | 134 | 134 | 0 |
| Editor einschließlich bestehender MCP-Prüfungen | 1 | 54 | 54 | 0 |
| Integration | 1 | 28 | 28 | 0 |
| **Gesamt** | **27** | **1.051** | **1.051** | **0** |

Damit sind **54 finale native Prozesse mit 2.102 bestandenen Prüfungen**
dokumentiert, alle Exitcode 0, kein Prozesstimeout. Die Zwischenläufe bleiben
unverändert erhalten; sie werden weder als finale grüne Ergebnisse ausgewählt
noch gelöscht. Die beiden historischen Starts je Konfiguration und der frühere
erfolgreiche A03-Release-Gegenlauf liefern zusätzliche unabhängige Lifecycle-
Wiederholungen. Die finale Ergebnisauswahl ist ausschließlich `native-resumed`.

## Vollständige Regression

Alle acht vollständigen Suiten in `artifacts/g1-a03/regression-final/`
beenden sich mit Exitcode 0. Vor jedem Konfigurationswechsel erfolgt ein
konfigurationsspezifischer Locked-Restore; Build/Test laufen mit `-m:1`.
Der Testhost läuft außerhalb der Sandbox, da deren Hängen bereits in A02
nachgewiesen wurde. Es wurden keine Tests deaktiviert oder Assertions entfernt.

| Suite | A02 je Konfiguration | A03 Debug Passed | A03 Release Passed | Failed |
| --- | ---: | ---: | ---: | ---: |
| GUI | 1.043 | 1.087 | 1.087 | 0 |
| MCP | 176 | 176 | 176 | 0 |
| Core | 11.512 | 11.512 | 11.512 | 0 |
| G1-01 Headless | 12 | 12 | 12 | 0 |

Die 13 MCP-Explicit-Fälle, 26 Core-Skips und ein Core-Explicit-Fall sind
weiterhin separat ausgewiesen. Die TRX-Einzelergebnisse enthalten deshalb
13 beziehungsweise 27 `NotExecuted`; Adapter-Zähler und Konsolen-Summary
zählen Explicit-Fälle teilweise anders. Die Evidence bewahrt beide Werte.

Die unabhängige Wiederholung unter `artifacts/g1-a03/independent/` besteht
ebenfalls in **Debug 44/44 und Release 44/44**, jeweils in einem neuen
Testprozess nach eigenem Locked-Restore. Diese Läufe enthalten die verstärkten
Output-/Diagnostics-Inhaltsprüfungen und die produktive Startup-Präsentation.

## Eigenständiges Review

| Priorität | Befund / Bewertung | Konsequenz |
| --- | --- | --- |
| Hoch, behoben im A03-Umfang | Legacy-Container/Roots dauerhaft fixiert; echte G1-07-Datei nicht ladbar | Rollenprojektion, begrenzte Normalisierung und rote Sequenzmatrix |
| Hoch, behoben im A03-Umfang | Wiederhergestellte native Hosts nicht automatisch präsentiert | Owner-Opened-Lifecycle, Fixture-Hilfestellung entfernt, native Restart-Prüfung |
| Hoch, offen und abgegrenzt | B4 lässt unabhängige Callback-Mutationen innerhalb einer bestehenden Mutation zu | Expliziter A04-Operationsvertrag erforderlich; keine A03-Zulassungserweiterung |
| Mittel, offen und abgegrenzt | Allgemeine Dock-Callback-Ausnahmen können partielle Strukturänderungen hinterlassen; Composite-Abschluss hat keinen neuen Abort-Vertrag | Kein allgemeines Rollbackversprechen, keine verdeckte A04-Implementierung |
| Mittel, Nachweisgrenze | Historisch leere Container-ID enthält keine belastbare Ankerherkunft | Neue dynamische Identität; feste Rolle nur aus überprüfbarem Hauptbaum |
| Mittel, Nachweisgrenze | Native Maus-/Completion-/externe Eingabe- und andere Desktopplattformen unvollständig | Historische offene F03-Befunde bleiben offen |

Projektversionen bleiben .NET 10, Avalonia 12.1.4, AvaloniaEdit 12.0.0 und
Dock 12.1.0.13. Keine Änderungen an Dock-Upstream, Prism, Standard-GUI,
CryptoScript-Semantik, Coordinator, Bridge, AutomationService oder MCP-Leases.
Keine Commits, Pushes, Merges, Paketupgrades oder L1-Übernahme.

## Geänderte Dateien und Bestandsschutz

Änderungen gegenüber der gesicherten A02-Arbeitskopie, nicht gegenüber HEAD:

| Datei / Gruppe | Änderung |
| --- | --- |
| `CryptoWorkBenchAvalonia/Shell/DockContainerRoles.cs` | Neue gemeinsame Rollenbestimmung |
| `DockContainerIdentity.cs`, `DockLayoutStore.cs` im selben Verzeichnis | Begrenzte Legacy-Namensnormalisierung und rollenbasierte Projektion |
| `EditorDockFactory.cs` | Laufzeitrollen, tatsächliche Rückkehranker und regulärer Collapse ohne ID-Ausnahme |
| `DockShellLayout.cs` | Gemeinsame Ankersuche; globale Empty-Host-Suche entfernt |
| `DockShellWindow.axaml.cs` | Restore-Hosts beim Öffnen ihres Owners präsentieren |
| `CryptoWorkBenchGuiTest/DockPersistenceCleanupTests.cs` | 44 permanente A03-Regressionen |
| `CryptoWorkBenchGuiTest/Fixtures/dock-schema1-pre-a02.json`, Testprojektdatei | Bytegleiche historische Embedded-Fixture |
| `CryptoWorkBenchGuiTest/DockToolTests.cs` | Bisherige künstliche Startup-Präsentation entfernt; alle Assertions erhalten |
| `CryptoWorkBenchGuiTest/DockPersistenceNativeProbe.cs`, `DockShellNativeProbe.cs` | Save-/Restart-Erweiterung des vorhandenen Probes |
| `eng/gui/native/Program.cs`, `eng/gui/Validate-G1LayoutRoles.ps1` | Zwei neue Modi und isolierte Szenario-Matrix |
| Dieser Bericht und `docs/gui/evidence/G1-A03-results.json` | Bewertung, Ergebnisse und Manifestverweise |

Die echte G1-07-Fixture besitzt SHA-256
`7E54E250BA7F791968E4522C53BBB15771E1B9251A134CEA2504A086BE29F34F`.
Das finale A03-Diff wird getrennt als `artifacts/g1-a03/a03-only.patch`
gesichert; sämtliche bereits uncommitteten G1-Arbeiten bleiben Baseline.
Die Integritätsprüfung erfasst 26 frühere GUI-Berichte/Evidence-Dateien und
66 Locks, dazu 748 Dateien unter A01/A02 und 451 historische Raw-Evidence-
Hashverweise. Die Benutzerlayoutdatei bleibt außerhalb aller Test-Stores.
Source-/Binary-/Artifact-Manifeste, finaler Git-Status, Binary-Parität und
Prozessabschluss liegen unter `artifacts/g1-a03/`.

Der Abschlussbestand umfasst 1.131 Dateien gegenüber 1.125 Baseline-Dateien:
zehn vorhandene Dateien geändert, sechs neue Dateien, keine ursprüngliche
Datei entfernt. Frühere Berichte/Evidence/Locks und A01-/A02-Artefakte bleiben
bytegleich. `git diff --check`, Whitespace- und Konfliktmarkerprüfung sind
erfolgreich. Die finalen Produkt- und Probe-Assemblies stimmen je Konfiguration
zwischen Test-, Desktop- und Native-Ausgabe überein; keine Probe läuft weiter.

## Abnahmegrenzen

B4 bleibt A04. F03-Floating-Completion, externe Eingaben, vollständige
Mausabnahme und Linux-/macOS-Desktopabnahme bleiben offen, sofern sie nicht
ausdrücklich erneut belegt werden. Nach A03 wird gestoppt; A04 ist nicht freigegeben.

Die Empfehlung **CONDITIONAL GO** bedeutet: A03 kann in seinem beschriebenen
Umfang abgenommen werden; der anschließende A04-Auftrag muss ausdrücklich
freigegeben werden und B4 sowie den Complete-/Abort-/Fehlervertrag behandeln.
Sie erklärt weder die beiden offenen F03-Completion-Befunde noch externe
Clipboard-/Output-/Diagnostics-/Layout-Eingaben, native Mausgesten oder
Linux-/macOS-Desktops für abgenommen.
