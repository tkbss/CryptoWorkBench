# CryptoScript Workbench Architektur

AR-01 rekonstruiert Kapitel 1–4 am Implementierungsstand `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb`. Status: Entwurf zur unabhängigen technischen und sprachlichen Prüfung; frühere Freigaben werden nicht übertragen. Englisch ist die Standardsprache, Deutsch verwendet dieselben Kapitel-/Abschnittsnummern, Quellenidentitäten und technischen Inhalte.

## Produktumfang

Wiederverwendbarer CryptoScript-Kern, Avalonia-Desktop, Komposition, Anwendungsdienste, Darstellung, Sprache, Laufzeit, Kryptografie, Diagnostik, Dokumentation und Prüfinfrastruktur bleiben im Umfang. Aktuelle Automatisierungs-/MCP-Grenzen werden berücksichtigt; detaillierte MCP-Analyse bleibt AR-MCP-01 vorbehalten. Unfertiger GUI-Neuentwurf und fremde Worktree-Änderungen sind ausgeschlossen.

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
| 8 | Schnittstellen und Integration | Geplant; nicht analysiert |
| 9 | [Datenflüsse und Ausführungsfolgen](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/chapters/09-data-flows-and-execution-sequences.md) | Historischer Teilzustandsvertrag; nicht in dieser Ausgabe |
| 10 | GUI-Architektur | Geplant; nicht analysiert |
| 11 | Tests und Qualitätssicherung | Geplant; nicht analysiert |
| 12 | Architekturbewertung | Geplant; nicht analysiert |
| 13 | Architekturentwicklung und Roadmap | Geplant; nicht analysiert |

## Nachweiskonventionen

**[F]** verifizierte Tatsache; **[O]** Eigentümerentscheidung; **[I]** Interpretation; **[Q]** offene Frage; **[R]** Empfehlung; **[P]** Planung. Statische Prüfung belegt keine erfolgreichen Tests, Zertifizierung oder Plattformunterstützung. Aktuelle Quellen stehen im [Nachweisregister](evidence/ar-01-baseline-reconciliation.md).

## Veröffentlichung

Eine gemeinsame Werkzeugkette verwendet publication.json und erzeugt getrennte Ausgaben. Kapitel 9 bleibt erhalten, wird aber nicht veröffentlicht. Markdown und Mermaid sind maßgeblich. PDF-/SVG-/HTML-/QA-Ausgaben liegen ignoriert unter generated/; Abhängigkeiten unter tools/node_modules/. Es erfolgt kein automatisches Einchecken.

```sh
node docs/architecture/tools/generate.mjs
node docs/architecture/tools/generate.mjs --lang de
node docs/architecture/tools/validate.mjs --lang en
node docs/architecture/tools/validate.mjs --lang de
```

Englische Quellen behalten ihre bisherigen Pfade. Deutsche Quellen stehen unter de/. Ausgabeorte: generated/CryptoScript-Workbench-Architecture.pdf und generated/de/CryptoScript-Workbench-Architecture.pdf. Ein Fehler invalidiert nur die gewählte Sprache.

## Erhaltene Historie

Kapitel 1–2 verwendeten d3099c8, Kapitel 3 efa81d6 und Kapitel 4 29b6b36. Analysezeitpunkte waren 13.–16. Oktober 2026. Der frühere Architektur-Checkout und seine Beobachtung ausgeschlossener lokaler Anwendungsänderungen bleiben historische Metadaten. Kapitel 1–3 waren geprüft; Kapitel 4 war ein Entwurf. Der historische CI-Lauf 38710123715 und die 51-seitige PDF-Prüfung bezogen sich auf damalige Eingaben. Teilkapitel 9 hat einen eigenen Stand und einen noch ausstehenden Dokumentationsreview. Alle importierten Implementierungsberichte und Nachweise bleiben erhalten.

[Ursprünglicher Dokumentationseinstieg](https://github.com/tkbss/CryptoWorkBench/blob/abbf7b7b2f22f1bbc06c5c8b28702831099e097f/docs/architecture/README.md) | [G03](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/G03-report.md) | [G03-Referenzprüfungen](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/docs/architecture/evidence/G03/run-reference.ps1)
