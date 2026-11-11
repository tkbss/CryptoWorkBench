# G1-I02 – Final Windows Owner Acceptance and Integration Baseline

Stand: **10.11.2026, Europe/Zurich**. Die kombinierte G1/L1-Implementierung und G1-I01 sind vom Projekteigentümer abgenommen. **Windows-Eigentümerabnahme: PASS.** Der nachfolgende Versionierungsplan liegt zur ausdrücklichen Freigabe vor; diese Dokumentation erstellt keine Commits und erteilt keine endgültige Master- oder Nicht-Windows-Freigabe.

Arbeitskopie: `artifacts/worktrees/g1-i01-composition`, Branch `codex/g1-i01-composition`, HEAD/Master-Basis `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`. Maßgeblich ist der vollständige uncommittete Stand, einschließlich aller 152 G1-Übernahmen und der sieben neuen G1/L1-Integrationstests.

Maschinenlesbare Baseline und vollständige pfadgenaue Commit-Zuordnung: [G1-I02-integration-baseline.json](evidence/G1-I02-integration-baseline.json). Neue I02-Prüfprotokolle: [artifacts/g1-i02](../../artifacts/g1-i02).

## 1. Tatsächliche Eigentümerabnahme

Der Projekteigentümer hat in seinem G1-I02-Auftrag am **10.11.2026** ausdrücklich erklärt: **„G1-I01 und die manuelle Windows-Release-Matrix sind abgenommen.“** Er hat die folgenden sieben Release-Mausprüfungen für die kombinierte Implementierung als PASS bestätigt:

| Fall | Bestätigter Release-Mausablauf | Ergebnis | Nachweisart |
|---|---|---|---|
| M1 | Einzelnes Tool | PASS | Eigentümerbestätigung |
| M2 | Vollständige Tool-Gruppe | PASS | Eigentümerbestätigung |
| M3 | CryptoScript-Dokument | PASS | Eigentümerbestätigung |
| M4 | DocumentDock seitlich | PASS | Eigentümerbestätigung |
| M5 | Verschachtelte Gruppen | PASS | Eigentümerbestätigung |
| M6 | Zwei Floating Windows | PASS | Eigentümerbestätigung |
| M7 | Layoutpersistenz | PASS | Eigentümerbestätigung |

Dies ist eine **tatsächliche manuelle Eigentümerabnahme des kombinierten G1/L1-Release-Stands**, kein automatischer Prüflauf und keine bloße Übernahme der früheren G1-M1-Abnahme. Quelle ist die ausdrückliche Erklärung des Eigentümers; I02 behauptet keine eigene Beobachtung, neue Screenshots oder zusätzliche Einzelmessungen dieser Handbedienung. I02 führt keine erneuten Windows-Dockingtests aus und beauftragt keine weitere Windows-Dockingkorrektur.

Die offenen M1–M7-Zeilen im [I01-Bericht](G1-I01-master-L1-composition.md) bleiben als damaliger Stand bytegleich. Dieser Nachtrag schließt die dort ausstehende Eigentümerentscheidung. Er verändert weder I01-Resultate noch historische Fehlversuche. Die Bedienregel bleibt: **Zum Zurückdocken einer maximierten View zuerst `Restore Layout` ausführen.**

## 2. Geprüfte Integrationsbaseline

Vor dem Schreiben der beiden neuen Lieferdateien wurden Branch, HEAD, vollständiger Status und die bestehenden I01-Manifeste geprüft. Ergebnis: exakt der akzeptierte I01-Stand, keine neu hinzugekommenen Produkt-, Test-, Projekt- oder Lockänderungen.

