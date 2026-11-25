# 1. Einführung

[Architekturdokumentation](../README.md)

| Metadaten | Wert |
| --- | --- |
| Status | AR-01-Entwurf; unabhängige technische und sprachliche Prüfung ausstehend |
| Implementierungsstand | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| D1-Ergänzung | `8c58e2bd564280b71412cfd6ed9997688bd0bde4` |
| Datum | 2026-11-06 |
| Verifikation | Statische Quellprüfung; keine neuen Anwendungstests |

## 1.1 Zweck dieses Dokuments

Diese Architekturrekonstruktion erläutert die implementierte CryptoScript Workbench anhand überprüfbarer Repository-Nachweise. Sie unterstützt Entwickler dabei, Verantwortlichkeiten, Abhängigkeiten und Ausführungsverhalten nachzuvollziehen. Sie rekonstruiert das bestehende System und definiert keine Zielarchitektur. Jede aktuelle Implementierungsaussage verwendet den oben festgelegten Stand und das gemeinsame Quellenregister. Statische Prüfung belegt Deklarationen und Kontrollfluss, jedoch keine bestandenen Tests, Plattformunterstützung oder kryptografische Konformität.

Die früheren Kapitel 1–3 wurden an ihren historischen Ständen geprüft; Kapitel 4 blieb ein Entwurf. Die AR-01-Änderungen übernehmen diese Freigabe nicht. Die exakten historischen Kapitel- und Diagrammdateien bleiben unter evidence/history/abbf7b7b erhalten; ursprüngliche Nachweise und Prüfberichte bleiben unverändert. Historische Aussagen sind im Kontext ihrer ursprünglichen Commits zu lesen.

## 1.2 Zweck des Systems

**[F]** Die Avalonia-Desktopanwendung beherbergt einen wiederverwendbaren CryptoScript-Interpreter und eine Bibliothek kryptografischer Operationen. Der Desktop-Einstiegspunkt startet den klassischen Avalonia-Lebenszyklus; GUI-Anwendungsdienste koordinieren Parsing, Ausführung, Ausgabe und Variablendarstellung. Die gemeinsame Ausführungspipeline ruft ParserBuilder und den asynchronen Runner auf. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

**[F]** Die Dispatch-Logik umfasst symmetrische Verschlüsselung, MACs, Hashing, Schlüsselableitung sowie TR-31- und PIN-Block-Operationen. Snapshot-Persistenz, sicherer Import/Export und dateibezogene Operationsverträge erweitern die Zuständigkeit der Laufzeit für lokale Ressourcen. Mechanismusnamen belegen keine Kommunikation mit Zahlungssystemen, Karten oder HSMs. [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

**[I]** Das System ist eine Desktop-Workbench zum Formulieren und Ausführen kryptografischer Operationen über einen eingebetteten, wiederverwendbaren Skriptkern. Automatisierungsverträge und MCP ergänzen eine Host-Integrationsgrenze, ohne CryptoScript zu einem separaten Server zu machen.

## 1.3 Umfang und Abgrenzung

**[O]** Der bestehende Produktumfang umfasst den wiederverwendbaren Kern, den Avalonia-Desktop-Host, GUI-Komposition, Anwendungsdienste, Darstellung, Sprachverarbeitung, Laufzeit, Kryptografie, Diagnostik, Dokumentation und relevante Prüfinfrastruktur. AR-01 berücksichtigt implementierte Automatisierungs-/MCP-Projekt- und Prozessgrenzen. D1 behandelt Transport, Sicherheit, Leases und Lebenszyklus in Kapitel 8 und 9. Der unfertige GUI-Neuentwurf und Änderungen anderer Worktrees sind ausgeschlossen.

**[F]** CryptoWorkBenchConsole und dessen konsolenspezifische GUI-Testfixture fehlen im festgelegten Baum. Auch der frühere Rsa-Platzhalter und die AsymmtricCryptoAlgorithm-Infrastruktur fehlen; ihre alten Beschreibungen sind historische Nachweise, keine aktuellen Fähigkeiten. Die vollständige Projektübersicht steht in Kapitel 3. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

**[P]** Der vom Eigentümer vorgegebene Versionsfokus bleibt symmetrische Kryptografie für Version 1; klassische asymmetrische Kryptografie, Zertifikate, CMS und Post-Quanten-Verfahren sind für Version 2 geplant. Dies ist Planungskontext, kein Nachweis veröffentlichter Versionen oder implementierter Unterstützung. **[Q]** Verträge und Lieferreihenfolge dieser Bereiche benötigen spätere Nachweise und Entscheidungen. Eine zukünftige Webanwendung bleibt außerhalb der aktuellen Rekonstruktion.

## 1.4 Nachweise und Verifikation

Die aktuellen Quellenidentitäten R01–R16 sind sprachübergreifend identisch. Das maschinenlesbare Register hält für jede Quelle unveränderlichen Commit, Repository-Pfad, SHA-256 und Zeilenzahl fest. Kapitelverweise benennen die relevante Verantwortung oder Deklaration; der Nachweisanhang enthält Quellenlinks. Die Prüfung von Testassertionen wird von der Testausführung unterschieden. Historische CI- und PDF-Ergebnisse behalten ihre ursprünglichen Daten und Eingaben.

**[R]** Jede Aussage ist bei Änderung ihrer Quelle neu zu prüfen. Deklarierte Abhängigkeiten, wiederhergestellte Pakete, beobachtetes Laufzeitverhalten und offiziell unterstützte Umgebungen sind zu unterscheiden. Aus der Projektstruktur folgt weder kryptografische Zertifizierung noch eine vollständige Isolationsgarantie.

## 1.5 Architektursichten

Die [Projektreferenzsicht](../diagrams/01-project-references.mmd) zeigt die eigenen Produktionsprojekte. Ein durchgezogener Pfeil führt vom referenzierenden zum referenzierten Projekt. Dies ist eine Build-Sicht, keine Laufzeitnachrichtenfolge und kein vollständiges Repository-Inventar. Vorbereitete externe Prism-Projekte sowie Test-/Hilfsprojekte werden in Kapitel 3 inventarisiert. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02)

