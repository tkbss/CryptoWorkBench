# F2 — Vollständige Implementierungs-Abnahmekriterien

**Normativer Architekturanhang · READY FOR OWNER REVIEW, nicht ACCEPTED**  
Dies sind verbindliche Kriterien für eine später autorisierte G2-04-02. F2 führt keine Produkt-/GUI-Tests aus und verändert keine Tests. Ein bestandenes Dokumentations-Skript ersetzt keine dieser C#-/Native-Abnahmen.

## 1. Typen, Inventar und Read-only

| ID | Nachweis | Erwartetes Ergebnis |
|---|---|---|
| A01 | Alle sieben tatsächlichen Deklarationstypen gegen Reflection-Metadaten prüfen. | 6/5/5/7/14/6/6 Root-Properties; geerbtes Text; Runtime-Typ statt VariableModel.Type; keine fehlende Property. |
| A02 | Alle Unterobjekte und BCL-Familien der Accessor-Matrix, einschließlich nicht-null HeaderReference und OriginalKeyMetadata. | Public-instance-Inventar vollständig; Count/Capacity/Keys/Values/Comparer/Length/SyncRoot, wo tatsächlich öffentlich. Collection-Einträge separat. Keine erfundenen Properties aus expliziten Interfaces. |
| A03 | Overrides, `new`-Verdeckung, Indexer, privater Getter, WriteOnly und unbekannter Runtime-Untertyp. | Override einmal, verdeckte Slots unterscheidbar; Indexer nur mit dokumentierten Argumenten; unbekannte Getter sichtbar, Aufruf-/Mutationszähler bleiben 0. |
| A04 | Unbekannte IList/IDictionary/Enumerable-Implementierung unter bekanntem Interface und ReadOnly-Wrapper. | Kein pauschales Enumerieren/Getter-Dispatch. Bekannte Core-Konstruktorherkunft separat nachweisen. |
| A05 | Öffnen, Expand/Collapse, Detailansicht, Copy, Theme, Docking. | Keine Änderung an Declaration.Value/Text, PARAM.Mechanism/Dictionary/HeaderReference, StateRevision, History oder Print-Ausgabe. Keine Setter/TwoWay-/Modify-Kommandos. |
| A06 | PARAM-Value gegen unveränderten vorliegenden Wert vergleichen; PrintOutput mit Mutationsprobe instrumentieren. | Inspector führt PrintOutput/BuildValue nie aus. Methodenadapter kopiert Dictionary vollständig unter Host und gibt keine lebende Referenz zurück. |
| A07 | TR31H ohne/mit Optional Blocks, ExpectedLength null/gesetzt, fremder JsonConvert.DefaultSettings-Delegate. | Aktueller bekannter Value exakt; OptionalBlocks/Definition vollständig. Fremder Delegate wird nie ausgeführt; Value dann UnsupportedFormatter statt erfundenem Ersatz. Keine globalen Serializer-Einstellungen verändern. |
| A08 | AES/TDEA KCV/KCVC/KCVK gegen bestehende Referenzwerte; Unknown/HMAC; malformed/überlang; Base64 mit viel Whitespace. | Berechnung nur ausdrücklich; identische Stringwerte/Null-Semantik; weder Originalnormalisierung noch Decoder-Allokation proportional zu unzulässig großem Material. Keine UI-Berechnung. |

## 2. Exakte Werte und Copy

