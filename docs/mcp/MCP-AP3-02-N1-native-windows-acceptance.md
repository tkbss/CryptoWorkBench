# MCP AP3-02-N1 – Native Windows History Acceptance

Datum: 2026-10-29. **Schlussentscheidung: FAIL für das native Abnahme-Gate.**

Die reguläre Enter-Eingabe erzeugte in der frischen Desktopinstanz wiederholt weder einen sichtbaren Zeilenumbruch noch ein Ausführungsergebnis. Deshalb konnte keine nachweisbar befüllte LineHistory über den vorgeschriebenen GUI-Eingang hergestellt werden. Die davon abhängigen Historyfälle bleiben OPEN. Das ist keine Widerlegung der im F2-Nachreview geschlossenen Codefindings und kein Nachweis einer Ursache in F2.

## 1. Desktop-Build und Windows-Umgebung

- Worktree: `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp-ap3`.
- Branch: `feature/mcp-ap3`; HEAD vor und nach Prüfung: `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`.
- Windows: `Microsoft Windows NT 10.0.26200.0`.
- Aktueller Debug-Build über `pwsh -NoProfile -File eng/prism/Build.ps1 -Configuration Debug`: beide Lösungen erfolgreich, jeweils 0 Warnungen und 0 Fehler. Protokoll: [build.log](../../artifacts/ap3-02-n1/build.log).
- Avalonia 12.1.4, AvaloniaEdit 12.0.0, DataGrid 12.1.2; unveränderte Prism-Quellreferenzen. Build bestätigt offiziellen Prism-Stand `358118cd640d9a22ff8cf21c8ad197fa038b7990` mit 1180 gepinnten Dateien.
- Gestartet wurde ausschließlich `CryptoWorkBenchAvalonia.Desktop\bin\Debug\net10.0\CryptoWorkBenchAvalonia.Desktop.exe` aus diesem Worktree. Vorher war kein entsprechendes CWB-Testfenster vorhanden. Frischer leerer Editor, keine geöffneten ScriptBooks, keine Benutzerschlüssel; Testinstanz abschließend über ihren Close-Button geschlossen und Abwesenheit in der Fensterliste bestätigt.

SHA-256 des getesteten Builds:

| Datei im Desktop-Ausgabeverzeichnis | SHA-256 |
| --- | --- |
| CryptoWorkBenchAvalonia.Desktop.exe | `4545CD9AF77F565C72E582B8EA0C4B34A4A474F26649A68FF7181C3FBF2C121F` |
| CryptoWorkBenchAvalonia.dll | `F41CD6742E63C2DFBC026C819107738D371FFDE12293AB44C467866631442F9D` |

## 2. Tatsächliche Eingabemethode und Bindungen

Native Windows-OS-Automation mit Computer-Use/`@oai/sky`: Mausklicks, `type_text` für literalen Text und `press_key` für Return, KP_Enter, End, Ctrl+A/Z/Y sowie Alt+PageUp/PageDown. Es wurden keine Historyservices, Commands oder ViewModels direkt aufgerufen und keine Headless-Instanz als Ersatz verwendet.

Editorfokus wurde über UI-Automation (`26 custom`), sichtbaren Caret und Statuszeile geprüft. Die Variablenansicht wurde über den sichtbaren Sidebarbutton geöffnet; ihr Editierfeld meldete nach gesonderter Aktualisierung Fokus (`51 edit`). Anschließend erfolgte der Rückklick in den Editor mit erneut bestätigtem Fokus. Zustandsabfragen direkt nach Aktionen konnten verzögert sein; relevante Endzustände wurden nochmals beobachtet. Bei verdecktem Fenster lieferte die Aufnahme teilweise Hintergrundinhalt; das Testfenster wurde aktiviert und erneut aufgenommen. Solche Hintergrundaufnahmen dienen nicht als Editorbeleg.

