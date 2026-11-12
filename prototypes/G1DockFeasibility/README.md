# G1-01: isolierter Dock-Prototyp

Aus dem **G1-Worktree** mit PowerShell 7 und dem durch `global.json` festgelegten SDK:

```powershell
pwsh -NoProfile ./prototypes/G1DockFeasibility/Validate.ps1 -Native -IncludeRegression -EvidenceDirectory artifacts/g1/new-run
```

Der Wrapper verwendet die unveränderte Prism-Quellvorbereitung, prüft Debug und Release,
führt die Headless-Tests und optional echte Desktop-Tests sowie die gesamten bestehenden
GUI-/MCP-/Core-Suites aus. `-Native` braucht einen entsperrten Desktop; dabei erhalten die
Testfenster wiederholt den Fokus. Falls Windows einen Hintergrundstart nicht aktiviert,
das Hauptfenster innerhalb von 30 Sekunden aktivieren; andernfalls werden die Foreground-Checks
ehrlich als fehlgeschlagen protokolliert. Während des Laufs keine andere Anwendung aktivieren.
Ohne Desktop den Schalter weglassen und native Tests
als nicht ausgeführt dokumentieren. `-Configuration Debug` oder `Release` begrenzt den Lauf.
Für jeden Lauf ein neues Evidence-Verzeichnis angeben. Native Ergebnisse stehen in JSON,
Testresultate in TRX, Prozess-Exitcodes und exakte Argumente in `commands.json`.

Interaktiv starten:

```powershell
. ./eng/prism/Enter-Prism.ps1
try {
    dotnet restore ./prototypes/G1DockFeasibility/G1DockFeasibility.csproj --locked-mode
    dotnet run --project ./prototypes/G1DockFeasibility/G1DockFeasibility.csproj --no-restore
} finally {
    . ./eng/prism/Leave-Prism.ps1
}
```

Variables-/Diagnostics-Kopf bzw. Dokument-Tab ziehen. Die Testbuttons erlauben deterministisches
Float/Redock, Verschieben der Variables in Diagnostics sowie einen Layout-Roundtrip im Speicher.
`Reset` stellt das Startlayout wieder her (auch nach explizitem Schließen eines Tools).
Der native Test speichert zusätzlich eine JSON-Layoutdatei und liest sie wieder ein.
Fehler bei einer interaktiven Testaktion erscheinen im Fenstertitel.

Die Anwendung bootet über das lokale `PrismApplication`. Eine normale ContentControl-Region
hostet genau einen DockControl; die inneren Dockables sind keine Prism-Regions. Dock.Model.Mvvm
bildet das Layout, Prism verwaltet den Prototyp-Workspace und die ViewModels. Ein DocumentSession
hält TextDocument und Caret außerhalb des visuellen Editors. Ein neuer visueller Editor ist kein
neues logisches Dokument. Für spätere mehrere Dokumente wird die Session nach Dokument-ID
aufgelöst, statt diese Demo-Singletonregistrierung zu übernehmen.

Keine Verbindung zum Interpreter, MCP, EditorCoordinator oder Produkt-MainWindow. Keine
Produktprojekt- oder Prism-Änderungen. Die Projekt-Lockfiles ergänzen ausschließlich den Prototyp;
die bestehende globale/Repository-NuGet-Konfiguration bleibt unverändert.

Ergebnisse, Integrationsrisiken und Entscheidung: [G1-01-Bericht](../../docs/gui/G1-01-dock-feasibility.md).
