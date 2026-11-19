# G2-04-02-I3-F2 · Identifier und verschachtelte Parameter

19. November 2026 · Implementierung und lokale Windows-Validierung · Die Eigentümerabnahme von I3 steht aus.

## Ergebnis

| Befund | Entscheidung | Ausschlaggebendes Ergebnis im nativen Drei-Spalten-Layout |
|---|---|---|
| R2-H01 · HIGH | **CORRECTED – READY FOR INDEPENDENT RE-REVIEW** | Bei 65.536 UTF-16-Einheiten wurde die Seite 93 ms nach Auswahl bedienbar. In R2 liefen zwei Prozesse in die Acht-Sekunden-Grenze. |
| R2-M01 · MEDIUM | **CORRECTED – READY FOR INDEPENDENT RE-REVIEW** | Bei 1.048.576 Zeichen Label-Inhalt sank die höchste beobachtete Eingabewartezeit von 1.200 auf 87 ms. Der kopierte Parameterwert enthielt alle 1.048.578 Einheiten einschließlich der Anführungszeichen. |

## Ausgangsstand und Abgrenzung

Der Windows-Worktree `artifacts/worktrees/gui-g2-04` stand auf `feature/gui-g2-04`, HEAD und lokaler Master auf `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Der Index enthielt keine vorgemerkten Änderungen. Vor der ersten Produktänderung wurden die 66 bereits geänderten oder unversionierten, von Git erfassten Arbeitspfad-Dateien einzeln mit SHA-256 inventarisiert. Dazu gehören die uncommittierten I1-/I2-/I3-Stände. Alle 65 Dateien der R2-Eingangsliste hatten am F2-Eingang denselben SHA-256 wie bei R2. Hinzu kam der R2-Bericht. Die F2-Differenz verwendet gesicherte Eingangsdateien aus diesem Worktree. [Status vor F2](../artifacts/test-results/g2-04-i3-f2/status-before.txt) · [Eingangshashes](../artifacts/test-results/g2-04-i3-f2/before.json) · [Originalkopien](../artifacts/test-results/g2-04-i3-f2/original) · [Abgleich mit den R2-Originalen](../artifacts/test-results/g2-04-i3-f2/prior-evidence.json)

R1, F1, R2, der I3-Abschluss, der akzeptierte I2-R3-Stand, der F3-korrigierte Anforderungssatz und die Originalquellen der R2-Gegenproben waren zugänglich. Geprüft wurden auch die im R2-Zugriffsinventar referenzierten lokalen Originale, darunter die außerhalb des Repositorys archivierten Vertragsdokumente. Für den Vorhervergleich wurden die R2-Messdaten für Identifier, Parameter und Value sowie der zugrunde liegende Probequelltext eingesehen. [R1](G2-04-02-I3-R1-independent-integration-review.md) · [F1](G2-04-02-I3-F1-correction-report.md) · [R2](G2-04-02-I3-R2-independent-re-review.md) · [R2-Vertrags- und Quelleninventar](../artifacts/test-results/g2-04-i3-r2/access.json) · [F2-Zugriffshashes](../artifacts/test-results/g2-04-i3-f2/access.json)

## R2-H01 · Identifier

Die Identifier-Spalte und das Feld `SelectedIdentifier` banden den vollständigen Namen. Visuelles Ellipsis im Grid griff erst nach Übergabe an Avalonia. Der umbrochene Inspector-Titel baute außerdem eine neue Zeichenfolge aus dem vollständigen Namen und dem Laufzeittyp auf. Nach Navigation übernahmen Breadcrumb, Tooltip und Automation-Name nochmals den gesamten Root-Namen.

Alle Variables-Zellen und Detailfelder verwenden jetzt dieselbe F1-Vorschauregel. Der Inspector sammelt für zusammengesetzte Texte höchstens 129 UTF-16-Einheiten im Vorschaupuffer. Eine Einheit dient der Überlauferkennung. Bei Kürzung verbleiben maximal 128 Einheiten einschließlich Ellipse, gegebenenfalls 127 bei einem Surrogatpaar an der Schnittstelle. Kurze Texte behalten Inhalt, Leerzeichen und Formatierung. Ein später Pfadteil löst nach erreichtem Limit keine weitere Pfaditeration aus. Die vollständigen Namen bleiben in Deklaration, Zeilenmodell und Capture-Stamp. Ziel der Navigation ist weiterhin das ursprüngliche Property beziehungsweise der originale Snapshot-Pfad. [Vorschau](../CryptoWorkBenchAvalonia/DisplayTextPreview.cs) · [Variables](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs) · [Inspector](../CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs) · [ViewModel](../CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs)

## R2-M01 · Parameter

`VariablePresentationCapture` übernimmt die Schlüssel und Werte aus `GetParameters()` in Reihenfolge und mit den originalen Stringreferenzen. In `ParametersGrid` gelangten bisher beide Strings vollständig an umbrechende `SelectableTextBlock`-Controls. Beim KDF-Test enthielt allein der Value von `#LABEL` die Nutzlast plus zwei Anführungszeichen. Der gemeinsame Bindungspfad begrenzt jetzt auch diese beiden Parameterzellen vor der Textmessung. Kurze Werte behalten ihre Anführungszeichen. [Capture](../CryptoWorkBenchAvalonia/Services/Automation/VariablePresentationCapture.cs) · [Grid-Templates](../CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs)

