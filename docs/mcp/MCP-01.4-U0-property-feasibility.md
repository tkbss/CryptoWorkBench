# MCP-01.4 – U0: Avalonia-12-Property-Machbarkeit

Stand: 24.10.2026. **NOT FEASIBLE AS DESIGNED.** Ausschließlich isolierter Machbarkeitsnachweis; keine Design-, Implementierungs-, Paket-, Migrations- oder Integrationsfreigabe.

Der vollständige Dokumentbindungsvertrag aus B2-R1 §2.3 lässt sich mit den untersuchten öffentlichen Hooks des unveränderten Avalonia-12.0.0-Propertysystems nicht erfüllen. Zwei unabhängige harte Grenzen sind experimentell belegt: **P04 verliert die neue identische Quellemission vor dem Bibliothekshook; P06 verliert die alte Subscription vor einem möglichen Veto.** Zusätzlich scheitern Folgeoperationen nach Clear-/Prioritätsabweisung und der vollständige Callbackschutz des untersuchten Coordinators. Erfolgreiche einfache Wertwechsel widerlegen diese Grenzen nicht.

**Phase A:** alle zwölf Matrixzeilen ausgeführt; **7 PASS / 5 FAIL**, insgesamt **208 PASS / 208 FAIL** bei 416 Sollassertions. **Phase B: BLOCKED, nicht durchgeführt**, weil Phase A den benötigten Vertrag nicht trägt. Die PASS-Angaben betreffen ausschließlich den jeweils angegebenen Minimalmodellumfang.

## 1. Quellen, Versionen und Prüfungsumfang

Produktiver Worktree: `C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp`, Branch `feature/mcp-server`, HEAD `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`. Der im Desktop geöffnete Scratchbereich `C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp` ist davon getrennt; er besitzt ein unabhängiges initialisiertes Gitverzeichnis ohne HEAD. Die Probe liegt außerhalb des produktiven Worktrees.

| Verbindliche Quelle | Verwendung in U0 |
| --- | --- |
| [B2-R1-Design](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-design.md:90) | §2.3 als Sollvertrag: alle drei Owner, frühe Admission, Commit/Finalisierung, Bindingkonsumierung, Generation; U0-Abnahmegrenze. Keine automatische Korrektur |
| [R1-Re-Review](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B2-R1-independent-review.md) | R1-01, §4.1–4.3, P01–P12 und Abbruchregel. Die dortige Einordnung des notifying-Hooks wird unten anhand der Originalquelle korrigiert |
| [U12-Feasibility](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-avalonia12-feasibility.md) | Trennung von Bibliotheksvertrag und produktiver GUI-Migration; historische Avalonia-12.1.4-Laufzeit ist kein Nachweis für 12.0.0 |
| [B1-Feasibility](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-B1-feasibility.md) | Begrenzte vorhandene Evidenz, Patch-/Testschutz; B1 nicht erneut gebaut oder ausgeführt |
| [Editorentscheidung](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench-Mcp/docs/mcp/MCP-01.4-editor-architecture-decision.md) | Frühe generische Admission, unveränderte Hostverantwortung, eingeschränkter Rohzugriff nur nach explizitem Architekturentscheid |

**Avalonia:** offizielles NuGet-Paket **12.0.0**, exakt gepinnt auf `[12.0.0]`; NuSpec-Repositorycommit und geladene Assembly-InformationalVersion stimmen mit **`220dea3f5e18002ff20a6603e70eb633eaa73b4c`** überein. Tatsächlich geladen: `Avalonia.Base, Version=12.0.0.0`, InformationalVersion `12.0.0+220dea3f5e18002ff20a6603e70eb633eaa73b4c`; SHA-256 `05F8E7069F24DC3CB82E9990007CBDB9826793C86BD451E9FE17912F419A9809`. [Laufzeitprovenienz](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/provenance.json).

Originalquellen am festen Commit: AvaloniaObject, AvaloniaProperty, AvaloniaPropertyMetadata, StyledProperty, ValueStore, EffectiveValue, EffectiveValue<T>, BindingEntryBase und PropertyNotifying. Unverändert als Lesebelege heruntergeladen, **nicht mitkompiliert**. [Quellmanifest mit URLs und SHA-256](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/sources/manifest.json).

**AvaloniaEdit:** Original **12.0.0**, Commit **`86fdebec4cff7affb0e14c7885df71d28edce777`**. Die 362 Dateien der vorhandenen Originalbasis stimmen mit dem B1-Ausgangsmanifest überein; 0 Abweichungen. TextEditor.cs, TextArea.cs und TextView.cs wurden zusätzlich vom festen Commit geladen und byteweise per Hash mit der Originalbasis verglichen: alle drei identisch. Nur statische Prüfung ihrer Documentverdrahtung; **kein AvaloniaEdit-Laufzeittest**. [Originalprüfung](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/avaloniaedit-original-verification.json).

