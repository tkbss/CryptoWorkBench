# S01: CryptoScript variable snapshots, JSON version 1

## API and scope

`CryptoScript.Serialization.VariableSnapshotJsonSerializer` is part of the Core
assembly and exposes:

```csharp
string Serialize(IEnumerable<VariableDeclaration> variables);
IReadOnlyList<VariableDeclaration> Deserialize(string json);
```

The service copies detached objects. It neither accesses `VariableDictionary` nor
Avalonia, executes CryptoScript, accesses files, generates keys/IVs/nonces/shares,
nor reconstructs secret keys. The caller controls publication of the returned
root variables. Deserialization returns only after the entire graph validates.
Errors are `Newtonsoft.Json.JsonSerializationException` with a material-free
message; null API arguments produce `ArgumentNullException`.

Serialization validates the detached snapshot with the same reconstruction path.
Unsupported subclasses and inconsistent runtime objects are rejected rather than
silently losing state. The legacy `KeyVariableDeclaration.Serialize/Deserialize`
API and its legacy JSON migration remain independent and unchanged.

## Container and common entry

```json
{
  "format": "CryptoScriptVariables",
  "version": 1,
  "variables": [
    {
      "objectId": "o1",
      "identifier": "message",
      "metadata": {
        "type": "VAR",
        "format": "HEX_STRING",
        "payloadKind": "scalar",
        "runtimeTypeId": "'VAR'",
        "gmac": ""
      },
      "value": "0x(aAbB00)"
    }
  ],
  "dependencies": []
}
```

All fields documented here are required, including nullable fields. Unknown fields,
incorrect casing, duplicate fields (including escaped equivalents), JSON comments,
trailing commas, non-JSON syntax and extra trailing documents are rejected.
Numbers are JSON integers, never strings or fractional numbers; strings are never
implicitly converted from numbers. Nesting is limited to 64 levels.

`variables` holds the requested roots in input order. `dependencies` holds embedded
header objects in first-reference order. Empty arrays are valid.

Every entry has `objectId`, `identifier`, `metadata`, and `value`.
`metadata.type` is exactly `VAR`, `KEY`, `PARAM`, `PATH`, `TR31H`, or `SECRET`.
`metadata.format` preserves the original `ValueFormat`, including an empty string.
Recognized nonempty formats are `HEX_STRING`, `BASE64_STRING`, `NORMAL_STRING`,
`JSON_STRING`, `PARAM_STRING`, `PATH_STRING`, and `TR31_STRING`.

`runtimeTypeId` preserves the runtime `CryptoType.Id`; null represents a null
runtime `Type`, which occurs in older PATH/KEY objects. The discriminant `type`
still determines the concrete variable class. A non-null runtime type must match
the variable class. Root identifiers must tokenize as one CryptoScript `ID`
(`[A-Za-z][A-Za-z0-9]*`, excluding reserved tokens); they are unique, case-sensitive.

`payloadKind` is `scalar` for VAR/PATH with empty inherited source text and
`structured` otherwise. The other four types always use `structured`.
The corresponding `value` must be a JSON string or a JSON object respectively.
There are no .NET type names or automatic polymorphic deserialization.

Every structured payload has a required `source` object:

```json
{ "text": "VAR message=0x(aAbB00)", "typeText": "" }
```

It preserves `VariableDeclaration.Text` and `CryptoType.Text`, both inherited
from `Statement`. `typeText` is null exactly when `runtimeTypeId` is null.
Source text is never executed. It belongs inside `value`, since original script
declarations can contain secret literals. For scalar VAR/PATH entries the source
texts are implicitly empty (and null type text for a null runtime type).

Order of roots, dependencies, parameter fields, attributes, optional blocks and
shares is preserved. Identical input order and state produce identical output.
This is deterministic serialization, **not cryptographic canonicalization**.
Consumers must not rely on object-property order when parsing.

## Type-specific metadata and payloads

### VAR and PATH

VAR adds the required string `metadata.gmac`. `value` preserves the exact original
string, without conversions or reinterpretation of HEX, Base64, JSON, numbers or
other supported representations.

When either inherited source text is nonempty, VAR/PATH instead use
`payloadKind: "structured"` with this value:

```json
{
  "content": "0x(aAbB00)",
  "source": { "text": "VAR message=0x(aAbB00)", "typeText": "" }
}
```

`content` is still the unchanged runtime `Value`. This avoids exposing source
text through metadata while retaining the simple scalar form when possible.

