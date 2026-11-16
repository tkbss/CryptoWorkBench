# G2-03-03-R1 – Visual Refinements

Datum: 13.11.2026. Status: **READY FOR OWNER VISUAL REVIEW**.

Umsetzung ausschliesslich im Worktree `artifacts/worktrees/gui-g2-03`, Branch `feature/gui-g2-03`. Keine Commits, Pushes oder Merges.

## R1-A: Zeilennummernabstand

`Themes/Controls.axaml` ergänzt für den bestehenden AvaloniaEdit-`LineNumberMargin` einen rechten Abstand von 8 DIP (12 physische Pixel bei 150 %). Der vorhandene Trenner und Code rücken entsprechend nach rechts. Keine Dokumentzeichen, ControlTemplates oder Editor-Eingabelogik geändert. Dokument, Undo-Stack und Quellpositionen bleiben erhalten.

## R1-B: Output-Metadaten

Ursache der grauen Flächen waren die regulären Fluent-Button-Hintergründe der `OutputSource`-Buttons in `Shell/DockOutputView.cs`, keine Textselektion und kein Bestandteil der Print-Daten. Die Buttons erhalten die Klasse `output-source`. Der zentrale Style setzt im Ruhezustand Background auf Transparent, BorderThickness auf 0 und Foreground per DynamicResource auf `CwbTextSecondaryBrush`: Light #505968, Dark #B4BDCB. Funktionale Fluent-Hover-/Pressed-/Fokuszustände bleiben bestehen.

Button, Click-Handler und Script-/Zeilen-/Spaltennavigation bleiben unverändert. Das Ergebnis bleibt ein unveränderter `SelectableTextBlock` mit NoWrap; horizontale und vertikale Auto-Scrollbars bleiben erhalten. Kein Eingriff in Pipeline, Datenstruktur, Theme-Architektur, Docking oder Floating-Verhalten. Core/MCP unverändert, deshalb keine erneute Core-/MCP-Regression.

## Validierung

`CryptoWorkBenchGuiTest/ThemeTests.cs` erweitert den vorhandenen integrierten Theme-Test: echtes Print(42), bestehender LineNumberMargin mit 8 DIP, transparentes Source-Button-Background und korrekte Sekundärfarbe in beiden Varianten, weiterhin selektierbarer Print-Text. Die vorhandenen Prüfungen für Document-/Undo-Identität, Theme-Vererbung und unverändertes Docklayout bleiben aktiv. Vollständige GUI-Suiten enthalten die bestehenden Navigation-, Auswahl-, Scroll- und Editor-Regressionsprüfungen.

Nachweise unter `artifacts/g2-03-03-r1/`:

- Debug-/Release-Builds: `build-Debug-r1.log`, `build-Release-r1.log`, erfolgreich.
- GUI Debug: `gui-Debug-r1.trx`, 1.143 bestanden, keine Fehler/Skips.
- GUI Release: abschliessender Wiederholungslauf `gui-Release-r2.trx` und gleichnamiges Log: 1.143 bestanden, keine Fehler/Skips.
- Erster Release-Lauf: 1.142 bestanden, ein bereits bekannter intermittierender Headless-Startfehler bei Dispatcher/DefaultRenderLoop in `SingleTabRowsSurviveSwitchCloseResetRestoreAndStartup`, vor dem Testkörper. Nicht als behoben behauptet; Nachweis bleibt erhalten.
- Erster nativer Build kollidierte mit gesperrten Release-Testassemblies. Sequenzielle Wiederholung `native-build-r2.log` erfolgreich. Keine Tests deaktiviert.

## Native Darstellung und Vergleich

[Light nach R1](../../artifacts/g2-03-03-r1/native-light-150.jpg) · [Dark nach R1](../../artifacts/g2-03-03-r1/native-dark-150.jpg)

Vergleichsbasis: [Light vor R1](../../artifacts/g2-03-03/native-light-150.jpg) · [Dark vor R1](../../artifacts/g2-03-03/native-dark-150.jpg).

Beide neuen Aufnahmen zeigen dieselbe produktive Shell mit echtem SHA-256-Beispiel, ausgewähltem Digest und drei Print-Ergebnissen. Wechsel Light → Dark über das native Menü ohne Neustart. Gegenüber G2-03-03 ist die Zahl vom Code sichtbar getrennt; die Metadaten erscheinen als dezente Textzeile statt als graues Rechteck. Die eigentliche Ausgabe bleibt kontrastreich. Dies nähert die Darstellung dem ruhigen Output des akzeptierten HTML-Prototyps an, ohne dessen Architektur nachzubauen.

`native-light.json` bestätigt RenderScaling 1.5, Client 1280 × 693,333 DIP und drei erfolgreiche Prints. Gleiche 150-%-Bedingungen wie zuvor; JPEG-Aufnahmen vom Werkzeug auf logische Pixel normalisiert. Keine neue 100-/125-%-Prüfung. Mausmarkierung stammt vom Aufnahmewerkzeug. Native Testfenster wieder geschlossen; isolierte Konfigurationsdateien im Nachweisordner.

Offen bleibt die finale visuelle und funktionale Eigentümerabnahme. Keine Freigabe weiterer Arbeitspakete.