Kennzeichnung: **E** = tatsächlich gemessen; **S** = statischer Originalquellenbefund; **M** = eigener Minimalmodellzustand; **O** = offen/gesperrt. Insbesondere sind M-Generation, M-Ressourcen und M-Faulted kein Nachweis realer Caret-/Selection-/Undo-/HeightTree-Lebenszyklen.

## 2. Isolierter Aufbau und Reproduktion

Neues Untersuchungsverzeichnis:

`C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024`

[Program.cs](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/Program.cs) enthält drei konkrete minimale Owner `First`, `Second`, `Third`, jeweils abgeleitet von `AvaloniaObject` über eine gemeinsame Basisklasse. Alle verwenden **dieselbe echte StyledProperty-Instanz**, registriert mit AddOwner und ausdrücklich gemeinsamer Coercion in den Metadaten jedes konkreten Ownertyps. A/B/C/D sind reine Dokumentidentitäten. Es gibt keine nachgebaute Propertyengine.

Der experimentelle Coordinator prüft in der tatsächlichen Coercion und finalisiert die drei Werte im protected `OnPropertyChanged`-Override vor dem äußeren Frameworkfanout. Interne Folgezuweisungen verwenden `SetCurrentValue`. Der interne Zielkontext ist nicht an Observer exportiert. Die Probe behauptet keinen vollständigen Guard: Ohne öffentlichen verlässlichen Abschluss-Hook endet die Sperre vor dem äußeren Fanout; P09 prüft genau diese Lücke. Eine aus bloßer Coercion offen gehaltene Reservation wäre wegen P02 ebenfalls unzulässig.

Die Veto-Varianten sind ausdrücklich **Versuchskandidaten**, keine angenommenen Alternativarchitekturen:

- `Throw`: Policyabweisung wirft aus der Coercion.
- `ReturnCurrent`: Coercion gibt den bisherigen Wert zurück.
- `RememberRejected`: eigener Ablehnungsspeicher hält den abgewiesenen Wert auch bei späterer Coercion zurück. Er besitzt keine erfundene Information über neue Quellemissionen.

Steuerbare IObservable-Quellen werden direkt an das echte `Bind` übergeben; sie protokollieren Subscribe/Dispose/OnNext. Ihre Zähler und Emissionsidentitäten werden **nicht** in die Coordinatorentscheidung eingespeist. P06 prüft zusätzlich den regulären `Binding`-/`BindingBase`-Pfad mit INotifyPropertyChanged-ViewModel. Style und Animation werden mit den tatsächlichen BindingPriority-Werten geprüft; kein XAML-Loader wird benötigt.

**Ereignisprovenienz:** `FW Coerce`, `FW OnPropertyChanged` und direkt abonnierte Property-/Objekt-/INPC-Empfänger sind echte Frameworkaufrufe. `SOURCE` bezeichnet instrumentierte Quellaktivität; sie ist kein PropertyChanged. `COORD` bezeichnet eigene Admission, Generation, Finalisierung, Policy-/Callbacktesthooks und Faultzustände. `TEST`/`ASSERT` sind Messung/Sollprüfung. Es werden keine selbst erzeugten notifying-Events als Frameworkevents ausgegeben. Reflection liest nur öffentliche API-/Assemblymetadaten; kein Zugriff verändert private Frameworkzustände.

Die Probe läuft synchron auf dem Besitzerthread, ohne GUI, Headlessplattform oder Dispatcher-Pump: Windows `10.0.26200`, SDK **10.0.401**, Laufzeit **.NET 10.0.12**, `net10.0`. Lokaler Paketcache, HTTPcache, CLI-Home, Build-/Restoreausgaben und alle Ergebnisse liegen im Untersuchungsverzeichnis. Der Paketgraph enthält Avalonia 12.0.0, Avalonia.Remote.Protocol 12.0.0, Avalonia.BuildServices 11.3.2 und MicroCom.Runtime 0.11.4; keine Prism-, MCP-, CryptoWorkBench- oder AvaloniaEdit-Abhängigkeit.

