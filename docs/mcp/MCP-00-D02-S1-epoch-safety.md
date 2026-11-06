# MCP-00-D02-S1 – Epoch Replay Safety Model

Stand: 26.10.2026. Grundlage: AP0-R1, AP0-D02 und Testmatrix E01–E12 in `MCP-00-AP0-contract-finalization.md`.

## 1. Ergebnis und Reichweite

**PASS für den vollständig explorierten endlichen Modellumfang unter den unten genannten Annahmen.** Das epochengebundene D02-Modell ist darin replay-sicher. Die Suche hat ihre Frontier vollständig geleert; keine der zehn Sicherheitsinvarianten wurde verletzt. Ein dauerhaft blockierter Drain verhindert sicher jede Kapazitätswiederverwendung. Er kann die Verfügbarkeit dauerhaft blockieren.

**Produktintegration: OPEN / INCONCLUSIVE. Keine Implementierungs- oder Hostfreigabe.** Der Nachweis setzt eine richtige Zuordnung sämtlicher realen Wirkungen und Producer zu den Modellpflichten voraus. Diese Zuordnung, die Synchronisation im Host, Credentialwiderruf, echte Plattformzeit und der reale .NET-Speicherverbrauch sind nicht durch dieses Modell bewiesen. Insbesondere ist ein Callbackbit kein Nachweis eines vollständigen Produktdrains.

Die genehmigte Designrichtung kann weiterverfolgt werden. Bis die Integrationsnachweise vorliegen, bleibt der konservative Rückfall verbindlich verfügbar: Erwerbsbudget pro Prozessinstanz, Tombstones bis Prozessende, kein Reset durch Disable/Enable. Ein Modell-PASS schliesst keinen AP1–AP8-Freigabepunkt automatisch.

## 2. Artefakte und Werkzeugwahl

Isolierter Prüfbereich: `C:\Users\stefa\Documents\ChatGPT\CryptoWorkBench-Mcp\mcp00-d02-s1`. Keine Imports aus dem Produkt, keine GUI, kein Passwortprovider, kein Dispatcher und keine produktive Registry werden durch das Harness ausgeführt.

Verwendet wird ein explizites endliches Transitionssystem in Python mit Breitensuche, struktureller Zustandsidentität und Prüfung **jeder** erzeugten Kante einschliesslich Stuttering. Dies ist der angeforderte gleichwertige State-Space-Ansatz: Initialzustand, Zustandsraum, Relation und Assertions sind ausführbar und getrennt sichtbar. Es gibt weder Zufallssampling noch einen Suchhorizont als Ersatz für vollständige Exploration. Ein erreichter Ressourcen-Cap liefert ausdrücklich INCONCLUSIVE. Python-Standardbibliothek genügt; kein zusätzlicher Modelchecker musste installiert werden. Der Checker selbst gehört zur Vertrauensbasis; eine unabhängige TLA+/TLC- oder zweite Checker-Replikation wurde nicht durchgeführt.

| Artefakt im Prüfbereich | Bedeutung |
|---|---|
| `model.py` | Endliches Kernmodell, zehn Invariantengruppen, vollständige BFS, Erreichbarkeitszeugen |
| `time_model.py` | Separates vollständig exploriertes Zeit-/CAS-/Timer-Modell |
| `probes.py` | E01–E12 als gerichtete Modellproben und elf absichtlich fehlerhafte Kontrollvarianten |
| `quotient_check.py` | Vergleich der Reduktion mit dem unreduzierten Modell in einer kleineren vollständig explorierten Instanz |
| `model_initial.py`, `model_ghost.py`, `quotient_ghost_check.py` | Erhaltene Vorstufen und Vergleich der ersten Reduktion |
| `results/*.json` | Maschinenlesbare Ergebnisse, vollständige Gegenbeispielzustände und Erreichbarkeitszeugen |
| `README.md`, `manifest.json` | Reproduktion und SHA-256-Zuordnung der Artefakte |
| `baseline.json`, `preservation.json` | Vorherbestand und abschliessende Erhaltungsprüfung |

Die Artefakte liegen absichtlich ausserhalb des Produktworktrees. Der Bericht ist die einzige neue Produktdatei; die lokalen Prüfartefakte sind nicht mitcommittet. Für eine unabhängige Weitergabe müssen Bericht und Prüfbereich zusammen aufbewahrt werden.

## 3. Formales System und Annahmen

### 3.1 Zustandsraum

Die Hauptinstanz enthält zwei Principals P0/P1, zwei aufeinanderfolgende Epochen E1/E2, zwei acquisitionIds je Principal, Kapazität 2 und Principalquote 1, Leaseversionen 1 bis 3. Dadurch sind zwei erfolgreiche Renew-CAS und ein älterer Retry darstellbar. Eine zusätzliche gerichtete Probe verwendet die vorgeschlagenen Originalgrenzen 4.096/256. Die kleinen Grenzen sind **keine** exhaustive Prüfung aller Belegungen einer 4.096-Einträge-.NET-Tabelle.

Zustand `S = (epoch, phase, ledger, lease, lease_releasing, serial, job, exhausted, fault, stopping, compact, grants, renews)`:

