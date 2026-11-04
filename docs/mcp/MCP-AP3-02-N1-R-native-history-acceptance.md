# MCP AP3-02-N1-R – Native Windows-History-Abnahme

Datum: 2026-10-29. Dokumentations-/Abnahmeauftrag ohne Produktkorrektur.

## 1. Ausgangsstand und Nachweise

Worktree `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp-ap3`, Branch `feature/mcp-ap3`, HEAD `1c0e86fecfa6dd011888ac98b3bf0a2b7f49dd00`. Geprüft wird der vollständige uncommittete AP3-Patch einschließlich akzeptiertem N1-F1.

Aktuelle nicht instrumentierte Debuganwendung aus `CryptoWorkBenchAvalonia.Desktop/bin/Debug/net10.0`. Produkt-DLL SHA-256 `CF5DB2D9B18138FD46EAD9F72B6EB2C427600740E704261B7415B53EF401D33B`, identisch zum unabhängigen N1-F1-Review. Avalonia 12.1.4, AvaloniaEdit 12.0.0, DataGrid 12.1.2, net10.0. Keine Paket- oder Prismänderung. Build und 589 GUI-PASS aus dem vorherigen Review sind vorhandene Evidenz, kein neuer Testlauf dieses Auftrags. Kein Neubuild notwendig bei unverändertem Build/Quellstand.

Vor Testbeginn: `artifacts/ap3-02-n1-r/status-before.txt`, `before.patch`, SHA-256 `before.xml` für Quellen/Tests/Berichte, `evidence-before.xml` für D1/F1/Independent-Review-Artefakte und `build-before.xml` für Desktopbinärdateien. Alle neuen Originalscreenshots liegen im selben isolierten Artefaktverzeichnis.

Vor Start keine CryptoWorkBench-Benutzerinstanz vorhanden. Eigene leere Anwendungssitzung gestartet, kein ScriptBook geöffnet oder gespeichert, keine Schlüssel verwendet.

## 2. N1 – Drei Einträge und vollständige Navigation

Notation: A=`Print(0x(11))`, B=`Print(0x(22))`, C=`Print(0x(33))`, ∅=leere Zeile. Texteingabe mit OS-Automation/Clipboard, Ausführung jeweils durch gesondertes Windows-Return-Keyereignis. Alle folgenden Zeilen: PASS im beobachteten nativen Umfang.

| Schritt | Ausgang → Aktion → tatsächliches Ergebnis | Originalevidenz |
|---|---|---|
| 1 | Leere Sitzung → A + Return → A, einmal out: 0x(11), neue leere Zeile 3; Erfolgsstatus | 01-enter11.jpg |
| 2 | Zeile 3 leer → B + Return → B, einmal out: 0x(22), neue leere Zeile 5; Erfolgsstatus | 02-enter22.jpg |
| 3 | Zeile 5 leer → C + Return → C, einmal out: 0x(33), neue leere Zeile 7; Erfolgsstatus | 03-enter33.jpg |
| 4 | Zeile 7 ∅ → Alt+PageUp → B | 04-back22.jpg |
| 5 | B → Alt+PageUp → A, Anfang erreicht | 05-back11.jpg |
| 6 | A → Alt+PageUp → A, No-op | 06-back-noop.jpg |
| 7 | A → Alt+PageDown → B | 07-forward22.jpg |
| 8 | B → Alt+PageDown → C, Ende erreicht | 08-forward33.jpg |
| 9 | C → Alt+PageDown → C, No-op | 09-forward-noop.jpg |

Der unveränderte Präfix während jeder Navigation lautet exakt:

```text
Print(0x(11))
out: 0x(11)
Print(0x(22))
out: 0x(22)
Print(0x(33))
out: 0x(33)
```

