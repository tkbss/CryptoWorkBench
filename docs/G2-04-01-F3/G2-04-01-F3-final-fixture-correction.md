# G2-04-01-F3 — Final Fixture Correction

**Ergebnis: R4-L01 im beauftragten Korrekturumfang geschlossen.**  
**Status: READY FOR OWNER REVIEW — nicht ACCEPTED**  
**Datum:** 17.11.2026  
**Repository:** tkbss/CryptoWorkBench  
**Verbindliche Baseline:** `32017a08c4771e73c12a50f6e1548476b9bb0a54`

## 1. Ausschließlich bearbeiteter Befund

R4-L01 betraf die Herkunft des KEY-Fixture-Members `Statement.Text`. Die Fixture verwendete das Leerzeichen des menschenlesbaren Scripts. Der Lexer verwirft dagegen Whitespace zwischen Tokens; `AntlrToStatement` übernimmt `context.GetText()`, und `StatementEvaluator` weist diesen Text der Property zu.

Das dokumentierte Script bleibt unverändert:

```text
KEY demoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)
```

Der korrigierte Property-Wert lautet:

```text
KEYdemoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)
```

Quellbelege auf der verifizierten Baseline: [Lexer-Whitespace-Regel](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Antlr/CryptoLexer.g4:177), [AST-Übernahme](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/AntlrToStatement.cs:14), [Zuweisung an Statement.Text](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/StatementEvaluator.cs:45) und [vorhandene Testquelle](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScriptUnitTest/StatementExecutionTests.cs:43). Letztere bestätigt außerdem, dass Leerzeichen innerhalb von Stringliteralen erhalten bleiben. Die Testquelle wurde gelesen, nicht ausgeführt oder verändert.

Die Korrektur verändert weder Parsersemantik noch Copy-Formatierung. Der Inspector-Prototyp kopiert weiterhin unverändert den bereits vorhandenen Property-Wert. Kein Trim oder anderer Normalisierungsschritt wurde eingeführt.

## 2. Exakt geänderte Dateien

Alle Änderungen liegen außerhalb des Repositorys im bestehenden F2-Dokumentationsverzeichnis:

`C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4/G2-04-01-F2/`

| Datei | Änderung |
|---|---|
| `prototype-model.js` | Zeile 8: ausschließlich KEY.Text ohne Trennleerzeichen. Zeile 18: Herkunftshinweis trennt Script und tatsächliches Statement.Text. Alle übrigen Modellzeilen unverändert. |
| `copy-golden-values.json` | Ausschließlich `root-key.expectedText` → dessen JSON-Member `Text`. Die übrigen 18 Fälle und alle anderen KEY-Member sind unverändert. |
| `G2-04-01-F2-visual.html` | Mit dem bestehenden Buildskript neu erzeugt; ausschließlich das korrigierte Modell ist anders eingebettet. HTML-Struktur, Styling und UI-Skript unverändert. |
| `G2-04-01-F2-copy-contract.md` | Ein zusätzlicher Herkunftsabsatz beim KEY-Beispiel mit exaktem Wert und Quellbelegen. Alle übrigen Bytes des Vertrags unverändert. |
| `validate-artifacts.cjs` | Unabhängiger vollständiger Erwartungsstring, Negativkontrolle, Dokumentationsabgleich und Vergleich der eingebetteten HTML-Quellen; zusätzlich Prüfung der Anzahl 19. Alle bisherigen Prüfungen bleiben erhalten. |
| `validation-results.json` | Neu erzeugtes Protokoll: 42/42 PASS einschließlich 19/19 Goldens. |
| `validation.md` | Aktueller Prüfstand und Beschreibung der vier F3-Prüfungen, des erwarteten vorherigen Fehlschlags sowie der Nachweisgrenzen. |
| `G2-04-01-F2-corrections-report.md` | Klar gekennzeichneter F3-Nachtrag; ursprünglicher F2-/Vor-R4-Stand bleibt als historischer Bericht erkennbar. |
| `provenance.json` | Aktuelle Artefaktgrößen und SHA-256-Werte, Kennzeichnung als F3-Korrektur und Verweis auf R4 sowie gesicherte Vorher-Provenienz. |

Unverändert und durch SHA-256-Vergleich bestätigt: `G2-04-01-F2-architecture.md`, `G2-04-01-F2-accessor-matrix.md`, `G2-04-01-F2-acceptance.md`, `prototype-ui.js` und `build-artifacts.cjs`. A01–A38 wurden nicht geändert. Die Copy-Vertragsdatei ist nur um den genannten Herkunftsabsatz ergänzt; der normative Wert-/Formatvertrag bleibt unverändert.

Neu im F3-Verzeichnis:

| Datei | Zweck |
|---|---|
| `G2-04-01-F3-final-fixture-correction.md` | Dieser Abschlussbericht. |
| `inputs-before.json` | Hashes aller F2-Eingabedateien sowie des unveränderten R4-Berichts. |
| `f2-provenance-before.json` | Unveränderte Kopie des ursprünglichen F2-Manifests. |
| `f2-before-f3.zip` | Vollständiges F2-Paket vor dieser Korrektur zur historischen Reproduzierbarkeit von R4. |
| `scope-validation.json` | Gezielter Vergleich: nur erlaubte Modellzeilen, nur KEY.Text im Golden, nur Modell-Einbettung im HTML, nur Herkunftsabsatz im Copy-Vertrag. |
| `final-evidence.json` | Vorher-/Nachher-Hashes, tatsächliche Änderungsmenge, Baseline, R4-Unverändertheit und abschließende Prüfergebnisse. |

