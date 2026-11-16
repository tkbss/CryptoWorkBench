# G1-A05-M1 – Maximize/Restore und finale Eigentümerabnahme

Stand: **10.11.2026 (Europe/Zurich)**. Repository `tkbss/CryptoWorkBench`, Worktree `artifacts/worktrees/gui-g1`, Branch `feature/gui-g1`, HEAD `7ecac04483c10722c4cb4b459e3f6cb5157c6af1`.

## 1. Entscheidung

**PASS – beabsichtigtes G1-Fokusmodus-Verhalten. Kein CHANGES REQUIRED aus diesem Befund.**

Die Beobachtung des Projekteigentümers entspricht der aktuellen Implementierung: Nach **Maximize Active View** sind neue Docking-Operationen absichtlich gesperrt. **Restore Layout** stellt die vorherige Layoutstruktur wieder her und entfernt die Fokusmodus-Sperre. Die üblichen Busy-/Pending-/Modal-/Fault-Gates bleiben dabei wirksam.

**Bedienregel: Zum Zurückdocken einer maximierten View zuerst `Restore Layout` ausführen.**

Die Regel bezieht sich auf den G1-Menübefehl und seinen Layout-Fokusmodus. Eine reine Betriebssystem-Fenstermaximierung über `WindowState` setzt diesen Fokuszustand nicht selbst.

## 2. Die fünf Prüfantworten

| Frage | Ergebnis |
|---|---|
| 1. Wird Redocking während Maximize Active View absichtlich verhindert? | **Ja.** Die Shell setzt einen expliziten Fokuszustand und sperrt neue Layoutmutationen auf Shell-, Factory- und Drop-Policy-Ebene. |
| 2. Welche konkrete Codebedingung bewirkt dies? | `DockShellLayout.CanChange` verlangt `!IsMaximized`; `EditorDockFactory.Allowed` verlangt bei einer neuen normalen Operation `!FocusMode`. `CanDrop` gibt genau diese Admission zurück. |
| 3. Bleiben Layoutsnapshot und Editoridentität geschützt? | **Ja, im vorgesehenen erfolgreichen Maximize-/Restore-Verlauf.** Vor der Projektion werden der normale Layoutsnapshot, ursprüngliche Listen-/Auswahlreferenzen und Proportionen gesichert. Restore verwendet diese Referenzen; die bestehende Session, View, TextDocument und UndoStack bleiben erhalten. Fehler werden weiterhin durch die vorhandene Recovery-/LayoutFaulted-Grenze behandelt. |
| 4. Ist Restore Layout der vorgesehene Weg? | **Ja.** Der dafür vorgesehene `ChangeLayout`-Einstieg darf den Fokusmodus verlassen; `RestoreCore` rekonstruiert die gespeicherte Präsentation und `ClearFocus` setzt `FocusMode = false`. |
| 5. Beabsichtigtes Verhalten oder übersehener Fehler? | **Beabsichtigtes Verhalten.** Benannte Zustände, explizite Sperren, eigener Restore-Einstieg und bestehende Fokuszyklus-Prüfungen belegen den Vertrag. Die gemeldete Folge „gesperrt im Fokusmodus, nach Restore wieder möglich“ passt dazu. |

## 3. Konkreter Codepfad und Admission

### Eintritt in den Fokusmodus