`Views/MainWindow.axaml` registriert Alt+PageUp/PageDown für LineHistory und Ctrl+PageUp/PageDown für InfoHistory. In der untersuchten Oberfläche und ihren Bindungen wurden keine Historybuttons gefunden. Der sichtbare Hilfetext im Fußbereich vertauscht diese Zuordnung; für die Prüfung wurden die tatsächlichen AXAML-Bindungen verwendet.

## 3. Durchlauf und Ergebnisse N1–N4

Die folgende Reihenfolge beschreibt tatsächliche GUI-Aktionen. Die drei Texte wurden nacheinander in derselben ersten Zeile eingegeben, weil Enter den erwarteten Abschluss nicht bewirkte; **drei erfolgreich ausgeführte Historyeinträge werden nicht behauptet**.

| Schritt / Gruppe | Eingabe und vorheriger Zustand | Sichtbarer Zustand danach | Bewertung |
| --- | --- | --- | --- |
| N1 Aufbau A | Leerer fokussierter Editor; `Print(0x(11))` eingeben | Text in Zeile 1, Caret am Ende | Texteingabe PASS |
| N1 Ausführen A | Return; anschließend End und erneutes Return bei Editorfokus | Unverändert `Print(0x(11))`, Zeile 1; kein sichtbares Print-Ergebnis und kein Umbruch | FAIL: regulärer Ausführungseingang |
| Kontrollprobe | Ctrl+Z auf eingegebenem A | Editor leer, Zeile 1 / Spalte 1 | Allgemeines Undo PASS |
| Kontrollprobe | Ctrl+Y auf leerem Editor | `Print(0x(11))` wiederhergestellt | Allgemeines Redo PASS |
| N1 Aufbau B | Ctrl+A, `Print(0x(22))`, Return | Text B in Zeile 1; Enter ohne sichtbare Änderung | FAIL erneut reproduziert |
| N1 Rückwärts | Alt+PageUp auf B | B unverändert | Keine wirksame Historynavigation nachgewiesen; OPEN |
| N4 Aufbau C | Ctrl+A, `Print("äöü € é")`, Return | Unicode korrekt sichtbar in Zeile 1, Spalte 17; kein sichtbares Ausführungsergebnis | Unicodeeingabe PASS; Ausführung blockiert |
| N4 allgemeines Undo | Ctrl+Z auf C | B wiederhergestellt und selektiert | PASS für manuelle Textersetzung |
| N4 allgemeines Redo | Ctrl+Y auf B | C wiederhergestellt und selektiert | PASS für manuelle Textersetzung |
| N3 Fokuswechsel | Variablenansicht öffnen, deren Editierfeld anklicken | Leere Variablenansicht; Fokus 51 bestätigt | Fokuswechsel PASS |
| N3 Refokus | Editor anklicken | C unverändert, Caret am Ende, Fokus 26 | Refokus PASS |
| N3 Ausführung nach Refokus | KP_Enter auf C | C unverändert, Zeile 1 | Enter-Blocker besteht auch nach Refokus |
| N3 Vorwärts nach Undo/Redo und Refokus | Alt+PageDown auf C | C unverändert | Historynachweis weiterhin OPEN |

**N1 insgesamt:** FAIL beim notwendigen Aufbau über den regulären GUI-Eingang. Mehrfachnavigation, korrekte Zielreihenfolge, Anfang/Ende, genau ein Schritt pro Geste und Ausschluss erneuter Ausführung bleiben OPEN. Aus unverändertem Text ohne nachgewiesene Historybefüllung folgt kein erfolgreicher Grenz-/No-op-Test.

**N2 insgesamt:** OPEN. Keine tatsächliche Historyersetzung erreicht; daher weder deren einzelne native Undo-Gruppe noch mehrere History-Undo-/Redo-Folgen oder No-op ohne zusätzlichen Undo-Schritt nativ abgenommen. Die oben erfolgreichen Ctrl+Z/Y-Kontrollen betreffen ausschließlich eingegebenen Text.