Die vorhandene Textauswahl in Variables kann weiterhin Originaldaten kopieren. Endet die Auswahl vor der Ellipse, kopiert Avalonia den markierten Ausschnitt. Enthält sie die abschließende Vorschauellipse, entspricht diese dem ausgelassenen Originalsuffix. Bei vollständig markierter Vorschau erhält die Zwischenablage daher das gesamte Original. Der Converter hält dafür nur eine Referenz auf die bereits vorhandene Quelle. Die Auflösung erfolgt beim ausdrücklichen Kopieren. Das gilt auch für rückwärts aufgezogene Markierungen. Die vorhandenen Inspector-Aktionen lesen wie bisher den Snapshot.

## Audit der produktiven Dock-Ansichten

| Route | Klasse am Eingang | Ergebnis |
|---|---|---|
| Variables, Value in Tabelle und Detail | A · F1 begrenzt | Gemeinsame 128er-Regel, NoWrap der Übersichtszeile und Wrapping der Details beibehalten. |
| Variables, Identifier und Typ in Tabelle und Details, Format, Mechanism, Derivation, GMAC | B · gleicher Darstellungsfehler oder keine verlässliche Längengrenze am Binding | Gemeinsamer Converter, auch bei zunächst unselektierten Zeilen und bei unsichtbaren Detailfeldern. |
| Variables, Algorithm, Material, KeySize | C · feste Algorithmusauswahl, Enum bzw. formatierte Schlüsselgröße | Derselbe Converter lässt die kurzen Texte unverändert. |
| ParametersGrid, Parameter und Value | B · Dictionary-Einträge | Beide Spalten gekürzt, Reihenfolge und Quellreferenzen erhalten. |
| Inspector, Titel sowie Root-Breadcrumb, Button-Content, Tooltip, Automation-Name | B · vollständiger Identifier | Begrenzung auch beim Zusammensetzen mit Präfix und Typ. Originaler Tiefenindex im Callback. |
| Inspector, Pfad, Page-Preview, Status und Fehlertexte | B · zusammengesetzter Pfad bzw. frei langer Fehlertext | Begrenzte Pfad-/Statusbildung und begrenzte Komposition am Text-Control. |
| Inspector, Property-, Typ- und Statuszellen, Aktionsnamen | A/C für Schema-, Index- und Dictionarylabels, B für frei lange Getter-Fehler | Alle dynamischen Labels und präfigierten Namen am Renderpfad begrenzt. Dictionarylabel verwendet bereits einen paar-sicheren 80er-Präfix und den Originalschlüssel für Navigation. Die vorhandenen 120er-/160er-Präfixe des Projektors teilen jetzt ebenfalls kein Surrogatpaar. Die bisherigen Projektionsbreiten bleiben erhalten. |
| Statische Überschriften, Buttontexte und Hilfen, nummerischer Textbereich | C · Literale oder begrenzte Ganzzahlen | Direkte Zuweisung. |
| Expliziter Inspector-Volltext | A · bereits seitenweise | Das read-only Textfeld erhält pro Seite höchstens 4.096 Einheiten. Vollwertaktionen lesen die Originaldaten. |

