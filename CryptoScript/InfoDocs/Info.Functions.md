# Function List

List of all functions of CRYPTO-SCRIPT. Upper and lower case letters in the function name are mandatory.  
Possible Parameters are given in overlay window during typing.

---

## Built-in Functions
- Encrypt a message: **VAR e = Encrypt(parameters, key, data)**
- Decrypt a message: **VAR d = Decrypt(parameters, key, data)**
- Compute a message authentication code (MAC): **VAR m = Mac(parameters, key, data)**
- Compute an unkeyed message digest: **VAR h = Hash(parameters, data)**
- Derive generic keying material: **KEY k = Derive(parameters, key, data)**
- Generate a key: **KEY k = GenerateKey(mechanism, length)**
- Find an existing key by check value: **KEY k = Check(value, method)** (method optional)
- Generate parameters: **PARAM p = Parameters(mechanism, p1, ..., pn)**
- Print variable to output: **Print(s)**
- Show Crypto Script information: **Info(type)**
- Load variable from file: **VAR d = Load(VAR, path)**
- Load parameters from file: **PARAM p = Load(PARAM, path)**
- Load key from file: **KEY k = Load(KEY, path)**
- Compare two variables of same type: **VAL res = Compare(VAR, VAR)**

Digital signatures are not supported in CryptoScript V1. Asymmetric cryptography is reserved for V2.

## Print(KEY)

`Print(k)` displays the actual KEY object's Identifier, Type, complete Value,
ValueFormat, KeyType, KeySizeInBits, KeyUsage, KeyPolicy, DerivationMechanism,
KeyAttributes, KCV, KCVC and KCVK. Unavailable values are shown as `N/A`.
The legacy `KeyValue` and `KeySize` fields are also included.
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