Reproduktion mit [run.ps1](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/run.ps1): `& .\run.ps1` im Untersuchungsverzeichnis. Restore verwendet `--locked-mode` und [packages.lock.json](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/packages.lock.json). **Restore Exit 0, Build Exit 0, Testprogramm Exit 1**. Der negative Testexit bleibt erhalten; er wird nicht als bestandener Vertrag interpretiert. [Restorelog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/restore.log), [Buildlog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/build.log), [Testlog](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/run.log), [Ergebnis-JSON](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/summary.json).

Finale Program.cs SHA-256: `CAE1AB905EB1B594274D2C4456977B4D41B9D9B00E4C7336EA2E8C5B8F893AC6`; Lockdatei SHA-256: `5F312105E0D98F8DFCED548E68FA04BCDE41F617EA704DD35FDD8D05321E03AC`.

**Harnesskorrekturen offengelegt:** Ein erster Build erfasste versehentlich heruntergeladene Lesebelege; er schlug fehl und wurde durch eine explizite Compile-Liste korrigiert. Ein weiterer Build belegte, dass `IsEffectiveValueChange` nicht öffentlich verfügbar ist; der Override wurde auf `OnPropertyChanged` umgestellt. Im ersten ausführbaren Aufbau fehlten Coercionmetadaten der Schwesterowner; lokales initiales null verdeckte außerdem Stylebindings. Diese Aufbaufehler wurden behoben, ohne Sollassertions zu lockern. Frühstände und Logs sind erhalten. Zusätzliche Prioritätsdiagnostik und eine strengere P07-Folgeprüfung machten eine zuvor unzureichend geprüfte Zeile rot. Details und Artefakte im [README](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/README.md). Ein abschließender Lauf mit Dependency-Lock reproduzierte das finale Ergebnis. Ein AVP1001-Hinweis betrifft die einmalige zusätzliche Defaultproperty-Registrierung im Testkörper; kein Buildfehler.

## 3. Vollständige Matrix P01–P12

PASS bedeutet jeweils **diese Assertions im Phase-A-Minimalmodell erfüllt**, nicht den vollständigen B2-Vertrag oder reale Editorintegration. FAIL bleibt FAIL, auch wenn einzelne Beobachtungen innerhalb derselben Zeile positiv sind. Die Anzahl der Assertions ist kein Abdeckungsprozentsatz.

