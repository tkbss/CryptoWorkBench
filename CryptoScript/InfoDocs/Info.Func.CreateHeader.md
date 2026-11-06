# FUNCTION CreateHeader

`CreateHeader` validates named header attributes and returns a structured **TR31H** variable. It does not generate, encrypt or wrap a key. It is the canonical builder for the general TR-31 Wrap mechanisms.

```cryptoscript
TR31H header = CreateHeader(
    KBVID:D,
    KEYU:D0,
    ALGO:A,
    MODEU:B,
    KEYVN:00,
    EXP:E,
    KEYCTX:0
)
Print(header)
```

`KBVID`, `KEYU`, `ALGO`, `MODEU` and `EXP` are required. `KEYVN` defaults to `00`, `KEYCTX` to `0`, and `RSV` is always `0`. `NUMOPTB` is computed, including any generated padding block. `KBLEN` is computed by Wrap. All attributes are case sensitive.

Before Wrap, Print displays `KBLEN:auto (unresolved)`. This is a definition, not a complete wire header. An explicit `KBLEN:144` is an expectation checked during Wrap; an explicit `NUMOPTB` is checked during construction. Inconsistent expectations produce errors.

## Optional Blocks

Use repeated, adjacent `OPTID` / `OPTBD` pairs. Order is preserved and duplicate IDs are rejected. Text blocks require quoted printable ASCII. Hexadecimal fields accept either a quoted hex string or `0x(...)`; the latter supplies hex digits for the wire field, not an implicit conversion to text. A KCV includes its algorithm prefix, so `0x(123456)` alone is not a valid KC value.

```cryptoscript
TR31H header = CreateHeader(
    KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E,
    OPTID:LB, OPTBD:"Test",
    OPTID:WP, OPTBD:"000",
    OPTID:TS, OPTBD:"2026-10-20T12:00:00Z"
)
```

The builder calculates short or extended lengths and appends `PB` if needed to align all Optional Blocks. An explicit PB must be last and already produce correct alignment. See the [attribute reference](cryptoscript-info://overview/tr31attributes) for every supported ID and its data format.

## AES transport and reuse

```cryptoscript
KEY kbpk = GenerateKey(AES-CBC, 256)
KEY shortKey = GenerateKey(AES-CBC, 128)
KEY longKey = GenerateKey(AES-CBC, 256)
TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E, OPTID:LB, OPTBD:"Example")
PARAM p = #MECH:WRAP-AES-TR31 #BLKH:header
VAR firstBlock = Wrap(p, kbpk, shortKey)
VAR secondBlock = Wrap(p, kbpk, longKey)
KEY firstKey = Unwrap(p, kbpk, firstBlock)
KEY secondKey = Unwrap(p, kbpk, secondBlock)
Print(header)
```

Each Wrap creates its own final serialization. Neither `header` nor `p` is changed. Existing obfuscation makes AES key payloads the same padded size for 128, 192 and 256 bits; reuse does not imply different total wire lengths.

## TDEA transport

```cryptoscript
KEY kbpk = GenerateKey(DES3-CBC, 192)
KEY transportKey = GenerateKey(DES3-CBC, 128)
TR31H header = CreateHeader(KBVID:B, KEYU:D0, ALGO:T, MODEU:B, EXP:E)
PARAM p = #MECH:WRAP-DES3-TR31 #BLKH:header
VAR wrapped = Wrap(p, kbpk, transportKey)
KEY recovered = Unwrap(p, kbpk, wrapped)
```

For AES protection of a TDEA key, use version D with `ALGO:T`, an AES KBPK, and `WRAP-AES-TR31`.

## Errors and compatibility

- Missing required attributes, duplicate fixed fields, unknown names and unsupported combinations are errors.
- An unknown `#BLKH` identifier is an error. A genuine TR31H reference preserves its object identity.
- The builder supports only the symmetric transport profiles listed in the reference. Asymmetric and HMAC header profiles are rejected.
- Optional Blocks are validated by ID. Non-ASCII text, an incomplete pair, malformed dates/KCVs, duplicate IDs, incorrect explicit padding, count conflicts and length overflow are errors.
- Semantic KEY metadata warnings retain their existing behavior; malformed protocol data remains blocking.
- Unwrap with typed `#BLKH` authenticates first, then compares every header byte except the automatically resolved length. Explicit expected lengths are checked too.
- `#BLKH:"D0144D0AB00E0000"` remains a raw header with an explicit length. Wrap checks it and does not rewrite its bytes. The general raw Unwrap adapters keep their legacy behavior; EP2 retains its own exact optional authenticated-header comparison.
- EP2 does not accept TR31H references. Use its documented strict profile.
- Both raw and typed Unwrap reject extra wire characters, incomplete hexadecimal bytes and invalid authenticated key lengths. Historical literal backslashes and newly escaped strings are resolved by complete structural validation before MAC verification.

`tr31` and `tr31attributes` remain valid variable names. In `Info(tr31)` and `Info(tr31attributes)` they select documentation topics; in ordinary arguments such as `Print(tr31)` they refer to variables. Bare function names such as `CreateHeader` are documentation arguments only within `Info(...)`.

The existing semicolon brace syntax now uses the same builder and validation:

```cryptoscript
TR31H header = {KBVID:D; KEYU:D0; ALGO:A; MODEU:B; EXP:E;}
```

See [TR-31 introduction](cryptoscript-info://overview/tr31), [attributes](cryptoscript-info://overview/tr31attributes), [WRAP-AES-TR31](cryptoscript-info://mechanism/WRAP-AES-TR31) and [WRAP-DES3-TR31](cryptoscript-info://mechanism/WRAP-DES3-TR31).