**N3 insgesamt:** Fokuswechsel und Refokus nachgewiesen, Historynavigation in diesen Situationen OPEN. Keine sichtbare unbeabsichtigte Textänderung bei den beschriebenen Fokusaktionen; kein Beleg für interne Historyindizes oder die Anzahl ausgeführter Commands.

**N4 insgesamt:** Gewöhnliche Unicodeeingabe und allgemeines Undo/Redo PASS im OS-Automationsumfang. Unicode-Historyfolgen und physische Schweizer AltGr-/Dead-Key-Eingaben OPEN.

## 4. Fehler, Sicherung und manuelle Restprüfung

**N1-F01 – hoch, Abnahmeblocker:** Enter ohne sichtbaren Zeilenabschluss/Ausführungsergebnis. Reproduktion: frische Debug-Instanz öffnen, Editor anklicken, `Print(0x(11))` eingeben, Return drücken; bei bestätigtem Editorfokus mit End/Return wiederholen. Erwartet wird der reguläre GUI-Ausführungspfad, beobachtet wurde unveränderter einzeiliger Text. Ctrl+Z/Y funktionierte im selben Editor. Der Blocker trat auch mit einem zweiten Text und nach Fokuswechsel auf. Kein Crash oder sichtbarer Fehlerdialog wurde beobachtet. Ein internes Ausführungs-/Exceptionprotokoll wurde nicht erhoben; interne Wirkungslosigkeit wird daher nicht behauptet.

Quellprüfung: `CryptoScriptEditView.textEditor_TextArea_TextEntering` startet bei Newline `ExecuteInputAsync`/`ParseLineAsync`; `BeginLine` befüllt dabei die History. `WorkbenchTextArea` kapselt native Editing-Commands in `RunGui` und prüft zusätzlich `Allowed` in `OnTextEntering`. Eine verschachtelte Admission beim nativen Enter ist ein **Untersuchungsansatz, keine durch Laufzeitinstrumentierung bestätigte Ursache**. Keine automatische Korrektur vorgenommen.

**N1-F02 – niedrig, Bedienhinweis:** Der Fußtext nennt für LineHistory Ctrl statt Alt. Abweichung von den tatsächlichen Bindungen ist sichtbar und im Quelltext verifizierbar; sie wurde für diesen Test berücksichtigt und erklärt den Enter-Blocker nicht.

Gesicherte Nachweise unter `artifacts/ap3-02-n1/` (lokale, ignorierte Prüfartefakte):

- [Enter ohne sichtbare Wirkung](../../artifacts/ap3-02-n1/enter-no-effect.jpg) und `enter-no-effect-accessibility.json`.
- [Unicode vor Undo](../../artifacts/ap3-02-n1/unicode-before-undo.jpg), [Undo](../../artifacts/ap3-02-n1/unicode-undo.jpg), [Redo](../../artifacts/ap3-02-n1/unicode-redo.jpg).
- [Variablenfokus](../../artifacts/ap3-02-n1/variable-focus.jpg), [Refokus/History unverändert](../../artifacts/ap3-02-n1/refocus-history-unchanged.jpg) und `refocus-accessibility.json`.
- `build.log`, `before.xml`, `before.patch`, `status-before.txt`, `preservation-after.txt`, `status-after.txt`.

Manuelle Restprüfung durch den Projekteigentümer, nach Klärung des Enter-Blockers:

