# A12-04-F01 – Native Prism Dialog Retention Investigation

Stand: 27. Oktober 2026. **Bewertung: BLOCKED. Kategorie: Ursache weiterhin ungeklärt.**

Die historische Retention ist belegt, wurde in dieser Untersuchung jedoch nicht erneut ausgelöst. Alle 44 neuen nativen Prozesse bestanden die ursprüngliche strikte Retentionprüfung nach insgesamt 880 Dialogzyklen. Das widerlegt die früheren Fehlbefunde nicht. Mangels auffälligem laufendem Prozess konnte keine tatsächliche GC-Root-Kette der damals verbliebenen Objekte erhoben werden. Weder ein Harnessfehler noch ein dauerhafter Leak oder eine bestimmte Bibliotheksursache ist nachgewiesen. **Keine Entwarnung und kein F01 RESOLVED.**

## 1. Basis und Schutzgrenzen

Produktiver Worktree: `C:\Users\stefa\Documents\projects\GITHUB\CryptoWorkBench-Avalonia12`.

Untersuchte Integration: `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\a12-04-lab`, Prism-Commit `358118cd640d9a22ff8cf21c8ad197fa038b7990`, externer Build-Pin `Microsoft.Build.Tasks.Git` 10.0.303. Neue Untersuchung ausschließlich unter `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\a12-04-f01-lab`.

Der vollständige bereits validierte Release-Probeoutput wurde zusammenhängend in neue Laufverzeichnisse kopiert. Es wurden keine Prism-DLLs gegen andere Builds ausgetauscht. Nur die neu erstellten diagnostischen Harness-DLL/PDB wurden in diesen neuen Verzeichnissen ersetzt. Die originale Anwendung und der offizielle Prism-Checkout wurden nicht neu gebaut oder geändert. Damit wird exakt der zuvor archivierte A12-04-Laufzeitgraph untersucht; der SourceLink-Pin ist eine Buildabhängigkeit, kein neu geladener Runtimebestandteil.

[Hashvergleich](../../artifacts/a12-04-f01/a12-04-binary-verification.json): alle sechs Prism-/Container-DLLs entsprechen exakt den unter A12-04 archivierten Binärhashes. Die konkreten geladenen Pfade und Assemblyversionen stehen zusätzlich in jedem Laufprotokoll.

[Abschlussprüfung](../../artifacts/a12-04-f01/verification.json): 778 ursprüngliche Produktdateien, 1.180 offizielle Prism-Dateien und die 305 im A12-04-Evidenzmanifest enthaltenen Dateien unverändert. Kein Schreiben in andere bestehende Worktrees oder historische Evidenzen. Im Produktworktree werden ausschließlich dieser neue Bericht und `artifacts/a12-04-f01/` abgelegt. Keine Änderungen an `.codex/config.toml`, kein Commit, Push, Merge oder automatische Integration.

## 2. Historischer Fehler und neue Reproduktion

Die unveränderten historischen Logs unter `artifacts/a12-04/` zeigen 0, 6, 15, 0 und 9 erreichbare Objekte von jeweils 60. Die drei auffälligen Prozesse endeten mit Exit 1. Jeweils alle funktionalen Lebenszyklusprüfungen waren zuvor erfolgreich; zurück blieben Window-/ViewModel-/View-Tripel. Diese Logs enthalten keine Heapadressen oder GC-Roots. Sie bleiben die maßgebliche positive Fehlerbeobachtung.

Neue tatsächlich ausgeführte Läufe:

| Variante | Prozesse | Dialogzyklen | Retentionfehler |
|---|---:|---:|---:|
| Unveränderte originale Harness-Binärdatei im vollständig kopierten Runtimeverzeichnis (`original-1..3`) | 3 | 60 | 0 |
| Unveränderter A12-04-Harness am bisherigen Pfad, ursprünglicher `dotnet run --project ... -c Release --no-build --no-restore -- native`-Aufruf (`original-launch-1..5`) | 5 | 100 | 0 |
| Diagnostischer Harness mit Haltepunkt ausschließlich nach fehlgeschlagener Assertion (`diagnostic-1..26`) | 26 | 520 | 0 |
| Minimale Prism-Anwendung ohne geladene CryptoWorkBench-/Core-Assemblies (`diagnostic-101..110`) | 10 | 200 | 0 |
| **Summe** | **44** | **880** | **0** |

Alle Prozesse melden `WEAKREFERENCES alive=0 total=60`, erfolgreichen nativen Windows-Handle/Message-Loop und Gesamtexit 0. [Einzelauswertung](../../artifacts/a12-04-f01/run-summary.json), zugehörige `.log`-/`.exit`-Dateien im selben Evidenzverzeichnis.