**[R]** Prozessgrenzen, Paketgrenzen und logische Komponenten bleiben getrennt. Beziehungen werden erst nach Prüfung ihrer Implementierung ergänzt.

## 1.6 Konventionen und Terminologie

**[F]** bezeichnet eine verifizierte Implementierungstatsache; **[O]** eine Eigentümerentscheidung bzw. deren Kontext; **[I]** eine Architekturinterpretation; **[Q]** eine offene Frage; **[R]** eine Empfehlung; **[P]** eine Planung. Bezeichner behalten die exakte Schreibweise der Quelle. CryptoScript Workbench bezeichnet das System, CryptoWorkBench das Repository bzw. die Solutions und CryptoScript den Kern. Ein Mechanismus ist ein Dispatch-Bezeichner, keine Zertifizierungsaussage.

Englische Quellpfade bleiben stabil. Deutsche Quellen verwenden dieselben Dateinamen unter de/. Kapitel-/Abschnittsnummern, Nachweisidentitäten, Code und Quellenziele sind identisch. Markdown und Mermaid sind maßgeblich; erzeugte PDFs, SVGs und QA-Dateien bleiben ignoriert. Übersetzungsterminologie und Prüfgrenzen stehen in der Entscheidung zur zweisprachigen Veröffentlichung.

## 1.7 Dokumentaufbau

Der Architekturplan umfasst dreizehn Kapitel. Auf den AR-01-Stand der Kapitel 1–4 folgen [Schnittstellen und Integration](08-interfaces-and-integration.md) und [Datenflüsse und Ausführungsfolgen](09-data-flows-and-execution-sequences.md) am A1-Stand. Die Kapitel 5–7 und 10–13 bleiben zurückgestellt.

Kapitel 2 erläutert den Laufzeitkontext, Kapitel 3 Paketierung und Build-Abhängigkeiten und Kapitel 4 die Sprachverarbeitung. Laufzeit- und Kryptografiedetails gehören in Kapitel 5–6, Diagnostik in Kapitel 7, Integration in Kapitel 8, umfassendere Abläufe in Kapitel 9, GUI-Architektur in Kapitel 10 sowie Tests, Bewertung und Weiterentwicklung in Kapitel 11–13.

[Architekturdokumentation](../README.md)