Für die geprüften Dock-Pfade bleibt keine ungeklärte Bindung der Klasse D. Das Dock-Presentation-Objekt erzeugt `DockVariablesView` und den Dock-Inspector. Die separat registrierte Legacy-Prism-Ansicht gehört zum anderen Shell-Einstieg. Die Untersuchung erfasst die Dock-Integration von I3. [Shell-Verdrahtung](../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs) · [App-Einstiege](../CryptoWorkBenchAvalonia/App.axaml.cs) · [Projektor](../CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs)

## Permanente Regressionstests

Drei neue Fälle prüfen die tatsächlichen Controls und kommen zu den 111 fokussierten bzw. 1.266 GUI-Fällen aus R2 hinzu. Der vorhandene F1-Value-Test läuft weiterhin in beiden Testmengen. [DockInspectorTests](../CryptoWorkBenchGuiTest/DockInspectorTests.cs)

| Test | Beobachtung und Gegenprobe |
|---|---|
| `IdenticalIdentifierPreviewsRetainDistinctSelectionsAndHistoricalValues` | Zwei unselektierte Zellen zeigen denselben gekürzten Namen. Auswahl, Detail und Titel, Breadcrumb inklusive Tooltip und Automation-Name, Voll- und Teilmarkierung, Originalreferenz und Copy stimmen für beide IDs. Nach Dokumentänderung funktionieren historische Navigation und Copy des zuvor erfassten Id. Abwahl leert die Seite. |
| `ParameterCellsPreserveSourceKeysValuesOrderAndQuotedFormatting` | Echte KDF-Factory mit langem zitiertem Label und zusätzlichem synthetischem Metadatenschlüssel an der UTF-16-Grenze. Jede Spalte wird in den sichtbaren Bereich gescrollt. Der Test prüft Text, Auswahl-Copy, Schlüssel-/Wertreferenzen, Reihenfolge, kurzen zitierten Wert und Abwahl. Der synthetische Schlüssel prüft die allgemeine Dictionary-Anzeige. Die KDF-Messungen verwenden ausschließlich unterstützte Namen. |
| `ScalarPropertyRowKeepsItsExistingPreviewWidthAtASurrogateBoundary` | Liegt ein Surrogatpaar über dem bisherigen 120er-Schnitt einer skalaren Property, zeigt die echte Inspector-Zelle die intakte abgekürzte Zeichenfolge. Der Originalwert bleibt referenzgleich. |

## Native Windows-Messungen

Pro Kombination startete ein eigener Release-Prozess mit der produktiven Dock-Shell. Im sichtbaren Fall teilte der Dock-Manager die Gruppe rechts ab. Im versteckten Fall war Inspector der aktive Tab. Die Probe prüfte die tatsächliche Sichtbarkeit und das native Top-Level. Skalierung 1,5. Die Identifier bestanden aus `v` und ausschließlich `a`, ihr Wert war `short`. Die KDF-Factory erhielt `KDF-SP800-108-COUNTER`, `#PRF:HMAC-SHA256`, `#OUTLEN:128` und ein zitiertes `#LABEL` aus der angegebenen Anzahl `q`. Konstruktion und Formatierung waren bei Messbeginn abgeschlossen. Ein separater Lauf nutzte den GUI-Parser für je 65.536 Zeichen und prüfte beide Veröffentlichungen. [Quellstand der Hauptmessung](../artifacts/test-results/g2-04-i3-f2/native/primary-source.cs.txt) · [Parserlauf](../artifacts/test-results/g2-04-i3-f2/final-integrity/parse.log)

