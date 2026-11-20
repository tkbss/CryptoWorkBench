# F2 — Überprüfbare Accessor-Matrix

**Normativer Architekturanhang · READY FOR OWNER REVIEW, nicht ACCEPTED**  
Baseline `32017a08c4771e73c12a50f6e1548476b9bb0a54`, Zielruntime .NET 10.

## 1. Registrierungs- und Prüfschlüssel

Ein Eintrag besteht aus konkretem Runtime-Typ, deklarierendem Typ, Property-Signatur/Indexerparametern und tatsächlichem Getter-Slot. Abgeleitete unbekannte Typen werden nicht über `is BaseType` mitfreigegeben. Generische BCL-Familien sind nur für die unten genannten geschlossenen Typen registriert. Nicht öffentliche Getter werden nie durch Reflection freigeschaltet. Metadaten enthalten auch nicht ausführbare Properties. `[JsonIgnore]` oder andere Serialisierungsattribute ändern das Inspector-Inventar nicht.

**Read-Arten:** D = direkter geprüfter Read unter Host; P = bedarfsgerechte Objektprojektion; R = Range-/Eintragsadapter unter Host; C = ausdrücklich berechneter Wert; M = nur Metadaten/Begründung. Alle bereits detached Werte werden ohne Live-Read gerendert/kopiert. „Direkt“ bedeutet weder UI-Binding an die Declaration noch pauschal billige Rekursion.

## 2. CryptoScript-Deklarationen

Gemeinsame fünf Properties: `Statement.Text`, `VariableDeclaration.Id`, `Type`, `Value`, `ValueFormat`. Text/Id/ValueFormat und einfaches Value: D, vollständige Strings; Type: P auf genau bekannten CryptoType. Nullable Type bleibt null. Es werden keine neuen CryptoType-Instanzen erzeugt, um einen vorhandenen Typ zu bestimmen: Konstruktoren können den globalen Lexer berühren.

| Runtime-Typ | Vollständige öffentliche Instanz-Properties | Read / besondere Prüfung |
|---|---|---|
| `StringVariableDeclaration` | gemeinsame fünf + `GMAC` | 6; Strings D. |
| `PathVariableDeclaration` | gemeinsame fünf | 5; Type darf null sein. Keine Datei öffnen oder Pfad normalisieren. |
| `ValVariableDeclaration` | gemeinsame fünf | 5; Type/Value-Overrides deduplizieren; Value ist der tatsächliche String `true`/`false`. |
| `ParameterVariableDeclaration` | gemeinsame fünf + `Mechanism`, `HeaderReference` | 7; Mechanism D, HeaderReference P. Value direkt lesen, niemals BuildValue/PrintOutput aufrufen. |
| `KeyVariableDeclaration` | gemeinsame fünf + `DerivationMechanism`, `KeyType`, `Usage`, `KeySizeInBits`, `KeySize`, `KeyAttributes`, `Kcv`, `Kcvc`, `Kcvk` | 14; Strings D; Typ/Usage/Size P; Attributes R; KCVs C. Exakter Root-Typ einschließlich transitivem virtuellem Value. |
| `SecretVariableDeclaration` | gemeinsame fünf + `SecretSet` | 6; Value exakt `SECRET`; SecretSet P. Kein Restore. |
| `BlockHeaderVariableDeclaration` | gemeinsame fünf + `Definition` | 6; Definition P; Value auditiertes TR31H-Formatting oder base.Value bei null. Keine generischen ToString-Aufrufe. |
| Exakt `VariableDeclaration`, falls in Dictionary vorhanden | gemeinsame fünf | 5; bekannte Basisgetter D/P; kein Freibrief für Unterklassen. |

`PARAM.GetParameters()` ist **keine Property**. Ein separater, so benannter Adapter liefert unter Host den tatsächlichen `Dictionary<string,string>`-Inhalt. Das zurückgegebene lebende Dictionary oder ein Enumerator darf den privaten Operationskontext nicht verlassen. Ein Root-PARAM-Copy erhält keine erfundene `Parameters`-Property; die Adapterzeile besitzt eigenes vollständiges Dictionary-Copy.