| ID | Nachweis | Erwartetes Ergebnis |
|---|---|---|
| A09 | Die 19 dokumentierten Goldens als unabhängige erwartete Strings verwenden; vollständige Rootwerte KEY/PARAM/SECRET/TR31H. | Codeeinheitenvergleich exakt; keine type/members/status/Revision-Hüllen. Kein erfundenes Parameters-Member am PARAM-Root. |
| A10 | Strings: Unicode, Combining Characters, Surrogatpaare, führende/nachlaufende Leerzeichen, CRLF, LF, abschließendes LF, empty/null. | Keine Normalisierung; Null/Empty/Fehler im UI getrennt. String-Hex behält Groß-/Kleinschreibung. |
| A11 | byte[] empty, 00..FF, mehrere Segmente, Share.Value ohne X-Präfix; ulong.MaxValue und negative long-Grenze. | Byte-Roundtrip exakt; vollständige Zahl ohne Double-/Formatverlust. Byte-Copy hexgroß, stringförmiger KCV unverändert. |
| A12 | JSON-Keys mit Steuerzeichen/Unicode und tatsächlichen Namen type/status/$ref/revision; Dictionary-Einträge mit numeric-looking Keys. | Ordinale Reihenfolge auch für „2“/„10“; echte Keys behalten. Escaping nach Vertrag; kein JavaScript-Eigenschaftsreihenfolge- oder Serializer-Default-Artefakt. |
| A13 | Wiederholte Referenz, echter Vorfahrenzyklus, Array.SyncRoot, unbekannter Getter, GetterError, NotCalculated und NotLoaded. | Aliaswert dupliziert; Cycle-Parent nicht kopierbar; Arraybytes trotzdem kopierbar. Kein Status/Pfad als Wert. Fehlende bekannte Werte nur bei gültigem Token nachladen; Berechnung ausdrücklich benannt. |
| A14 | Historisch/aktuell, ein-/ausgeklappt, andere UI-Seite und Auswahlwechsel während asynchroner Clipboard-Übergabe. | Gleicher aufgenommener Wert → identischer Payload; Herkunft bleibt die beim Start gebundene. Vollständige historische Werte ohne Live-Admission kopierbar. |
| A15 | Clipboard nicht verfügbar, native Längen-/Speicherfehler, Embedded-NUL/isolierte Surrogate; Close/Reset während Formatierung. | Kein verkürzter oder normalisierter „Erfolg“. Noch nicht übergebene Arbeit geordnet abbrechen; nach OS-Übergabe kein unsicheres Rückruf-/Löschversprechen. Exakte Plattformgrenzen protokollieren. |

## 3. Große Daten, Speicher und Zeit

| ID | Nachweis | Erwartetes Ergebnis |
|---|---|---|
| A16 | Algorithmischer 255×131072-Byte-SECRET-Golden (31,875 MiB) über einzelne Shares, Seiten und vollständigen Parent. | Alle 255 Values erreichbar/kopierbar; jedes 262148 Hexzeichen. Kein 16-MiB-Rootlimit. Parent enthält alle Member/Bytes, keine Vorschau. |
| A17 | String über 16 MiB, >10000 Nodes, tieferer endlicher Graph über Renderfenster, abgesenkte Cache-/Schrittbudgets. | Vollwertpfad arbeitet weiter; UI-Seiten/Expandfenster begrenzen nur Darstellung. Keine Rekursion ohne Zykluserkennung; keine stille Kürzung. |
| A18 | Neue SecretShare.CopyValueRange auf offset 0, Mitte, Ende, leerer Range, invalid Bounds; Vergleich mit bisherigem Value-Getter auf kleinen Fixtures. | Exakt identische Bytes; keine Mutation; kein Gesamtclone pro Teilrange; invalid Bounds kontrolliert abweisen. |
| A19 | Cachefüllung über 32 MiB, IO-/Quota-Fehler, knapper Diskspace, Cancel vor/nach Segment-Commit. | Spill in snapshotlokale Segmente; vollständige historische Daten nicht durch RAM-Eviction verlieren. Unvollständiges Staging nicht als vollständigen Wert publizieren. ResourceUnavailable statt Teilcopy. |
| A20 | Peak-Speicher messen bei Full Copy; mindestens ein vollständiger Payload oberhalb des RAM-Cachebudgets. | Cache/Arbeitsbuffer/abschließender Clipboard-String getrennt messen. Kein unkontrolliertes mehrfaches Hex-/JSON-Materialisieren. Kein funktionales Cachelimit am Clipboard-Payload. |
| A21 | Performance auf dokumentierter Referenzhardware: kleine Fixtures, Großwert, Cancel, lange laufende Operation. | Capture-/Render-Dispatcher-Arbeit p95 ≤8 ms, kein einzelner eigener Capture-/Formatblock >50 ms auf UI. Core-Schritte ≤256 Einträge/256 KiB; Ziel ≤10 ms. Abbruch am nächsten sicheren Schritt/IO-Abbruchpunkt. Native Clipboard-Handoff-Zeit getrennt berichten; keine verdeckte Ausnahme als bestandene Capture-Messung. |
| A22 | Snapshot freigeben, Tool-Close, Workspace-Reset und Shutdown bei Payload-Leases; simulierter Prozessabbruch. | Eigene Dateien/Handles nach Drain entfernt, keine fremden Pfade gelöscht. Crash-Reste erkennbar/dokumentiert. Keine Layoutpersistenz oder unendliche globale Payload-Registry. |