PATH uses only the common metadata and stores the complete path in `value`.
No normalization, platform conversion, or filesystem existence check occurs.

### KEY

Additional metadata:

```json
{
  "keyType": { "algorithm": "Aes", "materialKind": "Secret" },
  "keySizeInBits": 128,
  "keySize": "128",
  "usage": { "mode": "Restricted", "allowedUsages": 97 },
  "derivationMechanism": "KDF-HKDF",
  "kcv": "0x(c6a13b37878f5b826f4f8162a1c8d879)",
  "kcvc": "0x(be7ed6ae787dd5544c5561c7dcd06abb)",
  "kcvk": "0x(0a940bb5416ef045f1c39458c653ea5a)"
}
```

The example usage mask is Encrypt | WrapKey | UnwrapKey. Enum names are exact
case-sensitive symbolic names; usages are the existing `KeyUsage` integer flags.
The existing validating `KeyType` and `KeyUsagePolicy` constructors are used.

The complete payload is:

```json
{
  "material": "0x(000102030405060708090A0B0C0D0E0F)",
  "attributes": [
    { "id": "LB", "length": 9, "data": "label" }
  ],
  "source": { "text": "", "typeText": "" }
}
```

`material` maps exclusively to runtime `Value`; there is no `KeyValue` property.
Attributes retain order and exact ID, Length and Data, including nullable ID/Data
and legacy length values. They are generic key metadata, so they are not passed
through TR-31 optional-block normalization. Attribute data stays inside `value`.

Both typed and textual sizes are preserved (including textual leading zeros);
contradictory sizes are rejected. Size zero means unknown and remains unknown.
HEX is validated strictly; Base64 and normal-string byte conversions use the
existing formats. An empty format permits the existing tagged legacy encoding.
AES accepts 128/192/256 bits, TDEA 128/192 bits. HMAC and Unknown accept nonempty
material. RSA/EC use existing Bouncy Castle DER public/private key decoding to
validate algorithm and mathematical key size, rather than treating DER byte
length as key size. Known size metadata must match the actual material.

KCV, KCVC and KCVK use the existing calculator, with complete untruncated results.
Unavailable values are JSON null, not empty strings or fabricated zero values.
Deserialization recalculates and compares all three; mismatches fail. They are
readable metadata, not cached authoritative values or authenticity guarantees.

### PARAM

Additional metadata: `mechanism`, the exact runtime mechanism string.

```json
{
  "text": "#MECH:WRAP-AES-TR31#BLKH:header",
  "parameters": [
    { "name": "#MECH", "value": "WRAP-AES-TR31" },
    { "name": "#BLKH", "value": "header" }
  ],
  "explicitParameterNames": ["#MECH", "#BLKH"],
  "headerReference": "o2",
  "source": { "text": "", "typeText": "" }
}
```

The ordered parameter list and exact cached `Value` text are independent runtime
state. Direct dictionary changes can leave the text stale; snapshots preserve
both instead of rebuilding it. Explicit parameter provenance preserves order and
repetitions; automatically generated parameters remain non-explicit.
`headerReference` is null or an object ID, never a name lookup.

An internal `ParameterVariableDeclaration.FromSnapshot` factory sets the private
state without invoking `Mechanism`, `SetParameter`, `SetInstance`, `BuildValue`,
or any default generation. Names must be canonical known parameter names and
unique in the dictionary; provenance names must occur in it. Mechanism and
`#MECH` must agree. Where the mechanism supports `Parameters()`, its contract validates supported names,
required creation-time parameters and mechanism-specific values (including
PSN/EMV, EP2 and SECRET). Mechanisms such as TR-31, which support literal PARAM
declarations but no `Parameters()` call, retain the declaration's parameter-support
checks. Empty parameter objects are preserved.

This layer validates stored state, not completeness for an arbitrary future
cryptographic operation. For example, a parameter bag may legitimately lack an IV
until an operation requires it. Operation-specific value/length and required-input
validation remains with the existing operation contracts.

### TR31H

Structured payload:

```json
{
  "definition": {
    "attributes": [
      { "name": "KBVID", "value": "D" },
      { "name": "KEYU", "value": "D0" },
      { "name": "ALGO", "value": "A" },
      { "name": "MODEU", "value": "B" },
      { "name": "EXP", "value": "E" },
      { "name": "KEYVN", "value": "00" },
      { "name": "KEYCTX", "value": "0" },
      { "name": "RSV", "value": "0" },
      { "name": "NUMOPTB", "value": "00" }
    ],
    "optionalBlocks": [],
    "expectedLength": null
  },
  "legacyValue": null,
  "source": { "text": "", "typeText": "" }
}
```