| Schutzumfang | Ergebnis |
|---|---|
| Vollständiger akzeptierter Kompositionsstand | **1.173/1.173 Dateien bytegleich** zum I01-Abschlussmanifest, einschließlich aller unveränderten Masterdateien und vorhandenen Berichte. |
| Übernommene G1-Dateien | **152/152 bytegleich** zum gesicherten G1-Snapshot; 37 zuvor getrackte Änderungen und 115 damals neue Dateien. |
| Neue G1/L1-Tests | `DockAssignmentCompositionTests.cs` unverändert; sieben Fälle, in I01 jeweils Debug/Release bestanden. |
| L1-Pfade | **35/35 bytegleich**; keine L1-Änderung. |
| I01-Raw-Evidence und Lieferdateien | **3.082/3.082 Manifest-Einträge bytegleich**. Manifest-SHA-256: `D045599BB45E7BB368A995ACBC608011907F85D1856DAF685A79EC0715742DA4`. |
| Original-G1-Worktree | **1.151/1.151 Dateien und vollständiger Git-Status unverändert**, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. |
| Frühere G1-/A06-Artefakte | **15.831 historische Raw-Dateien und 2.004 A06-Artefakte unverändert** im Original-G1-Worktree. |
| Original-Master | Sauber, gleicher HEAD; **1.055/1.055 Dateien bytegleich** zu seinem eigenen I01-Rohbyte-Manifest. Die damals dokumentierten LF-/CRLF-Unterschiede zum frischen Checkout werden nicht als neue Änderungen interpretiert. |

Das neue JSON enthält für **jede der 1.173 akzeptierten Dateien Pfad, Byteanzahl und SHA-256**, zusätzlich die gesamte G1-Übernahmeliste, alle 35 L1-Pfade und die Versionierungszuordnung. Nicht ignorierte bislang ungetrackte Produktdateien sind damit ausdrücklich erfasst. Das neue I02-Dokument wird zusätzlich gehasht. Der JSON-Manifesttext kann nicht seinen eigenen vollständigen Hash enthalten; dessen SHA-256 und der Berichtshash stehen im separaten [delivery-seal.json](../../artifacts/g1-i02/delivery-seal.json). Dieser Selbstbezug ist ausdrücklich ausgenommen, keine ausgelassene Produktdatei.