| Fall | Status / Assertions PASS–FAIL | Tatsächliche Ergebnisse und Grenze |
| --- | --- | --- |
| [P01](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P01.log) CLR / SetValue / SetCurrentValue | **PASS, 18–0** | Alle 9 Kombinationen aus 3 Ownern und 3 Zuweisungswegen: Veto hält A und Generation; anschließender erlaubter Versuch bindet alle B, eine Generation und je eine Modellpolicyprüfung für altes/neues Dokument. Keine echten TextDocument-Policies implementiert |
| [P02](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P02.log) No-op / verdecktes Angebot | **PASS, 9–0** | Je Owner: identischer Wert, niedriger priorisierter Stylewert und direkter `StyledProperty.CoerceValue` lassen effektiven Verbund und Generation unverändert; keine offene Reservation. Folgeedit erfolgreich. Eine Coercionauswertung ist damit nachweislich kein verlässlicher Begin-/End-Rahmen |
| [P03](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P03.log) ClearValue / Default | **FAIL, 8–4** | Lokales A über Style-B und A→null mit Throw/ReturnCurrent. Sichtbares A bleibt bei Veto, aber öffentliche Diagnostik zeigt bereits geänderte Priorität/BaseValue. Bei Throw bleibt ein erneutes erlaubtes Clear wirkungslos; bei ReturnCurrent/Style wird B bei Recoercion nachgeholt. Folge-SetCurrentValue/Clear ebenfalls protokolliert. Zusätzlicher echter Default A: nachträglicher Override derselben bereits belegten Typmetadaten wirft InvalidOperationException, Default bleibt A; lokales C/Clear liefert A. Kein erfundener dynamischer Defaultwechsel |
| [P04](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P04.log) abgewiesenes B / Recoerce / identisches B | **FAIL, 5–7** | Alle 3 Veto-Varianten: neue identische Quellemission erreicht keinen Coercionhook, Delta **0**. ReturnCurrent holt B schon bei Recoercion nach. Throw und RememberRejected halten A, können das neue identische B jedoch nicht zulassen. Keine Zwischenemission eingesetzt und keine Assertion abgeschwächt |
| [P05](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P05.log) Zwischenwert A, dann B | **PASS, 2–0** | Throw und ReturnCurrent: nach abgewiesenem B und tatsächlicher Zwischenemission A erreicht das neue B wieder die Coercion und wird einmal verbundweit gebunden. Das ist eine Gegenprobe zum spezifischen Informationsverlust in P04, kein Ersatz für P04 |
| [P06](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P06.log) Bindingersatz / Dispose | **FAIL, 4–13** | Ersatz eines lokalen A-Bindings durch Quelle B oder A, Throw/ReturnCurrent: alte Subscription **active=0, disposed=1 vor Veto**. Alte Quelle C erreicht anschließend niemanden. ReturnCurrent installiert die Ersatzquelle trotz verweigertem Wechsel. Dispose über Style-B verliert ebenfalls die lokale Subscription, obwohl effektiv A bleibt. Zusätzlicher BindingBase-Pfad verliert das alte Binding ebenfalls. Gleicher sichtbarer Wert beweist keinen Subscriptionerhalt |
| [P07](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P07.log) Style / Local / Animation / CurrentValue | **FAIL, 5–1** | Verdecktes Style-D erzeugt keinen Commit; Current-C und Animation-B erzeugen je einen. Abgewiesenes Entfernen der Animation hält sichtbar B, aber Priorität ist bereits Unset. Der nächste explizite Clear legt D nicht frei. Recoercion erzeugt zwar keine doppelte Generation, repariert den verlorenen Propertyzustand aber nicht |
| [P08](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P08.log) Frameworkobserver werfen | **PASS, 9–0** | Je erster Property-/Objekt-/INPC-Empfänger an jedem der 3 Owner wirft. Die vorgezogene eigene Finalisierung endet jeweils mit allen Werten und M-Ressourcen auf B, Generation 1 und Stable. Fremde spätere Empfänger können ausfallen; ihr Erreichen ist keine Sollassertion. Keine Garantie vollständiger Fehleraggregation oder realer Viewfinalisierung daraus ableiten; Callbackschutz separat P09 |
| [P09](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P09.log) Reentrancy | **FAIL, 132–183** | 5 Callback-/Hookstellen × 3 Callbackmember × 3 Zielowner × 7 Operationen = **315** Kombinationen: CLR, SetValue, SetCurrentValue, ClearValue, Bindingersatz B, Bindingersatz gleicher Wert, Dispose. Policy-/OnPropertyChanged-Testhooks sind eigene Instrumentierung; Property-/Objekt-/INPC-Empfänger sind echte Subscriptions. Abweisung vor Wert- **und** Subscriptionwirkung fehlt in vielen Fällen. Äußerer Fanout kann einen zweiten Commit auslösen; selbst während aktiver Admission kann Bindingdisposal schon wirken |
| [P10](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P10.log) A→B→A / Detach | **PASS, 3–0** | M-Pendingmarker wird durch A→B→A und explizites Document=null→A ungültig; identisches A ohne Detach erhöht nicht. Tatsächlicher Visual-Detach/Reattach ist mit minimalen AvaloniaObjects nicht vorhanden und bleibt Phase B vorbehalten |
| [P11](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P11.log) interne Finalisierungsfehler | **PASS, 12–0** | Injizierter M-Ressourcenfehler am zweiten bzw. dritten Mitglied führt terminal zu M-Faulted; Änderungen über alle 3 Owner sowie synthetischer Edit werden verweigert. Explizites M-Verwerfen möglich. Kein Nachweis der Bereinigung echter Weak-Subscriptions, Caret-, Selection- oder Renderingressourcen |
| [P12](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/results/P12.log) Legacy ohne Policy | **PASS, 1–0** | Dieselbe Propertyfolge mit deaktiviertem Strictzweig gegen eine normale unveränderte AvaloniaObject-/StyledProperty-Registrierung: **60 gegen 60 identische** Traceeinträge für Werte, Reihenfolge, Subscriptionen und Exceptions. Enthält Style/Current/Animation, Clear, gleiche Emission, Recoerce, Ersatz/Dispose und Property-/Objekt-/INPC-Throw. Keine pauschale Legacyabnahme für AvaloniaEdit |

P09 nach Stelle: Policytesthook **36/27**, OnPropertyChanged-Testhook **24/39**, Property **24/39**, Objekt **24/39**, INPC **24/39** (PASS/FAIL). Die Policyfälle werden für jeden Callbackmember separat gefahren; diese Wiederholungen machen aus derselben Policygrenze keine drei verschiedenen Frameworkhooks.

## 4. Entscheidende Gegenbeispiele und Frameworkgrenzen

### 4.1 P04: neue identische Emission ist für den Bibliothekshook unsichtbar

**E:** Stylequelle emittiert A. Danach B unter Denied, Entsperren, `CoerceValue`, erneut B **ohne Zwischenwert**. Die Quelle protokolliert das letzte OnNext tatsächlich. Bei allen drei Varianten ist dessen Coerciondelta 0.