All definition attributes and optional blocks are recorded in existing order.
The definition is reconstructed through `Tr31HeaderDefinition.Create`, then
compared with the snapshot. Any new defaults, changed lengths, normalization or
added padding cause rejection. Thus the already computed PB block survives
exactly. Explicit `KBLEN` stays an expectation, checked against `expectedLength`;
an omitted `KBLEN` stays omitted. `IsLengthResolved` remains false.

The current immutable definition does not track whether a PB block was originally
explicit or generated. The snapshot preserves its observable state and the
unresolved-length semantics; it does not invent that provenance.

Legacy headers without a definition use `definition: null` and a string
`legacyValue`. Both representations at once, or neither, are invalid. Legacy text
is preserved verbatim; its existing operation-time validation remains unchanged.

### SECRET

```json
{
  "descriptor": {
    "mechanism": "SECRET_SHAMIR",
    "parts": 3,
    "minParts": 2,
    "keySizeBits": 8,
    "profile": "SHAMIR-GF256-11B"
  },
  "preferredShareFormat": "X-Y",
  "shares": [{ "x": 1, "bytes": "Kg==" }],
  "originalKeyMetadata": null,
  "source": { "text": "", "typeText": "" }
}
```

`SecretSet` is authoritative; the display-only runtime `Value == "SECRET"` is
never used as share data. The descriptor includes all five stored fields.
Share bytes are complete Base64 byte arrays, with X and list order preserved.
Zero shares and sets below the threshold are allowed.

When present, `originalKeyMetadata` contains `keyType`, `usage`,
`derivationMechanism` and ordered `attributes` in the same shapes as KEY.
All original key attributes remain inside the payload.

Existing `SecretDescriptor`, `SecretShare`, `SecretKeyMetadata` and `SecretSet`
constructors validate profiles, counts, formats, symmetric key metadata, share
indices (1..255), uniqueness and exact share lengths. A missing Shamir profile
cannot silently generate a default. No `Restore()` or polynomial interpolation
occurs during deserialization; consistency of extra Shamir shares is checked by
the existing `Restore()` only when the caller explicitly invokes it.

## Graph identity and publication

IDs have syntax `o[1-9][0-9]*` and are unique across both arrays. Serialization
assigns roots first, then newly encountered dependencies, using reference equality.
All headers are staged before PARAM restoration, so forward references work.

If roots are `[p1, p2, h]`, they receive IDs `o1`, `o2`, `o3`, and both parameter
payloads contain `"headerReference": "o3"`. `dependencies` is empty.
If only `[p1, p2]` are exported, `h` still gets `o3` but its full entry is placed
in `dependencies`. Deserialization returns only `[p1, p2]`; both refer to the
same reconstructed header instance.

Two distinct header instances remain distinct even if identifiers or contents
match. Embedded headers may be anonymous or share an identifier with a root;
only root identifiers participate in the publication namespace. Duplicate root
objects/identifiers, missing targets, references to non-headers, duplicate IDs,
non-header dependencies and unused dependencies are errors. No global lookup or
fallback can resolve a broken reference.

## S02–S04 boundary and security

S01 provides plaintext in-memory snapshots only. Save/Load, SecureSave/SecureLoad,
filesystem handling, GUI integration, Scriptbook import/export, encryption,
authentication, password derivation and cryptographic canonicalization belong to
later work.

The complete `value` token is the future protection boundary. Identifier and
metadata remain readable and can be authenticated as AAD. A future secure
container must additionally authenticate format/version, object IDs, root versus
dependency membership, ordering and all graph links; embedded dependency payloads
must receive the same protection as roots. S01's validation and KCV checks are
not authentication.

Temporary decoded key/share byte buffers are cleared where the serializer owns
them. The returned JSON and the existing runtime string-based key model contain
plaintext; .NET strings cannot promise zeroization.

## Verification

`CryptoScriptUnitTest/VariableSnapshotJsonSerializerTests.cs` covers all six
types, exact formats and metadata, detached copies, deterministic output,
shared/embedded header identity, immutable header reconstruction, incomplete
secret sets, all sharing profiles, hostile JSON, metadata conflicts and
independence from global JSON settings. It also performs AES encryption,
TR-31 wrap/unwrap and explicit secret restoration with reconstructed objects.