Ein Hintergrundthread reihte bereits vor der Veröffentlichung und anschließend ungefähr alle 12 ms Input-Priority-Callbacks ein. Die Uhr war eine monotone Stopwatch. Die Bereitschaftsmarke erforderte die fertige Seite, einen aktiven und angeordneten Copy-Button und einen nachfolgenden nativen Animationsframe. Die Messung lief danach weitere 200 ms. „Früh“ umfasst alle bis zur Bereitschaft eingereihten Aufträge. Die Timer-Spalte zeigt das längste Intervall und seine Abweichung von 50 ms. Der Working Set ist der extern alle etwa 90 ms abgetastete Prozesshöchstwert. Protokolle enthalten zusätzlich den Prozess-Peak am Ende des Messfensters. Die Zwischenablageprüfungen für ID und Parameter liefen in separaten Prozessen. [Monitor und Abbruchgrenzen](../artifacts/test-results/g2-04-i3-f2/measure.ps1) · [Auswertung](../artifacts/test-results/g2-04-i3-f2/summarize.cjs) · [Rohmessungen einschließlich aller Zeitpaare](../artifacts/test-results/g2-04-i3-f2/measurements/analysis.json) · [Prozessprotokolle](../artifacts/test-results/g2-04-i3-f2/measurements)

| Quelle | Einheiten | Variables | Bereit ab Start / Auswahl (ms) | Frühe Aufträge | Input-Max. früh / gesamt (ms) | Timer-Max. / Abweichung (ms) | Peak (MiB) | Bedient / eingereiht |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| Identifier | 4.096 | sichtbar | 110 / 69 | 6 | 67 / 67 | 74 / +24 | 190,7 | 20 / 20 |
| Identifier | 4.096 | verdeckt | 75 / 66 | 5 | 17 / 17 | 64 / +14 | 186,4 | 19 / 19 |
| Identifier | 65.536 | sichtbar | 117 / 93 | 6 | 66 / 66 | 73 / +23 | 185,1 | 21 / 21 |
| Identifier | 65.536 | verdeckt | 64 / 57 | 5 | 17 / 17 | 63 / +13 | 184,9 | 19 / 19 |
| #LABEL | 4.096 | sichtbar | 128 / 91 | 8 | 86 / 86 | 91 / +41 | 193,8 | 22 / 22 |
| #LABEL | 4.096 | verdeckt | 73 / 67 | 5 | 19 / 19 | 62 / +12 | 185,8 | 19 / 19 |
| #LABEL | 65.536 | sichtbar | 126 / 89 | 9 | 79 / 79 | 86 / +36 | 194,4 | 23 / 23 |
| #LABEL | 65.536 | verdeckt | 69 / 63 | 5 | 21 / 21 | 63 / +13 | 187,4 | 19 / 19 |
| #LABEL | 262.144 | sichtbar | 110 / 88 | 8 | 63 / 63 | 68 / +18 | 195,8 | 22 / 22 |
| #LABEL | 262.144 | verdeckt | 69 / 63 | 5 | 21 / 21 | 63 / +13 | 191,9 | 19 / 19 |
| #LABEL | 1.048.576 | sichtbar | 144 / 105 | 10 | 87 / 87 | 94 / +44 | 209,9 | 24 / 24 |
| #LABEL | 1.048.576 | verdeckt | 62 / 56 | 5 | 16 / 18 | 63 / +13 | 199,5 | 19 / 19 |

Alle zwölf Prozesse erreichten die Bereitschaftsmarke und beendeten sich mit Exit 0. Für beide Identifiergrößen maßen wir im sichtbaren Layout je 128 Einheiten in Tabellenzelle, Detail und Titel. Auch mit verdeckten Variables erhielt der Titel 128 Einheiten. Bei jeder Labelgröße hatte der KDF-Titel 64 Einheiten. In der sichtbaren verschachtelten Zeile maßen wir sechs für den Schlüssel und 128 für den Wert. Der größte Text in den sichtbaren Variables-Templates hatte 128 Einheiten. Verdeckt war das Grid außerhalb des sichtbaren Shell-Baums. [Identifier 65.536](../artifacts/test-results/g2-04-i3-f2/measurements/identifier-65536.log) · [KDF 1.048.576](../artifacts/test-results/g2-04-i3-f2/measurements/param-1048576.log)

