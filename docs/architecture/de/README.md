# CryptoScript Workbench Architektur

AR-MCP-01-D1 umfasst Kapitel 1–4, 8 und 9. AR-01 bleibt am Stand `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` verankert. D1-Ergänzungen und die Kapitel 8–9 verwenden `8c58e2bd564280b71412cfd6ed9997688bd0bde4`. Die Ausgabe liegt zur unabhängigen technischen und sprachlichen Prüfung vor.

## Produktumfang

Der Umfang umfasst den wiederverwendbaren CryptoScript-Kern, den Desktop-Host und die lokale MCP-Integration. Kapitel 8 erklärt die Schnittstellen, Kapitel 9 die Ausführung im gemeinsamen Arbeitsbereich.

## Inhaltsverzeichnis

| Kapitel | Titel | Status |
| --- | --- | --- |
| 1 | [Einführung](chapters/01-introduction.md) | AR-01-Entwurf |
| 2 | [Systemübersicht](chapters/02-system-overview.md) | AR-01-Entwurf |
| 3 | [Projektstruktur und Abhängigkeiten](chapters/03-project-structure-and-dependencies.md) | AR-01-Entwurf |
| 4 | [Spracharchitektur](chapters/04-language-architecture.md) | AR-01-Entwurf |
| 5 | Laufzeitarchitektur | Geplant; nicht analysiert |
| 6 | Kryptografische Architektur | Geplant; nicht analysiert |
| 7 | Diagnostik und Fehlerbehandlung | Geplant; nicht analysiert |
| 8 | [Schnittstellen und Integration](chapters/08-interfaces-and-integration.md) | D1 |
| 9 | [Datenflüsse und Ausführungsfolgen](chapters/09-data-flows-and-execution-sequences.md) | D1 |
| 10 | GUI-Architektur | Geplant; nicht analysiert |
| 11 | Tests und Qualitätssicherung | Geplant; nicht analysiert |
| 12 | Architekturbewertung | Geplant; nicht analysiert |
| 13 | Architekturentwicklung und Roadmap | Geplant; nicht analysiert |

## Nachweiskonventionen

**[F]** verifizierte Tatsache; **[O]** Eigentümerentscheidung; **[I]** Interpretation; **[Q]** offene Frage; **[R]** Empfehlung; **[P]** Planung. Statische Prüfung belegt keine erfolgreichen Tests, Zertifizierung oder Plattformunterstützung. Aktuelle Quellen stehen im [Nachweisregister](evidence/ar-01-baseline-reconciliation.md). [MCP-Quellennachweise](evidence/mcp-a1.md).

## Veröffentlichung

Eine gemeinsame Werkzeugkette verwendet publication.json und erzeugt getrennte Ausgaben. Beide Ausgaben enthalten das abgeglichene Kapitel 9. Markdown und Mermaid sind maßgeblich. PDF-/SVG-/HTML-/QA-Ausgaben liegen ignoriert unter generated/; Abhängigkeiten unter tools/node_modules/. Es erfolgt kein automatisches Einchecken.

```sh
node docs/architecture/tools/generate.mjs
node docs/architecture/tools/generate.mjs --lang de
node docs/architecture/tools/validate.mjs --lang en
node docs/architecture/tools/validate.mjs --lang de
```

Englische Quellen behalten ihre bisherigen Pfade. Deutsche Quellen stehen unter de/. Ausgabeorte: generated/CryptoScript-Workbench-Architecture.pdf und generated/de/CryptoScript-Workbench-Architecture.pdf. Ein Fehler invalidiert nur die gewählte Sprache.

## Erhaltene Historie

Kapitel 1–2 verwendeten d3099c8, Kapitel 3 efa81d6 und Kapitel 4 29b6b36. Analysezeitpunkte waren 13.–16. Oktober 2026. Der frühere Architektur-Checkout und seine Beobachtung ausgeschlossener lokaler Anwendungsänderungen bleiben historische Metadaten. Kapitel 1–3 waren geprüft; Kapitel 4 war ein Entwurf. Der historische CI-Lauf 38710123715 und die 51-seitige PDF-Prüfung bezogen sich auf damalige Eingaben. Kapitel 9 dokumentiert die Herkunft des früheren Teilkapitels. Alle importierten Implementierungsberichte und Nachweise bleiben erhalten.

[Ursprünglicher Dokumentationseinstieg](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/README.md) | [G03](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/G03-report.md) | [G03-Referenzprüfungen](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/evidence/G03/run-reference.ps1)