## 3. Fachliche Unterobjekte

| Exakt unterstützter Runtime-Typ | Alle öffentlichen Instanz-Properties | Read / Copy-Schema |
|---|---|---|
| `CryptoType`, `CryptoTypeKey`, `CryptoTypeVar`, `CryptoTypePath`, `CryptoTypeParameters`, `CryptoTypeTR31Header`, `CryptoTypeVal`, `CryptoTypeSecret` | `Text`, `Id`, `Name` | D; echtes Id einschließlich Vocabulary-Anführungszeichen bewahren. Statisches Lexer ausgeschlossen. JSON diese drei Member. |
| `KeyType` | `Algorithm`, `MaterialKind` | D; JSON zwei Enumwerte. |
| `KeySize` | `Bits`, `IsKnown` | D; ulong verlustfrei, bool. |
| `KeyUsagePolicy` | `Mode`, `AllowedUsages` | D; deterministische Enum-/Flags-Texte. Methoden Allows/EnsureAllows nicht aufrufen. |
| `OptionalBlock` | `ID`, `Length`, `Data` | D; keine Reparatur von Length, kein Serialize. Nicht versiegelt: nur exakten Typ lesen. |
| `SecretSet` | `Descriptor`, `Shares`, `PreferredShareFormat`, `OriginalKeyMetadata` | P/R/D/P; null-Metadaten ausdrücklich möglich. |
| `SecretDescriptor` | `Mechanism`, `Parts`, `MinParts`, `KeySizeBits`, `Profile`, `Field`, `ReductionPolynomial`, `SecretPosition` | D; abgeleitete einfache Properties bekannt und rein. Nullable Werte exakt. |
| `SecretShare` | `X`, `Value` | X D; Value R über geplanten Bounds-geprüften CopyValueRange-Accessor. Liefert exakt Getterbytes ohne vollständigen Clone je Seite. |
| `SecretKeyMetadata` | `KeyType`, `Usage`, `DerivationMechanism`, `Attributes` | P/P/D/R. |
| `SecretKeyAttribute` | `Id`, `Length`, `Data` | D; der Record besitzt kein zusätzliches öffentliches EqualityContract. JSON hier `Id`, bei OptionalBlock dagegen `ID`. |
| `Tr31HeaderDefinition` | `Attributes`, `OptionalBlocks`, `ExpectedLength`, `IsLengthResolved`, `Version`, `Algorithm`, `HeaderLength` | P/R/D. Versiegelt, Constructor validiert/kopiert; OptionalBlocks klont maximal 99 validierte Einträge. |

Alle tatsächlichen fachlichen Member erscheinen auch in der komplexen Wertdarstellung. Ein null-Property wird nicht weggelassen. Berechnete Member werden erst nach explizitem Bedarf aufgenommen. Ein Fehler wird nicht durch `null` oder ein Statusobjekt ersetzt.

## 4. Bekannte BCL-Typen und ihre Unterobjekte

Die Property-Namen wurden auf der verfügbaren .NET-10-Runtime per `GetProperties(Public | Instance)` überprüft. Generische Metadaten ändern sich durch die hier zulässigen T-Argumente nicht. Die spätere Abnahme wiederholt dies auf der tatsächlich ausgelieferten Runtime; ein neues Property muss als M sichtbar werden, bis ein geprüfter Accessor vorliegt. Keine automatische Freigabe durch Versionsgleichheit.

