# G1-08 – Verbleibende native Abnahme

Diese Liste ist ein **ausführbares, noch nicht ausgefülltes manuelles Protokoll**. Ein API-Float/Redock oder ein grüner Headless-Test erfüllt keinen Mausprüfschritt. Datum, Betriebssystem, Architektur, Bildschirm/Skalierung, Tastaturlayout, Quellmanifest und Prüfer zu jedem Lauf erfassen. Nur synthetische Testdaten verwenden.

## Vorbereitung und reproduzierbare Plattformläufe

Voraussetzungen: derselbe vollständige G1-Arbeitsstand einschließlich ungetrackter Dateien, SDK aus `global.json`, PowerShell 7, die bereits gepinnte Prism-Quelle beziehungsweise deren bestehender Bootstrap. Keine Übernahme von L1. Neue Evidence-Verzeichnisse verwenden; vorhandene Belege nicht überschreiben.

```powershell
# Im G1-Checkout: locked restore, Debug/Release und alle drei vollständigen Suiten,
# anschließend die unveränderten G1-01-Headless-Tests.
pwsh -NoProfile -File eng/gui/Validate-G1Platform.ps1 -Configuration Both -EvidenceDirectory artifacts/g1-08/linux-local
pwsh -NoProfile -File eng/gui/Validate-G1Platform.ps1 -Configuration Both -EvidenceDirectory artifacts/g1-08/macos-local
```

Die erste Zeile auf Linux, die zweite auf macOS ausführen, nicht beide auf Windows. `platform.json`, beide `commands.json`, sämtliche TRX und Logs aufbewahren. Fehlgeschlagene Schritte zuerst untersuchen; Wiederholungen in neue Verzeichnisse schreiben. Kein fremder Plattformname im Verzeichnis ist ein Plattformnachweis.

Mit `-Desktop` startet das Skript nach den Tests die echte Anwendung mit `--dock-shell`. Auf Linux ist eine echte X11-/Wayland-Desktopsitzung erforderlich; Xvfb allein ersetzt keine Fokus-/Clipboard-Abnahme. macOS benötigt einen angemeldeten Desktopbenutzer; für RID-Publish außerdem Xcode Command Line Tools. Das vorhandene `.github/scripts/Invoke-PrismCi.ps1 -Stage Publish -RuntimeIdentifier osx-arm64` beziehungsweise `osx-x64` prüft den bestehenden Publishpfad auf dem jeweiligen Mac. Linux verwendet `linux-x64`. Publish muss im autorisierten Checkout erfolgen; dieses Paket hat keine CI ausgelöst.

Die Desktop-Anwendung speichert ihr Layout im lokalen Anwendungsdatenverzeichnis unter `CryptoWorkBench/dock-layout-v1.json`. Vor einer manuellen Abnahme eine gegebenenfalls vorhandene eigene Datei sichern. Die automatisierten Windows-Probes nutzen dagegen ausschließlich ihre jeweilige Evidence-Datei plus `.layout.json`.

**Bekannte Plattformgrenze:** Die produktive MCP-Lease-Uhr verwendet `GetTickCount64` nur auf Windows und verweigert andere Plattformen sicher. Grüne Tests mit injizierter Uhr zertifizieren keine produktiven Linux-/macOS-Leases. Dafür ist eine separat nachgewiesene suspendzählende Plattformuhr nötig; G1-08 ersetzt sie nicht.

## Windows: Maus über Fenstergrenzen

Start: `dotnet CryptoWorkBenchAvalonia.Desktop/bin/Release/net10.0/CryptoWorkBenchAvalonia.Desktop.dll --dock-shell`. `View > Reset to Default Layout`. Text `VAR mouseProbe=0x(11) Print(0x(22))` eingeben und Return drücken. Editor, Variables und Output beobachten.

