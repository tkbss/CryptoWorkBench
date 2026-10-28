# A12-06-D1 – MCP/AP2.1 Baseline Reconciliation

Stand: 28. Oktober 2026. **Herkunft geklärt: Der aktuelle MCP-Worktree ist für alle 34 übertragenen Dateien bytegleich zur A12-01-Ausgangsbasis.** Es gibt in dieser Dateimenge keine nachträgliche MCP-Weiterentwicklung, die durch die historische Kopie verloren ginge. Die MCP-/AP2.1-Basis soll entsprechend der vorgegebenen Architekturentscheidung zuerst auf dem MCP-Branch autoritativ festgehalten werden; die Migration baut auf genau diesem späteren Commit auf.

Die fünf A12-06-Zwischenstände wurden als aufeinanderfolgende Quellbäume aufgebaut. **Alle fünf bauen beide Lösungen in Debug und Release.** Ein falscher historischer Dateipfad in A12-06 wurde identifiziert und für die Simulation korrigiert; der alte Bericht wurde nicht verändert. Keine Git-Integration wurde ausgeführt.

## 1. Exakte Arbeitsstände

| Merkmal | MCP | Avalonia 12 |
|---|---|---|
| Worktree | `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Mcp` | `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12` |
| Branch | `feature/mcp-server` | `feature/avalonia12-migration` |
| HEAD | `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0` | identisch |
| Status vor D1-Bericht | 6 modifizierte, 28 unversionierte Dateien | 44 modifizierte, 84 unversionierte Dateien |
| Erfasste Quelldateien | 767 | 823 |
| Staging | leer | leer |

Die 34 MCP-Statusdateien entsprechen exakt der historischen Transferliste; keine zusätzliche nicht ignorierte MCP-Quelldatei ist seit dieser Übertragung hinzugekommen. Der Avalonia-Status enthält zusätzlich die Migration, Prism, F1/C1 und Dokumentation. Wie bereits in A12-06 getrennt ausgewiesen, sind dort 23 der 44 Modifiziert-Meldungen ohne Git-Inhaltsdiff; der Status ist nicht mit einem fachlichen Delta gleichzusetzen.

Beide Branches enthalten bereits die MCP-Historie `5de3320` (Planung), `55297e2` (MCP-01.1), `0a9eb35` (MCP-01.2) und `bd48c57` (MCP-01.3). Diese Commits müssen nicht nochmals nachgebaut werden. D1 betrifft die noch uncommittete AP2.1-/Editorbasis darauf.

Evidenz: [MCP-Status](../../artifacts/a12-06-d1/mcp-status.txt), [Avalonia-Status](../../artifacts/a12-06-d1/avalonia-status.txt), [MCP-Log](../../artifacts/a12-06-d1/mcp-log.txt), [Avalonia-Log](../../artifacts/a12-06-d1/avalonia-log.txt), [MCP-Quellmanifest](../../artifacts/a12-06-d1/mcp-manifest.json), [Avalonia-Quellmanifest](../../artifacts/a12-06-d1/avalonia-manifest.json). Die jeweiligen Branch-/HEAD-/Stagingdateien und Arbeitsdiffs liegen im selben Evidenzordner. Ignorierte Caches und `.codex/config.toml` sind keine gelesene Quellbasis.

## 2. Einzelvergleich der 34 Dateien

[comparison.json](../../artifacts/a12-06-d1/comparison.json) enthält für jede Datei die drei vollständigen SHA-256-Werte, den Vergleich zum historischen Transfermanifest und alle Gleichheitsflags. Sämtliche historischen Sicherungsdateien stimmen mit den damals gespeicherten Hashes überein.

**Ergebnis:** MCP/Baseline 34 von 34 identisch; Avalonia/Baseline 26 identisch, acht erklärbare Anpassungen. Keine unerwartete oder widersprüchliche Änderung festgestellt. Die tatsächlichen Diffs der acht Dateien wurden gelesen und unter [migration-overlays.patch](../../artifacts/a12-06-d1/migration-overlays.patch) gesichert.

