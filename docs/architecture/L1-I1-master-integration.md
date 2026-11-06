# L1-I1 – Master Integration

Datum: 2026-11-06.

**L1 INTEGRATED INTO MASTER**

## Ausgangs- und Zielstände

| Stand | Commit |
|---|---|
| Ausgangsbasis, lokaler master und origin/master nach Fetch | `7ecac04483c10722c4cb4b459e3f6cb5157c6af1` |
| L1-A Implementierung | `9a710654a32456ddd93b10027bd34dbbdbffbe81` |
| Abgenommener L1-Abschluss, feature/script-assignment | `a34ffbdc580d00b4d8eb7fabef5b8569099ea512` |
| Regulärer GitHub-Merge, anschließend lokaler master und origin/master | `50e861226c315749004cb21f5277fda36602d7fb` |

Die vollständige L1-Kette besteht aus den beiden genannten Feature-Commits unmittelbar auf der Ausgangsbasis. L1-Worktree und dessen Index waren sauber. Die zwölf Abschlusscommit-Blobs stimmen mit dem C1-Freigabemanifest überein. Alle permanenten Tests sowie L1-A- und B1–B7-Berichte sind enthalten.

Dieser Bericht wird anschließend in einem separaten Dokumentationscommit auf dem integrierten master versioniert. Dieser zusätzliche Commit verändert ausschließlich diese Berichtsdatei, nicht die abgenommene Implementierung. Sein endgültiger SHA und der erneute Vergleich mit origin/master werden im lokalen Abschlussnachweis erfasst.

## Integrationsverfahren und Tree-Verifikation