| Variante | Nach Denied | Nach Recoerce | Nach neuem identischem B |
| --- | --- | --- | --- |
| Throw | A, Generation 0 | A, Generation 0 | A, Generation 0, kein Hook |
| ReturnCurrent | A, Generation 0 | **B, Generation 1: verbotenes Nachholen** | B unverändert, kein neuer Hook |
| RememberRejected | A, Generation 0 | A, Generation 0 | **A bleibt: legitimer neuer Versuch verloren**, kein Hook |

**S:** [BindingEntryBase.SetValue, ab Zeile 125](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/BindingEntryBase.cs#L125) speichert den geänderten Quellwert vor seiner Weitergabe und unterdrückt gleiche spätere Werte. Die Owner-Coercion bekommt weder eine Emissionskennung noch einen Callback für die unterdrückte Emission. [EffectiveValue<T>, ab Zeile 204](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/EffectiveValue%601.cs#L204) hält außerdem ungecoercte Werte für spätere Neubewertungen.

**Folgerung:** Für eine unverändert an die öffentliche Property gebundene beliebige Quelle kann der Coordinator aus den verfügbaren Propertycallbacks nicht erkennen, ob das nach Entsperrung unsichtbar gebliebene B erneut emittiert wurde. Ein eigener Ablehnungsspeicher löst diesen Informationsverlust nicht. Ein vorgeschalteter Quelladapter mit Emissionsidentität wäre ein zusätzlicher, eingeschränkter Bindingvertrag; er wurde hier nicht heimlich eingeführt.

### 4.2 P06: der mögliche Vetozeitpunkt liegt nach Subscriptionwirkung

**E, Throw-Pfad:** lokale Quelle A ist aktiv. Beim Versuch des Ersatzes wird zuerst `SOURCE old dispose active=0` protokolliert. Erst danach erreicht die Neubewertung die Coercion mit A→null und wird abgewiesen. Das neue Binding ist noch nicht aktiv, das alte aber bereits verloren. Ein anschließendes OnNext(C) der alten Quelle hat **0 Empfänger**, obwohl `GetValue` weiter A zeigt. Dasselbe tritt bei einem Ersatzangebot mit identischem A auf. Beim ausdrücklichen Bindingdispose über einem Style-B wird ebenfalls erst die Subscription beendet, dann der Wertwechsel verweigert.

**S:** [ValueStore.AddBinding, ab Zeile 47](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/ValueStore.cs#L47) trennt das bestehende lokale Binding vor dem Start des Nachfolgers. Das betrifft sowohl Observable- als auch BindingBase-Pfade. Die öffentlich erreichbaren Bind-/Clear-/Setmethoden sind keine überschreibbare Transaktionsschnittstelle.

**Folgerung:** Eine spätere Coercion kann bereits ausgeführten fremden Disposecode nicht zustandsneutral rückgängig machen. Re-Subscribe wäre eine neue beobachtbare Nebenwirkung und erhält weder die alte Subscriptionidentität noch beliebige externe Zustände. Das ist eine fehlende Eingriffsmöglichkeit **vor** der Wirkung, nicht lediglich ein fehlendes finally im Versuchskandidaten.

### 4.3 P03/P07: Value-Veto ist kein Store-Rollback

**E:** P03/Throw: vor Clear `value=A; priority=LocalValue; base=A`; nach Veto `value=A; priority=Unset; base=(empty)`. Nach Entsperrung bleibt auch ein zweites explizites Clear bei A. P07: nach verweigerter Animationsentfernung `value=B; priority=Unset; base=C`; Folge-Clear legt das verdeckte Style-D nicht frei. Diese Daten stammen aus dem öffentlichen `GetDiagnostic`/`GetBaseValue`, nicht aus privater Reflection.

**S:** [EffectiveValue.BeginReevaluation, Zeile 77](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/EffectiveValue.cs#L77) und [ValueStore.ClearValue, Zeile 190](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/PropertyStore/ValueStore.cs#L190) verändern Auswertungs-/Prioritätszustand vor Abschluss des späteren Coercionpfads. Die beschriebenen Ausnahmen liefern keinen allgemeinen Transaktionsrollback.

**Folgerung:** Auch ein Frameworkfehlerweg mit unverändertem effektiven A erfüllt den geforderten Erhalt von Binding-/Prioritätssemantik nicht. Diese Befunde sind keine Behauptung, Avalonia garantiere Transaktionalität für beliebig werfende Coercion; gerade eine solche zusätzliche Garantie benötigt B2-R1.

### 4.4 notifying ist intern; ein öffentlicher finaler Fanout-Rahmen fehlt

**Korrektur des R1-Re-Reviews:** Dessen §4.1 ordnet notifying zusammen mit öffentlich registrierbarer Coercion ein. Am vorgegebenen Originalcommit ist der [Register-Overload mit notifying, Zeile 300](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/AvaloniaProperty.cs#L300) **internal**. Der öffentliche Overload endet bei `enableDataValidation`. Auch `AvaloniaProperty.Notifying` und der StyledProperty-Konstruktor mit diesem Argument sind intern. Die geladene Paket-API bestätigt `PublicRegisterHasNotifying=false` und `PublicNotifyingProperty=false`. U0 nutzt keine private Reflection, keinen InternalsVisibleTo-Trick und keinen Frameworkpatch. Der alte Reviewbericht wurde nicht verändert.

**E/S:** Ein protected OnPropertyChanged-Override ist verfügbar. Avalonia ruft ihn vor Property-Changed, Objekt-PropertyChanged und INPC auf. Deshalb kann der Versuchskandidat seine minimalen internen Mitglieder noch vor einem werfenden fremden Empfänger fortschreiben (P08). Der Override umschließt den anschließenden fremden Fanout jedoch nicht; sein finally wäre zu früh. Ein später abonnierter Empfänger liefert bei einem früheren Throw ebenfalls keine garantierte Abschlussmeldung. [AvaloniaObject, ab Zeile 720 und 783](https://github.com/AvaloniaUI/Avalonia/blob/220dea3f5e18002ff20a6603e70eb633eaa73b4c/src/Avalonia.Base/AvaloniaObject.cs#L720).

**E, P09:** Ein Propertycallback am äußeren Owner 0 setzt Owner 1 auf C: `neutral=False; allB=False; gen=2; state=Stable`. Der äußere B-Wechsel ist also nicht bis zum Ende seines Callbackfensters geschützt. Andere Fälle bleiben während der internen Fortführung erfolgreich gesperrt; Bindingersatz/Dispose kann dennoch vor dem Hook Subscriptionwirkung entfalten.

**Folgerung:** P08 belegt einen Baustein für interne Wertfinalisierung, nicht den vollständigen Event-/Reservierungsvertrag. Eine Sperre ohne garantierten Abschluss bis zum nächsten Dispatcherturn offenzuhalten wäre eine zusätzliche asynchrone Semantik und löste weder synchrone Folgezugriffe noch P04/P06. Auch die bloße öffentliche Freigabe des heutigen notifying-Hooks würde P04/P06 nicht reparieren.

### 4.5 Einordnung weiterer Hooks

| Hook | Eignung und Grenze |
| --- | --- |
| Coercion-Metadaten | E: normale frühe Wertprüfung möglich. Kein Quell-/Emissions-/Operationstyp, kein garantierter Notify nach Auswertung, kein Gate vor Bindingdispose |
| ValidateValue | S: wertbezogener Parameter, kein Owner-/Dokument-/Operationskontext; keine Lösung des Vertrags |
| AddOwner / OverrideMetadata | E: gemeinsame Propertyidentität möglich. Coercion bei nicht voneinander abgeleiteten konkreten Ownern explizit einrichten; AddOwner erzeugt keinen gemeinsamen ValueStore |
| OnPropertyChanged | E: internen Verbund vor externem Fanout finalisieren möglich. Keine Umklammerung des Fanouts, kein Hook für unsichtbare gleiche Emission |
| Property.Changed / Objekt / INPC | E: echte, aber nachgelagerte Meldungen; früher Throw kann spätere Empfänger unterbrechen. Keine verlässliche spätere Finalisierungsstelle |
| GetDiagnostic / GetBaseValue | E: öffentliche Beobachtung bereits eingetretener Prioritätswirkung; keine Autorisierung oder Reparatur |
| Bindingquellen-/IDisposable-Kontrolle im Host | Könnte nur mit zusätzlich beschränktem Zugriff helfen. Nicht durch einen Bibliothekscoordinator für beliebige bereits öffentliche Bind-Aufrufe erzwingbar |

## 5. Phase B und tatsächliche AvaloniaEdit-Lebenszyklen

**BLOCKED / NICHT AUSGEFÜHRT**, entsprechend der expliziten Abbruchregel. Es wurden keine realen TextEditor-/TextArea-/TextView-Instanzen für eine Integration aufgebaut, keine Bibliothek verändert und kein AvaloniaEdit-Projekt gebaut. Daher kein Laufzeitnachweis für reale Dokumentfelder, Weak-/Undo-Subscriptions, Caret/Selection, HeightTree, Caches, Viewinvalidierung oder tatsächliche Visual-Detachgeneration.

**S:** Die Originaltypen verwenden dieselbe StyledProperty-Identität, verbinden ihre Dokumente aber über nachgelagerte Changed-Subscriptions: [TextEditor:137](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/TextEditor.cs#L137) pflegt Text-/Undo-Subscriptions und setzt TextArea.Document; [TextArea:293](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/TextArea.cs#L293) pflegt Dokumentereignisse, setzt TextView.Document und setzt Caret/Selection zurück; [TextView:141](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Rendering/TextView.cs#L141) erneuert interne Dokumentreferenz, HeightTree, Formatter und Caches. Diese drei realen Lebenszyklen sind umfangreicher als M-Ressourcen des Minimalmodells.

Selbst nach einem später erfolgreichen neuen Phase-A-Vertrag wäre Phase B weiterhin erforderlich. Die grüne Untermenge P01/P02/P05/P08/P10/P11/P12 darf GD-01 nicht auf bestanden setzen.

## 6. Auswirkungen auf GD-01 und B2-R1

**GD-01 bleibt BLOCKED. R1-01 ist durch konkrete Laufzeitgegenbeispiele verschärft, nicht erledigt.** B2-R1 §2.3 Punkte 1/5 scheitern an Erhalt/Konsumierung von Bindingzuständen; der verlangte vollständige Callbackrahmen aus Punkt 3 ist mit dem untersuchten öffentlichen Hookansatz ebenfalls nicht erfüllt. Punkt 4 hat einen begrenzten positiven Minimalbefund, aber keinen realen AvaloniaEdit-Nachweis.

Die technische Abnahmegrenze des Designs selbst sieht für diesen Fall einen neuen Designentscheid vor. U0 wird damit **nicht positiv abgeschlossen**, und U1–U4 dürfen aus diesem Bericht keine Serienimplementierungsfreigabe ableiten. Auch eine reine Dokumentkorrektur von „öffentlich“ zu „intern“ beim notifying-Hook reicht nicht: P04 und P06 bleiben unabhängig davon ungelöst.

Andere R1-Findings, B1-Assertions, Produktreviewstatus und Hostgarantien wurden nicht neu abgenommen. B1s historische grünen Tests unter Avalonia 12.1.4 werden weder entwertet noch in Propertynachweise für 12.0.0 umgedeutet. Save-/Execution-/MCP-Admission bleibt Hostaufgabe; diese Probe schafft weder zweite Ausführungspipeline noch neue Hostautorität.

## 7. Alternativen ausschließlich auf Designebene

| Alternative | Nötiger nächster Entscheid | Folgen / Grenzen |
| --- | --- | --- |
| **1. Eingeschränkter expliziter Bindingvertrag** | Eigentümer muss genau entscheiden, welche direkten Bind-/Dispose-/Clear-/Prioritätspfade unterstützt sind und was „neuer Versuch“ bedeutet. Etwa ausdrücklich vereinbarte Quelle-/Assignment-API statt beliebiger bestehender StyledProperty-Bindings; jeder Ausschluss gehört in APIvertrag und Gegenproben | Kann fehlende Quellidentität/Ownership durch neue vertragliche Voraussetzungen adressieren, erhält aber den heutigen universellen §2.3-Vertrag nicht. Ein CLR-Wrapper allein schützt Basistypzugriffe nicht. Ein abgewiesenes identisches B darf nicht still als „kein neuer Versuch“ umdefiniert werden |
| **2. Erweiterung des Avalonia-Propertysystems** | Maintainer müssen eine echte Eingriffsstelle vor Bindingersatz/-disposal und Prioritätswirkung, Identität/Verbrauch jeder neuen Quellemission trotz Gleichheitsfilter sowie einen ausnahmesicheren Abschlussrahmen bewerten | Für unverändert umfassenden Vertrag ist eine Frameworkebene erforderlich. Nur notifying öffentlich machen genügt nicht. Kosten: neue Frameworkabhängigkeit, Tests der Exception-/Reentrancysemantik, Versions-/Distributions-/Wartungs- und Exitentscheidung. Noch keine zugesagte Avalonia-API oder Upstreamannahme |
| **3. Alternative Document-Propertyarchitektur** | Eigentümer/Maintainer müssen neue autoritative Dokumentbindung und eventuell getrennte angeforderte/angenommene Werte spezifizieren, einschließlich Lebensdauer, Fehlern, Prioritäten und Bindungsbesitz | StyledProperty→DirectProperty oder andere Propertyidentität verändert öffentliche Feldtypen, XAML/Styles, Priority-/Bindingsemantik sowie API-/Binarykompatibilität. DirectProperty allein ist kein Nachweis früher Kontrolle jedes Bindingdisposes. Adapter und Migration müssten gesondert entworfen und geprüft werden |

**Empfehlung:** Als nächsten Auftrag einen begrenzten Architekturentscheid über den gewünschten Vertragsumfang treffen. Soll der universelle §2.3-Vertrag unverändert gelten, zuerst die notwendigen Frameworkhooks und ihre Maintainer-/Pflegeverantwortung klären; erst danach einen neuen isolierten Nachweis beauftragen. Falls diese Abhängigkeit nicht tragbar ist, muss der Eigentümer explizit zwischen eingeschränktem Vertrag und inkompatibler neuer Propertyarchitektur entscheiden. Keine dieser Alternativen wurde hier implementiert, ausgewählt oder veröffentlicht.

## 8. Verbleibende Risiken und Grenzen

- Die Messung gilt für den festen Avalonia-12.0.0-Stand, nicht pauschal für spätere Releases, alle Bindingimplementierungen oder andere Plattformen. Die Gegenbeispiele auf einem unterstützten öffentlichen Bindingpfad genügen bereits, um den universellen Vertrag zu verwerfen.
- Phase A arbeitet mit minimalen Ownern und Identitäten; keine realen Textmutationen, Undo-/Redo-Replays, Carets, Selections, Viewressourcen, Async-Paste-Tasks oder UI-Zyklen. Deren Abnahme bleibt gesperrt.
- Die P09-Fehler belegen auch Grenzen des konkreten Coordinatorkandidaten. Die stärkere Aussage zur Nichterfüllbarkeit des uneingeschränkten Vertrags stützt sich auf P04/P06 plus die unveränderten Originalpfade, nicht auf die Behauptung, jeder denkbare Coordinator sei erschöpfend implementiert worden.
- M-Faulted/Discard ist kein Reparaturbeweis für ein beschädigtes TextDocument. Legacy-Differentialtests prüfen nur die angegebene Minimalpropertyfolge; sie ersetzen keine AvaloniaEdit-/Hostregression.
- Paketidentität, Lockdatei und lokale Originalquellen sind reproduzierbar dokumentiert. Frischer Restore benötigt Paketnetzwerk; SDK/Runtime/OS müssen für einen identischen Lauf bereitstehen. Keine langfristige Paketdistribution oder Lizenzentscheidung wurde damit getroffen.

## 9. Schutz vorhandener Arbeiten und Abschluss

Vorher-/Nachhervergleich: **754 vorhandene Produktdateien** und **448 B1-Dateien** aus Quellen, Tests, Patch und ausgewählten Evidenzdateien unverändert; 0 Hashabweichungen. B1-Patch SHA-256 weiterhin `2265133825B9ED3364EA6F2646FDCBF1D8DA7C384D6218C0E4F81A671AF394B9`; B1-Testquelle weiterhin `F6B2914678FC20D0DA6AD7209E146295129426FA73CF24D5416E4D9455A82390`. Keine bestehende Sollassertion verändert. `.codex/config.toml` wurde weder gelesen noch verändert; geschützte Konfigurationsbereiche sind aus dem Dateivergleich ausgeschlossen. [Schutzprüfung und vollständiger Gitstatus](C:/Users/stefa/Documents/ChatGPT/CryptoWorkBench-Mcp/mcp014-u0-property-20261024/preservation-final.json).

Im produktiven Worktree ist ausschließlich dieser neue Bericht hinzugekommen: `?? docs/mcp/MCP-01.4-U0-property-feasibility.md`. Die vorher vorhandenen **6 modifizierten und 15 unversionierten Dateien** bleiben unverändert; danach **6 modifizierte und 16 unversionierte Dateien**, nichts staged. Branch und HEAD unverändert. Alle Builds/Restores/Programme ausschließlich im neuen Untersuchungsverzeichnis; keine Produktcodeänderung, kein Paketpublish, keine Migration, kein MCP-01.5, kein Commit/Push/Merge/Rebase.

**Abschlussurteil: NOT FEASIBLE AS DESIGNED.** Der benötigte Vertrag ist mit dem untersuchten unveränderten Framework und uneingeschränkten bestehenden StyledProperty-Zugriff nicht erfüllt. Phase B bleibt gesperrt. Nach Ablage dieses Berichts wird angehalten; keine Architekturentscheidung oder weitere Implementierung wird vorweggenommen.