Die Pane-Menüs verbinden `Maximize Active View` und `Restore Layout` mit `ExecuteLayout` ([DockShellPresentation.cs, Zeile 82](../../CryptoWorkBenchAvalonia/Shell/DockShellPresentation.cs#L82)); das Hauptmenü verwendet denselben Layoutcommand-Weg ([DockShellWindow.axaml.cs](../../CryptoWorkBenchAvalonia/Shell/DockShellWindow.axaml.cs)). `ExecuteLayout` dispatcht auf `Maximize` beziehungsweise `Restore` und erhält die bestehende Fehlergrenze ([DockLayoutAction.cs, Zeile 53](../../CryptoWorkBenchAvalonia/Shell/DockLayoutAction.cs#L53)).

[DockShellLayout.Persistence.cs, Zeile 101](../../CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs#L101) sichert vor der Fokusprojektion:

- `normal = Capture(PlaceholderWindow)`;
- Proportion und CollapsedProportion aller betroffenen Objekte;
- je Vorfahren-Dock die ursprüngliche Kinderliste, aktive Pane und Proportion.

`ApplyFocusParts(focused)` reduziert die Darstellung entlang der Vorfahrenkette auf die gewählte View. Anschließend werden `_focus`, `_normalDockJson`, `_focusProportions` und `MaximizedPaneId` gesetzt; ausdrücklich folgt **`Factory.FocusMode = true`** (Zeile 128). `IsMaximized` ist als `_focus != null` definiert (Zeile 20). Auch bei einer Floating-View ist dies ein Zustand der gemeinsamen Shell-/Factory-Instanz; die Sperre gilt für deren neue Docking-Operationen insgesamt.

### Drei wirksame Sperren

[DockShellLayout.cs, Zeile 62](../../CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs#L62):

```csharp
private bool CanChange => !LayoutFaulted && !IsMaximized &&
    (((EditorDockFactory)Factory).Session?.CanRelocate ?? true);
```

`Move` und damit `Dock` kehren bei `!CanChange` ohne Mutation zurück. Dasselbe Gate schützt Show, Float und Close. Der explizite Dokument-Dock-Grip erhält `() => owner.CanChange` und beginnt bei fehlender Zulassung keinen Drag ([DockShellLayout.cs, Zeile 213](../../CryptoWorkBenchAvalonia/Shell/DockShellLayout.cs#L213); [DockDocumentWindowChrome.cs, Zeile 33](../../CryptoWorkBenchAvalonia/Shell/DockDocumentWindowChrome.cs#L33)).

[EditorDockFactory.cs, Zeile 94](../../CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs#L94):

```csharp
private bool Allowed => Exiting || LayoutOwner?.LayoutFaulted != true &&
    (_mutationDepth > 0 || !FocusMode && (Session == null || Session.CanRelocate));
```

Bei normalem Betrieb gilt für eine **neue** Operation `Exiting == false` und `_mutationDepth == 0`. Dann macht `FocusMode == true` die Bedingung falsch. Die Ausnahmen für Shutdown und bereits laufende interne Operationsschritte sind keine allgemeine Benutzerfreigabe. `Change` verweigert bei `!Allowed` die Mutation.

Die Factory implementiert zusätzlich `IDockDropPolicy`: `CanDrop(source, target, operation)` gibt `Allowed` zurück (Zeilen 100–104). Der gepinnte [DockManager.cs, Zeile 101](../../artifacts/g1/dock-source/src/Dock.Model/DockManager.cs#L101) konsultiert diese Policy in `IsDockingAllowed`; auch `ValidateDockableCore` ruft sie vor der eigentlichen Operation auf (Zeile 652). Somit wird ein Drop bereits in der Validierung abgewiesen und nicht erst nach einer Teilmutation ignoriert.

### Dock-Capabilities und Zielanzeige

Die Root-Docks erhalten `EditorDockFactory.Policy` sowohl im initialen Shell-Aufbau als auch bei `CreateRootDock` und beim Wiederanbinden eines geladenen Layouts. `EditorPolicy.Get(capability)` liefert bei `!Allowed` **false**. Das betrifft unter anderem Drag, Drop und Float. Die Auflösung erfolgt über [DockCapabilityResolver.cs, Zeile 31](../../artifacts/g1/dock-source/src/Dock.Model/DockCapabilityResolver.cs#L31). Individuelle Overrides hätten dort höhere Priorität; die separate `IDockDropPolicy` bleibt unabhängig davon ein abschließendes Veto.

Für Tab-Drags und native Host-Drags werden lokale und globale Ziele vor dem Aufbau der Dock-Adorner validiert:

- [DockControlState.cs, Zeile 366](../../artifacts/g1/dock-source/src/Dock.Avalonia/Internal/DockControlState.cs#L366) und [HostWindowState.cs, Zeile 66](../../artifacts/g1/dock-source/src/Dock.Avalonia/Internal/HostWindowState.cs#L66): `Enter` ermittelt `isLocalValid` und `isGlobalValid`.
- [DockManagerState.cs, Zeile 73](../../artifacts/g1/dock-source/src/Dock.Avalonia/Internal/DockManagerState.cs#L73): Zielverfügbarkeit und Aufbau der Adorner hängen von der Validierung ab. Die konkrete Zielprüfung verwendet `ValidateDockable(..., bExecute: false)` (Zeile 192).
- [DockTargetBase.cs, Zeile 640](../../artifacts/g1/dock-source/src/Dock.Avalonia/Controls/DockTargetBase.cs#L640): Eine aktive Dropmarkierung setzt erfolgreiche Validierung voraus; bei Ablehnung bleibt ihre Opacity 0.

Für konkrete Dock-Panes sind im Fokusmodus folglich **keine zulässigen Andockziele** verfügbar. Die sichtbare Fokusprojektion ist kein frei umbaubares Layout. Ein eventuell generisch dargestelltes Zielgerüst vor Auflösung einer konkreten Pane wäre keine Dropfreigabe: Der Code enthält eine vorläufige `IsDockTarget`-Abfrage, während die konkrete Validierung und Ausführung weiterhin gesperrt sind. M1 beansprucht keine neue pixelgenaue Desktopaufnahme; diese Aussage beruht auf dem aktuellen UI-/Policy-Code und der mitgeteilten Eigentümerbeobachtung.

## 4. Schutz von Snapshot und Editor; Rückweg

Während des Fokusmodus liefert `Capture` den gesicherten normalen Layoutbaum aus `_normalDockJson` statt der reduzierten Fokusprojektion ([DockShellLayout.Persistence.cs, Zeile 42](../../CryptoWorkBenchAvalonia/Shell/DockShellLayout.Persistence.cs#L42)). Das verhindert, dass eine temporäre Einzelansicht als reguläre Pane-Struktur gespeichert wird. Eine durch Redocking veränderte Projektion wäre mit den gespeicherten Restore-Referenzen nicht mehr derselbe Ausgangszustand; genau solche Strukturänderungen unterbindet das Gate.

`Restore()` verwendet `EditorDockFactory.ChangeLayout(RestoreCore)`. Dieser Einstieg hat ausdrücklich den Kommentar **„Focus blocks ordinary docking, but Restore/Reset still use the original admission gate.“** ([EditorDockFactory.cs, Zeile 165](../../CryptoWorkBenchAvalonia/Shell/EditorDockFactory.cs#L165)). Er prüft Exiting, LayoutFaulted und Session.CanRelocate, sperrt aber nicht allein wegen FocusMode.

`RestoreCore` validiert zuerst den gespeicherten Normalzustand, setzt die ursprünglichen Listen-/Auswahlreferenzen in umgekehrter Reihenfolge und die Proportionen zurück, ruft `ClearFocus` auf und validiert den Capture erneut. `ClearFocus` entfernt den Fokus-Snapshot und setzt `FocusMode = false` (Zeilen 139–171). Danach ist Docking wieder möglich, **sofern die übrigen Zulassungsbedingungen erfüllt sind**. Restore umgeht also weder Busy/Pending/Modal noch LayoutFaulted.

Die Operationen erzeugen keinen neuen Editor, kein TextDocument und keinen UndoStack. `DockEditorSession.Relocate` bleibt im vorhandenen Observer-Vertrag und schließt vor Hoständerungen ungültige Popups; `CreatePresenter` reicht dieselbe View weiter ([DockEditorSession.cs, Zeile 92](../../CryptoWorkBenchAvalonia/Shell/DockEditorSession.cs#L92)). Das schützt Identität und History, ohne normale Textbearbeitung im Fokusmodus zu verbieten. Bei fehlgeschlagener Wiederherstellung gelten weiterhin die bestehende gezielte Kompensation und gegebenenfalls die LayoutFaulted-/Persistenzsperre; es wird kein universelles Rollbackversprechen hinzugefügt.

Bestehende, lediglich gelesene Nachweise:

- [DockLayoutTests.cs, Zeile 158](../../CryptoWorkBenchGuiTest/DockLayoutTests.cs#L158): `FocusCyclesPreserveExactTreeDocumentHistoryAndRegistration` prüft zehn Maximize-/Restore-Zyklen je Pane, identischen Capture und Baum, Pane-/Document-/UndoStack-/ViewModel-/Registrierungsidentität, Generation sowie Undo/Redo.
- `FloatingFocusAndResetKeepPaneAndEditorObjects` ergänzt Floating-Views; [DockLayoutRoleTests.cs](../../CryptoWorkBenchGuiTest/DockLayoutRoleTests.cs) enthält legale Dokumentdrops mit Capture, Menü-Maximize, Restore und unveränderter SaveIntent-Snapshot-Identität.
- Die bereits gespeicherte [A05-Evidence](evidence/G1-A05-results.json) weist GUI 1.108/1.108 sowie die nativen Layoutprüfungen 48/48 und Layoutrollen 194/194 je Debug/Release als bestanden aus. Diese sind **vorhandene A05-Ergebnisse, keine neuen M1-Testläufe**.

## 5. Nachtrag: manuelle Eigentümerabnahme

**Quelle:** ausdrückliche Bestätigung des Projekteigentümers in der M1-Beauftragung dieses Chats. **Abnahmedatum: 10.11.2026.** Die folgenden sieben Szenarien wurden vom Eigentümer als **PASS** bestätigt:

| Nr. | Manuell bestätigtes Szenario | Ergebnis |
|---:|---|---|
| 1 | Einzelnes Tool redocken. | PASS |
| 2 | Vollständige Tool-Gruppe redocken. | PASS |
| 3 | CryptoScript-Dokument redocken, maximieren und wiederherstellen. | PASS |
| 4 | DocumentDock seitlich andocken. | PASS |
| 5 | Verschachtelte Dock-Gruppen. | PASS |
| 6 | Zwei Floating Windows. | PASS |
| 7 | Layout speichern und nach Neustart wiederherstellen. | PASS |

Damit liegt die zuvor ausstehende manuelle Eigentümerbestätigung **für diesen ausdrücklich genannten Umfang** vor. Die zusätzliche Beobachtung zum gesperrten Redocking während Maximize Active View ist mit dem vorgesehenen Fokusmodus vereinbar.

Dieser Nachtrag ergänzt den [A05-Abschlussbericht](G1-A05-native-chrome-input-acceptance.md). Dessen damaliger Offenstatus und sämtliche damaligen automatischen, externen und historischen Ergebnisse bleiben unverändert als zeitlicher Stand erhalten. Es werden keine neuen automatischen Läufe, HWND-Protokolle, Einzelgesten oder Prüfumgebungsdetails aus der Eigentümerbestätigung abgeleitet.

## 6. Änderungsumfang und Integrität

M1 erstellt ausschließlich diese Datei: `docs/gui/G1-A05-M1-final-owner-acceptance.md`. Keine Produkt-/Testcodeänderung, kein neuer Testlauf, keine Dock.Avalonia-Änderung, kein Commit/Push/Merge.

Vor dem Schreiben wurden 16.979 vorhandene Dateien inventarisiert: sämtliche Git-erfassten und nicht ignorierten Arbeitsdateien sowie alle vorhandenen Dateien unter `artifacts`, dedupliziert nach relativem Pfad. Die SHA-256-Aggregation aus sortiertem Pfad und jeweiligem Datei-SHA-256 lautet:

```text
E9E3548362EC76F31F27C8FB06D8504C259037114EEB3FA8FBF1DC1031FC1D06
```

Der Abschlussvergleich ergibt für denselben Bestand erneut **16.979 Dateien und exakt denselben Hash**; der neue M1-Nachtrag ist davon ausdrücklich ausgenommen. Alle inventarisierten Bestandsdateien, insbesondere Berichte, Evidence und Paket-Locks, sind damit bytegleich. Gegen den Eingangsstatus ist ausschließlich diese neue Datei hinzugekommen; kein bisheriger Statuseintrag ist entfallen. Der Hauptcheckout bleibt sauber. `git diff --check` besteht mit Exit 0. Die zusätzliche Prüfung der neuen Datei mit `git diff --no-index --check` liefert keine Diagnose; Exit 1 bezeichnet dabei die neue Inhaltsdifferenz. Alle lokalen Dokumentlinks sind auf vorhandene Dateien geprüft.

**M1 ist mit erfolgreicher Integritätsprüfung abgeschlossen. G1-A06 wurde nicht begonnen und bleibt einer separaten ausdrücklichen Freigabe vorbehalten.**