| Datei | Aktuelles MCP zur Basis | Aktuelles Avalonia zur Basis / Einordnung |
|---|---|---|| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextArea.cs` | SHA-256-identisch | Avalonia 12: Clipboard.TryGetTextAsync und Namespace; Ticket-/Identitätsprüfungen unverändert. |
| `CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorCoordinator.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/Services/Automation/EditorReplaceConfirmation.cs` | SHA-256-identisch | Avalonia 12: UI-Handoffs über UiDispatch statt direktem Dispatcher. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationService.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs` | SHA-256-identisch | Avalonia 12: OnEditorUiAsync verwendet UiDispatch; Editor-/Leaseoperationen unverändert. |
| `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs` | SHA-256-identisch | Avalonia 12: drei Dispatcher-Handoffs über UiDispatch; keine neue MCP-Funktion. |
| `CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` | SHA-256-identisch | Avalonia-Port: avares-Icon und ownergebundene Dialoge; zusätzlich dokumentierte F1-Cancel-Korrektur. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs` | SHA-256-identisch | Identisch. |
| `CryptoWorkBenchGuiTest/EditorAutomationTests.cs` | SHA-256-identisch | Avalonia 12: gemeinsame GuiTestSession. |
| `CryptoWorkBenchGuiTest/EditorCoordinatorTests.cs` | SHA-256-identisch | Avalonia 12: GuiTestSession, PhysicalKey/Symbol und Clipboard-DataTransfer; fachliche Assertions erhalten. |
| `CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs` | SHA-256-identisch | Avalonia 12: gemeinsame GuiTestSession. |
| `docs/mcp/MCP-00-AP0-contract-finalization.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-AP2.1-F1-correction-review.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-AP2.1-F1-independent-rereview.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-AP2.1-implementation-review.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-AP2.1-independent-review.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-D02-S1-epoch-safety.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-editor-coordination-design.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-00-R2-consolidated-architecture-decision.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-avalonia12-feasibility.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-B1-feasibility.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-B2-design.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-B2-independent-review.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-B2-R1-independent-review.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-editor-architecture-decision.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-post-U0-architecture-options.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-review.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-U0-property-feasibility.md` | SHA-256-identisch | Identisch. |
| `docs/mcp/MCP-01.4-validation.md` | SHA-256-identisch | Identisch. |

EditorCoordinator, EditorBridge und WorkbenchTextEditor sind in allen drei Quellen unverändert. Ebenso bleiben WorkbenchAutomationRegistry, WorkbenchAutomationService, WorkbenchSaveCommit und die beiden Editor-Viewdateien bytegleich. Im WorkbenchTextArea-Diff ändern sich ausschließlich Clipboard-API und Import; die nach await ausgeführten Ticket-/Identitätskontrollen bleiben erhalten. Die WorkbenchEditorOperations-Änderung betrifft den Dispatcher-Handoff, nicht Admission oder Lease-Regeln. Die Coordinator-Tests behalten ihre fachlichen Assertions; angepasst werden die Avalonia-12-Test-/Clipboard-/Tastaturschnittstellen. Es gibt damit keinen Anhaltspunkt für eine versteckte zweite Editorarchitektur oder eine nachträgliche MCP-Vertragsänderung in diesen Deltas.

SidebarViewModel ist ausdrücklich **nicht nur** eine API-Portierung: Es enthält zusätzlich den bereits dokumentierten F1-Funktionsfix, der Close/Escape/Cancel vor Import als Abbruch behandelt. Dieser Deltaanteil gehört weiterhin zum dritten Commit der A12-06-Folge, nicht in den MCP-Basiscommit.

## 3. Autoritative Basis und sichere historische Abhängigkeit

Empfehlung: Für den ersten Commit die **aktuellen 34 MCP-Dateien** als fachlich autoritative Quelle auswählen. Ihre nachgewiesene Gleichheit zur historischen Sicherung erlaubt die Verwendung dieser Sicherung als Kontrollreferenz, aber nicht als pauschale Rückschreibquelle. Für die MCP-Seite ist keine Datenangleichung erforderlich.

Die spätere Historie soll konzeptionell so aussehen:

```text
bd48c57 (bereits gemeinsame MCP-01.3-Basis)
  └─ M: MCP/AP2.1-Basis, einmalig auf feature/mcp-server erzeugt
       └─ A: Avalonia 12 + korrigierte temporäre Prism-Integration
            └─ F: F1 Import-Cancellation
                 └─ T: C1 Diagnostic-Testkorrektur
                      └─ D: Migrationsdokumentation