R2 maß bei 4.096 Identifier-Einheiten sichtbar 795 ms Eingabeverzögerung, in F2 waren es 67 ms. Bei 65.536 Einheiten brach R2 sichtbar zweimal nach acht Sekunden ab. F2 erreichte die Seite ab Messbeginn nach 117 ms, bei höchstens 66 ms Eingabeverzögerung. Verdeckt sank die Eingabeverzögerung von 2.633 auf 17 ms. Für das größte KDF-Label lagen die Werte bei 1.200 → 87 ms sichtbar und 21 → 18 ms verdeckt. Die vier sichtbaren Labelgrößen ergaben 63–87 ms maximale Eingabewartezeit. Der Working Set stieg von 193,8 auf 209,9 MiB. Die verbleibende kurze Verzögerung beim Aufbau der Ansicht ist messbar. In diesen Läufen wuchs sie mit der Nutzlast weder auf Sekunden noch stetig an. Die Stichprobe beschreibt diese Maschine und diese Läufe. [R2-Identifier](../artifacts/test-results/g2-04-i3-r2/name-check/analysis.json) · [R2-Parameter](../artifacts/test-results/g2-04-i3-r2/large-params/analysis.json)

Vier zusätzliche Prozesse scrollten jeweils die tatsächliche `#LABEL`-Wertzelle ins Blickfeld und prüften ihre Schnittfläche mit den Clipping-Vorfahren und dem Shellfenster vor der Bereitschaftsmarke. In allen vier Fällen lagen 165,7 × 22 DIPs der Wertzelle im sichtbaren Bereich. Bei 4.096 / 65.536 / 262.144 / 1.048.576 Zeichen war die Seite ab Auswahl nach 100 / 108 / 114 / 101 ms bedienbar. Das jeweilige Input-Maximum betrug 80 / 79 / 73 / 88 ms. Alle Prozesse beendeten sich mit Exit 0. [Viewport-Probe](../artifacts/test-results/g2-04-i3-f2/native/Program.cs) · [Alle Messgrößen und Zeitpaare](../artifacts/test-results/g2-04-i3-f2/viewport/analysis.json) · [Sichtprüfung der größten Zelle](../artifacts/test-results/g2-04-i3-f2/viewport/paramviewport-1048576.log)

## Regressionen und Datenintegrität