1. Origin-Refs regulär abgerufen; master war sauber und Vorfahre von feature/script-assignment.
2. Im bestehenden Master-Worktree ausschließlich `git switch master` und `git merge --ff-only feature/script-assignment` ausgeführt.
3. Lokaler master entsprach danach exakt dem Abschlusscommit `a34ffbd`.
4. Feature-Branch regulär gepusht und [Pull Request #5](https://github.com/tkbss/CryptoWorkBench/pull/5) nach master erstellt.
5. Sämtliche vier Plattformjobs vollständig abgewartet und ihre TRX-Artefakte ausgewertet.
6. PR regulär mit Merge-Commit und Prüfung des erwarteten Feature-HEAD gemergt.
7. Origin erneut abgerufen; lokalen master ausschließlich per Fast-Forward auf `50e8612` aktualisiert.

Kein Force-Push, Rebase oder automatische Konfliktauflösung. Die öffentliche GitHub-Abfrage meldete master als nicht geschützt; es wurden keine Schutzregeln verändert oder umgangen.

Der Git-Tree von `a34ffbd`, lokalem master nach dem ersten Fast-Forward und dem späteren Merge-Commit `50e8612` ist identisch:

```text
f112d9df3e32bfac652e084a6e6015c080d93860
```

Somit gingen bei der Implementierungsintegration weder Dateien verloren noch kamen zusätzliche Dateien hinzu. Master-Worktree und Index waren nach beiden Fast-Forwards sauber. Der abschließende Dokumentationscommit ergänzt bewusst nur diesen Bericht; der restliche Tree bleibt identisch.

## Tatsächlich ausgeführte lokale Validierung

Alle folgenden Läufe wurden auf dem aktualisierten Master-Worktree ausgeführt; die früheren B7-Resultate wurden nur als Vergleichsbasis verwendet.

Builds über den vorhandenen Prism-Wrapper:

```powershell
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug
pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Release
```

Beide Konfigurationen bauten GUI- und Core-/MCP-Lösung erfolgreich. Anschließend alle drei Testprojekte vollständig mit `dotnet test -c <Debug|Release> --no-build --no-restore`.

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | NotExecuted je Konfiguration |
|---|---:|---:|---:|---:|
| Core | 11.719 | 11.719 | 0 | 27 |
| GUI | 800 | 800 | 0 | 0 |
| MCP | 176 | 176 | 0 | 13 |
| Gesamt | 12.695 | 12.695 | 0 | 40 |

Die NotExecuted-Namen wurden je Suite und Konfiguration gegen B7 verglichen: unverändert. Alle **214 L1-Fälle** sind in beiden Konfigurationen anhand der TRX-Einzelresultate Passed: 66 L1-A, 32 B2, 48 B4 und 68 B6, einschließlich nativer Enter- und authentifizierter MCP-Lease-Integration.

Buildwarnungen: Debug 84, Release 90 in der ersten Lösung; zweite Lösung jeweils null, überall null Buildfehler. Davon stammen jeweils 78 Warnungen aus dem in diesem Worktree neu aufgebauten unveränderten Prism-Checkout. Die sechs eigenen Debug- beziehungsweise zwölf eigenen Release-Warnungszeilen entsprechen nach Pfadnormalisierung exakt B6. Keine neue projektspezifische Warnung.

## GitHub Actions

[PR-CI-Lauf 42213297055](https://github.com/tkbss/CryptoWorkBench/actions/runs/42213297055), Workflow `.NET`, Run 137, Ergebnis **success**.

Alle Jobs erfolgreich: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`. Jeder Job umfasst Debug-/Release-Builds mit Locked Restore, den nativen Datei-Publikationsgate, vollständige Release-Suiten, zusätzliche Coordinator-/F1-/C1-Prüfungen, Desktop-Publish und Layout-/Native-Asset-/InfoDocs-Prüfung.

Aus den heruntergeladenen TRX-Artefakten:

| Plattform | Core Passed / NotExecuted | GUI Passed / NotExecuted | MCP Passed / NotExecuted | Failed | L1 Passed |
|---|---:|---:|---:|---:|---:|
| Windows x64 | 11.737 / 9 | 800 / 0 | 176 / 13 | 0 | 214 |
| Linux x64 | 11.731 / 15 | 799 / 1 | 171 / 18 | 0 | 214 |
| macOS ARM64 | 11.730 / 16 | 799 / 1 | 171 / 18 | 0 | 214 |
| macOS Intel | 11.730 / 16 | 799 / 1 | 171 / 18 | 0 | 214 |

Die zusätzlichen gezielten CI-Gates sind Teil der erfolgreichen Jobs, werden in dieser Tabelle aber nicht nochmals zu den vollständigen Suiten addiert.

Die Abweichungen zur lokalen Windows-Baseline sind geprüft:

- Der Windows-CI-Runner führt 18 Symlink-/Link-Prüfungen aus, die auf dem lokalen Windows-System mangels entsprechender Unterstützung nicht ausgeführt wurden.
- Linux/macOS überspringen bestehende Windows-Sharing-, Windows-Desktop- und CNG-spezifische Prüfungen.
- Darwin-native Prüfungen sind auf anderen Betriebssystemen ausgenommen; auf macOS wurden sie ausgeführt.
- macOS besitzt keinen systemeigenen CCM-Vergleichsprovider für die betreffenden unabhängigen Provider-Gegenvergleiche. Die TRX begründen diese Ausnahmen ausdrücklich; vorhandene andere CCM-Vektortests bleiben Bestandteil der Suite.
- Die explizite lokale Hardwaremessung und 13 explizite Installed-Codex-/Desktop-Prüfungen bleiben ohne passende externe Testumgebung nicht ausgeführt.

Kein L1-Test ist auf einer Plattform ausgefallen oder übersprungen. Die Tabelle zählt TRX-Einzelresultate, nicht die teilweise abweichende CLI-Skipanzeige.

## Bekannter Low-Befund: EOF-Leerzeilen

Die acht C1-Beanstandungen wurden geprüft: eine zusätzliche Leerzeile am Ende von ParameterAssignmentSerializationTests.cs und jeweils der sieben Berichte B1–B7. Sie stehen nach der abschließenden C#-Klammer beziehungsweise dem letzten Markdown-Absatz und verändern keine fachliche Semantik.

Der abgenommene Commit wurde nicht geändert oder amendiert. `git show --check a34ffbd` meldet diese Stellen weiterhin mit Exitcode 2; das wird nicht als erfolgreicher Commit-Whitespace-Check ausgegeben. Der leere Working-Tree-Diff nach Integration ist sauber. Der neue Bericht erhält keine zusätzliche EOF-Leerzeile.

## G1, lokale Nachweise und verbleibende Grenzen

G1 wurde ausschließlich lesend geprüft. Vor der Integration wurde ein Snapshot von HEAD, Git-Status und SHA-256 der bereits geänderten/untracked Dateien erstellt. Nach der Implementierungsintegration sind HEAD und sämtliche erfassten Inhalte unverändert. Im Git-Status ist parallel die neue untracked Datei `docs/gui/G1-04-variables-info-integration.md` hinzugekommen; sie wurde nicht durch diese Integration erstellt oder bearbeitet. Der Status wird daher nicht pauschal als unverändert ausgegeben. Insbesondere wurden die vier in C1 dokumentierten parallelen Änderungen nicht überschrieben oder zurückgesetzt:

- CryptoWorkBenchGuiTest/DockShellNativeProbe.cs
- CryptoWorkBenchGuiTest/DockToolTests.cs
- eng/gui/native/Program.cs
- eng/gui/Validate-G1Shell.ps1

Der G1-HEAD bleibt `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`. Seine bestehenden Arbeitsänderungen bleiben unangetastet. Auch der separate L1-Worktree bleibt sauber auf dem abgenommenen Abschlusscommit. Temporäre Probeprojekte, Logs, TRX und Downloads sind ignoriert und nicht Teil der Integration.

Lokale Nachweise liegen unter `artifacts/l1-i1/`: Build-/Testlogs, `local-test-results.json`, `l1-test-results.json`, `pr5-ci-jobs.json`, `ci-test-results.json`, `ci-l1-results.json`, `ci-skips.json`, die heruntergeladenen CI-Artefakte und `g1-before.json`. Die früheren L1-Nachweise im Feature-Worktree bleiben erhalten.

Die fachlichen Produktgrenzen aus B7 bleiben unverändert: konservative Ablehnung mehrdeutiger roher PARAM-Trenner bei Assignments, unveränderte Legacy-Deklarationen und keine neue globale Transaktion oder Mechanismus-Whitelist. Nicht ausgeführte externe/plattformabhängige Prüfungen werden nicht als bestanden behauptet.

L1 ist damit auf lokalem und entferntem master integriert und bildet den geprüften Ausgangspunkt für die weitere Architekturdokumentation.