- `phase ∈ {active, closing, closed}`: genau ein Aktivierungsdatensatz, kein zweites aktives oder schliessendes Kontingent.
- `ledger`: `(epoch, principal, acquisitionId, fingerprint, leaseId oder terminale Ablehnung)`. Jede angenommene neue Erwerbs-ID verbraucht einen Slot, auch bei belegter Lease; Retries und Konflikte erzeugen keinen neuen Eintrag. Bei erschöpfter Kapazität wird keine neue Berechtigung vergeben.
- `lease`: exakt `(epoch, principal, global eindeutige leaseId, version, letzter erfolgreicher CAS)`. Release während eines Jobs sperrt neue Arbeit und hält die Lease bis zum Drain.
- `job`: akzeptiertes Ticket mit unveränderlicher Epochen-/Principal-/Leasebindung und Flags für Bodyabschluss, Commit, offenen Callback, Producer-Sealing und dauerhaftes Hängen.
- `exhausted`: klebende Bytebudget-Sperre, die im Epochenmodell nur L2 zurücksetzen kann. `fault` und `stopping` verhindern jede spätere Aktivierung.
- `grants` und `renews`: ausschliesslich Beweisvariablen für doppelte Erwerbswirkung bzw. doppelten CAS. Sie sind kein vorgeschlagener produktiver Historyspeicher.

Die Instanz hat einen seriell angenommenen Job und einen offenen Callback. Das Callbackbit kann als abstrakte offene Verpflichtung gelesen werden; der Checker beweist jedoch nicht die korrekte Zusammenfassung mehrerer realer Callbacks. Erneutes Posten nach Callbackabschluss und Posten nach Bodyabschluss bleiben bis zum Sealing möglich. Mehrere Producer, parallele Save-/Return-/Passwortketten und beliebig viele Tickets benötigen eine gesonderte Refinement-Zuordnung; sie sind keine versteckt mitgeprüfte Implementierung.

### 3.2 Vertrauensannahmen

1. Der Server bindet den Principal und die ursprüngliche Epoche authentisch und unveränderlich an jeden Request. Clientfelder allein können diese Bindung nicht herstellen. Es gibt keine Credentials zukünftiger Epochen. Alte authentisierte Kontexte dürfen beliebig spät und beliebig oft ankommen.
2. Alle im Modell einzelnen Übergänge sind linearisiert. Authprüfung, Reservation und Registrierung bei Admission gehören zu einem atomaren Schritt. Der spätere Commit prüft unter derselben Ordnung nochmals den ursprünglichen Kontext. Eine Prüfung vor dem Lock genügt nicht.
3. Jede angenommene Wirkung wird durch ein Ticket erfasst, bevor sie sichtbar oder dispatchbar wird. Ein Producer kann nach seinem Sealing keine neue Verpflichtung erzeugen; ein Callback verschwindet erst nach seinem vollständigen Abschluss einschliesslich möglicher Folgewirkungen. `Finish` repräsentiert den vollständigen Abschluss, nicht nur ein beendetes Runner-Task.
4. Epochen und Leasekennungen werden nie wiederverwendet. Im endlichen Modell endet die Aktivierbarkeit bei E2; der Leasezähler wird bei Rotation nicht zurückgesetzt. Eine echte endliche Repräsentation muss vor Überlauf geschlossen ablehnen und darf nicht umbrechen. Prozessneustart erfordert eine frische, nicht wiederverwendete Instanz-/Credentialdomäne.
5. Kapazitätsentscheidung und Tabellenänderung sind atomar. Repräsentationen und temporäre Allokationen werden vor ihrer Zulassung begrenzt; Budgetfehler eröffnen keinen alternativen Erwerbspfad.
6. Die Zeitkomponente erhält eine monoton fortschreitende, Suspend einschliessende Ablaufzeit. Der reale Plattformadapter ist noch nachzuweisen.

`Authenticate` ist als wirkungsloser Schritt abstrahiert: Der Generator liefert jeden früher erworbenen Epochenkontext nondeterministisch erneut an. Eine zusätzliche endliche Transportwarteschlange würde dieselben möglichen Zustandsübergänge lediglich verzögern. Es gibt keine automatische Umetikettierung auf die aktuelle Epoche. Netzwerkspeicherbegrenzung und Kryptographie werden dadurch nicht geprüft.

### 3.3 Übergänge und Abschlussgrenzen

