# G2-03-02 – Light/Dark Visual Design Review

Datum: 12.11.2026
Branch: feature/gui-g2-03
Status: **READY FOR OWNER VISUAL REVIEW**

## Öffnen und vergleichen

[Eigenständiger HTML-Prototyp](../../artifacts/g2-03-02/prototype.html) – unter Windows per Doppelklick in Chrome oder Edge öffnen; keine Installation, kein Build, keine Netzwerkressourcen. Das Menü **View → Theme → System / Light / Dark** ist beim Öffnen aufgeklappt. Häkchen kennzeichnen die Präferenz; System folgt der Browser-Systemfarbpräferenz. Die Auswahl ist nur lokal im aktuellen Dokument und wird nicht gespeichert. Escape schliesst das Menü; View öffnet es erneut. Für direkte Varianten kann die URL um ?theme=light bzw. ?theme=dark ergänzt werden.

[Light, 1920 × 1080](../../artifacts/g2-03-02/light.png) · [Dark, 1920 × 1080](../../artifacts/g2-03-02/dark.png)

Die beiden Screenshots zeigen denselben Standardzustand mit geschlossenem Menü, damit der gesamte Beispielcode sichtbar bleibt. Variables und Output sind aktiv; Info und Diagnostics sind inaktive, anklickbare Tabs. Info zeigt eine redaktionelle Hash-Funktionshilfe. Diagnostics zeigt ausdrücklich einen separaten Warnungs-/Fehler-Zustandskatalog, keine angeblichen Fehler des erfolgreichen Beispiels. Alle fünf Inhalte sind im HTML vorhanden, ohne zusätzliche Dockflächen zu erfinden.

## Grundlage und Abgrenzung

Grundlage: [G2-03-01](G2-03-01-theme-architecture.md), Abschnitt 5, und das abgenommene G2-02-Standardlayout. Der vorhandene Worktree artifacts/worktrees/gui-g2-03 wird verwendet; der Hauptcheckout bleibt auf master. Der bereits vorhandene Architekturbericht wurde nicht geändert.

Dies ist ein HTML-Designentwurf, kein Screenshot der produktiven Avalonia-App. Syntax-Highlighting ist explizit als **Konzept, noch nicht implementiert** beschriftet. Farbige Spans zeigen die vorgesehene Palette; kein Lexer/Highlighter wurde implementiert. Auch Themeauswahl, Systemreaktion und Tabwechsel sind nur Vorschauverhalten. Native Titelleiste und Taskleiste sind schematisch; keine Zusage über Windows-Chrome oder DPI-Verhalten der App.

Das Beispiel nutzt die vorhandenen Formen VAR, 0x(...), Hash(HASH-SHA256, message) und Print(...). Es zeigt SHA-256 der drei Bytes 61 62 63 (abc). Der Digest ist BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD. Print-Ausgaben sind vorbereitete Nutzdaten, kein neuer Laufzeitnachweis oder Anspruch auf exakte Serialisierung aller produktiven Output-Metadaten. Keine echten Schlüssel oder vertraulichen Daten.

Keine Änderung an produktiven Avalonia-Dateien, Docking, CryptoScript oder MCP; keine Theme-Implementierung, kein Commit und kein Push.

## Geometrie, Typografie und Abstände

Desktopmodell: 1920 × 1080 CSS-Pixel bei deviceScaleFactor 1 (100 %). Davon 40 px schematische Taskleiste, 32 px Titelleiste, 32 px Menü und 26 px zentrale Statusleiste. Dockfläche y=64 bis 1014. Nach Abzug des 5-px-Trenners teilen sich oben/unten die Höhe 75/25: 708,75 / 236,25 px. Oben nach Abzug des 5-px-Trenners 70/30: Editor 1340,5 px, Variables/Info 574,5 px. Trennleisten sind rein statische Darstellungen; produktive Splitter-Hitflächen bleiben unberührt.

Beide Varianten haben identische DOM-Struktur, Inhalte, Zeilenumbrüche und gemessene Panel-/Tab-/Statusrechtecke. Es wechseln ausschliesslich Farbvariablen und die Variantenbeschriftung.

- UI: Inter, Segoe UI, sans-serif; 13 px, Regular. Inter wird nicht aus dem Netz geladen; ohne installierte Inter verwendet Windows Segoe UI.
- Code/Print/Info-Code: Cascadia Code, Consolas, Menlo, monospace; 14 px, keine Ligaturen. Editor-Zeilenhöhe 24 px, Print 22 px.
- Tabs: 13 px, 30 px Leiste, mindestens 28 px Höhe; aktive Tabs 600 und 2-px-Akzent. Vertikales Padding 2 px; horizontales Padding für den Entwurf 12 px statt der vorgeschlagenen kompakten 6 px.
- Status/Metadaten/Quellen: 12 px; Info-H1 23,4 px (= 1,8 × 13).
- Flächenabstände 8/12/16 px; Tabellenzellen 6 px vertikal, 8 px horizontal (bewusste Abweichung von 4 px für besser lesbare Zeilen). Details mit 90-px-Labelspalte, kein neuer Inspector.
- Rahmen 1 px; Fokus/Auswahlkontur 2 px; Radien 0–3 px. Kein Schatten um Editor/Panels; nur Menüpopup besitzt einen dezenten Schatten.

## Farbwerte

Alle Hexwerte aus dem Architekturvorschlag bleiben unverändert. Dark verwendet hellblauen Akzent und hellere Syntaxfarben auf dunklen Flächen, Light dunkle Akzent-/Syntaxfarben auf hellen Flächen. Die selektierte Variable verwendet je Variante den vorgesehenen SelectionForeground.

