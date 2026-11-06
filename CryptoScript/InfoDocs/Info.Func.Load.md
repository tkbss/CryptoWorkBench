# Load — import variables from plaintext JSON

```cryptoscript
Load(path)
Load(path, ALL)
Load(path, MasterKey)
Load(path, MasterKey, SessionKey)
Load(path, #ONCONFLICT:REJECT)
Load(path, #ONCONFLICT:REPLACE)
Load(path, ALL, #ONCONFLICT:REPLACE)
Load(path, MasterKey, #ONCONFLICT:REPLACE)
Load(path, MasterKey, SessionKey, #ONCONFLICT:REPLACE)
```

The first argument must reference an existing `PATH` variable. Selection arguments
name root variables in the snapshot; they need not exist in the current dictionary.
Names are case sensitive. With no selection, or with `ALL`, every root is selected.
Explicit selections retain argument order. Unknown names and duplicate selectors
are errors. `ALL` cannot be combined with any other selector. It is contextual,
not a globally reserved keyword: a variable named `ALL` remains usable elsewhere
and as the first PATH argument. Lowercase `all` is an ordinary selection.

`#ONCONFLICT` is accepted only by Load, at most once, as the final argument.
Its values are the unquoted, case-sensitive tokens `REJECT` and `REPLACE`.
The default `REJECT` aborts the entire import if any selected name already exists.
Explicit `#ONCONFLICT:REJECT` has the same behavior. `REPLACE` replaces each existing
selected variable with the complete deserialized object, including metadata and
references, and inserts new names. Unselected variables remain unchanged.

```cryptoscript
PATH source = "snapshot.json"
Load(source, key, message)
Info(Load)
```

The source PATH has no conflict exemption. If a snapshot contains `source`,
`Load(source)` rejects the whole import. `Load(source, #ONCONFLICT:REPLACE)` can
replace it after the file has been read and all checks have passed.

Load uses the S02 file store and S01 version 1 snapshot format for VAR, KEY, PARAM,
PATH, TR31H and SECRET. It first reads and validates the **entire** file, including
unselected roots, metadata and dependencies, subject to the S02 16 MiB limit.
It then resolves selections, checks conflicts and references, and prepares the
complete dictionary state. A single reference swap publishes that state within
the synchronous interpreter. Any failure before publication leaves existing
objects, metadata and references unchanged; no partial import is visible.
The dictionary is not a general-purpose concurrent collection.

Shared TR31H object identity is preserved. Selecting PARAM without its TR31H root
retains the header as an internal dependency without publishing an extra root.
An existing dictionary header with the same identifier is never automatically
linked to imported PARAM objects. Replacing a header is rejected if an unchanged
existing PARAM directly references that exact old object. Select all referring
PARAM roots as well to replace that graph together. Identifier equality alone
does not constitute a reference conflict.

Load has no return value. Assignment (`VAR result = Load(path)`) and nested use
(`Print(Load(path))`) are rejected before file access or argument side effects.
It never executes serialized contents as CryptoScript. S02's sanitized error
categories are retained; diagnostics omit payloads, key values and secret shares.

**Security:** snapshots are not encrypted or authenticated. Load cannot establish
their origin or integrity. KEY material and SECRET shares are present in full;
import only files from sources you trust. Validation does not provide authenticity.
The file and ancestor-directory constraints and filesystem race limitations of
S02 still apply. Load does not implement SecureLoad. See also
[Save](cryptoscript-info://function/Save).