| Neu ausgeführt | Ergebnis und Originalnachweis |
|---|---|
| Builds | GUI-Testprojekt und Desktop jeweils per Rebuild in Debug und Release, beide nativen Probehosts in Release. Alle sechs Builds Exit 0. Die Release-Produktassembly des Desktops und beider Hosts war bytegleich (SHA-256 im Inventar). [Befehle](../artifacts/test-results/g2-04-i3-f2/final/commands.log) · [Hashes](../artifacts/test-results/g2-04-i3-f2/product-hashes.json) · [Windows-Build und SDK](../artifacts/test-results/g2-04-i3-f2/environment.json) |
| Fokussierte / vollständige GUI-Suite | Debug und Release jeweils **114/114 fokussiert und 1.269/1.269 insgesamt**, jeweils 0 fehlgeschlagen und 0 übersprungen. Der Abgleich nach Testklasse und Testname bestätigt für beide Konfigurationen, dass alle fokussierten Fälle in der Gesamtmenge enthalten sind. [Zählung und TRX-Hashes](../artifacts/test-results/g2-04-i3-f2/final/matrix.json) · [Debug fokussiert](../artifacts/test-results/g2-04-i3-f2/final/focused-Debug.trx) · [Debug gesamt](../artifacts/test-results/g2-04-i3-f2/final/gui-Debug.trx) · [Release fokussiert](../artifacts/test-results/g2-04-i3-f2/final/focused-Release.trx) · [Release gesamt](../artifacts/test-results/g2-04-i3-f2/final/gui-Release.trx) |
| Variable und Parameter, native Zwischenablage | Zwei zunächst unselektierte IDs mit gleicher Vorschau und unterschiedlichen letzten Zeichen. Bei beiden exakter Feld-Copy, Copy Id, charakteristischer kurzer Value und Copy full value nach Navigation. Beim zweiten Id erfolgte Navigation nach Dokumentänderung im historischen Snapshot. Bei KDF exakt `#LABEL` und der vollständig zitierte Wert über die existierenden Inspector-Schaltflächen sowie der Vollwert aus der markierten Grid-Zelle. Vor jedem Vergleich wurde ein Sentinel gesetzt. Beide Abläufe endeten mit Abwahl. [Identifier](../artifacts/test-results/g2-04-i3-f2/final-integrity/identity.log) · [KDF](../artifacts/test-results/g2-04-i3-f2/final-integrity/paramcopy.log) |
| R1-H01, Value | Bei 1.048.576 Einheiten je ein neuer Prozess sichtbar, verdeckt und mit zunächst unselektierter sichtbarer Zeile. Seite nach 84 / 62 / 75 ms ab Auswahl, maximale Input-Wartezeit 71 / 17 / 36 ms. Alle drei nativen Vollwertkopien waren exakt. Zusätzliche Gegenproben für kurze Texte, Zeilenwechsel und Surrogatgrenzen. [Messwerte](../artifacts/test-results/g2-04-i3-f2/value-regression/analysis.json) · [Grenzfälle](../artifacts/test-results/g2-04-i3-f2/final-integrity/edge.log) |
| I2 und Inspector | Drei Themes (System, Light, Dark), je drei explizite AES-Calculate-and-copy-Vektoren, vier F3-Golden-Roots (PARAM, Header, Shamir, Split), Navigation, Paging, Detail, Dokument und Revision. Separater Stressfall mit aktivem Inspector und exaktem nativen Readback von **17.825.798 UTF-16-Einheiten**. Die vollständigen GUI-Suiten enthalten außerdem die akzeptierten Dictionary-, Großschlüssel-, historischen Copy- und SecretShare-Fälle. [Native Originalausgabe](../artifacts/test-results/g2-04-i3-f2/final-functional/functional.log) · [Oracle und Probe](../artifacts/test-results/g2-04-i3-f2/functional/Program.cs) |
| Docking und Cancellation | Nativer Redock bei leerem Quellhost, Reset aus Floating, fortbestehende Sitzung, zweite Zeile, normaler Wechsel, Abwahl, echter Close, Listener-Freigabe, Close vor Publikation und neues Show. Gehaltene Copy-Übergabe bei Close abgebrochen, kein spätes Ergebnis. Die Tastatur-Navigation zurück vom skalaren Blatt lief im frischen Headless-Test. Die physische Floating-Tastenfolge ist in R2 nativ belegt. [Lebensdauer](../artifacts/test-results/g2-04-i3-f2/final-integrity/life.log) · [Abbruch](../artifacts/test-results/g2-04-i3-f2/final-integrity/cancel.log) · [Native Tastaturfolge aus R2](../artifacts/test-results/g2-04-i3-r2/more/keyboard.log) |
| R1-M01, Layout | 35 Prozess- und Dateiprüfungen einschließlich gesperrtem v2, Rückfall ohne/mit Layoutänderung, fehlgeschlagener Erstinstallation mit erneutem Start, beschädigtem v2, Neustart und konkurrierendem Gewinner am Installationspunkt. Vollständige v2-Projektion mit Gruppen, aktiven Tabs, Proportionen, Geometrie und geschlossenem Inspector sowie byteidentisches v1. Der synchronisierte Wettlauf zweier tatsächlich konkurrierender Prozesse ist zusätzlich in R2 belegt. [Prozesschecks](../artifacts/test-results/g2-04-i3-f2/final-processes/checks.log) · [Projektionsvergleich](../artifacts/test-results/g2-04-i3-f2/final/projection.log) · [R2](G2-04-02-I3-R2-independent-re-review.md) |

## Anläufe und Nachvollziehbarkeit