| Token | Light | Dark |
|---|---|---|
| work | #F3F4F6 | #181A1F |
| editor | #FFFFFF | #1E2026 |
| panel | #F7F8FA | #24272E |
| header | #E9ECF0 | #292D35 |
| text | #20242B | #E6E9EF |
| muted | #505968 | #B4BDCB |
| border | #CCD2DB | #454C58 |
| strong | #747D8B | #8993A4 |
| accent | #005FB8 | #6CB6FF |
| selection | #CDE5FF | #264F78 |
| selected | #20242B | #FFFFFF |
| warn | #765600 | #F2D675 |
| warnbg | #FFF4CC | #3A321C |
| error | #A32121 | #FFB4B4 |
| errorbg | #FFE1E1 | #412527 |
| success | #216E39 | #88D8A0 |
| keyword | #7030A0 | #C9A0F0 |
| string | #8A3B12 | #E6B18A |
| number | #006B63 | #79D7C5 |
| comment | #52663A | #A0BD82 |
| function | #075A9C | #89C4FF |

## Kontrastprüfung und Korrekturen

Berechnung direkt aus den im Browser wirksamen CSS-Farbwerten: sRGB-Kanäle auf 0–1 normalisieren, bis 0,04045 durch 12,92 teilen, sonst ((c+0,055)/1,055)^2,4. Relative Luminanz = 0,2126 R + 0,7152 G + 0,0722 B. Verhältnis = (Lhell+0,05)/(Ldunkel+0,05). Ziele: normaler Text 4,5:1, erforderliche Zustands-/Bedienkonturen 3:1. Bewertung vor Rundung. Dies ist eine Farbprüfung, keine vollständige Accessibility-Zertifizierung.

Alle 64 geprüften Text-/Indikatorpaare erreichen das jeweilige Ziel. Vollständige Einzelwerte und Geometrie: [validation.json](../../artifacts/g2-03-02/validation.json).

| Text / Hintergrund | Light | Dark | Ziel |
|---|---:|---:|---:|
| text / panel | 14.65:1 | 12.29:1 | 4.5:1 |
| muted / header | 5.97:1 | 7.29:1 | 4.5:1 |
| selected / selection | 12.05:1 | 8.50:1 | 4.5:1 |
| warn / warnbg | 6.15:1 | 8.86:1 | 4.5:1 |
| error / errorbg | 6.12:1 | 8.19:1 | 4.5:1 |
| success / panel | 5.89:1 | 8.81:1 | 4.5:1 |
| keyword / editor | 8.02:1 | 7.57:1 | 4.5:1 |
| string / editor | 7.73:1 | 8.54:1 | 4.5:1 |
| number / editor | 6.40:1 | 9.57:1 | 4.5:1 |
| comment / editor | 6.32:1 | 7.83:1 | 4.5:1 |
| function / editor | 7.11:1 | 8.84:1 | 4.5:1 |
| accent / selection | 4.88:1 | 3.95:1 | 3:1 |
| strong / header | 3.51:1 | 4.45:1 | 3:1 |

Notwendige gestalterische Absicherungen: BorderSubtle bleibt rein dekorativ, nicht als alleiniger Fokus-/Bedienindikator. Die ausgewählte Tabellenzeile besitzt deshalb zusätzlich eine 2-px-Akzentkontur; aktive Tabs zusätzlich Akzentstrich und SemiBold. Menükontur und Buttonkonturen verwenden BorderStrong. Es gibt keine Akzentfüllung mit pauschal weisser Schrift: Hover nutzt SelectionBackground/Foreground, Text bleibt voll deckend. Warnung und Fehler haben Beschriftung und unterschiedliche Symbole. Keine Hexwertkorrektur der vorgeschlagenen Palette war für die verwendeten Paare nötig. Kommentarfarbe wurde als reservierte Syntaxrolle mitgemessen; das kurze Beispiel enthält keinen Kommentar.

## Verifikation und Grenzen

- Headless Chrome unter Windows, 1920 × 1080, zwei PNGs; beide Bilder visuell auf Lesbarkeit, abgeschnittene Inhalte und gleiche Aufteilung geprüft.
- Browsermessung: identische Panel-/Tab-/Statusgeometrie; kein Dokumentoverflow; keine JavaScript-Seitenfehler.
- Info und Diagnostics in beiden Varianten durch Tabwechsel sichtbar; Systemmodus folgt emuliertem Dark/Light. Ein erster Prüfversuch las den Zustand vor dem asynchronen Media-Event; die Prüfung wartet nun auf den erwarteten Zustand, der finale Lauf besteht.
- Reproduktion der Screenshots und Messung über verify.cjs (benötigt das dort angegebene lokale Playwright und Chrome; **nur die Nachweiserzeugung**, nicht das Öffnen des Prototyps).
- Keine Produkt-Builds oder Regressionstests: reine Artefakt-/Dokumentationsänderung. Native Avalonia-Fenster, 125/150/200 %, High Contrast, Screenreader und echte Ausführung sind nicht durch diesen Entwurf abgenommen. Menütastaturbedienung nutzt HTML-Buttons/Tab/Enter, keine vollständige native Menüsimulation.

Zur Eigentümerprüfung: Lesbarkeit beider Paletten, Dichte der Tabs/Tabellen, Akzent der ausgewählten Variable, Info-/Diagnostics-Zustände sowie die konzeptionellen Syntaxfarben. Die endgültige visuelle Freigabe steht aus.

**READY FOR OWNER VISUAL REVIEW**
