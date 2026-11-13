# G2-03-01 – Theme Architecture & Design Preparation

Datum: 12.11.2026

Status: **G2-03-01 READY FOR DESIGN REVIEW**

Umfang: Bestandsaufnahme und Umsetzungskonzept; keine Theme-Implementierung.

## 1. Verifizierte Entwicklungsbasis / G2-I02-F2

| Gegenstand | Verifiziertes Ergebnis |
|---|---|
| Vorheriger master | `787b9f2974edab4b4d7a9702e853cd5460297802` |
| Neuer Dokumentationscommit | `d7c59ef69b9bbbd477c5c0e5bdc330d25ec1ff71` |
| Nachricht | `Document G2-I02 final master integration` |
| Parent | `787b9f2974edab4b4d7a9702e853cd5460297802` |
| Resultierender Tree | `cec9ad6a713107d0b4f8c469940632f2eebabec2` |
| Commit-Inventar | Ausschließlich A `docs/gui/G2-I02-final-master-integration.md`, 319 Zeilen |
| SHA-256 der freigegebenen physischen Berichtsfassung | `E1DA075D6D405F81D546A34819EFEE287518EDFCCBB62DCE948BC640CEB62004` |
| Neuer Branch | `feature/gui-g2-03`, Start bei `d7c59ef69b9bbbd477c5c0e5bdc330d25ec1ff71` |
| Separater Worktree | `artifacts/worktrees/gui-g2-03` |

Vor dem Staging waren Index und getrackter Working Tree leer; ausschließlich der erwartete Bericht war untracked. Sein SHA-256 entsprach exakt dem F1-Abschlussinventar `artifacts/g2-i02-f1/final-evidence-sha256.json` im Hauptcheckout. Der gestagte Blob wurde mit `git hash-object --path` einschließlich Git-Zeilenendennormalisierung abgeglichen. Parent, Inventar und Tree wurden nach dem Commit gelesen; master war sauber. Der neue Branch und Zielpfad existierten vorher nicht. Kein Push oder Merge, keine Änderung fremder Worktrees.

Dieses Konzept bleibt im neuen Worktree uncommitted. Produktdateien, Tests und Paketversionen wurden nicht verändert. Kein Build und kein neuer Regressionstest: Dokumentation und Quellanalyse benötigen hier keinen erneuten Produktlauf. Die G2-I02-Ergebnisse sind historische Nachweise, keine Theme-Abnahme.

## 2. Bestandsaufnahme am Ausgangscommit

Verifizierte Referenzen aus `CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj`: Avalonia/Fluent/Inter **12.1.4**, DataGrid **12.1.2**, AvaloniaEdit **12.0.0**, Dock.Avalonia/Model/Serializer/Fluent **12.1.0.13**, Markdown.Avalonia **12.0.2**, MessageBox.Avalonia **12.0.0**, DialogHost **0.12.3**, UVtools **5.2.0**. Diese Versionen bleiben die Entwicklungsbasis; kein Upgrade für das Theme vorgesehen.

Alle folgenden Quellpfade beginnen unter `CryptoWorkBenchAvalonia/`, soweit nicht anders angegeben. Sie sind gegen den neuen Worktree gelesen, nicht aus einem Designmockup abgeleitet.