Avalonia 12.1.4, AvaloniaEdit 12.0.0, Dock 12.1.0.13, SDK 10.0.401 und der gepinnte Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990` bleiben unverändert. Es wurde weder restored noch gebaut oder ein Paket aktualisiert. Die erneute Prüfung ist ein Datei-/Evidence-Audit.

## 3. Erneut gelesene I01-Ergebnisse

Die folgenden Zahlen wurden aus den erhaltenen I01-TRX und nativen Ergebnisdateien erneut geprüft. **Keine neuen Testausführungen in I02.**

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|
| GUI | 1.118 | 1.118 | 0 | 0 |
| MCP | 176 | 176 | 0 | 13 Explicit |
| Core | 11.723 | 11.723 | 0 | 26 Skip + 1 Explicit |
| G1-Headless | 12 | 12 | 0 | 0 |

Alle 214 bisherigen L1-Fälle und alle sieben neuen Kombinationstests sind je Debug/Release enthalten und bestanden. Der I01-Inventarvergleich weist keine entfernten bisherigen Fälle, Ergebnisänderungen oder neuen Skips aus. Zusätzlich: je 16 kritische Wiederholungsfälle, je 11 installierte-Codex-Explicit-Fälle und **78 native Prozesse mit 2.794 bestandenen Checks**, null fehlgeschlagene native Assertion und kein Timeout. Die vier sichtbaren Produktstarts (Legacy/Dock, Debug/Release) sind Startsmokes; kontrollierter Shutdown ist separat in der nativen Vertragsmatrix geprüft. Windows-Publish und Validator sind bestanden.

Die Entwicklungs- und Infrastrukturfehlversuche in I01 bleiben rote historische Evidenz. Die zwei nicht ausgeführten gepaarten Desktop-Explicit-Fälle werden durch die Mausabnahme nicht nachträglich zu PASS. [Erneute Ergebnisauswertung](../../artifacts/g1-i02/results-audit.json) enthält Herkunft und Hashes der gelesenen TRX.

## 4. Vollständiger Versionierungsumfang

Die Baseline vor I02 enthält 155 geänderte oder ungetrackte Pfade. I02 ergänzt genau zwei Dokumentations-/Manifestdateien. Der geplante Umfang beträgt damit **157 Pfade**. Jeder Pfad steht genau einmal in der maschinenlesbaren Commit-Liste.

| Kategorie | Pfade | Inhalt |
|---|---:|---|
| 1. Produktive G1-Implementierung | 43 | CryptoScript-Print-Quellbezug; Desktop-Opt-in/Trace; App-, Editor- und Präsentationsanpassungen; komplette Dock-Shell, Layout-/Fault-/Admission- und Persistenzklassen. |
| 2. Permanente G1-Tests und Prüfinfrastruktur | 46 | PrintSourceTests, vorhandene G1-GUI-/Native-Probes und Regressionen, Schema-1-Fixture, `eng/gui`-Treiber sowie versionierbare Feasibility-Quellen. |
| 3. Neue G1/L1-Integrationstests | 1 | `CryptoWorkBenchGuiTest/DockAssignmentCompositionTests.cs`, sieben Fälle. |
| 4. Benötigte Build-/Projekt-/Lock-Dateien | 22 | Zwei Produkt-/GUI-Projekte, Native-/Prototyp-Projekte und ihre Locks sowie die akzeptierten Debug-/Release-/RID-Prism-Locks. |
| 5. Architektur-/Abnahmeberichte und strukturierte Ergebnisdokumente | 45 | 43 bestehende G1/I01-Dokumente beziehungsweise Ergebnis-JSON/Prototyp-README plus die beiden I02-Lieferdateien. |
| 6. Nicht zu versionierende Ausgaben | ausgeschlossen | Historische Raw-Evidence, Binär-/Buildoutputs, Dependencycheckouts, temporäre Prüfscripts, Sicherungskopien und Laufzeittraces unter ignorierten Pfaden. |

Die Kategorie ist eine Inhaltszuordnung, keine neue Dateiverschiebung. Eine Schema-1-Fixture und permanente Native-Probequellen sind benötigte Tests; sie werden nicht mit ihren erzeugten Raw-Ergebnissen verwechselt. Bestehende, bereits in Master versionierte historische Dokumentationsdateien bleiben unangetastet.

Ausgeschlossen bleiben insbesondere `artifacts/**`, `.deps/**`, projektbezogene `bin/**` und `obj/**`, `TestResults/**` sowie die dortigen TRX, Logs, Screenshots, Publishes, temporären Launcher und Baselinekopien. Das betrifft auch sämtliche I01-/I02-Hilfsscripts unter `artifacts`. Die akzeptierten strukturierten Ergebnis-JSON unter `docs/gui/evidence` sind dagegen bewusst Teil der Reviewdokumentation. Ihre Referenzen auf lokale Raw-Belege rechtfertigen keine Aufnahme ganzer Artefaktbäume. Keine generische `git add .`- oder `git add -A`-Anweisung ist geplant; maßgeblich ist die explizite Pfadliste.

## 5. Konkrete geplante reguläre Commit-Gruppen

| Reihenfolge | Vorgeschlagener Titel | Exakter Umfang |
|---|---|---|
| C1 | `Add opt-in G1 dock shell and permanent regression coverage` | **111 Pfade**: Kategorien 1, 2 und 4 vollständig. Produkt, bestehende permanente Tests, eingebettete Fixture und zugehörige Projekt-/Lockdateien bilden eine gemeinsame konsistente Übernahmeeinheit. |
| C2 | `Verify G1 and L1 composition in the dock editor` | **1 Pfad**: ausschließlich die neue Datei mit sieben Kombinationstests. Baut auf C1 und der vorhandenen L1-Masterbasis auf. |
| C3 | `Document G1 architecture and final Windows owner acceptance` | **45 Pfade**: Kategorie 5 vollständig, einschließlich I01, I02 und strukturierter Evidence. Historische Aussagen bleiben unverändert; I02 ist der neue Abnahmenachtrag. |

C1 führt die Kategorien bewusst zusammen: Beispielsweise müssen Dock-Paketreferenzen und Locks, die eingebettete Schema-1-Fixture und ihre Tests sowie die Native-Probes zu ihren APIs passen. Getrennte Inhaltskategorien bedeuten hier keine künstlich unvollständigen Zwischencommits. Die pfadgenauen Listen und Hashes in `commitPlan.groups` des JSON sind verbindlicher als eine Auswahl nach Verzeichnispräfix.

**Ausführung erst nach ausdrücklicher Eigentümerfreigabe:** vor dem ersten Staging Branch/HEAD, Masterbasis und alle Manifest-Hashes erneut prüfen; Abweichungen einzeln bewerten. Je Gruppe nur die aufgeführten Pfade stagen, den staged Diff und `--check` prüfen und einen normalen neuen Commit erstellen. Die finale versionierte Gesamtstruktur muss dem akzeptierten Kompositionsmanifest plus den zwei I02-Dokumenten entsprechen. Vor einem späteren Push/PR/Merge benötigt der tatsächliche Commitstand die passende Verifikation und eine gesonderte Freigabe. Es sind weder Amend, Force-Push, Rebase noch automatische Übernahme eines zwischenzeitlich geänderten Masterstands geplant.

**I02 führt keinen dieser Schritte aus.** Auch der Index bleibt unverändert. Die geplanten Commit-Grenzen selbst wurden nicht als Zwischenstände gebaut; I01 zertifiziert die vollständige Komposition. Das ist bei einer späteren Prüfung jedes tatsächlichen Commitstands zu berücksichtigen.

## 6. Weiterhin offene Freigabegrenzen

- Linux x64: NOT VERIFIED.
- macOS x64: NOT VERIFIED.
- macOS ARM64: NOT VERIFIED.
- Produktive MCP-Lease-Uhr auf Nicht-Windows: bekannte Einschränkung bleibt offen; keine Änderung von WorkbenchLeaseV2.
- Zwei gepaarte installierte-Codex-Desktop-Tests: weiterhin NOT VERIFIED.

Die Windows-Eigentümerabnahme hebt diese Grenzen nicht auf. Legacy bleibt Standard; G1 wird über `--dock-shell` aktiviert. **NO REACHABLE B4 FOUND** bleibt bestehen. Keine Dock.Avalonia-Modifikation, keine neue Dock-Architektur, keine Produkt-/Testkorrektur, kein Paketupdate und keine L1-Änderung.

## 7. Abschlussprüfung und Status

`git diff --check` ist erfolgreich. Die beiden neuen ungetrackten I02-Dateien werden zusätzlich mit `git diff --no-index --check` geprüft; Exit 1 ohne Diagnose bezeichnet hinzugefügten Inhalt und keinen Whitespace-Fehler. Der Hashvergleich schützt sämtliche akzeptierten Quellen und A06-/I01-Evidence. Master und Original-G1 bleiben unverändert.

Finaler Git-Status: **157 Zeilen, 37 modifizierte und 120 ungetrackte Dateien**, gegenüber I01 ausschließlich die beiden I02-Lieferdateien ergänzt. Vollständig im JSON unter `git.finalStatus` und im [Statusprotokoll](../../artifacts/g1-i02/final-git-status.txt). Branch/HEAD bleiben wie oben; keine gestagten Änderungen, Commits, Pushes, Merges oder Rebases.

**Ergebnis: Windows-Eigentümerabnahme vollständig dokumentiert; Integrationsbaseline geprüft; C1–C3 zur ausdrücklichen Freigabe vorbereitet.** Nach I02 wird gestoppt. Die geplante Git-Versionierung wurde nicht begonnen.