```

M muss derselbe Commit/Ancestor für beide Entwicklungslinien sein, nicht zwei separat erzeugte, nur inhaltlich ähnliche MCP-Basiscommits. Da beide heutigen Branchköpfe noch identisch sind, ist die inhaltliche Ausgangslage hierfür einfach. Der schmutzige Avalonia-Worktree verhindert aber eine gefahrlose pauschale Branchumstellung: Dort liegen bereits überlagerte Portierungsstände derselben Dateien.

Für einen **später gesondert autorisierten** Integrationsauftrag empfiehlt sich daher folgende Sicherungs- und Entscheidungsfolge, ohne hier Git-Eingriffe vorzunehmen:

1. Beide tatsächlichen Quellstände und die unten genannten Hashes vor Git-Arbeit erneut prüfen. Bei neuen MCP-Deltas stoppt die heutige Gleichheitsannahme; neuere MCP-Arbeit muss zuerst fachlich geprüft werden.
2. M ausschließlich aus dem expliziten 34-Datei-Umfang im MCP-Branch festhalten. Die aktuellen Portierungsinhalte aus dem Avalonia-Worktree gehören nicht hinein.
3. Den Avalonia-Stand samt seinen uncommitteten Änderungen unverändert sichern und die Migrationsfolge in einem später autorisierten isolierten Integrationsbereich auf **M als Basis** rekonstruieren. Die Deltaquellen für A/F/T/D sind in A12-06 und dieser Simulation exakt bezeichnet.
4. Den rekonstruierten Endbaum gegen den heute akzeptierten Avalonia-Stand vergleichen. Erst nach Identität beziehungsweise ausdrücklich bewerteten Abweichungen darf über die Zuordnung der bestehenden Branch-/Worktree-Namen entschieden werden. Kein stilles Überschreiben oder Verwerfen des jetzigen schmutzigen Avalonia-Worktrees.

Dies legt die autoritative Abhängigkeit fest, ohne Cherry-Pick-, Merge-, Rebase-, Reset- oder Stash-Aktionen auszuführen oder einen solchen Aktionsauftrag zu erteilen. Die konkrete spätere Git-Mechanik und die Erhaltung des bestehenden Worktrees benötigen einen neuen Integrationsauftrag. Eine direkte Integration nach master ist nicht Gegenstand von D1.

## 4. Simulation der fünf Commitstände

Wegen des ausdrücklichen Verbots von Staging und Commits wurde **keine temporäre Git-Historie erzeugt**. Stattdessen wurde HEAD per lesendem `git archive` exportiert und die fünf Dateiumfänge aus A12-06 nacheinander in eine neue, nichtproduktive Quellkopie unter `artifacts/a12-06-d1/simulation` eingespielt. Das ist eine Inhalts-/Buildsimulation, kein Nachweis ausgeführter Branchoperationen.

[Simulate.ps1](../../artifacts/a12-06-d1/Simulate.ps1), [Buildsummary](../../artifacts/a12-06-d1/results/summary.json). Für jeden Zustand gibt es ein eigenes `stage-N-manifest.json`; alle Logs liegen im [results-Verzeichnis](../../artifacts/a12-06-d1/results/).

| Zustand | Tatsächlich aufgelegter Inhalt | Debug / Release, jeweils beide Lösungen |
|---|---|---|
| C1 / M | HEAD + 34 historische MCP-Dateien; identisch zu aktuellem MCP | PASS / PASS |
| C2 / A | C1 + finaler Avalonia-/Prism-Umfang; Sidebar auf verifiziertem Vor-F1-Stand | PASS / PASS |
| C3 / F | C2 + aktuelles SidebarViewModel und ScriptBookImportCancellationTests | PASS / PASS |
| C4 / T | C3 + aktuelle EmvMasterKeyDerivationOptionCTests | PASS / PASS |
| C5 / D | C4 + 14 in A12-06 vorgesehene Dokumente einschließlich A12-06 | PASS / PASS |

Das sind zehn Konfigurationsläufe mit insgesamt zwanzig Solution-Builds. C1 verwendet normale dotnet-build-Aufrufe einschließlich Restore; C2 bis C5 verwenden die unveränderten dokumentierten Prism-Buildskripte. Der offizielle Prism-Checkout wird ausschließlich innerhalb der Simulation neu beschafft. Der vorhandene isolierte NuGet-Cache wurde wiederverwendet, ein eigener CLI-Home verwendet. Es wurde kein erneuter vollständiger Funktions-/Regressionstest aller Zwischenstände beauftragt oder behauptet. Buildwarnungen bleiben in den vollständigen Logs; Buildfähigkeit ist keine eigenständige Produktfreigabe.

Insbesondere C2 ist zwar buildfähig, enthält aber absichtlich noch nicht die nachfolgende F1-Korrektur. C1 enthält die bisherige Avalonia-11-Basis. Diese historischen Zwischenstände dürfen nicht allein wegen eines erfolgreichen Compilers als heutiger Releasekandidat gelten. C5 ist dokumentationsbezogen; seine Builds wurden trotzdem tatsächlich aufgerufen.

Der rekonstruierte Endbaum enthält den heutigen Quellbestand vollständig. Der [Endbaumvergleich](../../artifacts/a12-06-d1/final-tree-comparison.json) findet **keine unerwarteten Inhaltsunterschiede**. Er weist reine CRLF/LF-Unterschiede des HEAD-Exports gesondert aus; die 34-Datei-Herkunftsprüfung bleibt dagegen ein strenger Byte-/SHA-256-Vergleich ohne Normalisierung. Der neue D1-Bericht entstand erst nach dieser Simulation und kann später dem Dokumentationscommit zusätzlich zugeordnet werden.

### Nachgewiesene Korrektur an der A12-06-Anleitung

Der in A12-06 für C2 genannte Pfad `artifacts/a12-01-f1/baseline/source/CryptoWorkBenchAvalonia/ViewModels/SidebarViewModel.cs` existiert nicht. Tatsächlich liegt die vollständige Vor-F1-Datei unter **`artifacts/a12-01-f1/baseline/SidebarViewModel.cs`**. Ihr SHA-256 stimmt mit dem zugehörigen Eintrag des historischen `source-manifest.json` überein: [Pfad-/Hashnachweis](../../artifacts/a12-06-d1/historical-path-correction.json).

Der erste Simulationsversuch stoppte deshalb nach den beiden erfolgreichen C1-Konfigurationen. [Ursprünglicher Fehler](../../artifacts/a12-06-d1/initial-path-error.log). Ausschließlich das neue Simulationsskript wurde auf den verifizierten tatsächlichen Pfad eingestellt und bei C2 fortgesetzt. Der historische Bericht und die Sicherung wurden nicht geändert. Die Commitfolge ist **mit dieser expliziten Pfadkorrektur** aufbaubar; die alte Anleitung darf nicht unverändert als ausführbarer Dateiplan verwendet werden.

## 5. Konflikte und noch erforderliche Entscheidungen

- Keine Auswahl zwischen divergierenden MCP-Varianten nötig: Der heutige MCP-Stand und die Transferbasis stimmen vollständig überein. Die fachliche Autorität liegt gemäß Vorgabe beim MCP-Branch.
- Die acht überlagerten Dateien dürfen nicht als heutiger Gesamtinhalt in M gelangen. Sonst würden Avalonia-12-APIs, UiDispatch/GuiTestSession-Abhängigkeiten und F1 unbemerkt in die Avalonia-11-MCP-Basis geraten.
- Der falsche Vor-F1-Pfad aus A12-06 muss bei der späteren Ausführung durch den hier verifizierten Pfad ersetzt werden. Es handelt sich um einen Plan-/Dokumentationsfehler, keinen neuen Produktfehler.
- Eigentümerfreigabe bleibt erforderlich für die tatsächlichen Commits und die spätere Anordnung der bestehenden Branches/Worktrees. Auch die Entscheidung, C2 und C3 getrennt zu veröffentlichen oder F1 bewusst mit dem Migrationscommit zusammenzufassen, bleibt eine Release-/Historienentscheidung; beide Inhaltsgrenzen sind jetzt überprüfbar.
- Neue MCP-Arbeit nach dem heutigen Vergleich würde eine erneute Deltaabstimmung auslösen. Ein späterer Auftrag darf die historische Sicherung nicht als unveränderlich neueste Quelle behandeln.
- Die bereits in A12-06 dokumentierte CI-Anpassung und die master-/Headless-Backport-Integrationsgrenze bleiben separat. Linux und macOS bleiben OPEN; dieser Windows-Buildnachweis schließt diese Plattformen nicht.

## 6. Integrität und Abschluss

Die Abschlussprüfung bestätigt unveränderte Hashes aller 767 erfassten MCP-Dateien und aller 823 vor dem Bericht erfassten Avalonia-Dateien. Beide HEADs bleiben bd48c57; beide Staging Areas bleiben leer. [Integrität](../../artifacts/a12-06-d1/integrity.json). Die 34 historischen Dateien wurden nur gelesen, ihre Manifesthashes bestätigt. Historische Evidenzen wurden nicht überschrieben.

Keine Produkt-/Test-/Prism-Quelländerung, keine produktive CI-Änderung, kein Zugriff auf `.codex/config.toml`, kein Staging, Commit, Push, Merge, Reset, Rebase oder Stash. Neue Dateien sind ausschließlich der D1-Bericht und dessen Evidenzen im Avalonia-Worktree. Die Eigentümerentscheidung zu A12-04-F01 bleibt unverändert; keine Retention-Untersuchung.

**Empfehlung:** Die aktuelle MCP-/AP2.1-Basis kann als genau abgegrenzter erster Commit im MCP-Branch vorbereitet werden, sobald dies ausdrücklich beauftragt wird. Die übrige A12-06-Folge baut inhaltlich darauf auf und ist einschließlich aller Zwischenstände unter Windows buildfähig; die oben genannte historische Pfadkorrektur ist verbindlich zu berücksichtigen. D1 endet hier ohne Git-Integration.