| Übergang | Guard und Wirkung |
|---|---|
| Acquire | Aktuelle authentische Epoche, active, weder fault noch stopping; deduplizierter Schlüssel; Kapazität/Quote/Bytes vor Eintrag; eindeutige neue Lease nur ohne bestehende Lease/Job |
| Renew | Exakte Epochen-/Principal-/Leasebindung, nicht releasing; nur erwartete aktuelle Version erhöht Version/Frist. Retry des letzten erfolgreichen CAS ist wirkungslos; ältere CAS ebenfalls ohne Verlängerung |
| Release / ExpireOrReclaimLease | Exakte Bindung bei Release; bei offenem Job nur releasing, keine neue Admission/Commit/Renew; tatsächliche Freigabe nach Drain |
| Admit | Epochenguard und Leaseguard mit Registrierung des Jobtickets in einem Schritt |
| Commit | Eigenständige atomare erneute Prüfung; nach L1 keine neue Mutationsautorisierung |
| BodyDone / PostCallback / CallbackDone | Bodyabschluss entfernt das Jobticket nicht. Producer darf bis Sealing weitere Callbacks registrieren, auch während closing |
| SealProducer / Finish | Finish nur bei abgeschlossenem Body, keinem Callback, versiegeltem Producer, keinem permanenten Hänger |
| L1 | active → closing, atomarer Widerruf der alten Autorität; Requests mit alten Credentials bleiben alte Requests. Bereits angenommene Verpflichtungen bleiben im Drain |
| L2 | closing → closed ausschliesslich ohne Job, Lease oder unklaren Fehler; erst hier dürfen Epochen-Tombstones und die Bytebudget-Sperre freigegeben werden |
| L3 | closed → nächste active-Epoche, nur ohne stopping/fault; ein frisches Kontingent. Mehrfaches Enable ist kein paralleler Aktivierungspfad |
| Fault / Shutdown | Dauerhafte Sperre; Shutdown schliesst Admission. Kein erzwungenes L2 oder L3 |
| WaitCancelled / ResponseLost / CloseRetry | Stuttering: keine Freigabe, keine wachsende Liste zusätzlicher Close-Tickets, kein Budgetreset |
| HangForever | Verpflichtung kann dauerhaft nicht enden; kein Timeout beseitigt sie |

Vor L1 bereits autorisierte Wirkungen dürfen danach noch vollständig finalisiert werden. Deshalb sind `Commit` und die Vollendung seiner Folgen getrennt. Ein vor L1 nur authentisierter oder nur registrierter, noch nicht commitautorisierter Request erhält nach L1 keinen neuen Commit. Auf Produktseite müssen Textwirkung, Historywirkung und Finalisierung weiterhin nach D01 getrennt quittiert werden; ein unklarer Abschluss bleibt `faulted`, ohne automatischen Retry oder ungesicherten Rollback.

Deaktivierungsfehler vor einer Publikation können wirkungslose Fehlversuche sein; unklare Fehler nach L1 führen konservativ in `fault`. Das Modell bietet keinen Recoverypfad aus `fault` an. Ein später gewünschter Recoverypfad wäre eine neue Beweispflicht. Abbruch eines wartenden Aufrufers beendet nicht den dahinterstehenden Job. Ein Prozessabbruch wird ebenfalls nicht als erfolgreicher Drain interpretiert.

## 4. Vollständige Exploration und Reduktion

| Lauf | Zustände | Geprüfte Kanten | Ergebnis |
|---|---:|---:|---|
| `results/core.json`, Epochenmodell | 578.952 | 36.261.115 | PASS, Frontier leer |
| `results/fallback.json`, Prozessbudget | 323.124 | 14.006.390 | PASS, Frontier leer |
| `results/time.json`, Zeit/CAS | 268 | 12.166 | PASS, Frontier leer |
| `results/quotient.json`, kleinere Vergleichsinstanz | 64.500 unreduziert / 16.130 projiziert | 2.420.833 | PASS, Frontier leer |
| `results/quotient-ghost.json`, erste Reduktion | 64.500 unreduziert / 32.210 projiziert | 2.420.833 | PASS, Frontier leer |

Die JSONs des Kernmodells enthalten zusätzlich Erreichbarkeitszeugen für Rotation, Commit, Hänger während closing und Callbackregistrierung während closing. Die Safetyprüfung ist daher nicht bloss durch Unerreichbarkeit aller interessanten Aktionen erfüllt.

Die ersten Suchläufe erreichten je 1.000.000 Zustände ohne erschöpfte Frontier: `core-initial-cap.json` (25.131.213 Kanten), `core-ghost-cap.json` (31.312.138), `fallback-ghost-cap.json` (34.196.304). Diese Ergebnisse bleiben **INCONCLUSIVE** und sind erhalten. Sie werden nicht nachträglich als PASS umgedeutet.

Die finale Suche verwendet zwei begründete Reduktionen, keine Auswahl günstiger Schedules:

1. Abgeschlossene Ghost-Historie kann bei L2 entfallen. Sie beeinflusst keinen produktiven Guard. Alte Epochenkontexte bleiben ausdrücklich im Requestgenerator erhalten; der nie zurückgesetzte Leasezähler verhindert Leasealiasing. Die atomare Epochensperre verbietet jede neue Berechtigung für einen entfernten alten Schlüssel. Ein fehlerhafter Wiederzugang wird weiterhin durch I02/I06 erfasst. Im Prozessmodell bleiben Erwerbs-Ghosts über Rotation erhalten, da das Budget dort nicht erneuert wird.
2. Fingerprints werden pro Erwerbsschlüssel auf „erster Wert = 0“ und „abweichend = 1“ normalisiert. Die Relation verwendet ausschliesslich Gleichheit mit dem gespeicherten Wert; kein Recht hängt von dessen konkretem Wert ab. Eine konsistente Umbenennung pro Schlüssel erhält deshalb sämtliche sicherheitsrelevanten Nachfolger. Gleiches Retry und abweichender Konflikt bleiben erreichbar. Abgewiesene stale Requests dürfen zusätzlich identische Stutterkanten haben.