| Familie / zugelassene geschlossene Typen | Öffentliches Property-Inventar | Zugriff und Einträge | Wert-Copy |
|---|---|---|---|
| `string` | `Length`, `Chars[int]` | Length D; Chars R, UTF-16-Codeeinheit, 0 ≤ i < Length. Vollstring darf direkt gehalten werden. Keine Surrogatnormalisierung. | Direkt exakt; JSON-Member als JSON-String. |
| `byte[]` | `Length`, `LongLength`, `Rank`, `SyncRoot`, `IsReadOnly`, `IsFixedSize`, `IsSynchronized` | D; SyncRoot Referenz auf denselben Arrayknoten; Elemente R. Keine erfundene Item-Property. | Hexstring `0x(UPPERCASEHEX)`, nicht Array-Metadaten. |
| `List<OptionalBlock>` | `Capacity`, `Count`, `Item[int]` | D/D/R; bekannter Indexbereich, keine Setter. | JSON-Array fachlicher OptionalBlock-Werte. |
| `ReadOnlyCollection<SecretShare>`, `<SecretKeyAttribute>`, `<OptionalBlock>` | `Count`, `Item[int]` | D/R. Es muss eine bekannte von den auditierten Core-Konstruktoren erzeugte Collection sein; fremder virtueller IList-Unterbau nicht pauschal erlaubt. | JSON-Array. |
| `Dictionary<string,string>` | `Comparer`, `Count`, `Capacity`, `Keys`, `Values`, `Item[string]` | D/P/R; vorhandene Keys, keine Schreiboperation. Count/Capacity vorhanden in .NET 10. | JSON-Objekt mit tatsächlichen Keys; ordinal sortiert. |
| `ReadOnlyDictionary<string,string>` | `Keys`, `Values`, `Item[string]`, `Count` | P/P/R/D. Nur bekannter Dictionary-Unterbau aus Tr31HeaderDefinition; keine fremde IDictionary-Implementierung. | JSON-Objekt mit tatsächlichen Keys; ordinal sortiert. |
| jeweilige `Dictionary<string,string>.KeyCollection`, `.ValueCollection` | `Count` | D; Einträge R in der tatsächlichen Dictionary-Enumerationsreihenfolge der aufgenommenen Revision. | JSON-Array dieser Werte; keine Sortierung der Values unabhängig von Keys. |
| jeweilige `ReadOnlyDictionary<string,string>.KeyCollection`, `.ValueCollection` | `Count` | D/R, nur bekannter Unterbau. | JSON-Array der aufgenommenen Reihenfolge. |
| `KeyValuePair<string,string>` | `Key`, `Value` | D; innerhalb des sichtbaren Dictionary-Eintragsadapters. | `{"Key":…, "Value":…}` kanonisch ohne Leerzeichen. Dictionary-Parent verwendet direkt Key→Value. |
| bekannte Standard-String-Comparer: der Runtime-Typ von `StringComparer.Ordinal` und `EqualityComparer<string>.Default` | keine öffentlichen Instanz-Properties | Terminaler Knoten mit Runtime-Typ, „0 öffentliche Properties“. Keine Equals/GetHashCode/ToString-Auswertung. Runtime-Typ bei Start aus diesen bekannten BCL-Singletons bestimmen. | `{}` als ausdrücklich festgelegte leere öffentliche Memberprojektion dieses bekannten stateless Hilfsobjekts; keine CLR-Identitäts-/Rekonstruktionsgarantie. |
| `bool`, `char`, `byte`, `int`, `long`, `ulong` sowie die fachlichen Enumtypen | keine öffentlichen Instanz-Properties | Terminalwerte. Nur bekannte invariant Encoder. | Skalarformat gemäß Copy-Vertrag. |

Explizite nicht öffentliche Interface-Properties werden nicht als öffentliche Runtime-Properties ausgegeben. Beispielsweise wird bei List nicht zusätzlich ein öffentliches SyncRoot erfunden. Enumerable-Laufzeittypen fremder Herkunft bleiben M, selbst wenn sie `IEnumerable<T>` implementieren. Kein beliebiges Enumerable wird automatisch enumeriert.

Ein Dictionary kann einen tatsächlichen fachlichen Key `type`, `status`, `$ref` oder `revision` enthalten. Solche echten Daten dürfen in Copy vorkommen; verboten sind vom Inspector **hinzugefügte** Hüllen. Der Test prüft Datenherkunft, nicht eine pauschale Wort-Blacklist.

