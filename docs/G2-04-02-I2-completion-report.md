# G2-04-02-I2 – Property Inspector & User Interface

**Übergabe zur Eigentümerprüfung.** Die wiederverwendbare Inspector-Ansicht funktioniert im separaten Windows-Host. Sie verwendet die ausgewählte Variable der übergebenen Variables-VM. Für die I3-Integration sind die Lebensdauer der Ansicht und die fachliche Freigabe der Session getrennt. Die produktive Dock-Registrierung gehört weiterhin zu I3.

## Prüfstand und Herkunft

18. November 2026, `tkbss/CryptoWorkBench`, Worktree `artifacts/worktrees/gui-g2-04`, Branch `feature/gui-g2-04`. HEAD und lokaler master ergeben `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Die uncommittierte akzeptierte I1-Implementierung war beim Einstieg vorhanden. I1-Abschlussbericht, F1/F2-Korrekturen und R3-Nachprüfung liegen im docs-Verzeichnis.

Die normativen Dateien wurden direkt unter `C:/Users/stefa/.codex/visualizations/2026/11/17/01a1e8d5-f426-7721-82a1-11be4407b2f4` gelesen. Alle 14 F2-Paketdateien stimmen per SHA-256 mit den afterSha256-Werten in `G2-04-01-F3/final-evidence.json` überein. Der F3-Korrekturbericht stimmt mit dem dort separat verzeichneten reportSha256 überein. Der R4-Bericht ist als Original vorhanden. Die ins Testprojekt übernommene Golden-Datei ist bytegleich mit der korrigierten Originaldatei (SHA-256 `0A4F17910F92BAF80F1628FB2FBA491BFC9015FCB7AA9ABE71FAE8479FC7DC40`).

## Komponenten und Dateiliste

| Worktree-relative Datei | Aufgabe |
|---|---|
| CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs | Inventar, Slot-Auflösung, explizite Matrix, Property-/Entry-/Methodenpfade, Nachfrageberechnung, seitenweise Beschreibung und stückweise Wertformatierung |
| CryptoWorkBenchAvalonia/Services/Inspector/InspectorCapture.cs | Projector im bestehenden Host-Read ausführen. Resolve prüft Revision und Deklarationsidentität. Typisierte Metadaten bzw. Wertteile verlassen den Host erst nach Seal und finaler UI-Prüfung. |
| CryptoWorkBenchAvalonia/Services/Inspector/InspectorSnapshot.cs | I1-ReadSpec und SnapshotPart um Property-Pfad, Seitenmetadaten und Aufnahmezeit erweitert. Bestehende Range-, Digest-, Lease- und Commit-Prüfungen gelten weiter. |
| CryptoWorkBenchAvalonia/Services/Inspector/InspectorSession.cs | Optionale anfängliche Property-Seite, Transfer-Ticket für detached Werte, Ladezustand und Benachrichtigung bei fachlichem Reset |
| CryptoWorkBenchAvalonia/Services/Inspector/InspectorClipboard.cs | Plattformübergabe. Unter Windows erfolgt der Vergleich direkt gegen den nativen Unicode-Inhalt einschließlich Terminator mit einem begrenzten Arbeitsbuffer. |
| CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs | Navigation, angeforderte Seiten und Wertteile pro Snapshot, Herkunft, Status, Cancel und vollständige Kopie über Snapshot.CopyAsync |
| CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs | Schreibgeschützte Ansicht mit Property/Typ/Wert, Aktionsspalte, Ebenennavigation, Seiten, Berechnung und selektierbarer Vollwertansicht |
| CryptoWorkBenchAvalonia/AssemblyInfo.cs | Zugriff des separaten nativen Prüfhosts auf die internen Shell-Schnittstellen |
| CryptoWorkBenchGuiTest/PropertyInspectorTests.cs | Permanente Produkt-, Golden-, Metadaten-, Bedien- und Lebenszyklusprüfungen |
| CryptoWorkBenchGuiTest/Fixtures/inspector-f3-golden.json, CryptoWorkBenchGuiTest/CryptoWorkBenchGuiTest.csproj | Unabhängige Erwartungen als eingebettete Originalressource |
| eng/inspector/native/InspectorNativeProbe.csproj, eng/inspector/native/Program.cs | Eigenständiger Win32-Prüfhost mit realem Clipboard und optionaler interaktiver Vorschau |
| docs/G2-04-02-I2-completion-report.md | Dieser Bericht |

## Fachlicher Nachweis

| Quelle | Was tatsächlich geprüft wurde |
|---|---|
| Root-Inventar | String 6, Path 5, Val 5, PARAM 7, KEY 14, SECRET 6, TR31H 6. Vergleich mit den öffentlichen Instanz-Membern der tatsächlichen Typen über Modul und Metadatentoken. Geerbtes Statement.Text ist enthalten. Unbekannte Overrides bleiben ohne Ausführung, verdeckte Slots sind qualifiziert und ein reiner Setter ist sichtbar. |
| KEY | Golden aus dem tatsächlichen Parser und CryptoScriptRunner. Statement.Text beginnt mit `KEYdemoKey=`. Das Literal ergibt 256 Bit mit Unknown/Secret, leerer Attributliste, Unspecified-Usage und drei null-KCVs. AES/TDEA-Berechnungen werden explizit ausgelöst und mit den vorhandenen Gettern für HEX, BASE64 und NORMAL_STRING verglichen. Bei langem Base64 mit Whitespace ist der kompakt dekodierte Input begrenzt. |
| PARAM | FromSnapshot mit echten Dictionary-Einträgen und nicht-null HeaderReference. Root enthält die sieben Properties. GetParameters() ist separat navigierbar und kopierbar. Weder PrintOutput noch BuildValue ist ein Projector-Accessor. |
| SECRET | Split und Shamir mit echten SecretSets und OriginalKeyMetadata. Der Root-Value bleibt SECRET. Der Share-Adapter verwendet CopyValueRange. Die Metadaten eines Bytearrays und sein Inhalt sind getrennt zugänglich. SyncRoot verweist auf den Arrayknoten. |
| TR31H | Definition einschließlich Attributes, OptionalBlocks und Formatter-Value. Bei gesetzten globalen Newtonsoft-Settings wird der Formatter zurückgewiesen. Die Prüfung kontrolliert den Aufrufzähler des fremden Delegates und die unabhängige Zugänglichkeit der Definition. |
| BCL / Zugriff | Explizite Familien für Dictionary einschließlich ReadOnly-Varianten, Keys, Values, KeyValuePair, bekannte Comparer, String, Bytearray, List und fachliche ReadOnlyCollections. Getter werden nach exaktem Runtime-Typ und registriertem Namen geprüft. Indexer stehen im Metadateninventar, konkrete Zugriffe erfolgen über die Einträge. Für die ReadOnly-Wrapper beruht die Vertrauensgrenze auf den kopierenden Core-Konstruktoren und den zulässigen Pfaden ab der Declaration. |

**Alle 19 Golden-Vergleiche** verwenden erwartete UTF-16-Zeichenfolgen aus dem eingefrorenen F3-Original. Die Fälle sind einzeln im TRX auffindbar.

| Gruppe | IDs |
|---|---|
| Deklarationen | root-key, root-param, root-split, root-shamir, root-header |
| Adapter und Unterobjekte | param-adapter, key-type, key-size, split-shares, tr31-definition, tr31-value |
| Skalare und Collection-Grenzen | whitespace, empty, null, hex-string, bytes, empty-bytes, ulong-max, real-dictionary-key |

Der Encoder sortiert fachliche Objektnamen und Dictionary-Keys ordinal, hält Arrayreihenfolgen ein und prüft Vorfahrenidentität vor der rekursiven Projektion. Ein mehrfach verwendeter OptionalBlock erscheint zweimal im Wert und ist als Alias navigierbar. Array-Metadaten werden beim Hex-Copy ausgelassen. Das Ausgabeformat enthält keine zusätzliche Inspector-Hülle. Fehlende Berechnung oder fehlgeschlagener Getter bricht die vollständige Parent-Aufnahme ab.

## Bedienung, Herkunft und Ressourcen

Die Session erstellt die erste Seite nach Ende der Auswahlbeobachtung. Native UI- und Integrationstests verwenden eine vorhandene Variables-VM und einen vorhandenen Host. Die Ansicht hält keine lebende Declaration. Ein Read erhält eine private Staging-Instanz und veröffentlicht über den I1-Finalisierungspfad. Während der Ausgabe prüft der Worker den ursprünglichen Handle zwischen Schritten. Die UI erhält höchstens 64 Einträge pro Seite sowie die Property-Metadaten. Die vollständige Copy läuft unabhängig von der sichtbaren Seite.

Kleine bereits detached Skalare können auch nach einer Editoränderung kopiert werden. Der Integrationstest navigiert in einen echten String, kopiert eine Textauswahl per Ctrl+C, invalidiert den Live-Kontext durch einen Editor-Edit und prüft die vollständige historische Property-Kopie einschließlich Whitespace. Variable und StateRevision bleiben gleich. Die UI unterscheidet Root-Herkunft, Revision, Aufnahmezeit, Historie und Fehler. Bei tatsächlichem Reset oder Close wird ausstehende eigene Arbeit vor der OS-Übergabe abgebrochen. Eine bereits erfolgte Systemübergabe lässt sich nicht zurückrufen.

Die Kopie durchläuft die bestehende Reader-Lease und den Commit nach vollständiger Integritätsprüfung. Für die letzte Formatübergabe dient bei bis zu 256 KiB ein MemoryStream, darüber eine exklusive DeleteOnClose-Datei mit freier Diskreserve. Der endgültige String wird auf dem Worker direkt aus dem privaten Staging befüllt. Ein Host-Fehler bei verworfenem Staging erreicht den bestehenden F2-Cleanup-Owner. OS-Clipboard-Fehler werden in der UI angezeigt. Unter Windows wird eingebettetes NUL vor Übergabe abgelehnt. Die internen C#-Werte erhalten auch NUL und isolierte Surrogate.

**I3-Anschluss.** Auf dem UI-Dispatcher einmal `new VariableInspectorViewModel(existingVariableViewModel)` und `new DockVariableInspectorView(inspectorViewModel)` anlegen. Diese Ansicht kann neu eingehängt werden. Ein tatsächliches fachliches Close beendet die VM über Dispose. Für eine interaktive native Vorschau im vorbereiteten Prism-Buildumfeld:

```powershell
. ./eng/prism/Enter-Prism.ps1
dotnet run --project eng/inspector/native/InspectorNativeProbe.csproj -c Release -- --preview
```

Ohne Preview-Argument führt der Prüfhost den automatisierten Windows-Test aus und beschreibt die Systemzwischenablage mit dem synthetischen Großwert.

## Abnahmekriterien A01–A38

| Kriterien | Zuordnung / Evidenzgrenze |
|---|---|
| A01, A02, A03, A04 | Reflection-Inventar und Accessor-Matrix, Golden-Graphen und Negativtests. Die sieben Root-Inventare sind vollständig verglichen. Für die BCL-Familien ergänzen Quellprüfung und konkrete Eintrags-/Indexertests die Stichproben; es gibt keinen separaten Test jedes einzelnen geschlossenen Collection-Slots. |
| A05, A06, A07, A08 | Read-only-Sessiontest, PARAM-Adapter, Headerkonfiguration und Referenzgetter. Berechnete Werte bleiben beim Öffnen und Expandieren ungefragt. Die Berechnung findet im vorhandenen Host statt. |
| A09, A10, A11, A12 | 19 Wortlaut-Goldens, zusätzlicher UTF-16-/Escape-Test, segmentierte Share- und Stringtests aus I1. |
| A13, A14, A15 | Alias- und SyncRoot-Tests, Unknown-Getter, historische Kopie, native Vergleichsprüfung und vorgelagerte NUL-Ablehnung. Für echte fachliche Vorfahrenzyklen gibt es einen Code-Guard. Die bekannten Baseline-Wertschema-Typen liefern keinen konstruierbaren fachlichen Zyklus als End-to-End-Fixture. |
| A16, A17, A18 | Nativer vollständiger 255-Share-Parent, einzelne große Shares und lange Strings aus I1, 13 unveränderte Range-Regressionen. Sichtfenster und Ausgabeumfang sind unabhängig. Keine gesonderte Messreihe für beliebige synthetische Graph-Tiefen. |
| A19, A20, A21, A22 | Spill/Fehler/Cancel/Reader und Cleanup aus I1, neue reale Großkopie inklusive Speicher- und Zeitmessung. Die Messung deckt den verwendeten Rechner und den angegebenen Lastfall ab. Crash-Recovery, Zeit jedes einzelnen Core-Schritts und p95 unter allgemeiner Systemlast sind damit nicht vollständig vermessen. |
| A23, A24, A25, A26, A27, A28, A29, A30, A31 | Bestehende Session-/Host-Admission mit den 38 unveränderten Inspector-Regressionen und der zusätzlichen echten Property-/UI-Session. Auswahlwechsel, Observer, Revision, Assignment, MCP, Host-Drain und Cleanup bleiben am bestehenden Besitzer. |
| A32 | I2 stellt eine langlebige View bereit. Visuelles Detach enthält kein Dispose. Native Dock-Operationen folgen in I3. |
| A33, A34, A35, A36, A37 | Produktives Docking, Admission für Layoutänderungen und Migration sind I3 zugeordnet. |
| A38 | Reale Win32-Ansicht bei schmaler Breite, Umschalten Light/Dark/System, Fokus, native Enter-Navigation und beschriftete Aktionen. Ctrl+C für selektierten Volltext separat headless geprüft. Ein manueller Screenreader-Durchgang sowie Cross-Window-Redock sind offen. |

## Interner Review und verbleibende Grenzen

Im Review wurden der auswahlgebundene Nachweis der Zeilenherkunft, der Transfer kleiner historischer Skalare, Cancel vor Clipboard-Commit, die explizite Referenznavigation und die bounded Windows-Clipboard-Verifikation geprüft. Das Paging begrenzt die Darstellung. Die tatsächliche Laufzeit einer sehr großen Dictionary-Sortierung hängt von Eintragszahl und Schlüssellänge ab. Die kanonische Dictionary-Kopie benötigt die sortierte Schlüsselmenge. Eine feste 2-MiB-Obergrenze für alle Sortierhilfsdaten ist nicht belegt. Bei Speicher-/Plattformfehlern bleibt der Auftrag fehlgeschlagen.

Native Befunde gelten für Windows. Andere Desktop-Backends verwenden Avalonia-Readback. Screenreader-Sprachausgabe, Hochkontrastvarianten außerhalb der drei angeforderten Themes, OS-Clipboard-Manager und die Stabilität gegen externes Ersetzen des Clipboard nach erfolgreichem Handoff wurden nicht abgenommen. Die bekannte intermittierende Avalonia-Headless-Initialisierung bleibt offen.


## Native Windows-Abnahme und Performance

Windows 11 Home 10.0.26200, .NET 10.0.12 x64, Intel Core i7-14700F, 16.949.420.032 Byte physischer Speicher, NVMe-SSD mit NTFS. Je Konfiguration lief ein separater nativer Prozess. Der Messlauf folgt auf Theme- und Navigationstests. Der große Parent wird erstmalig aufgenommen, ein weiterer Clipboard-Aufruf verwendet den bereits aufgenommenen Wert. Die öffentliche Zwischenablage wurde tatsächlich beschrieben.

| Messgröße | Debug | Release |
|---|---:|---:|
| Nutzdaten in 255 Shares | 33.423.360 Byte | 33.423.360 Byte |
| Vollständiger Root-Clipboard-Text | 66.853.345 UTF-16-Einheiten | 66.853.345 UTF-16-Einheiten |
| Aufnahme bis geprüfte Windows-Übergabe | 733 ms | 610 ms |
| Separater warmer SetTextAsync-Aufruf | 12 ms | 11 ms |
| Aktualisierung der View, p95 / Maximum (je 155 Messungen) | 2,313 / 20,700 ms | 1,284 / 16,664 ms |
| Prozess-Working-Set vor Großaufnahme | 199.778.304 Byte | 201.187.328 Byte |
| Peak-Working-Set bis zum nativen Vergleich | 739.602.432 Byte | 741.224.448 Byte |
| Höchste beobachtete Größe benannter Payload-Dateien, 10-ms-Stichproben | 134.230.986 Byte | 134.230.986 Byte |
| Eigene Payload-Dateien nach Drain | 0 | 0 |

Die Messung der View umfasst ihre synchronen Änderungsbenachrichtigungen. Compositor/OS-Rendering läuft separat. Der Prozesspeak umfasst auch Avalonia, die Quellobjekte, Win32/OLE und die vollständige Rücklesung durch das Prüforakel. Der endgültige String allein enthält 133.706.690 Byte Zeichendaten. Der Disk-Sampler misst sichtbare Payload-Pfade. Die exklusive DeleteOnClose-Datei für das Commit-Staging kann zusätzlich bis zur vollständigen Ausgabelänge belegen. Zeit der tatsächlichen Bereitstellung des verzögert gerenderten Clipboardformats und Warm-SetTextAsync sind unterschiedliche Messgrößen.

Der native Prüfer öffnete CF_UNICODETEXT, verglich alle Zeichen und den Terminator und prüfte anschließend X, Länge und jedes einzelne Byte aller 255 Shares gegen die algorithmische Vorgabe. Eine einzelne Share-Vollansicht enthielt 262.148 Zeichen. Das Textfeld rendert Seiten zu höchstens 4.096 UTF-16-Einheiten. Seite zwei wurde gegen das Original verglichen. Enter und Tab liefen über Win32-Tastaturnachrichten. Unter allen drei Theme-Einstellungen war der benannte Property-Button bei 360 Pixel Fensterbreite fokussierbar. Native Kleinproben erhielten CRLF/LF, einen isolierten Surrogatwert, den leeren Wert und die Schreibweise des Hex-Strings. Embedded-NUL ergab die erwartete Fehlermeldung vor der OS-Übergabe.

Ein externer Windows-UI-Automation-Client fand die Buttons „Inspect SecretSet“, „Copy SecretSet“, „Copy full value“, „Open full value“ und „Cancel capture“ im Preview-Fenster. Ein echter UIA-Invoke auf SecretSet machte „Inspect Shares“ zugänglich. Die erste Abfrage unmittelbar beim Fensteraufbau lieferte E_FAIL. Der Wiederholungslauf wartete auf das Hauptfenster und auf die benannten Elemente. Der Test belegt die programmatische Erreichbarkeit, eine manuelle Sprachausgabe-Prüfung steht aus.

[Debug-Messprotokoll](../artifacts/test-results/g2-04-i2/keyboard-native-Debug.log) · [Release-Messprotokoll](../artifacts/test-results/g2-04-i2/delivery-native-Release.log) · [Externer UIA-Nachweis](../artifacts/test-results/g2-04-i2/preview-uia-repeat.log)

## Prüf- und Korrekturhistorie

- Der erste Desktop-Build traf auf den vorgeschriebenen Prism-Buildhook. Nach Laden von Enter-Prism.ps1 wurde ein Namespace-Konflikt bei Avalonia.Controls.Dock korrigiert. Später verursachte ein Debug-Build mit Release-Restore-Artefakten einen fehlenden AttachDeveloperTools-Verweis. Die Endläufe restaurierten je Konfiguration. Ein paralleler früher Native-Release-Build meldete MSB3027/MSB3021 durch eine vom Testhost gehaltene Desktop-DLL. Der erneute Build nach Freigabe gelang.
- Der erste Property-Debug-Lauf hatte 24 Passed und 7 Failed. Bei den sieben Inventaren verglich der neue Test Reflection-Objekte mit abweichendem ReflectedType. Der korrigierte Test vergleicht Modul und Metadatentoken und prüft weiterhin die erwarteten Anzahlen sowie das geerbte Text. Die 19 Golden-Vergleiche bestanden bereits im ersten Lauf.
- Zwei frühe Native-Proben konnten keine private Windows-Station anlegen (Win32 5). Die realen Nachweise stammen von der regulären Windows-Desktop-Station. Der interne Review ergänzte den Vergleich mit dem nativen CF_UNICODETEXT, eine begrenzte Vollwertdarstellung und den Test des Add-/Cleanup-Unterbaus über die erhaltenen I1-Fälle.
- In review-GUI-Debug.trx (1.219 Passed / 1 Failed) und owner-GUI-Debug.trx (1.223 Passed / 1 Failed) trat ShortPrintIsVisibleAndSourceButtonNavigates(911,512) intermittierend beim Avalonia-Headless-Aufbau auf. Der Stack enthält Dispatcher.VerifyAccess / DefaultRenderLoop.Add. Die grünen Wiederholungen belegen keine Behebung dieser Ursache. Zwischenläufe, Fehlermeldungen, erfolgreiche Wiederholungen und die abschließenden Original-TRX liegen getrennt im Nachweisverzeichnis.

## Abschließende Debug-/Release-Matrix

Alle sechs Suiten wurden in beiden Konfigurationen ausgeführt. Die Zahlen beruhen auf den Einzelresultaten der zwölf delivery-TRX. CLI-Skips bilden eine Teilmenge der TRX-NotExecuted. Die letzte Spalte weist die darüber hinausgehenden NotExecuted aus.

| Suite | Debug Passed | Release Passed | Failed je Konfiguration | CLI Skipped | TRX NotExecuted insgesamt | Weitere NotExecuted |
|---|---:|---:|---:|---:|---:|---:|
| Core gesamt | 11.736 | 11.736 | 0 | 26 | 27 | 1 |
| GUI gesamt | 1.224 | 1.224 | 0 | 0 | 0 | 0 |
| MCP gesamt | 176 | 176 | 0 | 0 | 13 | 13 |
| I2-Property/Copy/UI fokussiert | 40 | 40 | 0 | 0 | 0 | 0 |
| I1-Inspector fokussiert | 38 | 38 | 0 | 0 | 0 | 0 |
| I1-Share-Range fokussiert | 13 | 13 | 0 | 0 | 0 | 0 |

Die fokussierten Läufe sind Teilmengen der Gesamtsuiten. Beide Desktop-Builds wurden als Projektreferenz des nativen Hosts erfolgreich gebaut. Die Avalonia-Produkt-DLLs von GUI-Test, Desktop und nativem Prüfhost stimmen je Konfiguration per SHA-256 überein. Eine zusätzliche Probe prüfte am Release-Encoder 12.000 rückwärts eingefügte Dictionary-Keys gegen den vollständigen unabhängig aufgebauten ordinalen Solltext und 16.777.219 UTF-16-Codeeinheiten einschließlich eines isolierten Surrogats.

[Maschinenlesbare Testmatrix und TRX-Hashes](../artifacts/test-results/g2-04-i2/delivery-matrix.json) · [Alle Originalprotokolle und Zwischenläufe](../artifacts/test-results/g2-04-i2/) · [Zusätzliche Großwertprüfung](../artifacts/test-results/g2-04-i2/large-encoder-Release.log)

**Ergebnisstatus: READY FOR OWNER REVIEW.** Die Eigentümerentscheidung steht aus. Der master-Worktree ist sauber und steht auf der vorgegebenen Baseline.