Nur Zeile 7 wird ersetzt. Keine zusätzliche sichtbare Ausgabe, kein Textverlust und keine doppelte Navigation. Erwartete und tatsächliche Reihenfolge stimmen überein. Indexbegründung zusätzlich aus dem unveränderten HistoryService: AddLine setzt jeweils Count−1, nach C also 2; Back führt 2→1→0, weiterer Back ist gesperrt; Forward führt 0→1→2, weiterer Forward ist gesperrt. Damit beruht der Grenznachweis auf befüllter History, vollständiger Übergangsfolge und Quellvertrag, nicht allein auf unverändertem Text. Der interne Index wurde nicht instrumentiert ausgelesen.

## 3. N2 – Native Undo-/Redo-Folgen

Alle Schritte über Windows Ctrl+Z/Y; Präfix und genau drei Ausgaben unverändert. Tatsächliche Ergebnisse entsprechen der erwarteten Folge.

| Schritt | Zeile 7 vorher → Geste → nachher | Evidenz | Ergebnis |
|---|---|---|---|
| 10 | C nach End-No-op → Ctrl+Z → B | 10-undo22.jpg | PASS |
| 11 | B → Ctrl+Y → C | 11-redo33.jpg | PASS |
| 12 | C → Ctrl+Z → B | 12-undo22.jpg | PASS |
| 13 | B → Ctrl+Z → A | 13-undo11.jpg | PASS |
| 14 | A → Ctrl+Z → B | 14-undo22.jpg | PASS |
| 15 | B → Ctrl+Z → ∅ | 15-undo-empty.jpg | PASS |
| 16 | ∅ → Ctrl+Y → B | 16-redo22.jpg | PASS |
| 17 | B → Ctrl+Y → A | 17-redo11.jpg | PASS |
| 18 | A → Ctrl+Y → B | 18-redo22.jpg | PASS |
| 19 | B → Ctrl+Y → C | 19-redo33.jpg | PASS |

Vier wirksame Ersetzungen lassen sich mit vier Undo-Aktionen auflösen. Beide Grenz-No-ops benötigen keinen zusätzlichen sichtbaren Undo-Schritt. Exakte interne Stacktiefe ist mit OS-Automation nicht direkt gemessen; eigenständiger ergänzender Nachweis sind die unveränderten permanenten `HistoryCoordinatorTests.CommandsKeepOneUndoGroupAndSeparateNoOp` (ab Zeile 75), weitere No-op-Assertions und HistoryCorrectionTests. Diese waren im vorherigen vollständigen GUI-Lauf grün und wurden hier nicht neu ausgeführt.

## 4. N3 – Fokus-/Viewwechsel

**PASS im nativen beobachteten Umfang.** Ausgang nach Schritt 19: Zeile 7 C, drei unveränderte Print-Ausgaben.

| Schritt | Erwartete und tatsächliche Wirkung | Evidenz | Ergebnis |
|---|---|---|---|
| 20 | Editor fokussiert, Alt+PageUp: C → B | 20-focus-before22.jpg | PASS |
| 21 | Show Variables öffnet rechts Variable View; Editor bleibt links mit B erhalten | 21-variables.jpg | PASS |
| 22 | Klick ins leere Textfeld der Variablenansicht; Fokus tatsächlich auf edit 52, keine Eingabe | 22-variable-focus.jpg, 22-focus.json | PASS |
| 23 | Klick zurück in Editorzeile 7; B unverändert | 23-editor-return.jpg | PASS |
| 24 | Ctrl+Z direkt nach Fokuswechsel: B → C, kein zusätzlicher Undo-Schritt nötig | 24-focus-undo33.jpg | PASS |
| 25 | Ctrl+Y: C → B | 25-focus-redo22.jpg | PASS |
| 26 | Alt+PageUp nach Rückkehr: B → A, genau ein Schritt | 26-focus-back11.jpg | PASS |