Die fünf `original-launch`-Läufe überlappten zeitweise mit der späteren Serie des Diagnoseharness. Sie sind daher kein strikt isolierter A/B-Vergleich der Startmethode. Die anschließenden zehn Minimaltests liefen als eigene sequenzielle Serie. Es wird keine statistische Unabhängigkeit aller 44 Läufe behauptet. Windows-Fokus, Sichtbarkeit/Verdeckung, Compositor-/Render-Timing, Scheduling und frühere Desktopbedingungen wurden nicht vollständig instrumentiert oder identisch rekonstruiert.

`Start-Process` verwendete in den ersten sechs diagnostischen Läufen `WindowStyle Hidden`, in den weiteren zwanzig `Normal`. Das war eine Variation der Startbedingungen; die echten Avalonia-Fenster, Dialogsichtbarkeit und nativen Handles wurden weiterhin vom Harness geprüft. Es gab keinen Headless-Lauf als Ersatz für diese Ergebnisse.

**Nicht erreicht:** mindestens einen neuen auffälligen nativen Lauf reproduzieren. Die negative Serie erfüllt diese Anforderung ausdrücklich nicht. Ein unbegrenztes Wiederholen ohne kontrollierte neue Hypothese wurde beendet; der Auftrag sieht für ungeklärte Ursachen die Bewertung BLOCKED vor.

## 3. Analyse des Harness und der Lebenszyklen

Der Harness startet die tatsächliche App, öffnet den Owner und prüft die fünf Regionen sowie Navigation. `RunDialog` ist ausdrücklich `NoInlining`. Die lokalen starken Referenzen auf DialogWindow, View und ViewModel liegen in dieser Methode. Nach Rückkehr bleibt die äußere Liste mit nichtgenerischen WeakReferences zurück. Die äußere Closure enthält unter anderem Owner, Service und die WeakReference-Liste; daraus allein folgt kein nachgewiesener starker Root zu den geschlossenen Dialogen.

Jeder Zyklus prüft initiale und aktualisierte Bindings. Zyklus 0 prüft das Schließveto, Zyklus 1 Ergebnisfehlerweitergabe, Zyklus 2 direktes Window.Close mit None. Die übrigen Zyklen prüfen OK und Antwortparameter. Nach jedem Schließen: Callback-/Ergebnisassertion, `OnDialogClosed` genau einmal, `IsVisible=false`, `Content=null`, `DataContext=null`, `owner.OwnedWindows.Count=0`. Alle diese Assertions bestehen auch in den historischen Retentionfehlerläufen.

Im unveränderten offiziellen `DialogService.ConfigureDialogWindowEvents`:

- Der Opened-Handler entfernt sich selbst und initialisiert den RequestClose-Listener.
- Closing fragt `CanCloseDialog` ab.
- Der Closed-Handler ist `async void`, entfernt Closed-/Closing-Handler, ruft `OnDialogClosed` auf, wartet auf `callback.Invoke` und leert anschließend DataContext und Content.
- Der RequestClose-Listener schließt das Dialogfenster über eine Closure. Zyklen innerhalb einer unerreichbaren Objektgruppe wären für sich allein kein GC-Root.

Die im Harness registrierten Close-/Error-Callbacks führen unmittelbar Zähler-/Ergebniszuweisungen aus. Sie simulieren keinen absichtlich lange wartenden asynchronen Callback. Die beobachteten geleerten Content-/DataContext-Werte zeigen, dass der entsprechende Code hinter dem `await` erreicht wurde. Sie beweisen nicht, dass jede eventuelle Task-/Dispatcher-/Renderer-Referenz bereits verschwunden ist. Ein globaler Container- oder Prism-Service-Root ist weder nachgewiesen noch ausgeschlossen.

## 4. Dispatcher, GC und Beobachtereinflüsse

Die ursprüngliche Messung bleibt erhalten: maximal 30 Iterationen mit `Pump(100)`, `GC.Collect`, `GC.WaitForPendingFinalizers`, erneut `GC.Collect`, vorzeitigem Ende nur bei null lebenden WeakReferences. `Pump` erzeugt einen DispatcherTimer, der den CancellationToken nach dem Intervall abbricht, und betreibt eine verschachtelte Dispatcher-MainLoop.

