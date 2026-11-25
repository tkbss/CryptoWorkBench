# 2. Systemübersicht

[Architekturdokumentation](../README.md)

| Metadaten | Wert |
| --- | --- |
| Status | AR-01-Entwurf; unabhängige technische und sprachliche Prüfung ausstehend |
| Implementierungsstand | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| D1-Ergänzung | `8c58e2bd564280b71412cfd6ed9997688bd0bde4` |
| Datum | 2026-11-06 |
| Verifikation | Statische Quellprüfung; keine neuen Anwendungstests |

## 2.1 Systemkontext

**[F]** Der Desktop verbindet eine Avalonia-GUI und einen Desktop-Einstiegspunkt mit der wiederverwendbaren CryptoScript-Bibliothek. Der Benutzer bearbeitet und startet Skripte, betrachtet Ergebnisse, Variablen, Diagnosen und lokale Hilfe und öffnet bzw. speichert ScriptBooks. Automatisierungsverträge und eine implementierte lokale MCP-Integration ergänzen die Interaktion mit einem externen Client. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05)

**[I]** Der [Systemkontext](../diagrams/02-system-context.mmd) modelliert die Workbench als ein Softwaresystem. Der Akteur Workbench-Benutzer abstrahiert GUI-Interaktionen; er erfindet keine Administrator-, Auditor- oder Schlüsselverwalterrollen. Ein lokaler Automatisierungsclient ist jetzt belegt; die frühere Feststellung fehlender Netzwerkintegration beschreibt diesen Stand daher nicht mehr. Daraus folgt keine Verbindung zu entfernten Zahlungssystemen, Datenbanken oder HSMs.

**[O]** Der Desktop bleibt der strategische Anwendungsclient; die Wiederverwendung von CryptoScript ist ein Architekturziel. Eine zukünftige Webanwendung bleibt außerhalb des aktuellen Umfangs. [Kapitel 8](08-interfaces-and-integration.md) beschreibt Kopplung und Autorisierung.

## 2.2 Systemverantwortlichkeiten

