# Function List

List of all functions of CRYPTO-SCRIPT. Upper and lower case letters in the function name are mandatory.
Possible Parameters are given in overlay window during typing.

---

## Built-in Functions
- Encrypt a message: **VAR e = Encrypt(parameters, key, data)**
- Encrypt a binary file: **[PATH e = Encrypt(parameters, key, sourcePath)](cryptoscript-info://function/Encrypt)** (AES-CBC, PKCS-7/NONE)
- Decrypt a message: **VAR d = Decrypt(parameters, key, data)**
- Decrypt a binary container: **[PATH d = Decrypt(parameters, key, containerPath)](cryptoscript-info://function/Decrypt)** (AES-CBC, PKCS-7/NONE)
- Compute a message authentication code (MAC): **VAR m = Mac(parameters, key, data)**
- Compute an unkeyed message digest: **[VAR h = Hash(parameters, data)](cryptoscript-info://function/Hash)**
- Hash a binary file: **[VAR h = Hash(parameters, sourcePath)](cryptoscript-info://function/Hash)** (direct PARAM, PATH references; all eleven HASH mechanisms)
- Derive generic keying material: **KEY k = Derive(parameters, key, data)**
- Generate a key: **KEY k = GenerateKey(mechanism, length)**
- Find an existing key by check value: **KEY k = Check(value, method)** (method optional)
- Generate parameters: **PARAM p = Parameters(mechanism, p1, ..., pn)**
- Create a TR-31 header: **TR31H h = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)**
- Print variable to output: **Print(s)**
- Export variables as plaintext JSON: **[Save(path, variable1, variable2, ...)](cryptoscript-info://function/Save)** or **Save(path, ALL)**
- Import variables from plaintext JSON: **[Load(path, variable1, variable2, ...)](cryptoscript-info://function/Load)**, **Load(path)** or **Load(path, ALL)**; optional final **#ONCONFLICT:REPLACE**
- Export an authenticated password-protected snapshot: **[SecureSave(path, variable1, ...)](cryptoscript-info://function/SecureSave)** or **SecureSave(path, ALL)** (asynchronous)
- Import an authenticated password-protected snapshot: **[SecureLoad(path, variable1, ...)](cryptoscript-info://function/SecureLoad)** or **SecureLoad(path)** (asynchronous)
- Remove variables from the current dictionary: **[Delete(variable1, ...)](cryptoscript-info://function/Delete)** or **Delete(ALL)** (synchronous, no return value)
- Show Crypto Script information: **Info(type)**
- Compare two variables of same type: **VAL res = Compare(VAR, VAR)**

Digital signatures are not supported in CryptoScript V1. Asymmetric cryptography is reserved for V2.

## Print(KEY)

`Print(k)` displays the actual KEY object's Identifier, Type, complete Value,
ValueFormat, KeyType, KeySizeInBits, KeyUsage, KeyPolicy, DerivationMechanism,
KeyAttributes, KCV, KCVC and KCVK. Unavailable values are shown as `N/A`.
The legacy `KeySize` field is also included. `Value` is the sole canonical representation of key material.
The full key and full check values are printed; no truncation or masking is applied.
Other `Print` arguments retain their existing output behavior.

## Check(value [, method])

`Check` searches all currently visible KEY variables. `value` is a HEX literal
`0x(...)` or a VAR holding HEX content. Supply at least one complete byte (an even
number of hexadecimal digits). Invalid HEX and unknown methods are rejected.
The optional string method is `KCV`, `KCVC` or `KCVK`, case-insensitively. If omitted,
all three methods are searched. Their formulas, block sizes and PCI context are
documented in `Info(types)` under KEY check values.

Comparison is bytewise against the leading bytes of the full calculated result.
The input determines the comparison length, with no automatic shortening. An input
longer than a result cannot match it. A match through multiple methods of one KEY
counts once; different variables count separately even with identical material.

Exactly one matching variable produces a new independent KEY with the same material
and relevant metadata. Mutable attributes are copied. No match reports the Error
diagnostic `KeyCheckValueNotFound`; multiple matching variables report
`KeyCheckValueAmbiguous`. Neither establishes or replaces the requested variable.
Independent following statements continue. A failed nested search also prevents
the enclosing operation/declaration from executing. These search diagnostics belong
to the current execution. Other function failures keep their existing behavior.

```text
KEY aes = GenerateKey(AES-ECB, 0x(000102030405060708090A0B0C0D0E0F))
Print(aes)
KEY foundAes = Check(0x(C6A13B), "KCV")

KEY tdea = GenerateKey(DES3-ECB, 0x(0123456789ABCDEFFEDCBA9876543210))
Print(tdea)
VAR prefix = 0x(0A82458664)
KEY foundTdea = Check(prefix, "kcvc")
```

After each copy is declared, both original and copy are visible: searching their
shared check value again is ambiguous. Short prefixes can also match unrelated keys.
`Check` never chooses an arbitrary first match.





## PinBlockTranslate

`VAR result=PinBlockTranslate(sourceParams,sourceKey,encryptedPinBlock,targetParams,targetKey)` requires exactly five genuine references of type PARAM, KEY, VAR, PARAM, KEY. Source and target PARAMs must be distinct. Returns only the encrypted target block and commits both parameter outputs after success. See `Info(pintranslations)` and the [PCI translation matrix](cryptoscript-info://overview/pintranslations) for PAN requirements, allowed transitions and the offline ICC-only restriction for format 2.

## Secret sharing

[Share(PARAM, KEY)](cryptoscript-info://function/Share), [Secret(PARAM, Shares...)](cryptoscript-info://function/Secret), [Restore(SECRET)](cryptoscript-info://function/Restore). See [SECRET and complete Print output](cryptoscript-info://type/SECRET).


## Typed TR-31 headers

See [TR-31](cryptoscript-info://overview/tr31), [CreateHeader](cryptoscript-info://function/CreateHeader) and the [complete attribute reference](cryptoscript-info://overview/tr31attributes).

`TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)` creates a reusable definition. Pass it as `#BLKH:header`; Wrap computes KBLEN without modifying the definition. For WRAP-DES3-TR31 use A/B/C and ALGO:T.

## MAC over a binary file

`VAR tag = Mac(p,k,source)` accepts a declared `PATH source` and authenticates the
raw file bytes incrementally, using a fixed 64-KiB read buffer. The complete file
is not converted to hexadecimal. `Mac(VAR)` continues to authenticate decoded
in-memory data. A VAR or string literal containing a filename is data, not a PATH.
Only direct PATH calls returning VAR are allowed; nested calls and nested argument
calls are rejected. No output file or CWE container is created. For authenticated CWM containers use [MacPack](cryptoscript-info://function/MacPack) and [MacVerify](cryptoscript-info://function/MacVerify): `PATH container = MacPack(p,k,source)` and `VAL valid = MacVerify(p,k,container)`.

For external POS/HP-NonStop comparisons, transfer files in binary mode. Do not
convert ASCII/EBCDIC, translate CR/LF, strip NUL bytes or record delimiters, or
pad/truncate records. Both systems must authenticate the identical byte stream,
including any record-framing bytes. Reproducible fixtures and complete key,
parameter and expected-tag records are in `docs/F03.2-reference/`.
