# TR-31 key blocks

TR-31, standardized as ANSI X9.143-2022, packages a key together with its usage attributes. Encryption protects the transported key; authentication binds the clear header and Optional Blocks to that encrypted key. Altering an authenticated field causes Unwrap to fail.

The **KBPK** (Key Block Protection Key) protects the package. It is distinct from the **transported key** inside the package. The header's `ALGO` describes the transported key, not the KBPK. The selected Wrap mechanism and `KBVID` determine the protection algorithm.

## Layout

The fixed header occupies 16 printable ASCII characters:

| Offsets (zero based) | Field | Meaning |
|---|---|---|
| 0 | KBVID | Binding version |
| 1-4 | KBLEN | Decimal length of the complete wire block |
| 5-6 | KEYU | Intended key usage |
| 7 | ALGO | Transported key algorithm |
| 8 | MODEU | Permitted operations |
| 9-10 | KEYVN | Key version |
| 11 | EXP | Exportability |
| 12-13 | NUMOPTB | Optional Block count |
| 14 | KEYCTX | Storage/exchange context |
| 15 | RSV | Reserved zero |

Ordered Optional Blocks follow this header. The encrypted confidential data and authentication value follow as hexadecimal ASCII. Optional Blocks are visible, authenticated metadata: they do not provide a place to hide secrets.

## Versions and supported mechanisms

| Version | Binding | Runtime mechanism | Builder's transported keys |
|---|---|---|---|
| A | TDEA variant binding, legacy/deprecated | WRAP-DES3-TR31 | TDEA 128/192 bits |
| B | TDEA derivation binding | WRAP-DES3-TR31 | TDEA 128/192 bits |
| C | TDEA variant binding | WRAP-DES3-TR31 | TDEA 128/192 bits |
| D | AES derivation binding | WRAP-AES-TR31 | AES 128/192/256 or TDEA 128/192 bits |

`CreateHeader` returns an independent `TR31H` definition. It needs no key and performs no cryptography. `KBLEN` stays unresolved until `Wrap` has created the confidential data, including the existing key-length obfuscation and cipher padding. Wrap then calculates:

`16 + serialized Optional Blocks + 2 * encrypted-data bytes + 2 * authentication bytes`

The authentication value is 4 bytes for A/C, 8 for B and 16 for D. The final decimal length is inserted **before** authentication. The exact header authenticated is the one returned in the key block. Wrap does not change the original header or PARAM.

## Complete example

```cryptoscript
KEY kbpk = GenerateKey(AES-CBC, 256)
KEY transportKey = GenerateKey(AES-CBC, 128)
TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)
PARAM p = #MECH:WRAP-AES-TR31 #BLKH:header
VAR wrapped = Wrap(p, kbpk, transportKey)
KEY recovered = Unwrap(p, kbpk, wrapped)
Print(header)
```

For a typed `#BLKH`, Unwrap first authenticates the received block, then compares the complete header with the definition. Only the unresolved length is filled from the authenticated wire header; fixed fields, Optional Block bytes and order must match exactly. An explicit expected KBLEN must also match.

The general mechanisms retain their existing raw-header behavior. EP2 uses its own stricter profile and authenticated raw-header comparison; it does not accept builder references.

All general TR-31 readers require the exact declared wire length, complete hexadecimal ciphertext/MAC bytes and a nonempty aligned ciphertext. Trailing characters, truncated data and incomplete bytes are errors, including on raw input. After authentication, the two-byte key bit length must be positive, byte-aligned and fit the confidential data. AES headers accept 128/192/256-bit keys; TDEA headers accept 128/192-bit keys.

Historical literal quoted representations and new JSON-escaped representations are both readable. The reader checks their complete wire structure and requires a unique byte sequence before authenticating it. It never retries an alternative interpretation after a failed MAC. Optional Block text is authenticated exactly as selected; backslashes are not silently repaired.

Continue with [CreateHeader](cryptoscript-info://function/CreateHeader), the [complete attribute reference](cryptoscript-info://overview/tr31attributes), [AES TR-31](cryptoscript-info://mechanism/WRAP-AES-TR31), [TDEA TR-31](cryptoscript-info://mechanism/WRAP-DES3-TR31), or [EP2](cryptoscript-info://mechanism/EP2-TR31-KEY-BLOCK).
