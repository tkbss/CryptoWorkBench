# MCP-01.4 – Architekturvarianten nach U0

Stand: 24.10.2026. **Entscheidungsvorlage, keine Freigabe.** Bestehende Architekturentscheidungen, B2-R1, B1, U0 und Solltests bleiben unverändert. MCP-01.4 bleibt BLOCKED.

**Empfehlung an den Projekteigentümer:** Als nächste Entwurfsrichtung eine **alternative, bibliotheksseitig kontrollierte Document-Propertyarchitektur** wählen: genau eine autoritative Dokumentbindung, lesbare Documentoberflächen aller drei Mitglieder und ein expliziter, synchron zugelassener Bindungswechsel. Keine beliebigen eingehenden Documentbindings und keine öffentlich unabhängig beschreibbaren Teilbindungen. Die generische frühe Text-/History-/Composite-Mutationskontrolle aus Variante B bleibt zusätzlich notwendig. Das ist ein Vorschlag für einen neuen Designauftrag mit anschließendem isoliertem Nachweis, keine Aufforderung, jetzt zu implementieren.

Die geringe produktive Nutzung dynamischer Dokumentzuweisung spricht gegen die Kosten eines allgemeinen Avalonia-Frameworkumbaus allein zur Bewahrung beliebiger StyledProperty-Bindings. Sie beweist aber **nicht**, dass ein Konventionsverbot ausreichend wäre. Soll der bisherige vollständige schreibbare StyledProperty-/Bindingvertrag einschließlich P04/P06 unverändert erhalten bleiben, ist stattdessen eine Frameworkerweiterung zu prüfen. Die ursprüngliche Alternative A genügt der hier verlangten Laufzeitabsicherung öffentlich erreichbarer Mutatoren nicht.

## 1. Basis und tatsächlicher Prüfungsumfang