Die Anwendung besitzt einen gemeinsamen sichtbaren Editor; der Viewwechsel betrifft das rechte Panel. Es wurde kein zweiter Editor geöffnet. Inhalt, bisherige Ausgaben und Undo-Historie bleiben erhalten. Kein verspätetes Überschreiben während dieser Folge beobachtet; dies ist kein allgemeiner Beweis für alle möglichen asynchronen Observer. Die interne Dokumentreferenzidentität ist ohne Instrumentierung nicht direkt gemessen.

## 5. N4 – Unicode und Schweizer Tastatur

**PASS für OS-/Clipboard-Unicodeübertragung und sichtbare native History-/Undo-/Redo-Folgen; physische Schweizer Eingabe OPEN.**

U bezeichnet `Print("äöü € é ê é")`; das letzte Zeichen wurde als U+0065 gefolgt von U+0301 übergeben. ê wurde fertig zusammengesetzt übertragen, nicht durch Dead-Key-Eingabe erzeugt. Die sichtbare Ausgabe lautet `out: "äöü € é ê é"`. Screenshots belegen die Darstellung; eine bytegenaue Normalisierungsprüfung des Dokuments wird daraus nicht abgeleitet.

| Schritt | Ausgang, Aktion und erwartete/tatsächliche Wirkung | Evidenz | Ergebnis |
|---|---|---|---|
| 27–28 | Zeile 7 A auswählen (Shift+Home), mit U via type_text/Clipboard ersetzen; Unicode sichtbar | 27-unicode-selection.jpg, 28-unicode-input.jpg | PASS |
| 29 | Windows Return: eine Unicodeausgabe, Erfolgsstatus, neue leere Zeile 9 | 29-unicode-execute.jpg | PASS |
| 30 | Zeile 9 Print(0x(44)) per type_text, Windows Return: einmal out: 0x(44), leere Zeile 11 | 30-enter44.jpg | PASS |
| 31 | Alt+PageUp: Zeile 11 leer → U | 31-unicode-back.jpg | PASS |
| 32 | Ctrl+Z: U → leer | 32-unicode-undo.jpg | PASS |
| 33 | Ctrl+Y: leer → U | 33-unicode-redo.jpg | PASS |
| 34 | Alt+PageDown: U → Print(0x(44)) | 34-unicode-forward44.jpg | PASS |
| 35 | Alt+PageUp: Print(0x(44)) → U | 35-unicode-return.jpg | PASS |

Während 31–35 bleiben alle fünf Ausgaben (11, 22, 33, Unicode, 44) jeweils einmal und unverändert erhalten. Keine sichtbare Neuausführung bei Navigation oder Undo/Redo. Die übertragene kombinierende Zeichenfolge wird sichtbar wiederhergestellt. Tatsächlich physisches Enter, aktives Schweizer Layout, physisches AltGr und Dead-Key-Komposition wurden nicht geprüft und bleiben OPEN. Die Freigabe des Desktops durch den Benutzer ist keine physische Tastatur-Testausführung.
## 6. Nachweisklassen und offene Befunde

Native Windows-Keyereignisse: Return, Alt+PageUp/PageDown, Ctrl+Z/Y. Die Zeichenfolgen werden mit Computer-Use type_text (Clipboard/Paste) übertragen. Screenshotbelege zeigen sichtbare Text-/Ausgabewirkungen, nicht interne Taskzähler, Dokumentreferenzidentität oder lückenlose Beobachtung sämtlicher asynchroner Callbacks.

IR-N1-01 bleibt unverändert OPEN für P8/AP5: erfolgreiche normale Historynavigation beweist keine vollständige asynchrone Ausführungsautorität. Keine Bearbeitung dieser Lücke. Synthetische UpdateFinished-Rekursion bleibt OPEN für AP3-P6. N1-F02 (irreführender History-Hilfetext: Ctrl statt tatsächlich Alt für LineHistory) bleibt als separater niedriger UI-Befund erhalten. Keine Korrektur. CJK-IME-Restrisiko und frühe Deaktivierung unverändert akzeptiert.