**[F]** Das Sprachfrontend erzeugt Lexer-/Parserzustand, bildet Anweisungen auf kerneigene AST-Records ab und wertet Deklarationen, Zuweisungen und Aufrufe aus. Laufzeitoperationen lösen Argumente und Mechanismen auf, verwalten Variablen/Schlüssel/Parameter, erzeugen Diagnosen und Ausgaben und greifen gemäß operationsspezifischen Verträgen auf lokale Ressourcen zu. [R06](../evidence/ar-01-baseline-reconciliation.md#r06), [R08](../evidence/ar-01-baseline-reconciliation.md#r08), [R09](../evidence/ar-01-baseline-reconciliation.md#r09), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

Der Host koordiniert zugelassene Arbeit, Syntaxablehnung, asynchrone Ausführung, Passwortanfragen, Ausgabeerfassung und abgelöste Darstellungssnapshots. Das Öffnen eines ScriptBooks ist kein rein passiver Import: Der Editor-Importpfad führt Skriptzeilen aus und lässt aufgezeichnete Ausgabezeilen aus. Abbruch und Host-Koordination sind relevant; daraus entsteht keine neue Transaktion für das gesamte Buch. [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

Save/Load, sichere Snapshot-Operationen, Delete und Verträge für Dateiverschlüsselung, MAC und Hash erweitern die frühere rein speicherbezogene Übersicht. Variablensnapshots sind von ScriptBook-Quelldateien zu unterscheiden. Jede Operation hat eigenes Validierungs- und Publikationsverhalten; dieses Kapitel leitet daraus keine allgemeine Dateisystemtransaktion ab. [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

## 2.3 Architekturüberblick

**[F/I]** Ein .NET-Projekt ist eine Build-Definition; eine Assembly ist eine daraus abgeleitete Paketierungsgrenze; eine logische Komponente gruppiert Verantwortlichkeiten. Keine dieser Einheiten definiert automatisch einen weiteren Betriebssystemprozess. CryptoScript ist eine eingebettete Bibliothek, die im Desktopprozess aufgerufen wird. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04)

Die [Containersicht](../diagrams/02-container-overview.mmd) umfasst den Desktopprozess und den separaten Aufruf derselben Desktop-Programmdatei mit --mcp-stdio. Dieser Startzweig führt den Proxy mit Zertifikat-Pinning aus und kehrt vor dem Avalonia-Start zurück. Der normale Desktop kann LocalMcpHost starten; dieser delegiert über gemeinsame Schnittstellen an die Automatisierungsimplementierung des Hosts. Mcp ist ein Bibliotheksprojekt, kein separates Server-Programmprojekt. Proxy und Desktop sind unterschiedliche Ausführungskontexte; Kapitel 8 und 9 beschreiben die Sitzung. [R04](../evidence/ar-01-baseline-reconciliation.md#r04)

Lokale Hilfe, ScriptBooks und Laufzeitdatendateien sind Ressourcen, keine bereitgestellten Dienste. Console.WriteLine-Aufrufe im Kern existieren weiterhin, doch der normale Desktopstart leitet geerbte Standardausgabe und Standardfehler auf TextWriter.Null um; die Darstellung verwendet erfasste Ausgaben. Ein Console.WriteLine-Aufruf darf nicht als garantiert sichtbares Terminal dargestellt werden. [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

**D1.** Standardshell und optionale Dock-Shell binden denselben MCP-Dienst an. Ablauf und Rückgabe der Steuerung beschreibt [Kapitel 9](09-data-flows-and-execution-sequences.md). [E02](../evidence/mcp-a1.md#e02)

## 2.4 Wesentliche Systemkomponenten

| Komponente | Verantwortung |
| --- | --- |
| Desktopstart | Proxy- oder klassischen Desktoppfad auswählen; Desktop-MCP-Integration anbinden |
| GUI-Komposition | Shell, Views/ViewModels und Dienste mit Prism/DryIoc zusammensetzen |
| Host-Dienste | Zulassung, Editorkoordination, Ausführungspipeline, Passwortinteraktion und Darstellung |
| Automatisierungsverträge | Hostneutrale Schnittstellen, DTOs und Vertragsrepräsentation |
| MCP-Adapter | Lokaler Transport/Proxy und Delegation an Automatisierungsschnittstellen |
| Sprachfrontend | Grammatikbasierter Lexer/Parser, Syntaxdiagnosen und explizite AST-Abbildung |
| Laufzeit | Geordnete Anweisungsauswertung, gemeinsame Variablen und ausführungsbezogene Diagnosen |
| Operationen | Funktions-/Mechanismusauflösung, Kryptografie, Persistenz und Dateioperationen |
| Dokumentation/Ausgabe | Lokaler InfoDocs-Provider, Ausgabeereignisse und Host-Erfassung |

**[F]** Diese Verantwortlichkeiten werden durch [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R09](../evidence/ar-01-baseline-reconciliation.md#r09), [R13](../evidence/ar-01-baseline-reconciliation.md#r13), [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15) belegt. **[I]** Sie sind logische Gruppierungen, keine unabhängig bereitgestellten Dienste. Detaillierte Algorithmus-, Diagnose- und GUI-Interna gehören in spätere Pakete.

## 2.5 Externe Abhängigkeiten

**[F]** Das Produkt verwendet .NET 10 als Zielplattform. Der Kern deklariert Antlr4.Runtime.Standard 4.13.1, BouncyCastle.Cryptography 2.7.0 und Newtonsoft.Json 13.0.4; Kryptografie-APIs des Frameworks sind von diesen NuGet-Deklarationen getrennt. GUI/Desktop deklarieren Avalonia 12.1.4 und zugehörige UI-Pakete. AR-01 verwendete vorbereitete Prism-Quellreferenzen. Am D1-Stand verwendet die GUI temporäre Prism-Pakete. [E01](../evidence/mcp-a1.md#e01) Mcp deklariert ModelContextProtocol.AspNetCore [2.3.0] und Microsoft.AspNetCore.App. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

Paketdeklarationen beschreiben Abhängigkeiten, keine beobachteten wiederhergestellten oder ausgelieferten Versionen. UI-Pakete unterstützen Editor, Markdown-Darstellung, Symbole, Dialoge und Tabellen; sie sind keine externen Geschäftssysteme. NUnit, Test-SDKs, Assertions- und Coverage-Werkzeuge sind Entwicklungsabhängigkeiten. Kapitel 3 hält Paket-/Build-Details fest.

## 2.6 Ausführungsumgebungen

**[F]** Der Desktop verwendet StartWithClassicDesktopLifetime, UsePlatformDetect und Inter-Schriften. global.json wählt SDK 10.0.401 mit deaktiviertem rollForward und ohne Vorab-SDKs. CI deklariert Jobs für Windows x64, Linux x64, macOS ARM64 und macOS Intel mit RID-spezifischer Veröffentlichung. Die Prism-bewussten Stufen umfassen jetzt beide Build-Konfigurationen, Kern-/GUI-/MCP-Testsuiten, gezielte Regressionen und eine native Prüfung der Dateipublikation. [R03](../evidence/ar-01-baseline-reconciliation.md#r03), [R04](../evidence/ar-01-baseline-reconciliation.md#r04)

Das produktive Desktop-Steuerelement zur MCP-Kopplung ist in diesem Stand nur unter Windows sichtbar. Diese Einschränkung bedeutet weder, dass sämtliche Desktopfunktionen nur Windows unterstützen, noch, dass MCP für jede Matrixplattform zertifiziert ist. In AR-01 wurden weder ein aktueller Anwendungslauf noch ein CI-Ergebnis beobachtet. Historische erfolgreiche CI-Beobachtungen bleiben historisch. **[Q]** Offizielle Betriebssystem-/Laufzeitversionen, Distributionsvoraussetzungen und Unterstützung des interaktiven Starts benötigen ausdrückliche Richtlinien und geeignete Ausführungsnachweise.

## 2.7 Architekturgrenzen

**[F/I]** Der Kern hat keine GUI-/MCP-Projektabhängigkeit. Das unterstützt Wiederverwendung, beweist aber keine vollständige Hostunabhängigkeit: Variablen und Ausgabe bleiben gemeinsam, Syntaxdiagnosen enthalten statischen Zustand, und Operationen können Dateien verwenden oder Hostinteraktion anfordern. InterpreterExecution koordiniert Zuständigkeit und Zustandszugriff; es erzeugt nicht für jeden Runner ein eigenes Variablenverzeichnis. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R07](../evidence/ar-01-baseline-reconciliation.md#r07), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R13](../evidence/ar-01-baseline-reconciliation.md#r13), [R14](../evidence/ar-01-baseline-reconciliation.md#r14)

Der Host zentralisiert jetzt das Zurücksetzen und Prüfen von Syntaxdiagnosen sowie die asynchrone Ausführung in WorkbenchExecutionPipeline. Die GUI-/Kerngrenze bleibt breiter als eine reine Ausführungsfassade, weil Host-Dienste auch Variablenzustand erfassen und darstellen. Bei Ausführungsfehlern können zuvor abgeschlossene Wirkungen bestehen bleiben; Erfassungserfolg ist von Skripterfolg getrennt. Ein Erfassungsfehler kennzeichnet die Darstellung als veraltet und erlaubt keine Wiederholung der Ausführung. Das Ersetzen eines Zuweisungsziels und einzelne Datei-/Snapshot-Publikationsverträge dürfen nicht als Rollback des gesamten Skripts dargestellt werden. [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

**[Q]** Langfristige Clientisolationsrichtlinien, umfassende Plattformunterstützung und externes Verhalten delegierter Hilfe-Hyperlinks bleiben eigenständige Untersuchungen. **[R]** Tatsächliche Koordination ist von zugesicherter Isolation zu unterscheiden, ebenso Paket-, Prozess- und logische Grenzen.

[Architekturdokumentation](../README.md)