## 5. Abweichungs- und Fehlerzustände

| Fall | Sichtbarkeit / Wertbehandlung |
|---|---|
| Nicht geladen | Property mit Typ/Signatur sichtbar; Laden möglich, wenn Handle gültig. Nicht mit null verwechseln. |
| Kein öffentlicher Getter | `WriteOnly` bzw. `GetterNotPublic`; keine Reflections-Ausführung. |
| Unbekannter Override/Runtime-Typ/Unterbau | Property sichtbar als `UnsupportedAccessor`; keine automatische Registrierung. |
| Getter wirft | `GetterError`; andere Zeilen bleiben erhalten; kein Exceptiontext als Wert-Copy. |
| KCV nicht angefordert | `NotCalculated`; explizites Berechnen oder „Laden, berechnen & kopieren“. |
| KCV unanwendbar | Actual getter value null; UI kann NotApplicable anzeigen, Copy bleibt `null`. |
| Fremde Newtonsoft DefaultSettings bei TR31H.Value | `UnsupportedFormatter`, keine Ausführung des fremden Delegates/Converters. Andere Definition-Properties zugänglich. |
| Wiederholte Referenz | Navigation zum Ziel; Wert-Copy verwendet Zielwert, keine Pfad-Hülle. |
| Echter fachlicher Zyklus | Baum endlich navigierbar; vollständiges zyklisches Parent-Copy nicht verfügbar; einzelne endliche Werte kopierbar. |

## 6. Verbindlicher Audit für jeden Eintrag

Der Implementierungsnachweis nennt Runtime-Typ/Assembly, Property-Signatur, konkreten Accessor und transitive Abhängigkeiten. Er prüft: keine Setter/Dictionary-/PARAM-Mutation, kein IO oder UI im Getter, keine Scriptausführung, kein globaler benutzerdefinierter Formatter, keine versteckte Aufwertung unbekannter Overrides. Zeit-/Allokationskosten werden als O(1), O(n) mit Range-Adapter oder ausdrücklich berechnet dokumentiert.

Das vollständige Reflect-Inventar wird gegen die Zeilen des Projectors verglichen. Für jeden Baseline-Member muss entweder ein erfolgreicher Wert oder ein erklärter, absichtlich nicht ausführbarer Zustand vorhanden sein. Ein fehlender Eintrag ist ein Abnahmefehler. Unbekannte Test-Overrides besitzen Aufruf-/Mutationszähler und müssen bei 0 bleiben. Indexer-Range-Grenzen werden vor Aufruf geprüft. Setter und Collection-Mutatoren werden niemals ausgelöst.

## 7. Quellstellen der Baseline

- [VariableDeclaration und virtuelle Basisslots](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/VariableDeclaration.cs:6), [Text](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/Statement.cs:12).
- [Key-Properties](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/KeyVariableDeclaration.cs:13), [KeyType/KeySize](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/KeyType.cs:21), [Usage](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/KeyUsage.cs:31).
- [PARAM-Getter und Live-Dictionary](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/ParameterVariableDeclaration.cs:103), [mutierendes PrintOutput](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/ParameterVariableDeclaration.cs:184).
- [Secret-Objektgraph und defensive Kopien](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/SecretSharing/SecretSet.cs:6), [Secret-Declaration](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/SecretVariableDeclaration.cs:6).
- [TR31H-Definition und Collection-Unterbau](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderDefinition.cs:7), [Formatter](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/WRAPPERS/Tr31HeaderDefinition.cs:118).
- [KCV-Calculator](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/CryptoAlgorithm/KeyCheckValueCalculator.cs:13), [tatsächliche Formatkonstanten](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Variables/FormatConversions.cs:15), [Type-IDs mit Vocabulary-Quotes](C:/Users/stefa/Documents/projects/GITHUB/CryptoWorkBench/CryptoScript/Model/AntlrLanguageMetadata.cs:12).