Das ist keine instrumentierte Bestätigung, dass alle Dispatcher-Prioritäten oder Render-/Compositoraufträge abgearbeitet sind. Es wurden keine Queue-Zähler, Rendering-Fences oder GC-Roots erfasst, die eine bestimmte ausstehende Operation als Ursache belegen. Der nominelle Drei-Sekunden-Pumprahmen ist außerdem keine harte Gesamtzeitgrenze einschließlich GC.

`WeakReference.IsAlive` ist eine Momentaufnahme. Die anschließende `RETAINED`-Ausgabe liest für Typnamen zusätzlich `WeakReference.Target`. Diese Diagnose kann kurzfristig selbst starke lokale Referenzen erzeugen; sie findet aber **erst nach** dem ursprünglichen `WEAKREFERENCES alive`-Befund statt. Sie erklärt deshalb nicht ohne weiteren Nachweis dessen erstmalig positive Zählung. Auch `NoInlining` ist kein Beweis, dass keinerlei weitere Stack-/JIT-Referenzen existieren.

Die letzte strikte Assertion `references.All(r => !r.IsAlive)` und ihr Fehlerexit wurden nicht entfernt oder abgeschwächt. Kein Timeout wurde verlängert. Die neue Instrumentierung schreibt nur bei einer bereits fehlgeschlagenen Assertion eine PID-Markerdatei und pausiert bis zur expliziten Freigabe. Danach wirft sie weiterhin denselben Fehler. In allen aktuellen Läufen wurde dieser Zweig nicht erreicht.

Für einen zukünftigen Root-Snapshot wäre zusätzlich ein Haltepunkt unmittelbar nach der ersten positiven Zählung und vor dem Auslesen von Target sinnvoll, um Beobachterreferenzen im späteren Fehlerhaltepunkt abzugrenzen. Die vorbereitete Variante hält aktuell erst in `Check` an; ein dort gefundener Stackroot müsste entsprechend kritisch interpretiert werden.

## 5. GC-Root-Analyse: vorbereitet, nicht erfolgreich durchgeführt

`dotnet-dump` 10.0.750501 wurde ausschließlich lokal unter `a12-04-f01-lab/tools` installiert. [Installationslog](../../artifacts/a12-04-f01/tool-install.log). Der Capture-Wrapper überwacht nur den von ihm gestarteten isolierten Probeprozess. Erst nach dessen Fehlermarker würde er `dotnet-dump collect -p <PID> --type Heap -o .../private/retained.dmp` ausführen, anschließend den Prozess freigeben und den Fehlerexit dokumentieren. Die [Microsoft-Dokumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump) beschreibt Heap-Erfassung und die SOS-Kommandos `dumpheap`/`gcroot` zur weiteren Analyse.

**Es wurde kein Heapdump erzeugt, weil kein neuer Lauf die Retention reproduzierte.** Daher existiert in diesem Auftrag keine tatsächliche GC-Root-Kette zu einem nachweislich verbliebenen Dialogobjekt. Ein Dump eines bestandenen Prozesses wäre kein Ersatz für diesen Nachweis und wurde nicht als solcher erzeugt.

Die benötigte Unterscheidung bleibt offen:

| Mögliche Herkunft | Nachweisstand |
|---|---|
| Harness-/JIT-/Stackreferenzen | Quellprüfung und NoInlining vorhanden; kein positiver Root-Nachweis |
| Dispatcher-/Renderaufträge | Nicht durch Queue-/Rootdaten bestätigt |
| Prism-Service/Container/Callback | Cleanup beobachtet; kein positiver Root-Nachweis |
| Avalonia-Fenster-/Eventinfrastruktur | Owner-Liste leer; sonstige Rootketten unbekannt |
| Dauerhaft erhaltene Referenzen | Nicht nachgewiesen; historische zeitbegrenzte Erreichbarkeit genügt dafür nicht |

Die Probe verwendet ausschließlich harmlose Testtexte und synthetische Dialogwerte. Es wurden keine produktiven Dokumente, Schlüssel oder Geheimnisse geladen. `private/`, Toolcache und Runtimeverzeichnisse werden nicht in Repository-Evidenzen übernommen. Keine Dumps oder Uploads an externe Dienste.

## 6. Vergleich mit A12-02

[Quellvergleich](../../artifacts/a12-04-f01/harness-comparison.json): beide Program.cs-Dateien besitzen SHA-256 `DBCEC973E8A5E1114689107517501DACDEE3A8E3F77F12FD311DDEEBDE153110`. Damit sind Dialogreihenfolge, Pumpfunktion, NoInlining, GC-Schleife und WeakReference-Messverfahren identisch. Ein geänderter Harness-Quelltext erklärt die Abweichung nicht.