Zusätzlich wird `G2-04-01-F3.zip` im übergeordneten Dokumentationsverzeichnis als Auslieferung des korrigierten F2-Pakets samt F3-Nachweisen erstellt. Der ursprüngliche `G2-04-01-F2.zip` bleibt ausdrücklich der historische, nicht korrigierte Stand. Der R4-Bericht wird nicht nachträglich umgeschrieben.

## 3. Unabhängige Validierung

Der neue Erwartungswert in `validate-artifacts.cjs` ist ein vollständiges Literal:

```javascript
const expectedKeyStatementText='KEYdemoKey=0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)';
```

Er wird weder aus `D.fixtures`, aus `keyValue`, aus dem Golden noch aus dem gemeinsamen Buildskript abgeleitet. Gegen ihn werden die tatsächliche Modell-Property, das Text-Member des geparsten Root-Goldens und das skalare Text-Copy geprüft. Die Quelle des Erwartungswerts ist die dokumentierte Parser-/Evaluatorsemantik, keine zweite Serialisierung derselben Fixture.

**Nachweis vor Neugenerierung:** Nach Ergänzung der Prüfung und Korrektur des Modells wurde der noch alte Golden geprüft. Der Validator scheiterte mit Exitcode 1 und `KEY golden must reflect parser Statement.Text`. Der fehlende bzw. zusätzliche Abstand nach KEY wurde exakt ausgewiesen. Dieser erwartete Fehler verhinderte ein neues erfolgreiches Prüfprotokoll.

**Dauerhafte Negativkontrolle:** Der Validator weist auch absichtlich identische falsche Texte in Fixture und Golden zurück. Zusätzlich weist er einen allein falschen Golden zurück. Damit reicht gegenseitige Konsistenz der Eingaben nicht zum Bestehen der Prüfung.

| Prüfung | Ergebnis |
|---|---|
| `key-statement-text-independent-oracle` | PASS: Modell, Golden und Text-Copy entsprechen dem festen Erwartungswert. |
| `key-text-oracle-rejects-shared-fixture-error` | PASS: Der ursprüngliche Fehler wird auch bei gemeinsam falschen Eingaben erkannt. |
| `key-text-documentation-provenance` | PASS: Script mit Abstand und Property ohne Abstand sind in Dokumentation und Herkunftshinweis korrekt getrennt. |
| `html-embedded-sources-match` | PASS: Modell- und UI-Skript sind exakt in das HTML eingebettet. |
| Alle bisherigen 38 F2-Artefaktprüfungen | PASS; jeder ursprüngliche Prüfungsname ist im neuen Ergebnis genau einmal mit PASS enthalten. |
| Alle 19 Copy-Golden-Values | PASS; sämtliche Fälle des vollständigen bestehenden Copy-Pfads erneut geprüft. |
| Gezielte Änderungsabgrenzung | PASS; keine Änderung weiterer Modellfunktionen, Goldens, HTML-Struktur oder Copy-Regeln. |

**Gesamtergebnis: 42/42 Artefaktprüfungen PASS.** Die 19 Golden-Prüfungen sind Teil dieser 42, kein zusätzlicher Satz von 19 Produktprüfungen. Ebenso bleiben die 38 Implementierungs-Abnahmekriterien A01–A38 ein eigener, nicht ausgeführter Nachweiskatalog.

Der bestehende vollständige große Shares-Datenpfad wurde erneut geprüft: 33.423.360 Rohbytes, 66.852.988 JSON-Zeichen, unveränderter SHA-256 `b1b6e0e777b6678f113ce4e59fc8f7d3cf282a21828828f8b2799f49d061ed3a`. Dies bleibt ein JavaScript-Artefaktnachweis, kein nativer Clipboard-, Speicher- oder Host-Nachweis.

Reproduktion der regulären Artefaktprüfung im korrigierten F2-Verzeichnis:

```text
node build-artifacts.cjs
node validate-artifacts.cjs
```

Die beiden Befehle erzeugen nur Dokumentationsartefakte. Provenienz und Vorher-/Nachher-Vergleich sind gesondert in den mitgelieferten JSON-Dateien festgehalten.

## 4. Grenzen und Abschluss

Vor und nach der Bearbeitung gilt `HEAD == master == 32017a08c4771e73c12a50f6e1548476b9bb0a54`; der Worktree ist sauber. Keine Produktcode- oder Produktteständerungen, keine Änderungen an master, keine Commits, Pushes oder Merges. Keine Implementierung von G2-04-02. F1 und R4 bleiben unverändert.

Nur R4-L01 wurde korrigiert. Keine zusätzliche umfassende Architekturprüfung, keine neue Architekturentscheidung und keine native Implementierungsabnahme. Host-, Accessor-, Ressourcen-, Lifecycle-, Docking- und Migrationsverträge sind unverändert. Die Copy-Formatierung ist unverändert; der korrigierte Beispielwert entspricht jetzt ihrer schon bestehenden Forderung nach exakter Property-Wiedergabe.

**R4-L01: im F3-Korrekturumfang CLOSED. READY FOR OWNER REVIEW — nicht automatisch ACCEPTED.**