| Bereich / Quelle | Ist-Zustand | Konsequenz |
|---|---|---|
| `App.axaml`, `App.axaml.cs` | `RequestedThemeVariant="Default"`; FluentTheme, UVtools, Icons, AvaloniaEdit-Fluent und DataGrid-Fluent global geladen. Keine Theme-Präferenz oder Theme-Auswahl. | Framework kann Basis liefern; Benutzerpräferenz ergänzen, Initialisierung vor erster Shell-Anzeige. |
| `Shell/DockShellPresentation.cs` | Fügt DockFluentTheme und DockShellStyles auf Application-Ebene hinzu; entfernt beides bei Dispose/Fallback. Kontextmenüs werden hier erzeugt. | Gemeinsame Ressourcen für alle TopLevels; Style-Reihenfolge und Fallback erhalten. Nicht bei jedem Themewechsel neu installieren. |
| `Shell/DockShellStyles.axaml` | Tabs MinHeight 28, FontSize 13, Padding 6,2, ausgewählt SemiBold. Single-Tab bleibt sichtbar. Eigenes ToolControl-Template setzt Tabs nach oben; PART_BorderFill ist DragArea. | Metriken zentralisieren, bestehende Template-Parts, Drop-/Drag-Markierungen und Presenter-Verhalten unverändert lassen. |
| `Shell/DockShellWindow.axaml[.cs]` | Status nutzt bereits dynamische DockSurfacePanelBrush/DockBorderSubtleBrush; Textgröße 12, Padding 8,3, Spacing 12. View-Menü ohne Theme-Untermenü. | Theme-Menü hier ergänzen, Status auf semantische CWB-Ressourcen ausrichten. |
| `Views/CryptoScriptEditView.axaml`, `Controls/WorkbenchTextEditor.cs` | AvaloniaEdit mit StyleKey TextEditor, Zeilennummern, Cascadia Code/Consolas/Menlo/Monospace, Padding 2. Kein angeschlossenes SyntaxHighlighting, keine eigene Colorizer-/TextMate-Installation in der GUI-Quelle gefunden. | Editoroberfläche, Auswahl, Caret, aktuelle Zeile, Suchmarkierung, Zeilennummern und Popups getrennt betrachten. Syntaxfarben sind neu zu konzipieren, keine vorhandenen Syntaxfarben zu migrieren. |
| `Shell/DockEditorSession.cs` | Eine langlebige Editorinstanz; entfernt im Dockbetrieb Schatten/Margin des EditorFrame und blendet EditorStatusFrame aus. | Themewechsel darf weder Editor noch Document/TextArea austauschen; Undo, Auswahl, Caret und Session erhalten. |
| `Shell/DockVariablesView.cs` | Read-only DataGrids und Detailansicht, überwiegend geerbte Farben. Margin/Spacing 3–5, Labelspalte 90, Mindestbreiten 60, MinHeight 100, Parameters MaxHeight 300. | Tabellenkopf, Auswahl/Fokus, Gridlines und Scrollbars prüfen; bestehende Ansicht nur gestalten. Kein G2-04-Inspector. |
| `Views/InfoView.axaml` | MarkdownScrollViewer mit eigener Engine, Margin 10; Zurück-Button transparent, Padding 6,2. Mechanismus-H1: Theme-Schriftgröße ×1,8. | Markdown-Text, Links, Codeblöcke, Tabellen, Auswahl und Überschriften in beiden Themes prüfen. |
| `Shell/DockOutputView.cs` | PrintText Consolas, Quellnavigation Größe 11; kleine Abstände, kein fester Output-Farbwert. | Gemeinsame Code-Schrift und gut lesbare Metadatengröße; Quellnavigation und Ausgabeinhalte unverändert. |
| `Shell/DockDiagnosticsView.cs` | Schwarzer Text; Warnung Rahmen #8A6500 / Fläche #FFF4CC; Fehler #A32121 / #FFE1E1. Rahmen links 4, Padding 8, Margin 4. | Höchste Dark-Theme-Priorität: lokale Brushes durch dynamische semantische Ressourcen ersetzen. Kategoriebezeichnung beibehalten. |
| Menüs, Kontextmenüs, Buttons, Scrollbars | Größtenteils Fluent-Defaults, keine vollständige eigene Farbpalette. | Vorhandene ControlThemes nutzen; Hover, Pressed, Disabled, Focus, Checked und Popup-Scope abnehmen. |
| Dialoge: `Views/PasswordDialog.cs`, `Services/Automation/EditorReplaceConfirmation.cs`, `Shell/DockEditorSession.Files.cs` | Programmatisch erzeugte Avalonia-Fenster; Abstände 10/12/16/20, sonst überwiegend Defaults. Zusätzlich Drittanbieter-Dialogpakete und native Storage-Dialoge. | Eigene Dialoge erben App-Theme; OS-Dateidialoge bleiben systemverwaltet. Passwort-/MCP-Consent-Logik unverändert. |
| `Shell/EditorDockFactory.cs`, `Shell/DockShellLayout.cs` | OwnerMode.None, ShowInTaskbar=true; Full WindowDecorations, keine erweiterte Client-Titelleiste. | Native Titelleisten und unabhängige Fenster erhalten; keine Custom-Chrome-Rückkehr. |
| Legacy/Fallback: `Views/MainWindow.axaml`, `StatusView.axaml`, `KeyMapView.axaml`, `VariableView.axaml` | LightGray-Flächen, DarkGray-Schatten, weißer Acrylic-Tint, schwarze Diagnosetexte; Rahmen 1/2/3/4, Schriften 9/11/12/14/20. VariableView hat #80000000-Schatten. | Shared Ressourcen auch im Fallback verfügbar halten; Legacy-Styling als klar begrenztes Kompatibilitätspaket, keine Umstrukturierung. |
| Gemeinsame Editoransicht im Legacy-Modus | Purple-Statusfläche, weißer Text, DarkGray-Schatten und Margin 5 bzw. 15,3,5,5. Im Dockmodus teilweise deaktiviert. | Nicht irrtümlich als aktuell sichtbare Dock-Dekoration behandeln; verbleibende Legacy-Farben ebenfalls tokenisieren. |

