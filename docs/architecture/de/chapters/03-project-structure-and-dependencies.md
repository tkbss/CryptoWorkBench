# 3. Projektstruktur und Abhängigkeiten

[Architekturdokumentation](../README.md)

| Metadaten | Wert |
| --- | --- |
| Status | AR-01-Entwurf; unabhängige technische und sprachliche Prüfung ausstehend |
| Implementierungsstand | `fbbadae2f68b015ccedfea92ce8bba4cd7737ecb` |
| D1-Ergänzung | `8c58e2bd564280b71412cfd6ed9997688bd0bde4` |
| Datum | 2026-11-06 |
| Verifikation | Statische Quellprüfung; keine neuen Anwendungstests |

## 3.1 Repository-Struktur

**[F]** Produktions-/Testprojekte liegen weiterhin nebeneinander im Repository-Stamm statt in einer src/tests-Hierarchie. CryptoScript enthält Antlr, Model/Ast, Operations, CryptoAlgorithm, Variables, ErrorListner, Documentation und InfoDocs sowie zusätzliche Bereiche für Persistenz, Secret Sharing und Dateioperationen. Die GUI enthält Views, ViewModels, Models, Services, Stile und Assets. Dies sind Verzeichnisse innerhalb von Projekten, keine separat kompilierten Schichten. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R10](../evidence/ar-01-baseline-reconciliation.md#r10), [R14](../evidence/ar-01-baseline-reconciliation.md#r14), [R15](../evidence/ar-01-baseline-reconciliation.md#r15)

Die acht Projekte im Stamm umfassen fünf Produktionsbibliotheks-/Hostprojekte und drei Testprojekte. Weitere versionierte Projekte sind TestInfrastructure/Avalonia.Headless.Backport und eng/mcp/native/B3NativeProbe. Der erhaltene Backport deklariert Avalonia 11.3.20; die aktiven GUI-Tests deklarieren dagegen Avalonia.Headless 12.1.4. Die Aufnahme ins Inventar bedeutet keine aktive Produktionsnutzung. Vorbereitete externe Prism-Quellen unter .deps bilden eine eigene Abhängigkeitskategorie. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

ScriptBooks enthält Beispiele. .github enthält CI- und Validierungsskripte; eng/prism enthält Vorbereitungs-/Build-Richtlinien und Sperrdateien; docs enthält Architektur- und Implementierungsnachweise. docs/architecture existiert jetzt sowohl im Implementierungsstand als auch im integrierten Rekonstruktionsbaum. CryptoWorkBenchConsole und dessen reflektionsbasierte Testfixture ConsoleSemanticDiagnosticTests sind entfernt.

## 3.2 Solution-Struktur

**[F]** Die Root-Solution enthält CryptoScript, CryptoScriptUnitTest, Automation.Abstractions, Mcp und Mcp.Tests. GUI und Desktop-Host sind nicht enthalten. Die Avalonia-Solution enthält CryptoScript, GUI, Desktop, GUI-Tests, Automation.Abstractions, Mcp und Mcp.Tests. Das Kerntestprojekt ist nicht enthalten. ScriptBooks bleibt ein virtueller Solution-Ordner und kein Projekt. [R02](../evidence/ar-01-baseline-reconciliation.md#r02)

Beide Solutions behalten Debug-/Release-Konfigurationen. Solution-Mitgliedschaft unterscheidet sich von transitiver Projekterreichbarkeit und Produktumfang. Keine Solution allein ist ein vollständiges Repository-Inventar. **[Q]** Eine umfassende Einstiegs-Solution für Entwickler bleibt eine Richtlinienfrage; explizite Build-Skripte bilden den aktuellen Build-Einstieg, ohne einen spekulativen Solution-Neuentwurf zu erfordern.

**D1.** Die Avalonia-Solution enthält am A1-Stand sieben Projekte. Die GUI bezieht Prism über temporäre Pakete. Die vier vorbereiteten Prism-Quellprojekte gehörten zum früheren AR-01-Stand. [E01](../evidence/mcp-a1.md#e01)

## 3.3 Produktionsprojekte und Verantwortlichkeiten

| Projekt | Ausgabe und Verantwortung |
| --- | --- |
| CryptoScript | Bibliothek; Sprache, Auswertung, Kryptografie, Laufzeitzustand, Diagnosen und Hilfe |
| CryptoWorkBench.Automation.Abstractions | Bibliothek; Automatisierungsschnittstellen und DTOs/Verträge ohne Projektreferenzen |
| CryptoWorkBench.Mcp | Bibliothek; Transport-/Proxy-Anpassung an Automatisierungsschnittstellen |
| CryptoWorkBenchAvalonia | Bibliothek; GUI, Prism-Komposition, Host-/Anwendungskoordination und Darstellung |
| CryptoWorkBenchAvalonia.Desktop | WinExe; Plattformstart, Desktop-MCP-Anbindung und Startzweig --mcp-stdio |

**[F]** Diese Projekte verwenden net10.0 als Ziel. App erbt von PrismApplication. Die GUI implementiert und registriert Automatisierungsdienste; Desktop verbindet den Transport mit dieser Implementierung. Die Pipeline verantwortet gemeinsame Parsing-/Ausführungs-/Darstellungsbehandlung anstelle des alten ausschließlich direkten Editor-Parser-/Runner-Pfads. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R04](../evidence/ar-01-baseline-reconciliation.md#r04), [R05](../evidence/ar-01-baseline-reconciliation.md#r05)

**[I]** Start, Hostanpassung und Kernwiederverwendung sind unterschiedliche Verantwortlichkeiten. Sprachverarbeitung und Kryptografie teilen weiterhin eine Kernassembly; ihre Verzeichnisstruktur erzwingt daher keine unabhängigen Schichten.

## 3.4 Projektabhängigkeiten

| Referenzierendes Projekt | Direkte ProjectReference-Ziele |
| --- | --- |
| CryptoScript | Keine |
| Automation.Abstractions | Keine |
| Mcp | Automation.Abstractions |
| GUI | CryptoScript; Automation.Abstractions |
| Desktop | GUI; Mcp |
| Kerntests | CryptoScript |
| GUI-Tests | GUI; Mcp |
| MCP-Tests | Mcp |
| B3NativeProbe | Desktop; GUI-Tests |

**[F]** Der [Produktgraph](../diagrams/01-project-references.mmd) zeigt direkte eigene Produktionsreferenzen; externes Prism ist oben separat deklariert. Desktop erreicht CryptoScript transitiv über GUI und Automation.Abstractions über GUI und Mcp. Mcp hat keine direkte GUI- oder CryptoScript-Referenz. GUI-Tests verwenden Kerntypen transitiv. Die direkte Desktop-Referenz des nativen Probes darf nicht mit einer gewöhnlichen Testprojektreferenz verwechselt werden. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02)

**[I]** Der eigene Projektgraph enthält keinen Projektzyklus. Gemeinsame Schnittstellen ermöglichen Hostanpassung; allein daraus folgen weder Laufzeitisolation noch Autorisierung oder separat bereitgestellte Dienste.

## 3.5 Externe Paketabhängigkeiten

**[F]** Dies sind direkte Deklarationen, kein Audit wiederhergestellter transitiver Pakete. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R02](../evidence/ar-01-baseline-reconciliation.md#r02), [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

| Bereich | Deklarierte Pakete / Richtlinie |
| --- | --- |
| Kern | Antlr4.Runtime.Standard 4.13.1; BouncyCastle.Cryptography 2.7.0; Newtonsoft.Json 13.0.4 |
| GUI-Framework | Avalonia, Markup.Xaml.Loader, Themes.Fluent und Fonts.Inter 12.1.4 |
| UI-Steuerelemente | AvaloniaEdit 12.0.0; DataGrid 12.1.2; FluentIcons 2.1.344; Markdown.Avalonia 12.0.2; MessageBox.Avalonia 12.0.0; DialogHost 0.12.3; UVtools 5.2.0 |
| Diagnostik / Desktop | DiagnosticsSupport 2.2.3 nur in Debug; Avalonia.Desktop 12.1.4 |
| MCP | ModelContextProtocol.AspNetCore [2.3.0]; Frameworkreferenz Microsoft.AspNetCore.App |
| Kern-/GUI-Tests | Test-SDK 18.9.0; NUnit 4.6.1; Adapter 6.3.0; FluentAssertions 8.10.0; Analyzer 4.14.0; coverlet 6.0.4 |
| Zusätzliche Tests | GUI: Avalonia.Headless 12.1.4; MCP: JsonSchema.Net 9.4.1, Test-SDK/NUnit/Adapter |

Am AR-01-Stand verwendete Prism vorbereitete Quellreferenzen. Directory.Build.targets verlangt die temporäre Prism-Umgebung. Die eng/prism-Richtlinie ordnet Sperrdateien nach Konfiguration und Veröffentlichungs-RID zu und aktiviert gesperrtes Restore. Am AR-01-Stand führte [NuGet.Config](https://github.com/tkbss/CryptoWorkBench/blob/fbbadae2f68b015ccedfea92ce8bba4cd7737ecb/NuGet.Config) ausschließlich nuget.org auf. Am A1-Stand ordnet die [Paketquellenzuordnung](https://github.com/tkbss/CryptoWorkBench/blob/8c58e2bd564280b71412cfd6ed9997688bd0bde4/NuGet.Config#L3-L12) `CryptoWorkBench.Temporary.Prism.*` dem lokalen Feed `.packages/prism` zu. nuget.org bleibt als Quelle für das allgemeine Muster `*` konfiguriert. Der zentrale AvaloniaVersion-Wert ist jetzt 12.1.4, während Projektdeklarationen explizit bleiben. Nullable ist aktiviert; die GUI verwendet die neueste Sprachversion und behält ausdrücklich Reflexionsbindungen als Standard bei. Die historische Aussage fehlender Sperrdateien/NuGet-Konfiguration ist überholt.

Generierte ANTLR-Header nennen weiterhin 4.13.2 bei Laufzeitversion 4.13.1. Hilfsskripte rufen externes antlr4 ohne festgelegte Generatorversion auf. Dies ist eine Reproduzierbarkeitsfrage, keine nachgewiesene Inkompatibilität. Unterschiedliche Versionsnummern der UI-Pakete beweisen ebenfalls keine Inkompatibilität.

## 3.6 Build- und Testorganisation

**[F]** Der Workflow läuft für Pushes/PRs nach master und behält vier Runner-/RID-Paare: windows-latest/win-x64, ubuntu-latest/linux-x64, macos-26/osx-arm64 und macos-26-intel/osx-x64. Er verwendet Invoke-PrismCi für Debug-/Release-Builds, native Prüfungen, Core/Gui/Coordinator/F1/C1/Mcp-Testsuiten und RID-Veröffentlichung. Der Wrapper prüft exakt SDK 10.0.401, aktiviert die vorbereitete Prism-Umgebung, prüft Rückgabecodes und verlangt gefundene erfolgreiche Tests, statt veralteten TRX-Dateien zu vertrauen. [R03](../evidence/ar-01-baseline-reconciliation.md#r03)

Die Veröffentlichung ist frameworkabhängig mit Apphost, gesperrtem RID-Restore und expliziter Ausgabevalidierung. Der Validator prüft erforderliche Assets, Laufzeit-/Abhängigkeitsmanifeste, RID, native Assets, gegebenenfalls Ausführungsrechte und paketierte InfoDocs. Der Kern importiert native Build-Targets für Dateipublikation; macOS-Veröffentlichung erfordert einen tatsächlichen macOS-Host samt Werkzeugkette statt angenommener Cross-Veröffentlichung unter Windows. Das [Abdeckungsdiagramm](../diagrams/03-build-and-test-dependency-coverage.mmd) trennt CI-Auswahl von Build-Referenzen.

Der historische Master-CI-Lauf 38710123715 bei efa81d6 belegt weiterhin, dass die frühere fehlende Desktop-Build-Abdeckung dort behoben wurde. Er ist kein Ergebnis für fbbadae2. Für AR-01 wurden weder ein aktueller CI-Lauf noch Anwendungstests ausgeführt; konfigurierte Prüfungen sind kein beobachteter Erfolg. Native Dateiprüfungen belegen nicht jede grafische Interaktion, jedes Installationsverhalten oder Betriebssystemversprechen. Generierte ANTLR-Quellen bleiben versioniert und werden im geprüften Anwendungs-Build-Pfad nicht neu erzeugt.

## 3.7 Abhängigkeitsgrenzen und Einschränkungen

**[F/I]** Die Unabhängigkeit des Kernprojekts, die Trennung von Host-/GUI-Start und gemeinsame Automatisierungsverträge sind reale Codegrenzen. Sie beseitigen weder veränderlichen globalen Zustand noch Host-Callbacks, statische Ausgabeereignisse oder lokale Dateizugriffe. Friend-Assembly-Sichtbarkeit ist Testzugriff, keine umgekehrte Projektabhängigkeit. Neue Verzeichnisse oder Verträge implizieren keinen allgemeinen Neuentwurf der Assembly-Schichten. [R01](../evidence/ar-01-baseline-reconciliation.md#r01), [R05](../evidence/ar-01-baseline-reconciliation.md#r05), [R12](../evidence/ar-01-baseline-reconciliation.md#r12), [R13](../evidence/ar-01-baseline-reconciliation.md#r13)

Die Konsolentestabhängigkeit und die frühere Beobachtung eines bedingungslos referenzierten GUI-Diagnostikpakets sind überholt. Offen bleiben eine umfassende Entwickler-Solution, verbindliche ANTLR-Erzeugung, zukünftige Prism-Paketierungsrichtlinien und offizielle Plattformunterstützung. Laufende Prism-/GUI-Worktree-Änderungen sind ausgeschlossen. **[R]** Aktuelle Deklarationen, historische Validierung und zukünftige Empfehlungen bleiben getrennt; Produktionsabhängigkeiten werden nicht im Zuge der Architekturrekonstruktion umgeschrieben.

[Architekturdokumentation](../README.md)