[Runtimegraphvergleich](../../artifacts/a12-04-f01/runtime-graph-comparison.json): die Bibliotheks-ID-/Versionsmengen der beiden Bootstrap.deps.json sind identisch. Die vier selbstgebauten Prism-Assemblies haben gegenüber A12-02 unterschiedliche Binärhashes, die beiden Container-Pakete identische; vollständige Werte stehen im [Assemblyvergleich](../../artifacts/a12-04-f01/assembly-comparison.json). Gleiche technische Assemblyversion 1.0.0.0 darf nicht als Byteidentität ausgegeben werden.

A12-02 und A12-04 verwenden denselben offiziellen Commit, net10.0, Avalonia 12.1.4 und deaktivierte Git-Versionierung. A12-04 ergänzt den externen Build-Sicherheits-Pin und Lockfilemechanismus; Buildpfade unterscheiden sich. Die Anwendung enthält in A12-04 zusätzlich die bereits validierten F1-/C1-Korrekturen. Der Einfluss dieser Unterschiede auf das sporadische Verhalten ist nicht experimentell isoliert; aus unterschiedlichen Binärhashes folgt kein semantischer Prism-Fehler. Es wurde keine alte Prism-DLL in den neuen Graphen eingesetzt und kein neuer A12-02-Build als angeblich identische Kontrolle ausgegeben.

## 7. Unabhängige minimale Gegenprobe

Die neue `MinimalApp` erbt direkt von `Prism.DryIoc.PrismApplication`, registriert eine leere Shell mit FluentTheme und keine CryptoWorkBench-Dienste, Regionen, Editor- oder Anwendungs-ViewModels. Die zwanzig Dialogzyklen einschließlich Bindings, Ergebnisfehler, Veto, Window.Close, fehlendem Title und strikter GC-Assertion bleiben übernommen.

Jeder der zehn Läufe bestätigt explizit, dass keine Assembly mit Namen beginnend mit `Crypto` geladen ist. Prism-/Avalonia-Binärdateien stammen aus dem vollständigen A12-04-Output und bleiben unverändert. Die Gegenprobe ist damit vom CryptoWorkBench-Editor unabhängig, teilt aber bewusst das Dialogzyklus-/Pump-/GC-Messverfahren mit dem ursprünglichen Harness. Sie ist keine unabhängige Validierung dieses Messverfahrens selbst.

Ergebnis: 10/10 native Prozesse, 200/200 Dialogzyklen, jeweils 0/60 erreichbar. Dies schließt einen sporadischen Fehler in Prism oder Avalonia nicht aus und beweist insbesondere nicht, dass CryptoWorkBench die historische Retention verursacht.

## 8. Evidenzen und nächste Eigentümerentscheidung

[Neue Evidenzen](../../artifacts/a12-04-f01/) enthalten alle aktuellen Laufprotokolle, Exitcodes, maschinenlesbare Zusammenfassung, Hashvergleiche sowie die diagnostischen und minimalen Probequellen und Ausführungsskripte. Ein früher Diagnosebuild referenzierte versehentlich auch Bootstrap.dll und erzeugte CS0436-Konfliktwarnungen; der finale Projektstand schließt diese Datei aus, baut ohne Warnungen und wurde für die Diagnose-/Minimalserien verwendet. Der frühe Build wurde nicht als Laufzeitbefund verwendet.

**BLOCKED – Ursache weiterhin ungeklärt.** Der geforderte neue positive Reproduktionsfall und seine tatsächliche Root-Analyse fehlen. Die historischen Fehler dürfen trotz 44 aktuell grüner Prozesse nicht verworfen werden.

Nächster sinnvoller Untersuchungsschritt nach Eigentümerentscheidung: Bedingungen eines erneut auffälligen Desktops festhalten (Fokus, Verdeckung, Rendering, Prozess-/Runtimeumgebung), beim ersten positiven Zähler vor Target-Inspektion anhalten und mit `dumpheap`/`gcroot` reale Ketten erfassen. Anschließend Root-Lebensdauer mit gezieltem, protokolliertem Dispatcher-/Renderfortschritt vergleichen, ohne die ursprüngliche Assertion oder Zeitgrenze als Erfolgskriterium zu verändern. Nur bei belegter Ursache ist eine Kategorisierung als Harnessfehler, temporäre Retention oder konkreter Bibliotheksfehler gerechtfertigt.

Keine Änderung offizieller Prism-Quellen erscheint anhand der vorliegenden Daten begründbar und wurde vorgenommen. Keine Produktintegration, keine Freigabe weiterer MCP-Pakete. Stopp zur Eigentümerentscheidung.