## 7. Manuelle Restprüfung

Neue Sitzung des geprüften Debugstands öffnen, physische Schweizer Tastatur und aktives CH-Layout dokumentieren. `Print("äöü € é ê é")` physisch eingeben: € mit der tatsächlichen AltGr-Kombination des aktiven CH-Layouts, ê über Dead Key und e. Tastenfolgen dokumentieren; letzte Kombination é besteht aus U+0065/U+0301 und ist optional separat zu prüfen. Physisch Enter drücken, eine Ausgabe und neue Leerzeile prüfen. Danach `Print(0x(44))` ausführen; Alt+PageUp muss die Unicodezeile liefern. Ctrl+Z muss den vorherigen Zustand, Ctrl+Y dieselbe Unicodezeile herstellen. Mit Alt+PageDown/Up wiederholen; keine neue Ausgabe. Screenshots und PASS/FAIL je Tastensequenz ergänzen. Keine persönlichen ScriptBooks verwenden.

## 8. Erhaltung und Schlussentscheidung

**Schlussentscheidung: INCONCLUSIVE für die vollständige Abnahme einschließlich physischer Schweizer Tastatur.** N1, N2, N3 und N4 im beschriebenen OS-Automationsumfang sind PASS. Kein neuer Produktfehler bestätigt. Die abschließende uneingeschränkte AP3-02-Abnahme wird nach manueller Ergänzung der offenen physischen N4-Eingaben empfohlen; die automatisierten Ergebnisse müssen dafür nicht als ungetestet behandelt werden.

Die Unterbrechung vor Schritt 19 erfolgte wegen gleichzeitig erkannter Benutzereingabe. Nach ausdrücklicher Desktopfreigabe wurde dieselbe eigene Sitzung wiederaufgenommen: Text B vor dem letzten Redo erneut beobachtet, erst dann Ctrl+Y ausgeführt. Unterbrochene Versuche wurden nicht als erfolgreiche Schritte gezählt. Nach Schritt 35 eigene Instanz geschlossen und deren Verschwinden aus der Fensterliste verifiziert.

Abschluss-SHA-256-Abgleich: 768 manifestierte Quell-/Test-/Berichtsdateien, 1057 bestehende Evidenzdateien und 74 Desktop-Builddateien unverändert, jeweils null fehlende Dateien/Hashabweichungen (`preservation-final.json`). Neues Dokument und neue Prüfartefakte sind die einzigen beabsichtigten Ergänzungen. Keine bestehenden Testassertionen oder Produktdateien geändert, keine fremden Worktrees bearbeitet. `.codex/config.toml` weder gelesen noch verändert. Kein Commit, Push oder Merge.

Stop-Gate: Auftrag beendet. Keine Fortsetzung mit AP3-03, P8/AP5 oder Produktkorrekturen.


## 9. Abschlussnachtrag vom 02.11.2026

**PASS.** Der Projekteigentümer bestätigt am 02.11.2026, dass er die zuvor offene manuelle Prüfung mit der physischen Schweizer Tastatur erfolgreich durchgeführt hat. Diese Bestätigung schließt den manuellen Teil von N4. Die Schritte und Originalbelege in den Abschnitten 2–5 dokumentieren den zuvor durchgeführten automatisierten Windows-Test. Die Entscheidung INCONCLUSIVE in Abschnitt 8 bezeichnet den Stand vor der Eigentümerbestätigung.

Damit ist die native History-Abnahme vollständig und AP3-02 abgeschlossen. Die spätere Bearbeitung von IR-N1-01 in AP3-P8/AP5, der synthetischen UpdateFinished-Rekursion in AP3-P6 und des History-Hilfetexts N1-F02 sowie das akzeptierte CJK-IME-Restrisiko sind im [Abschlussbericht](MCP-AP3-02-final-acceptance.md) aufgeführt.