`quotient_check.py` vergleicht jeden Nachfolger modulo dieser Fingerprintumbenennung in der kleineren Instanz (2 Epochen, 2 Principals, 1 ID, Kapazität/Quote 1, Versionen 1–2). Dies ist eine zusätzliche ausführbare Kontrolle des Reduktionsarguments, **kein maschinengeprüfter allgemeiner Äquivalenzsatz**. Die ursprüngliche und finale Assertionsammlung unterscheidet sich ausserdem durch die verstärkte Prüfung eines Release-Effekts auf `lease_releasing`; die finale Suche prüft diese zusätzliche Verpflichtung.

## 5. Zehn Sicherheitsinvarianten

Für die geprüften Instanzen gilt `Init ∧ □Next ⇒ □I`. Der Checker prüft sowohl Zustandsprädikate als auch Übergangsbedingungen. Die eindeutige Repräsentation einer Epoche ist eine Spezifikationsentscheidung; der Nachweis, dass reale parallele Enableaufrufe sie implementieren, ist offen.

| ID | Geprüfte Verpflichtung und Beweismechanismus | Ergebnis |
|---|---|---|
| I01 | Ein aktiver/schliessender Epochenzustand; L3 nur aus closed mit leerem Drain und ohne Fehler/Shutdown; keine zweite Kontingenttabelle | PASS im Modell |
| I02 | Jede neue Erwerbs-, Renew-, Admission- oder Commitwirkung prüft die ursprüngliche aktive Epoche; nach L1 keine neue Autorisierung, releasing ebenfalls gesperrt | PASS |
| I03 | Admission registriert sofort das exakt gebundene Ticket; Finish darf weder Body-, Callback-, Sealing- noch Hängepflicht vergessen | PASS |
| I04 | L2 verlangt den vollständigen leeren Drain; ein offener schreibfähiger Callback hält das Jobticket offen | PASS |
| I05 | Kein doppelter erfolgreicher Erwerbsschlüssel; Tombstones wachsen monoton bis L2, beim Rückfall bis Prozessende; Compact entfernt keine Identität | PASS |
| I06 | Neue Berechtigung trägt die originale authentisierte Epoche; gleiche acquisitionId in E2 ist nur im neuen serverseitigen Kontext ein neuer Schlüssel | PASS |
| I07 | Renew, Release und Admission mit Wirkung müssen exakt zum aktuellen Tripel Epoche/Principal/leaseId gehören; alte leaseId beeinflusst keine Übernahme | PASS |
| I08 | Pro leaseId und erwarteter Version höchstens ein erfolgreicher CAS; Wiederholung erzeugt keine zweite Fristverlängerung | PASS, mit Zeitkomponente |
| I09 | Ledger und Principalquote begrenzt, keine parallele Tabelle; klebende Bytebudget-Sperre; Version kein Wrap; nur ein letztes Receipt | PASS für abstrakte Struktur, reale 4 MiB OPEN |
| I10 | Permanenter Hänger hält Epoche fest und verbietet L2; über I01 ist damit L3 ausgeschlossen | PASS |

### 5.1 Induktives Argument über weitere Rotationen

Dies ist ein nachvollziehbarer parametrisierter Beweisentwurf zusätzlich zur vollständigen endlichen Prüfung, kein mechanisierter Beweis für beliebige .NET-Ausführungen:

- **Initialisierung:** Eine aktive Epoche, leere Tabelle, keine Lease und keine angenommenen Verpflichtungen erfüllen Eindeutigkeit, Kapazität und Replayfreiheit.
- **Innerhalb einer Epoche:** Nur der atomare Acquire-Schritt ergänzt einen fehlenden Schlüssel nach Budgetprüfung. Retries verändern dessen Wirkung nicht; Freigabe der Lease löscht dessen Tombstone nicht. Renew kann nur die aktuelle Version konsumieren. Registrierung und Sealing erhalten die Verpflichtungsmenge. Kein normaler Schritt vergisst einen noch wirksamen Schlüssel oder eine offene Verpflichtung.
- **L1:** Die Autorisierung wird geschlossen, während bereits angenommene Verpflichtungen erhalten bleiben. Jeder alte Request ist entweder zuvor angenommen und erfasst, oder bei Admission/Commit nach L1 abgewiesen. Dieses Entweder-oder hängt wesentlich von der atomaren Grenze ab.
- **L2:** Nur das vollständige Drainzertifikat erlaubt das Entfernen der alten Tabelle. Damit existiert keine alte schreibfähige Fortsetzung mehr. Beliebig verspätete Requests existieren weiterhin, scheitern aber am unveränderlichen alten Kontext. Replayabwehr braucht deshalb keine unbegrenzt wachsende Liste aller alten Schlüssel oder Epochen.
- **L3:** Frischer authentischer Kontext und nicht wiederverwendete Lease-/Instanzkennungen stellen die Initialbedingungen für die nächste Epoche her. Dass eine acquisitionId dieselbe Zeichenfolge hat, ändert ihre Bindung nicht. Genau eine neue Tabelle wird aktiviert. Bei Erschöpfung eines Identitätszählers findet L3 nicht statt.

