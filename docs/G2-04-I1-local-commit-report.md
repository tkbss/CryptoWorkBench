# G2-04-I1 – Lokaler Commit des akzeptierten Implementierungsstands

Der Owner hat I3 am 19. November 2026 mit den unten genannten Prüfgrenzen akzeptiert. Dieser Commit bündelt G2-04 auf `feature/gui-g2-04` über dem Ausgangsstand `32017a08c4771e73c12a50f6e1548476b9bb0a54`. Vor der Aufnahme stimmten alle 73 Dateien des R5-Schlussinventars per SHA-256 mit dem Arbeitsstand überein. Der Index war leer.

| Umfang | Inhalt |
|---|---|
| Produkt, 28 Dateien | Inspector-Snapshots und Host-Zugriff, begrenzter Share-Lesezugriff, Wertnavigation, Anzeige großer Daten, Clipboard-Validierung und Meldungsreihenfolge, Docking und Layoutmigration |
| Dauerhafte Tests und Prüfwerkzeuge, 23 Dateien | Core- und GUI-Regressionen, Golden-Fixture, native Prüfeinstiegspunkte unter `eng/gui` und `eng/inspector` einschließlich ihrer Projektquellen |
| Verträge und Berichte, 32 Dateien | F3-korrigierte Architektur, Accessor-Matrix, Copy-Vertrag, Abnahmekatalog und Golden, Architekturkorrekturen und unabhängige Reviews, I1–I3 einschließlich der Korrekturberichte F1–F4 für I3, R5 und dieser Commitbericht |

Die Architekturunterlagen stammen aus dem in I3 dokumentierten lokalen Originalarchiv. Alle 14 F2-Paketdateien wurden mit den F3-Nachher-Hashes verglichen. Übernommen wurden die Vertragsunterlagen, die normative Fixture und die einschlägigen Berichte. Die visuellen Prototypen und historischen Paketarchive bleiben lokal. [Architektur](G2-04-01-F2/G2-04-01-F2-architecture.md) · [Abnahmekatalog](G2-04-01-F2/G2-04-01-F2-acceptance.md)

R5 bestätigte in Debug und Release jeweils 21/21 Clipboard-Fälle, 122/122 fokussierte GUI-Fälle und 1.277/1.277 vollständige GUI-Fälle im ersten Lauf. Die Teilmengen sind im Gesamtergebnis enthalten. Alle sieben Befunde aus den I3-Reviews R1 bis R4 sind geschlossen. Für diesen Commit genügte der Hashabgleich mit dem geprüften Stand. [Abschlussverifikation](G2-04-02-I3-R5-final-verification.md)

**Lokale Nachweise.** Die relativen Berichtslinks unter `artifacts/test-results/` verweisen auf ignorierte TRX, Messprotokolle, Snapshots, Prüfbinaries und temporäre Review-Hosts. Absolute historische Links setzen die ursprünglichen lokalen Verzeichnisse voraus. Die lokalen Markdown-Linkziele der übernommenen Berichte waren beim Verpacken erreichbar. Die Originalberichte behalten ihre historischen Befunde und Statusangaben. Es wurden keine fachfremden Änderungen festgestellt.

**Offene Prüfungen bei der Eigentümerabnahme** sind natives 100 % und 125 % DPI, manuell gehörte Screenreader-Sprachausgabe und der spezielle fensterübergreifende Maus-Redock.

Die Prüfung mit git diff --cached --check meldete 23 bereits vorhandene Markdown-Zeilenenden mit Leerzeichen oder abschließende Leerzeilen in den übernommenen Dokumenten. Die akzeptierten Originale wurden beibehalten.