Messprotokoll nennt OS/.NET, CPU, RAM, Storage, Payloadgrößen, Warm-/Cold-Run, UI-Zeitverteilung, Peak-RAM/Disk und Cancel-Latenz. Eine einzelne schnelle Demo oder ein niedriger Mittelwert reicht nicht. Physische OS-/RAM-/Disk-Grenzen sind technische Fehlerzustände, keine Erlaubnis zur stillen Kürzung. Ohne Nachweis des großen vollständigen Copy-Wegs gilt A16/A20 als nicht bestanden.

## 4. Observer, Host und Revisionen

| ID | Nachweis | Erwartetes Ergebnis |
|---|---|---|
| A23 | SelectedVariable.PropertyChanged innerhalb realer WorkbenchBindable-/Coordinator-Observation. | Handler erfasst nur Absicht; genau ein nachgelagerter Dispatcher-Aufruf. Kein synchroner Capture; Observer-Sperre bleibt wirksam. |
| A24 | A→B→A, ApplyVariables während Benachrichtigung, Close/Dispose vor Dispatcher, eigene laufende Aufnahme plus neue Auswahl. | Nur jüngste Absicht publiziert; eigene Cancellation/Drain vor einmaliger neuer Aufnahme. Echte Busy-Ablehnung startet keine Retry-Schleife. |
| A25 | Interner Read-Zweck im vorhandenen Accept/RunAdmitted-/Finalization-Pfad; Annahmefehler an jedem Schritt. | Ein aktiver Hostowner, keine zusätzliche Queue. Reservation/Intent genau einmal freigegeben. Stop/epochClose sieht alle Reads. Fehler vor Workerstart hinterlassen kein Active-Read. |
| A26 | Viele Seitenreads oberhalb üblicher Execution-Retentionzählung; öffentliche Execution-Statusabfragen parallel. | Keine Execution-ID-Tombstones/Retention-Leaks, kein LastExecution-Überschreiben, keine Variablen-Reprojektion. Read-Ergebnisse verlassen den internen Inspector-Pfad nicht als Scriptresultate. StateRevision bleibt konstant. |
| A27 | Worker kann live lesen, aber keine UI-affine ValidateStateAccess-/Intent.Current-Prüfung aufrufen; Await-Grenzen instrumentieren. | UI-Admisson vor Annahme und UI-Validierung vor Publish; Dictionary/Getter nur innerhalb AccessState. Niemals await innerhalb eines synchronen AccessState. |
| A28 | Assignment gleicher Name/neues Objekt, In-place-Mutation gleiches Objekt, GUI-Draft-Edit, Weak-Origin weg; Änderung zwischen zwei Read-Operationen. | Origin+Original-Identifier+StateRevision+Publikation prüfen. Alte Teile bleiben R; fehlende Teile von R+1 nicht ergänzen. Draftwerte nicht als Quelle verwenden. |
| A29 | Vollständiger Großread hält Admission, während GUI-Execution/MCP Acquire versucht wird; Cancel/Shutdown während eines aktiven Getter-Schritts. | Neue State-Jobs erhalten Busy. UI bleibt bedienbar. Gate wird erst nach tatsächlichem Abschluss/Drain freigegeben, nie allein wegen Timeout/Cancelled-Flag. |