Dieses Argument lässt sich unter denselben Annahmen auf jede folgende Rotation anwenden. Der tatsächlich ausgeführte vollständige Zustandsraum endet dennoch bei zwei Epochen. Fehler in Kryptographie, Compiler, Lockzuordnung oder Callbackinventar liegen ausserhalb des Satzes.

### 5.2 Safety und Liveness

Es wird keine Fairness vorausgesetzt, um Safety zu erhalten. Der Zustand `hung ∧ closing` besitzt unendliche Stutterausführungen; keine davon aktiviert ein neues Kontingent. Das ist eine zulässige sichere Nichtverfügbarkeit. Auch ein unklarer Fault kann dauerhaft sperren.

Eventueller Abschluss setzt dagegen voraus, dass alle Provider und Callbacks terminieren, Producer erfolgreich versiegelt werden, notwendige Abschlussaktionen fair laufen und kein blockierender Fault/Shutdown verbleibt. Diese Liveness-Annahmen werden weder vom Modell erzwungen noch für echte Provider bewiesen. Ein Timeout, Cancel-Request oder „Task ist schon completed“ ersetzt kein Drainzertifikat.

## 6. Experimentelle Matrix E01–E12

Alle zwölf gerichteten Proben liefern `PASS_MODEL_ONLY` in `results/probes.json`. „PASS“ bezeichnet die jeweilige abstrakte Teilprobe; die vollständige produktbezogene AP0-Testforderung bleibt offen.

| Probe | Ausgeführt im isolierten Modell | Noch notwendige Produktprobe |
|---|---|---|
| E01 | Alte authentisierte Kontexte nach L1/L2/L3 liefern keine Acquire-/Admit-/Renew-/Releasewirkung | Reale HTTP-/MCP-DTOs, Edit/Execute, keine Text-/History-/Fristwirkung; unveränderliches Callerobjekt |
| E02 | Beide Ordnungen Commit vor L1 und L1 vor Commit; angenommener Job bleibt erfasst | Barrieren exakt zwischen Auth, Lock, Ticketregistration und Editorcommit, mit konkurrierendem Disable |
| E03 | 16 Principals × 256 IDs = 4.096; Principal-/Globalüberlauf, Retry, frühe klebende Bytesperre | Tatsächliche Allokationsgrenzen und Fehlerpfade, Compact, weiterhin zulässige Renew/Release ohne zusätzliche History |
| E04 | Gleiche ID erst nach sicherem Abschluss mit neuem Kontext; alte Lease bleibt wirkungslos | Credentialausgabe/Widerruf und reale atomare Aktivierung einschliesslich paralleler Enables |
| E05 | Offener Callback blockiert Finish/L2/L3 trotz Bodyabschluss | Return-P, Save-Write, Passwortantwort, Cancellationcallback, Resultatkopie/Q und Lockfreigabe jeweils einzeln pausieren |
| E06 | Nach Bodyabschluss darf vor Sealing erneut gepostet werden; nach Sealing nicht; Finish verlangt beides | Reale Dispatcher-/Providerregistrierung gegen pending=0-Race; mehrere Producer und Folgecallbacks |
| E07 | Fault vor/während/nach Schliessen und wiederholter Waitabbruch erzeugen kein Zertifikat | Disable/Dispose/Cancel/Timer/Resultatkopie wirft; genau ein Close-Ticket unter Parallelität und keine Publikation nach Fehler |
| E08 | 1.000 unveränderte Warteversuche und vollständige Zustandsraumprüfung des Hängerzustands | Echter dauerhaft blockierter Provider; Transport-/Wartelisten müssen ebenfalls begrenzt bleiben |
| E09 | 10.000 synthetische sichere Rotationen, eine Tabelle, unabhängiges Execution-ID-Budget unverändert | .NET GC-/Heap-/Retentionmessung für Tables, Keys, revokedEpochs, Closures, Callbacks und Timer |
| E10 | 10.000 identische CAS-Retries ohne weitere Verlängerung; 10.000 rechtzeitige neue Renewals ohne absolute Höchstdauer; letztes Receipt | Echte CAS-Linearisation, verlorene Antwort, alte Timer und Serialisierung eines unveränderten letzten Receipts |
| E11 | Suspendzählende Fakezeit, Ablauf vor/nach 600 s, alte Timer, Wanduhränderungen; zusätzlich vollständiges Zeitmodell | Jede unterstützte Plattform suspendieren; Resume- und Timerdispatch verzögern; Deadline vor nächster Wirkung prüfen |
| E12 | Shutdown an Abschlussgrenzen; endliche Versions-/Epochgrenze lehnt geschlossen ab | Echte Crash-/Neustart-Instanztrennung, Zählerüberlauf, alte Credentials und fortlebende externe Providerwirkungen |

Zwei Principals können bei Quote 256 maximal 512 Slots verbrauchen; deshalb verwendet die numerische E03-Probe 16 Principals. Die 10.000-Rotations-Probe ist eine gerichtete Strukturprobe, kein Ersatz für exhaustive Prüfung eines 10.000-Epochen-Systems und keine Heapmessung.