Der erste isolierte Lauf der zwei neuen Bindungstests ergab 1/2. Im Parameter-Test war beim langen synthetischen Schlüssel nur die gerade sichtbare Spalte realisiert. Der Test scrollt jetzt jede zu prüfende Spalte einzeln in den Viewport. Der erste Build der erweiterten nativen Probe scheiterte mit CS0136 an zwei lokalen Variablennamen. Nach ihrer Umbenennung baute die Probe. Beide Erstfehler und die TRX bleiben erhalten. Die erste vollständige Messserie unter `timings/` und die vier TRX im Stamm des Nachweisordners gehören zur Zwischenfassung vor der ergänzten Behandlung von Strg+C und der paar-sicheren Projektorkürzung. Die oben verwendeten Zahlen stammen aus der abschließenden Wiederholung. Die Release-Probe für den zusätzlichen Viewportlauf ergänzt allein die Sichtflächenprüfung. [Erster Bindungstest](../artifacts/test-results/g2-04-i3-f2/new-controls.trx) · [Erster Probebuild](../artifacts/test-results/g2-04-i3-f2/build-native-initial.log) · [Drei gezielte Controls-Tests](../artifacts/test-results/g2-04-i3-f2/copy-controls.trx) · [Zwischenmessung](../artifacts/test-results/g2-04-i3-f2/timings/analysis.json) · [Endgültige Build- und Testbefehle](../artifacts/test-results/g2-04-i3-f2/final/commands.log)

## Änderungen und offene Grenzen

| F2-Quelldatei | Änderung |
|---|---|
| `CryptoWorkBenchAvalonia/DisplayTextPreview.cs` | Neu. Gemeinsame Vorschau, paar-sicherer Schnitt, begrenzte Zusammensetzung. |
| `CryptoWorkBenchAvalonia/Shell/DockVariablesView.cs` | Zellen, Details und Auflösung der Originalauswahl beim Kopieren. |
| `CryptoWorkBenchAvalonia/Views/DockVariableInspectorView.cs` | Titel, Breadcrumbs, Zeilen, Tooltip und Automation-Namen. |
| `CryptoWorkBenchAvalonia/ViewModels/VariableInspectorViewModel.cs` | Begrenzte Komposition von Anzeigepfad und Status. |
| `CryptoWorkBenchAvalonia/Services/Inspector/InspectorProjector.cs` | UTF-16-Schnitt der bereits begrenzten Property-/Eintragsvorschauen. |
| `CryptoWorkBenchGuiTest/DockInspectorTests.cs` | Drei permanente Tests am produktiven Control-Baum. |

Von 66 vorher inventarisierten Dateien sind 61 byteidentisch. Die fünf geänderten vorhandenen Dateien stehen in der Tabelle. Das SHA-Inventar umfasst nun 68 Arbeitspfad-Dateien einschließlich neuer Vorschauklasse und Bericht. Die Rohbelege liegen separat im lokalen Testartefaktordner. Das Inventar nennt die exakten Pfade sowie die Hashes vor und nach F2. Die Arbeit blieb im Feature-Worktree. [F2-Diff ab gesichertem Eingang](../artifacts/test-results/g2-04-i3-f2/f2-only.diff) · [Geänderte Dateien und Hashes](../artifacts/test-results/g2-04-i3-f2/changed-files.json) · [Ausgangsinventar](../artifacts/test-results/g2-04-i3-f2/after.json) · [Vergleich von HEAD, Master und Index](../artifacts/test-results/g2-04-i3-f2/git-final.json) · [Reproduktionsskript](../artifacts/test-results/g2-04-i3-f2/validate-final.ps1)

**Offen** bleiben native 100 % und 125 % DPI, manuell gehörte Screenreader-Sprachausgabe und die spezielle fensterübergreifende Maus-Redock-Sequenz. Die historische Tabrückkehr innerhalb des Hauptfensters bei 150 % ist ein eigener Prüffall. Native Tastatur-, Focus- und HWND-Prüfungen haben jeweils engeren Umfang. Die breiten Core-/MCP-Suiten und der native 255-Share-Clipboard-Oracle sind übernommene frühere Nachweise. [Grenzen und Originalverweise aus R2](G2-04-02-I3-R2-independent-re-review.md)

**READY FOR INDEPENDENT RE-REVIEW.** Ich empfehle R3, die beiden R2-Gegenproben und die Originaldatenpfade auf diesem lokalen Stand unabhängig zu wiederholen. Die Entscheidung zur Abnahme trifft der Owner.