| Schritt | Genaue Bedienung | Erwartung | Ergebnis/Beleg |
|---|---|---|---|
| M1 | Variables-Titel mit gedrückter linker Maustaste aus dem Hauptfenster ziehen und auf freier Desktopfläche loslassen. | Genau ein erreichbarer nativer Host, dieselben Variablen. | OFFEN |
| M2 | Den Variables-Tab aus dem Floating Host in die Mitte der linken Hauptfenster-Drop-Zone ziehen. | Sichtbare Zielanzeige; Variable wieder gedockt; leerer Host verschwindet. | OFFEN |
| M3 | Info und Output über `View > … > Float` öffnen; Info-Tab per Maus in Output-Host ziehen. | Beide Tools im Zielhost, keine doppelten Views oder leeren Restfenster. | OFFEN |
| M4 | Einen Tab ziehen, über einer ungültigen Fläche Escape drücken, dann Maustaste loslassen. | Gültige ursprüngliche Anordnung; keine hängende Drag-Anzeige. | OFFEN |
| M5 | CryptoScript-Tab herausziehen, `ä€😀` in einen Kommentar schreiben, Undo/Redo; per Maus ins Hauptfenster zurückziehen. | Identisches Dokument und History, funktionierende Eingabe/Fokus. | OFFEN |
| M6 | Mit autorisiertem MCP-Client eine Editor-Lease erwerben; M1/M2/M5 versuchen; danach Lease freigeben. | Während Lease keine Relokation/GUI-Mutation; nach Freigabe dieselbe Session wieder bedienbar. | OFFEN |
| M7 | Während einer kontrolliert ausstehenden Ausführung/Ticket dieselben Drag-Versuche durchführen. | Keine Verlagerung, keine zusätzliche Interpreteroperation; nach Drain wieder bedienbar. | OFFEN |
| M8 | Nach den Bewegungen einen Bereich maximieren, wiederherstellen, MainWindow mit zwei offenen Hosts schließen und neu starten. | Normale Anordnung/geschlossene Tools wiederhergestellt; kein fachlicher Zustand geladen; kein Zombie-Host. | OFFEN |

Die verfügbare Computer-Use-API bietet eine vollständige `drag`-Geste nur zwischen Koordinaten **eines** Zielfensters. Sie bietet hier kein getrenntes MouseDown/Move/MouseUp über mehrere native Fenster. Deshalb bleiben M1–M8 offen; die programmgesteuerten Dock-Tests werden nicht dafür umbenannt. Drop-Adorner im laufenden Drag visuell fotografieren, nicht aus optionalen Service-Overlay-Logs ableiten.

## Gemeinsamer Desktop-Ablauf pro Plattform

1. Neues opt-in Dock-Fenster: ein CryptoScript-Editor, Variables, Info, Output, Diagnostics, Status. Legacy bleibt Standard ohne Schalter.
2. `VAR acceptance=0x(AB) Print(0x(CD)) Info(HASH-SHA256)` eingeben; Return und separat KP_Enter testen. Genau eine Ausführung/History pro Enter, passende Variablen und Print-Ausgabe.
3. Schweizer Tastatur separat protokollieren: `ä`, `ö`, `ü`, physisches AltGr+E (`€`), Shift+8 (`(`), Unicode `😀`. Literaltexteingabe und physische Tastenkombinationen unterscheiden.
4. Info-Prosa selektieren/Ctrl+C; sichtbaren Codeblock über Copy Example kopieren. Variables dürfen nicht editierbar sein; Output bleibt read-only, selektierbar und kopierbar.
5. `  @` ausführen; Syntaxdiagnostic und Quellposition prüfen. Editor ändern und alte Quelle anklicken: sichtbarer Stale-Hinweis, kein Sprung. `KEY missing=Check(0x(AA)) VAR continued=0x(01)` ausführen: semantischer Fehler, Fortsetzung unverändert.
6. Diagnostics auf etwa 220 Pixel Breite verkleinern. Alle Filter müssen erreichbar umbrechen, Meldung und Quellaktion lesbar bleiben.
7. Undo/Redo, Alt+PageUp/PageDown und Completion im Hauptfenster sowie Floating-Editor prüfen.
8. Über native Dialoge als neue `.sbk` speichern; anderen Text eingeben, erneut öffnen und Ersatz bestätigen. Dateitext exakt, keine automatische Ausführung, Undo verfügbar. Abbrechen in beiden Dialogen darf nichts verändern.
9. Tools schließen, ausführen, wieder zeigen: globale Ergebnisse aktualisiert, keine zweiten Zustände. Maximize/Restore und Reset über Haupt- und Floating-Menüs bedienen.
10. Layout speichern durch kontrolliertes Schließen; neu starten: Geometrie/Tools wiederhergestellt, Editor/Variablen/MCP-Daten nicht aus dem Layout geladen. Offscreen-Anordnung muss erreichbar begrenzt werden.
11. MCP-Transport verbinden, Lease, Editoroperation, GUI-/Dock-Sperre, Release, GUI-Fortsetzung und Shutdown mit offenen Hosts prüfen. Auf Linux/macOS zunächst die oben benannte Uhrgrenze beheben und separat zertifizieren.
12. Mit zwei offenen Hosts MainWindow schließen: alle Hosts, Editorregistrierung und Transport enden. Prozessliste und Logs sichern.

Für jeden Schritt PASS/FAIL/NOT RUN mit beobachtetem Ergebnis ergänzen. Fensterstart oder Exitcode 0 allein ist keine vollständige native Freigabe.