Das Zeitmodell verwendet Tick 200 Sekunden, `now ∈ 0..6`, Leasefrist 3 Ticks, Versionen 1–3 und zwei Epochen. Advance/Suspend kann mehrere Ticks überspringen. Wanduhränderungen sind wirkungslos. Admission/Renew prüfen `now >= deadline` unabhängig vom Timerdispatch; nur der zur aktuellen Epoche/Version/Deadline passende Timer kann wirken. `CertifiedNewEpoch` setzt das L2-Zertifikat des Kernmodells voraus. Beide Komponenten wurden separat geprüft; ein vollständig exploriertes Produkt ihrer Zustandsräume und die OS-Refinement-Prüfung wurden nicht ausgeführt.

## 7. Gegenbeispieltraces

Elf absichtlich fehlerhafte Varianten werden durch unveränderte Sicherheitsassertions zurückgewiesen. Die gerichteten Traces samt allen Zwischenzuständen stehen in `results/<Variantenname>.json`. Sie sind keine behaupteten minimalen BFS-Traces und keine nachgewiesenen Produktbugs. Für das korrekte Modell wurde kein Gegenbeispiel gefunden.

Notation: A = Acquire, J = Admit, C = Commit, P = PostCallback, B = BodyDone, S = SealProducer, F = Finish, R = Release.

| Fehlerhafte Variante | Verletzender Trace (gekürzt) | Verletzung |
|---|---|---|
| `missing_epoch_fence` | L1 → A(E1) | I02: neue Berechtigung nach Widerruf |
| `missing_commit_fence` | A → J → L1 → C | I02: spätes Commit trotz alter Epoche |
| `finish_ignores_callback` | A → J → P → B → S → F | I03/I04: Callback vergessen |
| `finish_ignores_seal` | A → J → B → F | I03/I04: noch offener Producer vergessen |
| `close_ignores_drain` | A → J → P → L1 → L2 | I04: Tombstones trotz offener Wirkung freigegeben |
| `rebind_epoch` | L1 → L2 → L3 → A(original E1) | I02/I06: alte Anfrage als E2 umgebunden |
| `old_lease_alias` | A(E1,l1) → R → L1/L2/L3 → A(E2,l2) → Renew(E2,l1) | I07: alte Lease beeinflusst neue Übernahme |
| `renew_replay_extends` | A → Renew(v1) → Renew(v1) | I08: derselbe CAS zweimal wirksam |
| `evict_tombstone` | A(id0) → R → Compact(löscht) → A(id0) | I05: früher Verlust und doppelte Erwerbswirkung |
| `enable_while_closing` | A → J → HangForever → L1 → L3 | I01/I10: Wiederverwendung trotz Hänger |
| `fallback_reset` | Prozessbudget: A → R → L1 → L2 → L3(löscht Tabelle) | I05: Disable/Enable setzt Prozessbudget zurück |

Die Kontrollvarianten zeigen, dass zentrale Guards und Retentionpflichten von den Assertions erkannt werden. Sie beweisen nicht die Fehlerfreiheit des Checkers oder die Vollständigkeit des realen Mutationsinventars.

## 8. Kapazität, Speicher und Rückfall

**Replay-Sicherheit und begrenzter Speicher sind vereinbar, wenn der Epochenabschluss eine tatsächliche Autoritäts- und Wirkungsgrenze bildet.** Innerhalb der Epoche bleiben sämtliche verbrauchten Erwerbsschlüssel erhalten. Nach L1 kann kein alter Kontext neu autorisieren. Nach L2 existiert auch keine angenommene schreibfähige Fortsetzung mehr. Erst dann können die Tombstones verschwinden; alte Requests benötigen keine historische Liste, weil sie mit dem aktuellen, authentisch gebundenen Kontext nicht übereinstimmen. L3 publiziert nur ein neues Kontingent. Wiederholtes Disable/Enable während closing ist wirkungslos.

| Eigenschaft | Aussage des Nachweises | Offener Teil |
|---|---|---|
| 4.096 je Epoche / 256 je Principal | Numerische Probe erfüllt beide Grenzen; allgemeiner Einfügeguard erhält Kardinalitätsgrenzen induktiv | Reale atomare Reservation, Speicher für beliebig viele abgewiesene Principals/IDs darf nicht unbegrenzt wachsen |
| Höchstens 4 MiB Leaseprotokoll | Abstrakte Byte-Sperre ist klebend. Probe nimmt ausdrücklich 1 MiB Basis und 512 Byte je Slot an (bei 4.096 Slots 3 MiB) | Diese Kosten sind **angenommen**, keine .NET-Grössen. Strings, DTOs, Hashkapazität, Receipts, Timer, temporäre Allokationen und Retention messen/reservieren |
| Letztes Renewreceipt | Genau ein Receiptfeld; jeder neue erfolgreiche CAS ersetzt es; alte Retries verlängern nie erneut | Serialisierte Receiptgrösse, Cache-/Logging-/Closureduplikate und reale Fristatomarität |
| Tombstones / Rotation | Keine Freigabe vor L2, kein paralleles Kontingent, keine Akkumulation alter Tabellen im abstrakten Zustand | Tatsächliche Freigabe aller Referenzen; kein wachsendes revokedEpochs-/Schlüsselarchiv |
| Lange rechtzeitige Lease | Keine semantische absolute Höchstdauer; gerichtete Probe erneuert über mehr als fünf Millionen Sekunden | Zählerdimensionierung und fail-closed Überlauf; lange Laufzeit bei konstantem Speicher |
| Identitätszähler | Kein Reset bei Rotation, endliche Instanz ohne Wrap | Ein unbeschränkter mathematischer Integer hat keine konstante Bitgrösse. Produkt benötigt feste Repräsentation, Nichtwiederverwendung und sichere Erschöpfung |