## 5. Ereignismatrix und Docking

| ID | Nachweis | Erwartetes Ergebnis |
|---|---|---|
| A30 | Normaler Edit, MCP Acquire/Release/Drain und InvalidateDraft nach vollständigem/partiellem Capture. | Vollständige historische Werte bleiben exakt kopierbar. Handle ungültig, keine Nachladung aus neuem Zustand. Kein Löschen allein aus WorkspaceChanged. |
| A31 | Tatsächlicher fachlicher Reset/Dokumentwechsel, explizites null, ApplyVariables-null, Auswahlwechsel, Close, Shutdown. | Getrennte semantische Regeln gemäß Matrix; kein Name-Reselect, keine späte Publikation, keine Wiederbelebung nach Dispose. |
| A32 | Tabwechsel, Reparenting, Float, Maximize/Restore, Layout-Reset. | Gleiche VM/View; keine Aufnahme/Revision/Mutation; Layout-Reset behält Snapshot und setzt Variables aktiv. Visuelles Detach kein Close. |
| A33 | Frischer Start/Reset und echte Mausoperation in dritte Spalte, native Floating Windows, Home. | Zwei-Spalten-Default; Variables/Info/Inspector gemeinsame rechte Gruppe; Output/Diagnostics unten; Status geschlossen. Dritte Spalte echter Split, keine zweite Session. |
| A34 | Dock-Gates: Ready, Busy, executing, pending, observer, MCP, draining, stopping, Editor-Lock, Dokumentupdate, disabled window, focus, layoutfault. | Vorhandene Verweigerungen bleiben wirksam. Restore/Reset nur über passenden ChangeLayout-Pfad. Keine versteckten Retries. |
| A35 | v1 mit verschiedenen Gruppen/aktiven Tabs/geschlossenen Tools/0..6 Floating Hosts migrieren. | Historische v1-Prüfung, Inspector geschlossen ergänzen, danach genau sieben IDs; alte Geometrie erhalten; v1 bytegleich. |
| A36 | v2 mit sieben Hosts; achtes, leeres oder verschachteltes Hostlayout; doppelte/fehlende IDs; unlesbares/ungültiges vorhandenes v2. | Zulässiges v2 akzeptieren, übrige Fälle diagnostizieren. Kein v1-Fallback bei vorhandenem ungültigem v2. Nur fehlendes v2 migriert. |
| A37 | Atomarer Fehler vor Replace, erneuter Start, Save im Maximize-Zustand, Restore und Reset nach Migration. | Letzte Datei bleibt intakt; kein Delete-first. Normalbaum gespeichert; Restore exakt, Reset neuer Default. Keine VM-/Wert-/Payloaddaten im JSON. |
| A38 | Native Windows-Maus-Redock einschließlich spezieller Cross-Window-Fälle, Tastatur, Fokus, Screenreader, Light/Dark. | Eindeutige Tab-/Property-/Copy-Namen, Lade-/Historie-/Fehlerzustände verständlich, keine verlorene Auswahl durch Reparenting. HTML ist dafür kein Nachweis. |

## 6. Freigaberegel

R4 prüft vor Implementierungsfreigabe die sechs korrigierten Verträge, nicht einen noch nicht existierenden Inspector. G2-04-02 darf erst nach Owner-Freigabe beginnen. Nach der späteren Implementierung sind A01–A38 mit reproduzierbarem Ergebnis und Beleg abzunehmen. Abweichungen von Full Copy, Host-/Revisionskonsistenz oder Read-only sind keine kosmetischen Restpunkte. Technische Plattformfehler müssen korrekt gemeldet werden; sie dürfen nicht als erfolgreicher Teilwert kaschiert werden.