Schriftbasis: Desktop `Program.cs` verwendet `.WithInterFont()`. Icons sind überwiegend Vektorressourcen; deren Foreground und Disabled-Zustände müssen mit dem Text mitgehen. Splitter stammen aus Dock; Breite und Hit-Test-Zonen werden nicht für kosmetische Kompaktheit reduziert.

## 3. Framework-Entscheidung

**Eine globale Avalonia-Theme-Auswahl, keine zweite Theme-Engine.** `System` bildet auf `ThemeVariant.Default` ab, Light/Dark auf die gleichnamigen Varianten. `ActualThemeVariant` dient als effektiver Zustand für Darstellung und gegebenenfalls Syntaxadapter. ThemeDictionaries mit Light/Dark und dynamische Ressourcen tragen die Farben. Die Framework-Vererbung ist der Ausgangspunkt für bestehende und neue Fenster. [Avalonia: Theme variants](https://docs.avaloniaui.net/docs/styling/theme-variants)

Dock bleibt mit seinem FluentTheme installiert. Die offiziellen semantischen Tokens für Flächen, Tabs und Splitter bieten die vorgesehenen Erweiterungspunkte. Bereits lokal verwendet: DockSurfacePanelBrush und DockBorderSubtleBrush. Weitere geplante Zuordnungen sind DockSurfaceEditorBrush, DockTabActiveIndicatorBrush und DockSplitterHoverBrush. Verfügbarkeit und Lookup-Priorität dieser weiteren Schlüssel müssen im installierten Paket 12.1.0.13 vor Implementierung mit einem Ressourcentest bestätigt werden; aktuelle Online-Dokumentation ist kein versionsgenauer Laufzeitnachweis. [Dock: Theme design tokens](https://wieslawsoltes.github.io/Dock/articles/dock-theme-design-tokens.html)

AvaloniaEdit hat bereits ein Fluent-Theme-Include. Die Upstream-Quelle zeigt Ressourcen für Suchpanel, Auswahl und Completion/Overload-Popups. Diese sind geeignete Integrationspunkte, aber die konkrete Ressourcenliste wird gegen das installierte 12.0.0-Paket geprüft. Kein TextMate-Paket allein für Farbwechsel hinzufügen. [AvaloniaEdit: Fluent theme source](https://raw.githubusercontent.com/AvaloniaUI/AvaloniaEdit/master/src/AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml)

Native Titelleisten bleiben OS-/Framework-Dekoration. Die Avalonia-Dokumentation beschreibt automatische Anpassung unter Windows 11 und Einschränkungen unter Windows 10. Native Chrome-Farbgleichheit ist deshalb ein eigener Plattformnachweis, keine Zusage aufgrund von Ressourcen allein. Kein undokumentierter DWM-Workaround und kein Wechsel zu Custom-Chrome in diesem Vorhaben. [Avalonia: Windows appearance](https://docs.avaloniaui.net/troubleshooting/platform-specific-issues/windows)

## 4. Vorgeschlagene Ressourcen- und Style-Architektur

Die folgenden Dateien sind **geplant**, in diesem Auftrag nicht erzeugt:

| Datei/Baustein | Verantwortung |
|---|---|
| `Themes/ThemeResources.axaml` | Ein gemeinsames ResourceDictionary; Light/Dark-ThemeDictionaries importieren, kein wiederholtes Laden bei Wechsel. |
| `Themes/Colors.Light.axaml`, `Colors.Dark.axaml` | Identische CWB-Schlüssel für semantische Farben und Brushes. Keine Layout- oder Verhaltenswerte. |
| `Themes/Metrics.axaml` | Themeunabhängige Typografie, Spacing, Rahmen, kleine Radien. |
| `Themes/Controls.axaml` | Gezielt klassifizierte CWB-Controls; geerbte Fluent-ControlThemes behalten. |
| `Themes/DockResources.axaml` | Wenige bestätigte Dock-Tokens auf CWB-Werte abbilden; keine neuen Dock-Templates. |
| `Themes/EditorResources.axaml` | Editoroberfläche, Auswahl, Caret/Zeile/Gutter, Suchpanel/Completion; Syntaxpalette. |
| `Services/Appearance/ThemePreferenceStore.cs` | Validierte kleine Präferenzdatei, unabhängig vom DockLayoutStore. |
| `Services/Appearance/ThemePreferenceService.cs` | Enum System/Light/Dark, Laden/Speichern und Setzen von Application.RequestedThemeVariant auf dem UI-Thread. Kein eigener Ressourcenresolver. |
| `CryptoWorkBenchGuiTest/Theme*Tests.cs` | Ressourcen-, Persistenz-, Fenstersynchronisierungs- und Session-Invarianten. |

Global: Fluent und bestehende Drittanbieter-Includes bleiben erhalten; CWB-Ressourcen stehen auf Application-Ebene bereit. Dock-spezifische Styles weiterhin nach dem DockFluentTheme laden, damit dessen spätes Hinzufügen in DockShellPresentation nicht lokale Anpassungen verdrängt. Dispose entfernt nur Dock-spezifische Einträge, nicht die globale Präferenz oder gemeinsamen Ressourcen. Die tatsächlich wirksamen Schlüssel werden am Control getestet, nicht nur ihre Existenz im Dictionary.

In XAML `{DynamicResource CwbTextPrimaryBrush}` verwenden. Bei C#-Controls entweder Klassen mit Style-Settern oder DynamicResource-Bindings setzen; alte lokale Brush-Zuweisungen entfernen, da sie Style-Setter übersteuern. Keine einmalig gelesene Brush im Konstruktor cachen. Semantische Klassen wie `diagnostic-warning`/`diagnostic-error` betreffen nur Darstellung, nicht Kategorien oder Ausführungslogik.

## 5. Farb- und Typografiekonzept

**Designvorschlag, noch nicht visuell abgenommen.** Neutraler Rahmen und ruhige Toolflächen; der Editor bleibt die größte, kontrastreich lesbare Arbeitsfläche. Keine neuen Panels, keine Layoutänderung zwischen Light/Dark. Blau nur für Fokus, aktive Tab-Markierung, Links und bewusste Auswahl. Warnungen/Fehler zusätzlich durch Beschriftung und Symbol unterscheiden.

| Semantik (Cwb…-Ressource) | Light | Dark |
|---|---|---|
| SurfaceWorkbench | #F3F4F6 | #181A1F |
| SurfaceEditor | #FFFFFF | #1E2026 |
| SurfacePanel / Header | #F7F8FA / #E9ECF0 | #24272E / #292D35 |
| TextPrimary | #20242B | #E6E9EF |
| TextSecondary | #505968 | #B4BDCB |
| BorderSubtle (dekorativ) | #CCD2DB | #454C58 |
| BorderStrong (Bediengrenze) | #747D8B | #8993A4 |
| Accent / Focus / Link | #005FB8 | #6CB6FF |
| SelectionBackground | #CDE5FF | #264F78 |
| SelectionForeground | #20242B | #FFFFFF |
| WarningForeground / Background | #765600 / #FFF4CC | #F2D675 / #3A321C |
| ErrorForeground / Background | #A32121 / #FFE1E1 | #FFB4B4 / #412527 |
| SuccessForeground | #216E39 | #88D8A0 |

Hover/Pressed/Inaktiv-Zustände aus Fluent übernehmen oder als eigene geprüfte Tokens ergänzen; keine unkontrollierten Opacity-Überlagerungen auf Text. Die subtile Grenze ist kein ausreichender alleiniger Fokusindikator. Akzentfüllungen brauchen einen separat geprüften Vordergrund; Dark-Akzent nicht pauschal mit weißem Text kombinieren.

| Typografie/Metrik | Vorschlag für beide Varianten |
|---|---|
| UI | Inter mit System-Fallback, 13 DIP, Regular; Sekundärstatus mindestens 12 DIP |
| Editor/Print/Codeblock | Cascadia Code, Consolas, Menlo, Monospace; 14 DIP, keine verpflichtenden Ligaturen |
| Tab | 13 DIP, Höhe mindestens 28 DIP, Padding 6,2; aktiver Tab zusätzlich SemiBold und schmale Akzentmarkierung |
| Überschriften | Dialog 18 DIP; Info-Hierarchie lesbar, bestehendes 1,8×-Mechanismus-H1 als Ausgangspunkt |
| Abstände | Grundraster 4/8/12/16 DIP; dichte Tabellen 4 DIP vertikal, Dialoge 16 DIP |
| Rahmen/Radien | 1 DIP regulär, Fokus 2 DIP; Radien 0–4 DIP; keine dekorativen Schatten um Editor oder jeden Dockinhalt |

Die Metriken sind nicht Themevarianten. Auswahlzustände dürfen keine Breiten-/Höhensprünge erzeugen. DIP-Werte sind keine physischen Pixel: 100/125/150/200 % sowie kleine Fenster prüfen; kompakte Tabs dürfen weder Beschriftungen noch Schließen-Schaltflächen abschneiden.

### Syntaxpalette und Grenze zur Sprache

| Tokenrolle | Light | Dark |
|---|---|---|
| Standardtext/Bezeichner | #20242B | #E6E9EF |
| Schlüsselwort | #7030A0 | #C9A0F0 |
| String | #8A3B12 | #E6B18A |
| Zahl/Hex-Literal | #006B63 | #79D7C5 |
| Kommentar | #52663A | #A0BD82 |
| Funktionsname | #075A9C | #89C4FF |

Es gibt derzeit keinen eigenen Highlighter. Zuerst nur Palette und Editoroberfläche vorbereiten. Ein späterer kleiner AvaloniaEdit-Highlighter darf ausschließlich visuelle Tokenbereiche aus der bestehenden Sprachdefinition ableiten; keine neue Grammatik, Parseränderung, Semantik oder Ausführung auf Themewechsel. Lexikalische Mehrzeilen-/Escape-Regeln und Performance müssen vor Freigabe dieses Schritts geklärt werden. Ein gegebenenfalls notwendiger Adapter aktualisiert nur Highlighting-Farben anhand ActualThemeVariant, nicht Document oder UndoStack. Ohne implementierten Highlighter darf eine Abnahme nicht behaupten, Syntaxfarben würden bereits angezeigt.

## 6. Laufzeit, Persistenz und Fenster

1. Präferenz nach XAML-Ressourceninitialisierung und vor Prism-Shell-Erstellung laden. Default bei fehlender Datei: System. Dadurch möglichst kein heller Startframe bei gespeicherter Dark-Präferenz.
2. Unter `View → Theme` drei gegenseitig ausschließende, per Tastatur erreichbare Einträge **System**, **Light**, **Dark**. Häkchen zeigen die gewählte Präferenz, nicht bei System den momentan effektiven Modus. Menüauswahl bleibt auch nach OS-Wechsel System.
3. Änderung auf dem UI-Thread durch Setzen der globalen RequestedThemeVariant. Application/Fenster erhalten keine widersprüchlichen lokalen Overrides. Kein Neuerzeugen von Shell, Dock-Root, Toolviews, Editor oder Menüs beim Wechsel.
4. Bestehende und künftig geöffnete bzw. restaurierte Floating Windows erben App-Ressourcen. Unowned bedeutet nicht ressourcenlos: Das Verhalten muss mit drei gleichzeitig offenen Hosts explizit geprüft werden. Nur bei belegter Vererbungslücke einen kleinen presentation-only Anschluss an den globalen Wert ergänzen; keine zweite Fensterliste mit eigenem Lifecycle und keine Ownership-Änderung.
5. Bereits geöffnete Dialoge, ContextMenus, Completion-/Insight- und Suchpopups gesondert prüfen. Abonnements eines etwaigen Editoradapters sauber lösen; beim Reparenting keine Doppelregistrierung. Themewechsel darf keinen Fokuswechsel, neue Editorregistrierung oder Schließvorgang auslösen.

Vorgeschlagenes Format: `%LOCALAPPDATA%/CryptoWorkBench/appearance-v1.json` (plattformneutral über SpecialFolder.LocalApplicationData), Inhalt `{"schemaVersion":1,"theme":"System"}`. Whitelist der drei Werte, begrenzte Dateigröße; fehlende/ungültige/zu neue Daten führen zu System und einer unaufdringlichen Meldung. Original bei Korruption nicht still überschreiben; erst bei expliziter neuer Benutzerauswahl speichern. Kleine temporäre Datei im selben Verzeichnis und atomarer Ersatz; IO-Fehler lassen die Laufzeitauswahl bestehen, melden aber nicht fälschlich erfolgreiche Persistenz. Letzte erfolgreich gespeicherte Auswahl gewinnt bei mehreren App-Prozessen; keine Dateibeobachtung/Synchronisierung zwischen Prozessen erforderlich.

**Strikt getrennt von `dock-layout-v1.json`:** kein neues Layoutfeld, keine Layoutmigration, kein Theme im Capture. Layout-Reset/Restore ändern die Theme-Präferenz nicht. Themewechsel schreibt keine Layoutdatei und ändert keine Pane-IDs, Containerrollen, Positionen oder WindowStates. Normale spätere Layout-Speicherung beim Beenden bleibt unverändert.

## 7. Zugänglichkeit und technische Risiken

Zielwerte für die spätere Messung: normaler Text mindestens 4,5:1 gegen den tatsächlichen Hintergrund; große Schrift mindestens 3:1; erforderliche Bediengrenzen, Fokus- und Zustandsindikatoren mindestens 3:1. Das sind Design-/Abnahmekriterien, noch keine gemessene Konformität. Auch Syntaxtext, Links, Selection, Warnungen und Fehlermeldungen messen. Status nie nur über Farbe transportieren; Tastaturfokus muss auf allen Flächen sichtbar sein. High-Contrast-Systemmodus gesondert untersuchen; Light/Dark allein ist kein High-Contrast-Nachweis.

| Risiko | Gegenmaßnahme / offener Nachweis |
|---|---|
| Späte Dock-Styles überschreiben App-Anpassungen | Wirksame Brush-Werte nach Shell-Erstellung, Themewechsel und Fallback testen; keine invasive Template-Kopie. |
| Editor-Themewechsel triggert Templatewechsel/SearchPanel-Neuinstallation | Bestehenden WorkbenchTextEditor.OnApplyTemplate-Pfad berücksichtigen; Documentidentität, History, Coordinator und Suchbedienung prüfen. |
| Native Titelleiste folgt unter bestimmtem Windows-Build nicht | OS-Version protokollieren, native Chrome erhalten; Einschränkung sichtbar dokumentieren statt Lifecycle umbauen. |
| Markdown/Drittanbieter-Popup cached Farben | Offene und neu erzeugte Instanzen prüfen; gezielte Resource-Overrides, keine globale Framework-Brush-Mutation. |
| Feste lokale Werte gewinnen gegen Styles | C#- und XAML-Zuweisungen gezielt ersetzen; Fundstellenliste aus Abschnitt 2 abarbeiten. |
| 125-%-Skalierung bisher nicht abschließend bestätigt | Eigene Pflichtabnahme; keine Übernahme des 150-%-Nachweises als Ersatz. |
| Bekannter Cross-Window-Mouse-Redock-, Fokus-, Popup-Cleanup- oder Headless-Fehler | Erstversuche/Retry getrennt protokollieren; gegen G2-I02 vergleichen, keine Behebungsbehauptung durch Theme-Arbeit. |
| Frischer Worktree ohne lokale Build-Abhängigkeiten | `.deps/prism` fehlt im neuen Worktree. Vor Implementierungsbuild vorhandenen offiziellen Pin nach `eng/prism`-Anleitung reproduzierbar bereitstellen; keine fremde Arbeitskopie verändern. |

## 8. Umsetzung in überprüfbaren Schritten

1. **Ressourcenvertrag und Basis:** Paket-Schlüssel prüfen; Light/Dark-Palette und gemeinsame Metriken, Ressourcen-/Kontrasttests. Noch keine Verhaltensänderung am Dock.
2. **Präferenz und Auswahl:** kleiner Store/Service, Startup-Anbindung, View-Menü; Fehlerfälle, Neustart und Systemmodus testen.
3. **Shell und Tools:** Dock-Tokens, Status, Diagnostics, Output, Variables, Info, Menüs/Dialoge. Legacy/Fallback-Farben im selben Ressourcenmodell angleichen. Variable Inspector ausdrücklich ausgeschlossen.
4. **Editor und Popups:** Oberfläche, Code-Schriften, Suche, Completion und Insight; Syntax-Adapter als getrennt prüfbarer Unterpunkt mit bestätigtem lexikalischem Umfang. Keine Änderung am Ausführungspfad.
5. **Abnahme:** Debug/Release, native Fenster-/Persistenzmatrix, Screenshots und Kontrastmessungen; Eigentümerreview beider Varianten. Erst danach Implementierungsabschluss, keine automatische Commit-/Push-Freigabe.

## 9. Konkrete Abnahmekriterien für die spätere Implementierung

- Frisches Profil startet in System. Light/Dark-Wechsel des Betriebssystems aktualisiert die App nur bei System-Auswahl. Manuelle Auswahl bleibt gegenüber OS-Wechsel stabil.
- View → Theme hat exakt System/Light/Dark mit genau einem korrekten Häkchen; Maus und Tastatur funktionieren. Alle drei Präferenzen überleben Neustart. Korrupte/unlesbare Datei und Schreibfehler werden kontrolliert behandelt.
- Themewechsel aktualisiert sichtbare Hauptfensterinhalte und gleichzeitig abgedockte CryptoScript-, Variables- und Info-Fenster ohne Neustart. Nachträglich erzeugte und aus Layout restaurierte Hosts haben dieselbe effektive Variante.
- Native Titelleisten, unabhängiges Minimieren/Maximieren, Taskleistenzugriff, Verschieben und Anwendungsschließen bestehen weiterhin; keine verwaisten Fenster. Plattformabhängige Chrome-Farbe separat bewerten.
- Vorher/nachher dieselben Editor-/Document-/Undo-Objekte, Text, Dirty-State, Caret, Auswahl, Scrollposition, Ausführungsergebnisse und MCP-Registrierung. Themewechsel erzeugt keine History- oder Undo-Aktion und startet keine Ausführung.
- Dock-Capture bei unveränderter Geometrie vor/nach Wechsel semantisch identisch; Themeaktion schreibt kein Layout. Reset und Wiederherstellung verändern die Präferenz nicht. Redock-Baseline nicht verschlechtern.
- Komponentenmatrix: Editor/Gutter/Caret/Selection/Suche, Completion/Insight, Variables-Tabelle/Details, Markdown inkl. Code/Links/Tabellen, Output/Quellnavigation, Diagnostics aller Kategorien, Status, Menüs/ContextMenus, Dialoge, Buttons, Scrollbars, Splitter, Tooltips jeweils in Light und Dark; aktive/inaktive/fokussierte/deaktivierte Zustände einbeziehen.
- Screenshotmatrix mit identischen Testdaten und Geometrien: Light/Dark × docked/floating bei 100/125/150/200 %. Kein Clipping; Tabs und Lesbarkeit konsistent. Kontrastwerte numerisch dokumentieren, keine reine Sichtprüfung als Messung ausgeben.
- Automatisierte Präferenz-/Ressourcen-/Session-Tests sowie relevante bestehende GUI-, MCP- und Core-Suiten in Debug/Release; native Layout-, Fenster-, Neustart- und Shutdown-Probes. Tatsächliche Fallzahlen aus neuen Läufen berichten; historische Zahlen nicht als Ziel-PASS übernehmen. NotExecuted und Erstfehler/Retry bleiben getrennt.
- Kein Diff an Sprache, Variablensemantik, Ausführung, MCP-Verträgen, Multi-Dokument-Modell oder Dock-Lifecycle. Kein G2-04-Variable-Inspector. Keine Theme-Information in Layoutdateien.

Diese Kriterien sind geplant und noch **nicht ausgeführt**. Der vorliegende Status bescheinigt ausschließlich die fertige Architekturvorbereitung für das Designreview.