Die 4 MiB sind eine harte Zulassungsobergrenze, keine Zusage, dass jede beliebige Implementierung stets alle 4.096 Slots ausschöpfen kann. Frühe Bytesperre darf Kapazität reduzieren, niemals Tombstones verdrängen. Transport-/Executionbudgets sind getrennt zu begrenzen und dürfen durch Epochenrotation nicht zurückgesetzt werden. Eine unbegrenzte Liste abgewiesener Requests oder wartender Enableaufrufe würde das Speichermodell verletzen.

| Aspekt | Epochenmodell | Konservativer Rückfall |
|---|---|---|
| Erwerbsbudget | Je sicher abgeschlossener Aktivierungsepoche erneuerbar | Für gesamte Prozessinstanz verbraucht |
| Tombstones | Bis L2 vollständig erhalten | Bis Prozessende vollständig erhalten |
| Disable/Enable | Kein Reset ohne L1/L2/L3 und frische Autorität | Niemals Budgetreset |
| Zusätzliche Sicherheitsannahme für Freigabe | Vollständige Producer-/Callback-/Wirkungsfinalisierung plus unwiderruflicher Kontextwechsel | Kein laufzeitlicher Retentionsreset; daher keine Wiederverwendungsprämisse nötig |
| Gemeinsame Pflichten | Authentische Callerbindung, exakte Leaseidentität, atomare Admission/Commit/CAS, sichere Zeit, geschlossene Fehlerbehandlung | Dieselben Pflichten; Rückfall repariert keine fehlende Commitprüfung |
| Preis bei langem Betrieb | Verfügbarkeit bei blockiertem Drain nicht garantiert | Auch nach sauberem Drain keine neue Erwerbskapazität ohne Prozessende |

Die vollständige Fallback-Suche erhält Tabelle und Principalverbrauch über beide Aktivierungen. Das Prozessmodell darf seine Quote nicht pro Epoche neu berechnen. Neustart ist bei beiden Modellen ein eigener Sicherheitsübergang: alte Credentials dürfen nicht wieder gültig werden, und ein abgebrochener Prozess gilt nicht rückwirkend als erfolgreich drainiert.

## 9. Erforderliches Refinement auf das Produkt

Die folgenden Nachweise sind einzeln erforderlich und bleiben **OPEN**:

1. **Registry / Aktivierung:** `CryptoWorkBenchAvalonia/Services/Automation/WorkbenchAutomationRegistry.cs`, insbesondere `ActivateMcp`, `RevokeMcpCore`, `DisableMcp`, `BeginShutdown`, `DrainAsync` und `DisposeAsync`. Im gelesenen Iststand führt `ActivateMcp` unmittelbar nach `RevokeMcpCore` die neue Epoche ein; `revokedEpochs` ist ein wachsendes HashSet. `DrainAsync` wartet auf `active?.Task` und `saveCompletion?.Task`. Das ist kein belegtes L1/L2/L3-Zertifikat des hier spezifizierten Systems. Die bestehende Implementierung wird durch dieses Ergebnis weder verändert noch freigegeben.
2. **EditorCoordinator / Admission und Commit:** Jede spätere Coordinator-Reservation muss einen konkreten linearen Übergang des Modells erhalten. `WorkbenchAutomationService`, `EditorBridge`, `WorkbenchEditorOperations`, Save-Reservation und jeder GUI-/MCP-Mutationspfad müssen dieselbe Sperrordnung einhalten. Vor dem Lock authentisierte Requests brauchen an Admission und am tatsächlichen Commit nochmals ihre ursprüngliche Epoche. Tests müssen die Lücke absichtlich mit Barrieren treffen, auch bei Reentranz und Exceptions.
3. **Vollständiger Drain / Producer-Sealing:** `WorkbenchExecutionPipeline`, `WorkbenchSaveCommit`, Passwortantworten und Cancellationregistrierungen sowie `CryptoScriptEditView.axaml.cs` mit `PendingExecution` und interaktiver Return-Ausgabe benötigen separate Ticketzuordnung. Runnerabschluss darf nicht mit Return-, Save-, Passwort-, UI-Post-, Resultatkopie-, Q- oder Lockabschluss gleichgesetzt werden. Für jeden Producer ist nachzuweisen: Registrierung vor Dispatch, kein Nachregistrieren nach Sealing, Ticketfreigabe erst nach letzten möglichen Folgen. AP0 M43/M48 und OBA26 sind hierfür relevante Inventarbezüge, keine bereits erfüllten Zertifikate.
4. **MCP-v2-Verträge und Credentials:** Striktes `contractVersion="2"`, keine v1-Fallbackmutation; `editor.control` und `leaseId` sind zusätzliche, nicht hinreichende Bedingungen. Tests gegen echte DTO-/JSON-Schema-/Transportpfade müssen Principal, Instanz, Epoche, Erwerbsschlüssel, Fingerprint und Lease vollständig binden. Credentialwiderruf darf keine Umwandlung alter Caller in neue Kontexte enthalten. Parallelität, verlorene Antworten und Neustart sind einzubeziehen.
5. **Speicherinstrumentierung:** An der tatsächlichen .NET-Repräsentation 4.096/256, frühe Bytesperre und maximal 4 MiB inklusive definierter Overheads nachweisen. Wiederholte sichere Rotationen mit GC-/Heap-Dumps und Retentionanalyse; keine historischen Tabellen, revokedEpochs, Credentials, Receipts oder Closures. Grenzfälle dürfen Renew/Release nicht durch neue unbegrenzte History erzwingen.
6. **Zeit / Renew / Suspend:** Plattformgerechte suspendzählende Zeitquelle auswählen und real prüfen. Resume darf keine Frist verlängern. Vor nächster Wirkung auch bei verspätetem Timerdispatch die Deadline prüfen. Alte Timer müssen Epoche, Lease, Version und Deadline prüfen; nur der letzte erfolgreiche CAS-Receipt bleibt gespeichert. Keine absolute Höchstdauer rechtzeitiger Erneuerung einführen.
7. **Fehler / Shutdown / Neustart:** E07/E08/E12 mit echten Dispose-/Cancel-/Provider-/Dispatcherfehlern, parallelem Enable, abgebrochenem Warten, dauerhaftem Hänger und Prozessabbruch ausführen. Keine erzwungene Freigabe, kein behaupteter Drainerfolg bei Crash, keine Zählerwiederverwendung. Für jeden Recoverypfad einen eigenen Refinementbeweis erbringen.