Geprüft wurde der bestehende Arbeitsstand einschließlich uncommitteter/unversionierter Arbeiten im produktiven Worktree `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Der geöffnete Scratchbereich unter `Documents/ChatGPT/CryptoWorkBench-Mcp` ist ein anderes Verzeichnis.

Produktquellen: alle über die repositoryweite C#-/XAML-Suche gefundenen Documentzuweisungen, DocumentProperty-, TextArea.Document- und TextView.Document-Verwendungen; GUI-XAML-/Propertyzugriffe; WorkbenchTextEditor, Editor-View/VM, Completioncontroller, EditorBridge, Edit-/Savepfade sowie relevante Identitäts- und Callbacktests. Nicht nach produktiven Anforderungen und Gegenproben zu unterscheiden würde den Umfang verfälschen: Tests enthalten deutlich mehr Dokumentwechsel als die GUI.

Verbindliche Kontextquellen: [MCP-00 §§4.4–4.5/5.1](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-00-architecture.md), [Editorentscheidung, insbesondere Alternative A §§4.1–4.4 und B §5](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-editor-architecture-decision.md), [Review einschließlich BLOCKED-Nachtrag](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-review.md), [B2-R1 §2.3](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md), [R1-Re-Review](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-R1-independent-review.md), [U0 samt P03/P04/P06/P07/P09](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-U0-property-feasibility.md), [B1](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B1-feasibility.md) und [U12](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-avalonia12-feasibility.md).

Bibliotheksanalyse am vorhandenen Original **AvaloniaEdit 12.0.0**, Commit `86fdebec4cff7affb0e14c7885df71d28edce777`, insbesondere TextEditor, TextArea, DefaultInputHandlers, EditingCommandHandler, SelectionMouseHandler und Options. Frameworkreferenz: die unveränderten U0-Quellen von **Avalonia 12.0.0**, Commit `220dea3f5e18002ff20a6603e70eb633eaa73b4c`. Keine Aussage über eine inzwischen veröffentlichte Upstreamlösung oder deren Annahme; das wurde nicht recherchiert oder zugesagt.

**S** = statischer Quellbefund dieser Runde; **E-U0/B1** = vorhandener experimenteller Nachweis, hier nicht erneut ausgeführt; **D** = neuer Designvorschlag; **O** = offener Nachweis/Entscheid. Es gab keine Builds, Restores, Laufzeittests, GUI-Migration oder Implementierung in dieser Runde. Produktiv sind weiterhin Avalonia **11.3.20** und AvaloniaEdit **11.4.1** gepinnt, siehe [Projektdatei:22](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/CryptoWorkBenchAvalonia.csproj:22). Zielkompatibilität mit 12 ist daher keine bereits erfolgte Produktmigration.

## 2. Tatsächliche Produktnutzung

### 2.1 Document, Properties und Lebenszyklus

| Pfad / genaue Fundstelle | Tatsächliche Nutzung S | Produktbedarf und Bedeutung |
| --- | --- | --- |
| [CryptoScriptEditView.axaml:10](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml:10) | WorkbenchTextEditor mit literalem `Text="Hello AvaloniaEdit!"`, Zeilennummern, Font, Scrollbars, WordWrap. **Kein Documentbinding** | Editorinitialisierung und Darstellung; ein Textwert ist kein Dokumentidentitätsbinding |
| [View-Konstruktor:35](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs:35) | FindControl als **TextEditor**, Clear, Zuweisung der Editorreferenz an VM | Basistypzugriffe sind heute produktiv. Eine nur versteckte Property eines abgeleiteten Controls schützt sie nicht |
| [View Attach/Detach:43](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs:43) | VM-Editor setzen/registrieren, bei Detach abmelden und Completioncontroller entsorgen | Echte Verfügbarkeit/Lifecycle müssen erhalten bleiben. Visual-Detach ist **kein** produktiver `Document=null`-Setter |
| [VM OnHistoryChanged:127](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:127) | Document lesen, letzte Zeile per Remove/Insert ändern, **dieselbe Instanz** bei Zeile 136 zurücksetzen | Einzige gefundene produktive explizite `.Document = …`-Zuweisung; kein funktionaler Wechsel auf ein neues Dokument. Eine spätere Entfernung dieser redundanten Zuweisung wäre ein gesonderter Codeauftrag |
| [WorkbenchTextEditor:43](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs:43) | Metadaten-Coercion nur für den äußeren Ownertyp; bei Lock anderer Wert verboten; ObserveDocument bindet UpdateStarted/Changing | Aktueller Guard, kein Beweis vollständiger Bibliothekssicherheit. Keine analoge produktive Coercion für TextArea/TextView. Die seit U0 bekannten Binding-/Prioritätslücken bleiben |
| [WorkbenchTextEditor:26](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Controls/WorkbenchTextEditor.cs:26) | Undo/Redo lesen **TextArea.Document** und dessen Stack | Eine abweichende TextArea-Bindung wäre sicherheitsrelevant: UI könnte anderes Dokument als EditorBridge bearbeiten |
| [Completioncontroller:30](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs:30), [Complete:400](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/InfoCompletionController.cs:400) | Hält TextEditor; beobachtet TextArea.DocumentChanged, liest aber Editor.Document; tatsächlicher Completionedit über `textArea.Document.Replace` | Konsistenz aller drei Ebenen ist nötig, auch ohne produktiven inneren Documentsetter. Ein auseinanderlaufender Verbund ist kein harmloser Darstellungsfehler |
| [EditorBridge Registration:24](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs:24), [ObserveDocument:115](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/Automation/EditorBridge.cs:115) | Hält echte Dokument-/Versionsreferenz; liest tatsächliche Editor.Document-Identität, erneuert editorId bei Wechsel, erhöht Revision, wechselt Changed-Subscription | Unterstützung und Beobachtung von Dokumenttausch sind vorhanden. Ein exklusiver Editor.Document-Beobachter entdeckt keinen bloßen inneren TextArea-/TextView-Wechsel zuverlässig |
| [VM TextEditor:89](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:89) | Öffentlicher getter/setter für komplette TextEditor-Referenz; Identity-/Mutation-Locks begrenzen Wechsel | Neuer Editor/neue View bleibt erforderlich; keine Notwendigkeit, dafür beliebige Document-Stylebindings zu erhalten. Rohreferenzen sind heute tatsächlich exponiert |
| Gesamte produktive C#-/XAML-Suche | Kein explizites `TextArea.Document =`, `TextView.Document =`, `Document="{Binding …}"`, Document-Style/Setter oder Document-Bind/SetValue/SetCurrentValue/ClearValue gefunden | **Nicht als UI-Anforderung nachgewiesen:** eingehende Documentbindings, Prioritätsanimation von Document, freie Teilbindungen. Keine Behauptung, solche öffentlichen Aufrufe seien unerreichbar |

Die Bibliothek selbst stellt die anfängliche Dokumentinstanz bereit und kaskadiert Editor→Area→View. Für die 12er-Basis belegen [TextEditor:137](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/TextEditor.cs:137) und [TextArea:293](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/Editing/TextArea.cs:293) diese internen Schreibwege. „Keine produktiven inneren Setter“ erlaubt deshalb nicht, interne Bindung oder ihre Ressourcenpflege zu entfernen.

### 2.2 Textmutationen und GUI-Funktionen bleiben nötig

| Funktion | Fundstelle / tatsächlicher Pfad | Von einer Bindungsbeschränkung allein gelöst? |
| --- | --- | --- |
| Historyzeile einsetzen | VM:127–137, Remove + Insert auf vorhandenem Dokument | Nein; frühe Text-/Gruppenzulassung nötig |
| Datei/ScriptBook laden und ausführen | [VM:243](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/ViewModels/CryptoScriptEditViewModel.cs:243): festgehaltenes Dokument, Lifecycleprüfung; `.Text = string.Empty`, anschließend Insert pro Zeile | Kein Dokumentersatz nötig. Asynchrone Zielbindung, Callback-/Partialfehler und Admission bleiben eigenständige Anforderungen |
| Ausgabe nach Return | [View:100](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Views/CryptoScriptEditView.axaml.cs:100): await, Documentidentität/Lifecycle/Anchor, Insert | Keine Wiederholung auf Nachfolger; neue Architektur muss ABA/Detach weiter erkennen |
| MCP-Insert/Append/Replace | [WorkbenchEditorOperations:80](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs:80), :108–145: No-op, echte Dokumentmutation, eine Gruppe, ursprüngliche Identität | Generische frühzeitige Bibliothekskontrolle bleibt nötig; voller Textersatz ist kein Grund für neue Dokumentinstanz |
| Bookausgabe | [WorkbenchEditorOperations:240](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchEditorOperations.cs:240) | Bestehendes gebundenes Dokument wird verändert; kein Documentbinding |
| Save | [WorkbenchSaveCommit:32](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchAvalonia/Services/Automation/WorkbenchSaveCommit.cs:32) | Snapshot, Completion vor Leasecallbacks, Revalidierung, Delete/Write und Drain bleiben Hostverantwortung. Ein eingefrorener Writertext allein sperrt GUI-/Historyänderungen nicht |
| Completion/Overloadhilfe, Caretstatus | InfoCompletionController; View:118–145 | Popups, Anchors, TextArea.Replace und Caretmeldungen bleiben. Fremdcallback darf keine interne Berechtigung erhalten |
| Tippen/IME, Auswahl, Clipboard, Undo/Redo, Einrückung, Drag/Drop | Bibliotheks-DefaultInputHandler; produktiv nur Undo/Redo und TextEntering gezielt ergänzt | Nicht wegen fehlender Hostcalls aus der Schutzfläche streichen. Native asynchrone und mehrteilige Operationen benötigen eigene Gates |
| Suche/Ersetzen | 12er-[TextEditor.OnApplyTemplate:307](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u12/upstream/AvaloniaEdit-12.0.0/src/AvaloniaEdit/TextEditor.cs:307) installiert SearchPanel automatisch | Für Zielversion nativ erreichbar; fehlendes `SearchPanel.Install` im Host ist kein Nachweis deaktivierter Funktion. Nutzerhäufigkeit wurde nicht gemessen |

In der 12er-Basis aktiviert der DefaultInputHandler Editing und SelectionMouseHandler; TextDragDrop ist standardmäßig true, DefaultIndentationStrategy registriert. Diese statischen Befunde sind keine neue native Plattformabnahme. Keine Funktion wird in dieser Vorlage still deaktiviert.

### 2.3 Dokumenttausch: Tests und Vertrag sind mehr als heutige GUI-Calls

[EditorAutomationTests:199](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchGuiTest/EditorAutomationTests.cs:199) verlangt fehlendes Dokument→Unavailable und Wiederherstellung mit neuer Identität; [:209](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchGuiTest/EditorAutomationTests.cs:209) prüft Attach/Detach/Swap und Revision. [:145](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchGuiTest/EditorAutomationTests.cs:145) enthält sogar Document=null aus einem Changed-Observer. [EditorReviewCorrectionTests:290](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/CryptoWorkBenchGuiTest/EditorReviewCorrectionTests.cs:290) versucht Dokument-/Editorwechsel während kritischer Operationen.

Das sind zum Teil absichtliche Gegenproben, keine versteckten XAML-Anforderungen. Trotzdem sind sie bestehende Sollverträge. Ein dauerhaft festgebundenes Dokument würde erlaubte Idle-Wechsel und einige bisherigen Callbackfälle ändern. Ein neues Bindungsverfahren müsste ihre Semantik ausdrücklich abbilden oder eine vom Eigentümer genehmigte Abweichung benennen. Weder Tests löschen noch bestehende rote Fälle als „durch Einschränkung repariert“ zählen.

## 3. Sicherheitsmaßstab nach U0

Die Absicherung benötigt zwei getrennte Grenzen:

1. **Text-/Historien-/Compositegrenze am tatsächlichen Dokument und Editor.** Direkte `.Document.Insert`, Basistyp-Text/Clear/SelectedText, Begin/EndUpdate, Undo-/Redo- und Provider-/Eventpfade dürfen nicht durch Hostwrapper oder geerbte Callbackrechte an einer aktiven Sperre vorbeikommen. B1 ist begrenzte Evidenz für diese Richtung, kein fertiges B2-R1-Release.
2. **Identitäts-/Bindungsgrenze am gesamten Verbund.** Kein öffentlich erreichbarer Propertypfad darf einen Teil auf ein anderes, ungeprüftes Dokument umleiten. Ein nur an A installierter Textguard schützt kein nachträglich über Area/View gebundenes B. Alle neuen Dokumente müssen vor ihrer Veröffentlichung die nötige Policy/Ownership besitzen.

**E-U0:** P04 identische Emission wird vor Coercion gefiltert; P06 trennt alte Subscription vor Veto; P03/P07 verändern Prioritätszustand trotz altem sichtbaren Wert; P09 zeigt unvollständigen Callbackrahmen des untersuchten Coordinators. `notifying` ist **intern**. Keine Variante darf diese Tatsachen durch bloß andere Namen oder einen Changed-Rollback umgehen.

Der Maßstab ist Konsistenz gegenüber normalen öffentlichen APIs und reentrantem Anwendungscode, keine Sandbox gegen beliebigen voll vertrauenswürdigen Prozesscode, private Reflection oder absichtliche Frameworkmanipulation. **Schon normale** SetValue/Bind/Dispose- und Basistypzugriffe fallen nicht unter diese ausgeschlossene Angreiferklasse.

## 4. Vier Varianten

### 4.1 Eingeschränkter Bindingvertrag bei bestehenden StyledProperties

**Konventionsvariante – nicht ausreichend:** „Nur TextEditor.Document setzen, keine Bindings/inneren Setter“ reduziert aktuelle Callsites, sperrt die anderen APIs aber nicht. Sealed WorkbenchTextEditor, private VM-Felder, Analyzer oder `new Document` sind keine vollständige Laufzeitgrenze. Inneres TextArea/TextView bleibt erreichbar; bestehende Dokumentreferenzen bleiben mutierbar. Eine neue CLR-Methode um das alte Framework-Bind herum fängt direkte AvaloniaObject-Aufrufe nicht ab.

**Stärkerer Kandidat D:** genau ein Dokument je strikter Editorlebensdauer; alle drei effektiven Propertywerte dauerhaft an diese autoritative Instanz binden, unterschiedliche Angebote stets abweisen, niemals einen gespeicherten „desired document“-Wert nach Entsperrung zulassen. Neues Dokument nur durch einen neuen kontrollierten Editorlebenszyklus. Damit könnte P04 als eingehender Bindingvertrag entfallen, anstatt fälschlich zu bestehen. Text-/Historyschutz bleibt separat verpflichtend.

**Durchsetzbarkeit O:** Noch nicht bewiesen. Coercion muss nicht nur neue Werte, sondern Default/null, Clear, Prioritäten und alle drei Owner dauerhaft absichern; kein unveränderter sichtbarer Wert darf als Beleg intakter Engine-/Bindingzustände gelten. Vor allem kann weiterhin kein neutraler Erhalt fremder Bindings vor Dispose zugesagt werden. Eine Sperre, die nach Veto versteckte Werte später übernimmt, ist ausgeschlossen. Dauerhaftes Einfrieren löst außerdem nicht automatisch den Callbackrahmen für Text-/Editoroperationen.

Der Eigentümer müsste daher den Binding-/Subscriptionvertrag **wirklich ändern** und lebenslange Identitätsbindung einschließlich Tests genehmigen. Ohne neuen Gegenbeweis der Umgehbarkeit ist diese Variante kein „technisch erzwingbar“ bestätigter Weg. Sobald schreibbare Frameworkproperties entfernt oder durch eine eigene autoritative Oberfläche ersetzt werden, handelt es sich sachlich um Variante 4.3.

### 4.2 Erweiterung des Avalonia-Propertysystems

**D:** Früher Transaktionseintritt vor Bind-/Dispose-/Clear-/Prioritätswirkung, beobachtbare Identität neuer Quellemissionen vor Gleichheitsfilterung, eindeutig konsumierte Ablehnungen und garantierter ausnahmesicherer Abschluss über Fanout und No-op-Auswertung. Die bisherige notifying-Option nur öffentlich zu machen reicht nicht.

**Stärke:** Einziger hier betrachteter Weg, der den bisherigen **vollen** universellen StyledPropertyvertrag einschließlich Subscriptionneutralität und erneuter identischer Quellemission prinzipiell erhalten könnte, ohne ihn einzuschränken. Gemeint ist ein neuer Frameworkentwurf, keine belegte fertige Lösung. Auch dann muss AvaloniaEdit die Hooks korrekt nutzen und echte Ressourcen-/Eventfinalisierung implementieren.

**Kosten:** Zwei Bibliotheksebenen, neue Frameworksemantik, Legacy-/ABI-/Paketgraph-/Plattformregressionen und Maintainerabhängigkeit. Ein Avalonia-Fork beträfe wesentlich mehr als den Editor; übrige Controls, Prism und transitive Pakete müssen zu demselben konsistenten Frameworksatz passen. Kein zugesagter Upstreamtermin. Für die aktuelle GUI wird eine Flexibilität finanziert, die ihre Documentverwendung nicht benötigt; für eine allgemeine Bibliotheks-API kann sie dennoch gewollt sein.

### 4.3 Alternative Document-Propertyarchitektur – bevorzugte Entwurfsrichtung

**D, noch keine Machbarkeitsabnahme:** Die authoritative Dokumentbindung wird bibliothekseigener Zustand, nicht das Ergebnis dreier unabhängig beschreibbarer StyledProperty-Stores. `TextEditor.Document`, `TextArea.Document`, `TextView.Document` lesen diesen konsistent koordinierten Zustand. Eingehende Binding-/Stylewerte sind kein unterstützter Bindungsbefehl. Ein expliziter synchroner Operationsweg führt Initialisierung, Wechsel und Detach aus, mit reserviertem Verbund, alter/neuer Policy, genau einer Generation, vollständiger Ressourcenfinalisierung und Fehlerzustellung.

Eine mögliche öffentliche Darstellung sind **read-only DirectProperties** ohne Frameworksetter. Avalonia bietet dafür `RegisterDirect` mit optionalem Setter an ([Originalquelle:439](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/sources/AvaloniaProperty.cs:439)). Das ist ein statisch vorhandener Baustein, **kein** Nachweis, dass die gesamte Architektur oder jeder untypisierte Bind-/Clearpfad bereits sicher ist. Eine schreibbare DirectProperty allein löst P06 gerade nicht.

Der Entwurf muss folgende Grenzen einhalten:

- **Keine Schatten-Property:** Ein neues read-only Feld über geerbten beschreibbaren Documentproperties reicht nicht. Kein erreichbarer alter Basistyp-/Propertypfad darf weiterhin den effektiven Dokumentzustand irgendeines Mitglieds verändern. Das erfordert Bibliotheksänderung an den echten Typen oder wirklich eigenständige strikte Typen ohne diesen beschreibbaren Altpfad; kein bloßes Host-Subclassing.
- **Explizite Operation statt nachträglicher Reparatur:** Der Bindungsbefehl prüft vor Wirkung und besitzt seinen eigenen try/finally-Rahmen bis nach eigenen und Frameworkmeldungen. Keine Auswertung eines fremden Delegate unter interner Autorität. Kein öffentliches allgemeines Execute(Action).
- **Eindeutiger Commit:** Vor Commit Ressourcen vorbereiten, danach intern vollständig fortführen oder Faulted. Frameworkobserverfehler dürfen nicht die notwendige interne Fortführung abbrechen. Nicht garantierbare fremde Fanouts getrennt dokumentieren.
- **Kein zweites Dokument:** Es bleibt die tatsächliche native TextDocument-Instanz mit ihren Anchors und ihrer Undo-Historie. Für normale Textänderungen wird sie nicht ausgetauscht. Ein Wechsel ist ein Identitätsereignis, kein Rollback und kein Ersatz für Textmutation.
- **Fail-closed auch über Rohzugriffe:** Öffentliche Property-/Bindingversuche besitzen keinen Schreibzugang zur autoritativen Bindung. Unsupported muss über den spezifizierten Exception-/Bindingfehlerweg erkennbar sein; insbesondere darf nichts verzögert wirksam werden. Gehaltene TextDocument-Referenzen bleiben durch das separate frühe Dokumentgate geschützt.

**Kompatibilitätspreis:** Bestehende Documentsetter, der öffentliche StyledProperty-Feldtyp, XAML-Targetbindings, Styles und Teilbindungen ändern ihren Vertrag. Ein Austausch StyledProperty→DirectProperty ist kein binärkompatibler Patch. Ein opt-in neuer strikter Typensatz könnte Legacytypen erhalten, erfordert aber explizite Typ-/Abhängigkeitsabgrenzung; unveränderte Completion-/Search-/Extensionpakete sind nicht automatisch kompatibel. Alle tatsächlichen Bibliotheksaufrufer müssen neu geprüft/ggf. kompiliert werden. Die konkrete API-/Typstrategie bleibt ein Maintainerentscheid vor Implementierung.

Für CryptoWorkBench ist der direkte Anpassungsbedarf der **Documentzuweisung** gering. Dagegen bleiben der Bibliotheksumbau aller drei realen Lebenszyklen und der vollständige Mutationsteil anspruchsvoll. Dynamischer Dokumenttausch lässt sich als explizite Operation weiter vorsehen; lebenslang unveränderliche Dokumentidentität ist bei dieser Variante keine notwendige Einschränkung.

### 4.4 Ursprüngliche Alternative A – Hostadapter mit vertrauenswürdigem Backend

Die ursprüngliche Alternative A vermittelt Host- und inventarisierte GUI-Schreibwege, behandelt AvaloniaEdit-Rohobjekte aber als vertrauenswürdige Backenddetails. Sie benötigt eigene Eingabe-/Command-/Asyncintegration, darf keine Rohreferenzen an normale Anwendungs-Callbacks ausgeben und muss den problematischen UpdateStarted/EndUpdate-Guard ersetzen.

**Sicherheitsgrenze:** Aufbewahrte Basistyp-/Dokumentreferenzen können den Adapter weiterhin umgehen. Das war im ursprünglichen Entscheid ausdrücklich als abgeschwächter Vertrag benannt. Public VM.TextEditor, Completionprovider mit TextArea, Visual-/Controlzugriff und echte Dokumentevents machen diesen Unterschied im Produkt konkret. Eine eingeschränkte Schnittstelle und statische Zugriffstests vermindern versehentliche Nutzung, erzwingen die geforderte Laufzeitgrenze aber nicht.

A ist daher **unter der aktuellen Anforderung nicht zu empfehlen**. Eine Wahl wäre nur nach ausdrücklicher Änderung von MCP-00 und Aufgabe genau benannter Raw-/Callbackgarantien konsistent. R2/R4/R6/R7 wären dann teilweise aus dem neuen Vertragsumfang genommen, nicht technisch behoben. Die alten Gegenproben bleiben unverändert Evidenz. Erweitert man A um echte Bibliotheksgates gegen diese Rohzugriffe, ist es nicht mehr A allein.

## 5. Begründete Vergleichsmatrix

Aufwände sind relative fachliche Einschätzungen, keine gemessenen Termine. Die früheren Personentagschätzungen vor U0 werden nicht als weiterhin gültig übernommen.

| Kriterium | Eingeschränkte StyledProperty-Bindung | Avalonia-Erweiterung | Alternative Documentarchitektur | Ursprüngliches A |
| --- | --- | --- | --- | --- |
| Sicherheit der Mutationssperre | Konvention unzureichend. Dauerhafte Bindung nur Kandidat; sämtliche Rohpfade und Coregate neu nachzuweisen | Potenziell umfassend, wenn neue frühen Hooks und AvaloniaEdit-Core korrekt; O | Strukturell kleinerer Schreibzugang; autoritative Bindung + Coregate, neue Beweislast O | Nur vermittelte Wege; offene Rohumgehung widerspricht aktueller Anforderung |
| U0 P04/P06 | Nicht behoben. Eingehende Bindings und Subscriptiongarantien müssten entfallen; kein stilles Nachholen erlaubt | Genau diese Frameworkgrenzen müssten neu gelöst werden | Kein schreibbares Documentbinding mehr; expliziter Wechsel ersetzt diesen Vertrag. Unsupported-Pfade müssen wirkungslos bleiben | Problempfade werden aus Vertrauensgrenze ausgeschlossen, nicht abgesichert |
| MCP-00 | Nur bei sicherer Sperre aller echten Textwege und genehmigtem Identitäts-/Bindingumfang; Konvention genügt nicht | Stärkste Chance, bestehende Semantik vollständig zu erhalten | Hostsemantik grundsätzlich erhaltbar; API-/Bindingvertrag ausdrücklich neu entscheiden | Explizite Abschwächung „alle Mutationswege“/Callbackgarantie erforderlich |
| R2/R4/R6/R7 | Propertyeinschränkung allein löst keinen der Text-/Abschlussblocker | Weiterhin Bibliotheks-Core-/Compositekorrektur erforderlich | Ebenfalls Core-/Compositekorrektur; explizite Bindungsoperation verbessert nur Identitätskontrolle | Raw-Gegenbeispiele bleiben; keine unveränderte volle Abnahme |
| Avalonia 12 | Framework unverändert, aber E-U0 widerlegt universellen Vertrag | Verändertes Framework erforderlich bis geeignetes offizielles Release verfügbar | Unveränderte öffentliche Frameworkbausteine als Ziel; neue Probe erforderlich | Unverändertes Framework möglich, keine starke Raw-Garantie |
| AvaloniaEdit 12 | Bibliotheksänderung für alle Owner plus Textkontrolle weiterhin nötig | Bibliotheksänderung zusätzlich zum Framework | Deutliche Änderung der Documentoberfläche und internen Verdrahtung plus Core | Originalbackend möglich; zahlreiche Hostadapter/Defaultpfade ändern |
| API-/Binarykompatibilität | Feldtypen eventuell gleich, Semantik eingeschränkt; dauerhafte Bindung bricht Wechselzusagen | Potenziell additiv, Legacykompatibilität nicht automatisch gegeben | Bewusster API-/ABI-Bruch oder neuer Typensatz; kein Drop-in-Ersatz behauptet | Bibliotheks-ABI eher erhalten, Host-API/Callbackvertrag schwächer |
| Implementierung | Mittel bis hoch; geringe Callsitezahl verdeckt schwierige Durchsetzung | Sehr hoch, zwei Ebenen und Frameworkregressionen | Hoch, aber auf Editorbibliothek begrenzbar; native Engine weiterverwenden | Hoch bei vollständiger GUI-Umleitung; kein kleiner Wrapperfix |
| Wartung | Policies/Metadaten/Storeverhalten je Upgrade erneut prüfen | Höchste laufende Abhängigkeit, ggf. Framework- und Editor-Patchlinien | Editor-Patchlinie/Kompatibilitätstests; deutlich weniger Frameworkbreite | Adapter und private/native Handler bei jedem Upgrade nachinventarisieren |
| GUI-Auswirkungen | Heute kaum Bindings zu migrieren; ggf. neuer Editor pro Dokument, Idle-Swap-/Recoveryfolgen | Größter Frameworkregressionsradius, reguläre UI-Funktionen sollen bleiben | Document-Lesezugriffe/Visuals können fachlich bleiben; innere Bindung, Completion/Search und Lifecycle anpassen | Breiter Umbau von Completion, Output, History, Eingabe, Paste/Drag/Search |
| Eigene Pakete/Forks | Geänderte AvaloniaEdit-Bibliothek bis Upstreamannahme; kein Frameworkfork beabsichtigt | Ggf. eigener abgestimmter Avalonia-Paketsatz **und** AvaloniaEdit | Geändertes AvaloniaEdit-Artefakt/neue strikte Bibliothek; kein Frameworkfork als Ziel | Kein eigener Bibliotheksfork zwingend; Kosten verlagern sich in Hostbackend |
| Aktueller Nachweisstatus | **Nicht freigabefähig** | **Nicht implementiert/nachgewiesen** | **Empfohlener neuer Entwurf, nicht nachgewiesen** | **Erfüllt aktuelle Sicherheitsanforderung nicht** |

## 6. R2/R4/R6/R7 und MCP-00 einzeln

| Verpflichtung | Unverzichtbar unabhängig vom gewählten Propertymodell |
| --- | --- |
| R2: Save sperrt alle unterstützten Mutationen | Frühe Sperre vor Text, Gruppen, Caret-/History-Nacharbeit; auch gleicher Text/leerer Setter. Identitätsfixierung oder IsReadOnly allein reicht nicht |
| R4: ursprüngliche Identität und konsistenter Abschluss | Dokument/Editor/Generation festhalten; Wechsel im kritischen Vorgang verweigern; nach Texteffekt korrektes Ergebnis der ursprünglichen Instanz, keine halb gebundene Nachfolgerantwort |
| R6: leerer Basistyp-Textsetter aus UpdateStarted | Gesamten Compositeeintritt sperren, nicht erst Textänderung. Propertyumbau betrifft Document, nicht automatisch den geerbten Text-/Clear-Pfad |
| R7: rekursiver UpdateFinished-Normalisierer | Abgewiesene Mutation darf keine neue Gruppe öffnen und durch kompensierendes EndUpdate neue Abschlusscallbacks erzeugen. Kein Retry/Queue und kein generisches geerbtes Callbackrecht |
| MCP-00 §4.4 | Wirklicher Editor als Wahrheit, stabile Identität, monotone Revision, revisionsgebundene No-ops/Bestätigung, reale Undo-Gruppe und Anchors; kein Schattenmodell |
| MCP-00 §4.5 | Save-Completion vor beobachtbaren Leasecallbacks, vollständige Save-Mutationssperre bis Writerende, bestätigter Snapshot, Delete-vor-Write ohne neue Cancellationlücke, Drain |
| MCP-00 §5.1 | Eine globale fail-fast Admission für Run/Book/History/MCP/Save; gewöhnliche Benutzereingabe darf während Scriptausführung weitergehen. Kein pauschaler globaler Editorlock als Vereinfachung |

Bei bevorzugter Variante 4.3 ändern sich bibliotheksseitige Document-Schreiboberflächen, **nicht automatisch** die fachlichen MCP-DTOs. Trotzdem brauchen die bestehenden Dokumentwechsel-/Callbackzusagen und Tests eine explizite Disposition. Nicht genehmigte Semantik darf nicht unter gleicher DTO-Signatur abgeschwächt werden. Die weiteren offenen R1-Reviewpunkte (historische UI-Aktionen, Planräume, Faulted-Quarantäne, Textsetter-Nacharbeit und Gates) bleiben unabhängig davon offen.

## 7. Durchsetzung und erforderliche Gegenproben vor jeder Freigabe

Die Frage ist nicht nur „welche Setter nutzen wir?“, sondern „wer kann die autoritative Bindung oder den Inhalt während der Sperre tatsächlich noch verändern?“. Folgende Abnahme wäre für einen später genehmigten Entwurf erforderlich; sie wurde hier **nicht ausgeführt**:

| Angriff / Zugang | Verbindliches Soll |
| --- | --- |
| CLR- und Basistypzugriff auf alle drei Documentoberflächen | Kein unabhängiger Teilwechsel. Bei neuer read-only API Compilefehler für nicht vorhandene Setter; untypisierte Frameworkzugriffe trotzdem gesondert testen |
| SetValue, SetCurrentValue, ClearValue, Default/null, Style/Animation | Keine Änderung autoritativer Dokumentidentität/Generation, Ressourcen, Auswahl oder History durch nicht unterstützte Wege; keine spätere Übernahme nach Entsperren |
| Observable-/BindingBase-Binding, gleiche/neue Emission, Ersatz/Dispose | Für Variante 4.3 keine akzeptierten eingehenden Bindings auf die effektive Documentproperty. Spezifizierter Fehlerweg und unveränderte Bindungsautorität. Keine heimliche Zusage der alten Subscriptiontransaktion; gewünschte externe Bindings über neue APIs wären ein eigener Designumfang |
| Versteckte/geerbte alte DocumentProperty, anderer Owner, direkte TextArea/View-Referenz | Kein beschreibbarer Altpfad darf eine operative Teilbindung verändern. Analyzer und API-Snapshots ergänzen die Laufzeitprobe, ersetzen sie nicht |
| Alte/neue gehaltene TextDocument-/Undo-/Selectionreferenz | Unterstützung gemäß expliziter Raw-Grenze; alle zugesagten direkten Text-/Compositeeintritte vor Wirkung gesperrt. Neues Dokument vor Veröffentlichung geschützt, Policywechsel während Vorgang verboten |
| Fremder Callback aus Policy, Framework, Completion, Indentation, Update/History/Caret | Kein geerbtes Recht; keine neue Mutation oder Bindungsänderung. Fehler nach Commit müssen notwendigen internen Abschluss zulassen |
| A→B→A, Detach/Reattach, Recovery nach Faulted | Monotone Generation; alte Asyncarbeit bleibt stale, auch bei gleicher Referenz. Explizites Ersatzverfahren statt Fehlerrückrollen |
| Fehler am zweiten/dritten realen Mitglied, früh werfender Frameworksubscriber | TextEditor/TextArea/TextView einschließlich Weak-/Undo-Subscriptions, HeightTree, Caret/Selection entweder konsistent neu oder ausdrücklich Faulted/quarantänisiert |
| Realer Save-Commit sowie R6/R7-Kontrollen | Während Delete-await gehaltene Rohreferenzen, No-op-Textsetter, rekursiver Normalisierer; vollständige Nachweise für Historie, gruppengeschlossenen Zustand, Folgeedit, Ergebnis und Drain |
| Alle heutigen GUI-Funktionen und Legacy | Completion, Return/Output, History, Import, Paste/Cut/DragMove, Suche/Ersetzen, Undo/Redo, IME, native Plattformen. Nicht behaupten, Defaultfeatures seien inaktiv, weil der Host sie nicht selbst registriert |

P04/P06 dürfen für den **alten** Vertrag nicht nachträglich grün umdefiniert werden. Ein genehmigter neuer read-only Bindingvertrag bekommt eigene Solltests; U0 bleibt unverändert negativer Nachweis des alten Designs. Soweit alte Tests konkret den Documentsetter verlangen, ist ihre fehlende API-/Semantikkompatibilität offenzulegen. Die unabhängigen Text-/Callbackgegenbeispiele R6/R7 bleiben weiterhin zu erfüllen, nicht wegzudefinieren.

## 8. Paketstrategie, Entscheidung und nächste Grenze

Eine geänderte Editorbibliothek benötigt bis zu einem geeigneten offiziellen Release ein ausdrücklich genehmigtes reproduzierbares Artefakt: feste Quellrevision, Patch, eigenes eindeutiges Paket-/Versionsschema, festgelegte Assemblyidentität, unverwechselbarer Dependencygraph und Regressionen. Keine lokale Überschreibung des offiziellen Pakets unter gleicher Identität/Version. „Neuer NuGet-Name“ allein verhindert keine gleichnamigen Assembly-/Typkonflikte. Upstreamziel, Wartungsverantwortlicher, Rückkehrpfad und Abbruchbudget müssen feststehen.

Für Variante 4.3 bedeutet Exit insbesondere API-Migrationsaufwand zurück zu einer später akzeptierten Upstreamoberfläche; ein bewusst inkompatibler Fork hat keinen kostenlosen Paketwechsel als Exit. Für Variante 4.2 müsste zusätzlich der gesamte Avalonia-Paketsatz konsistent gepflegt werden. Für A entfällt dieser Paketaufwand eventuell, aber der dauerhafte Hostadapter und seine Sicherheitsausnahmen bleiben.

**Konkret vorgeschlagene Eigentümerentscheidung, noch nicht getroffen:**

1. Die vollständige Text-/Save-/Callbackabsicherung nach MCP-00 und R2/R4/R6/R7 bleibt Ziel. Keine Freigabe der ursprünglichen Alternative A unter dem unveränderten Vertrag.
2. Dynamische eingehende Documentbindings und unabhängig schreibbare Teilbindungen werden als **nicht benötigte Produktfunktion** zur Aufgabe vorgeschlagen. Idle-Dokumentwechsel/Detach bleiben über eine explizite, geschützte Operation fachlich möglich. Das ersetzt B2-R1 §2.3 erst nach genehmigter neuer Designfassung; heute gilt keine Änderung.
3. Einen begrenzten Designauftrag für Variante 4.3 vorsehen: autoritative Bindung und read-only Propertydarstellung, genaue API-/Binaryfolgen, Error-/Callback-/Faultedmodell und Disposition vorhandener Tests. Keine bloß versteckten Setter oder Restpfade über Basistypen.
4. Erst nach dessen Review einen getrennten isolierten Framework-/Editor-Proof beauftragen. Alle öffentlichen Umgehungswege prüfen, anschließend die realen TextEditor-/TextArea-/TextView-Lebenszyklen. Kein Start von U1–U4 aus dieser Vorlage ableiten.
5. Falls der Eigentümer den bisherigen schreibbaren StyledPropertyvertrag und seine volle Bindingsemantik behalten will, stattdessen Variante 4.2 samt Frameworkpflege budgetieren. Falls weder APIänderung noch Frameworkabhängigkeit akzeptabel sind, bleibt das Ziel **BLOCKED**; keine stillschweigende Herabstufung auf Konventionen.

**Warum diese Empfehlung:** Sie adressiert die tatsächlich benötigte Konsistenz und kontrollierte Identitätsänderung, ohne den in U0 widerlegten allgemeinen Bindingtransaktionsvertrag erneut zu versprechen. Im Host fallen keine gefundenen Document-XAML-Bindings weg; der Preis liegt transparent in Bibliotheks-API und Ressourcen-/Coreintegration. Die Empfehlung ist eine begründete Priorisierung unter offener technischer Abnahme, keine Feststellung „FEASIBLE“.

## 9. Arbeitsgrenzen und Abschluss

Nur dieser neue Produktbericht wurde angelegt. Keine Produktcodeänderung, keine Änderung bestehender Architekturtexte, B1/U0 oder Tests; keine Migration, Builds/Restores, Paketveröffentlichung, Commits oder Pushes. `.codex/config.toml` wurde weder gelesen noch verändert. Vorhandene Arbeiten wurden durch Hashvergleich geschützt; Suchinventar und Schutzmanifeste liegen separat unter `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-post-u0-options-20261024`.

**Status: OWNER DECISION REQUIRED / BLOCKED.** Nach dem Bericht anhalten. Keine Variante ist freigegeben; frühere Architekturentscheidungen und Reviewurteile werden durch diese Empfehlung nicht automatisch geändert.