1. Aktuellen Debug-Stand in einer neuen Sitzung öffnen. Physische Schweizer Tastatur und tatsächlich aktives CH-Layout protokollieren; kein bestehendes ScriptBook verwenden.
2. Im Editor drei unterschiedliche harmlose Zeilen (`Print(0x(11))`, `Print(0x(22))`, `Print(0x(33))`) jeweils mit physischem Enter ausführen. Sichtbare Ausführung und Reihenfolge festhalten. Falls Enter erneut ausbleibt: Zustand sichern und nicht als erfolgreiche Historybefüllung weiterwerten.
3. Alt+PageUp mehrfach bis zur Anfangsgrenze, anschließend Alt+PageDown bis zur Endgrenze drücken. Vorher-/Nachhertext je Geste dokumentieren; keine erneute Print-Ausgabe und keinen doppelten Schritt zulassen. Nach jeder wirksamen Ersetzung Ctrl+Z/Y prüfen, anschließend mehrere Ersetzungen rückwärts/vorwärts auflösen. An der Grenze prüfen, dass ein No-op keinen zusätzlichen Undo-Schritt erzeugt.
4. Variablenansicht fokussieren, Editor erneut fokussieren und dieselbe History-/Undo-/Redo-Folge wiederholen.
5. Mit der physischen CH-Tastatur `äöü`, ein AltGr-Zeichen (z. B. `€` über AltGr+E beim passenden CH-Layout) und ein Dead-Key-Ergebnis (z. B. `^`, danach `e` zu `ê`) eingeben. Tatsächliche Tastenfolge und Layout notieren. In einer harmlosen Print-Zeile ausführen und über History, Ctrl+Z und Ctrl+Y auf exakte Wiederherstellung prüfen. Abweichende CH-Layoutbelegung explizit dokumentieren.

## 5. Grenzen der Nachweisklassen

Dieser Durchlauf liefert native Desktop-**OS-Automation**, keine physische Tastaturprüfung. `type_text` beweist Unicodeübertragung, nicht AltGr-/Dead-Key-Komposition. Die vorherigen Headless-/Unit-Nachweise aus dem [F2-Nachreview](MCP-AP3-02-F2-independent-rereview.md) bleiben eigenständige Evidenz; sie ersetzen keine der hier offenen nativen Historyprüfungen. Im N1-Auftrag wurden keine neuen Headless-Tests als Abnahmeersatz ausgeführt. Das akzeptierte CJK-IME-Restrisiko und die frühe IME-Deaktivierung bleiben unverändert.

## 6. Offener AP3-P6-Punkt

Die synthetische `UpdateFinished`-/`BeforeUpdate`-Rekursion aus dem [F2-Nachreview](MCP-AP3-02-F2-independent-rereview.md) bleibt **OPEN für AP3-P6**. Es wurde kein unbeschränkter Stack-Overflow-Test in der Desktopanwendung ausgeführt. N1 behauptet weder allgemeine Observerfreigabe noch Behebung dieser Grenze.

## 7. Git- und Erhaltungsprüfung

Vor Testbeginn wurden Git-Status, binärer Diff und ein SHA-256-Manifest der relevanten Produkt-, GUI-Test- und Berichtsdateien gesichert. Der Ausgangsstand enthielt vier modifizierte Produktdateien, vier unversionierte Historytestdateien und sieben unversionierte AP3-Berichte (vollständige Pfade in `status-before.txt`). Diese bestehenden Änderungen wurden nicht zurückgesetzt oder überarbeitet.

Abschließender Vergleich gegen `before.xml`: **122 von 122 Bestandsdateien unverändert; 0 fehlend, 0 Hashabweichungen.** Der neue N1-Bericht ist die einzige zusätzliche nicht ignorierte Datei gegenüber dem Ausgangsstatus. Produktdateien, permanente Tests und Assertions wurden nicht geändert. Andere Worktrees und `.codex/config.toml` wurden nicht verändert; die Konfigurationsdatei wurde nicht gelesen. Kein Commit, Push oder Merge.

## 8. Schlussentscheidung und Stop-Gate

**FAIL.** Der reproduzierte GUI-Enter-Blocker verhindert den vorgeschriebenen Historyaufbau und damit eine vollständige native Abnahme. Erfolgreiche Teilprüfungen (Unicodeeingabe, allgemeines Ctrl+Z/Y, Fokuswechsel/Refokus) bleiben erhalten. N2 und die abhängigen Teile von N1/N3/N4 sowie die physische CH-Tastaturprüfung bleiben ausdrücklich OPEN. Eine Fehlerursache in der History-Capability wurde damit nicht nachgewiesen.

Der Auftrag endet mit diesem Bericht. Keine Produktkorrektur und keine Fortsetzung mit AP3-03.