Die bestehenden 51 Mutationspfade und 31 Observergruppen bleiben unverändert und müssen bei dieser Zuordnung vollständig abgedeckt werden. Historische Findings, Testassertions und R2-Entscheidungen werden nicht umgeschrieben. D01, D03–D08 bleiben verbindlich; insbesondere bleiben native IME-Unterdrückung und die vollständige Hostfreigabe OPEN. Dieser Auftrag implementiert keine AP2–AP8-Arbeit und erteilt auch keine automatische Freigabe für AP1.

## 10. Reproduktion und Erhaltungsnachweis

`README.md` im Prüfbereich enthält die vollständigen Befehle. Die Kernläufe lassen sich mit Python ohne externe Pakete wiederholen; `--max-states 3000000` ist nur eine Ressourcengrenze und war bei den PASS-Läufen nicht ausschlaggebend. Maschinenlaufzeiten sind keine Sicherheitsbehauptung. Die JSONs enthalten Konfiguration, Zustands-/Kantenzahlen und beim Kernmodell den SHA-256 des ausgeführten Modells; `manifest.json` ergänzt die übrigen Artefakte.

Die abschliessende Erhaltungsprüfung vergleicht alle 23.333 vor Beginn erfassten Originaldateien einschliesslich H1–U2 und Git-Metadaten per SHA-256. HEAD, vollständiger Porcelain-v2-Status und Staging werden separat verglichen. Die einzige zulässige Statusergänzung ist `? docs/mcp/MCP-00-D02-S1-epoch-safety.md`. Das maschinenlesbare Ergebnis steht in `preservation.json`; nur dessen erfolgreiches Ergebnis bestätigt die nachstehende Abschlussaussage.

Abschluss: Originaldateien unverändert; bestehende sechs modifizierte und zwanzig ungetrackte Dateien erhalten, zusätzlich ausschliesslich dieser neue Bericht im Produktworktree. HEAD bleibt `bd48c57fa4cae9dc733da7d4521eb6f79a9017f0`, Staging unverändert leer. `.codex/config.toml` wurde von Lese-/Hashoperationen ausgeschlossen und weder gelesen noch verändert. Keine produktiven C#-/AXAML-/Projekt-/Teständerungen, keine AvaloniaEdit-Änderung, kein Build, Commit, Push, Merge, Reset oder Rebase.

## 11. Empfehlung

**Ja: innerhalb der expliziten endlichen Modellannahmen ist D02 replay-sicher.** Die entscheidende Verbindung zwischen Replayabwehr und Speichergrenze ist das nicht überspringbare L2-Zertifikat nach widerrufender L1-Grenze: alte Requests bleiben unwirksam, alte angenommene Wirkungen sind vollständig beendet, bevor ihre Tombstones entfallen und L3 ein neues Kontingent erhält.

Für das reale Produkt bleibt die Bewertung INCONCLUSIVE, bis insbesondere die atomaren Admission-/Commitstellen, vollständige Producer-/Callbackdrains, Credential-/Instanztrennung, suspendzählende Plattformzeit und die instrumentierte 4-MiB-Grenze nachgewiesen sind. Scheitert eine dieser Voraussetzungen, darf keine Epochenkapazität wiederverwendet werden; der konservative Prozessbudget-Rückfall ist beizubehalten. Dieser Bericht beendet den isolierten Prüfauftrag ohne produktive Implementierungsfreigabe und ohne automatische Fortsetzung.
