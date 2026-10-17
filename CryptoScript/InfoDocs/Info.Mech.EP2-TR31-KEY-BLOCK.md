# MECHANISM EP2-TR31-KEY-BLOCK

Strict authenticated key transport for EP2 Security Specification 8.3.0, sections 5.1.4, 5.5.1, 7.2, 8.7 (Table 11, example page 9-63) and 8.11.

## Profile and boundaries

| Transported value | Usage | Mode | Algorithm |
| --- | --- | --- | --- |
| Key PAN Receipt Trm | 99 | X | H |
| Key PAN Surrogate Merchant Primary | 9A | G | H |
| Key PAN Surrogate Merchant Secondary | 9A | G | H |
| Pepper PAN Surrogate Merchant | 9B | G | H |

The complete 16-character header is `D0112{Usage}H{Mode}00N0000`. Only
`D011299HX00N0000`, `D01129AHG00N0000` and `D01129BHG00N0000` are accepted.
Version D, length 0112, key version 00, exportability N, optional-block count 00
and reserved 00 are fixed. 9C-9F are RFU. Primary and Secondary have identical
headers; the surrounding protocol must identify their role. Neither equal key
bytes nor KeyType metadata establish that role.

KBPK and transported material must each contain exactly 16 bytes. The caller
supplies the already derived KEY-ENCRYPTION session variant as KBPK. This
mechanism performs no session KDF. Confidential data is exactly 32 bytes:
`0080 || key[16] || random filler[14]`. There is no AES key-length obfuscation
region in this fixed H profile. AES Key Derivation Binding derives separate
128-bit KBEK and KBAK. Full AES-CMAC authenticates ASCII header plus binary
confidential data. AES-CBC with no padding encrypts confidential data using the
16-byte MAC as IV. Wire form is header plus 64 ciphertext HEX characters plus
32 MAC HEX characters: exactly 112 ASCII characters.

Unwrap validates form and profile, decrypts, verifies all 16 MAC bytes in constant
time, validates the authenticated 0080 length, and only then returns a KEY.
Malformed or unauthenticated data never produces a key. Temporary owned key and
plaintext buffers are cleared; immutable script strings and provider-internal
copies cannot be guaranteed erased by managed code.

## CryptoScript contract

- `Parameters(EP2-TR31-KEY-BLOCK, ...)` returns PARAM.
- `Wrap(parameters, kbpk, key)` requires genuine PARAM, KEY, KEY references.
- `Unwrap(parameters, kbpk, block)` requires genuine PARAM, KEY, VAR references.
- `#BLKH` is required for Wrap: a quoted complete header, optionally supplied
  through a VAR. For Unwrap it is optional and, if present, must equal the
  authenticated received header. It never overrides the received header.
- `#RND` is optional for Wrap, exactly 14 HEX bytes. Omission uses a cryptographic
  random source; an explicitly invalid or empty value is rejected, never replaced.
  It is forbidden for Unwrap. Use a separate PARAM for Unwrap.
- Other and duplicate parameters, unsupported functions, nested results and
  reconstructed/serialized objects in reference positions are rejected.
- Wrap returns the existing TR-31 composite VAR: `"header"0x(ciphertext)0x(mac)`.
  Unwrap accepts that complete composite VAR or a VAR containing the quoted full
  112-character wire string. Partial blocks and trailing characters are rejected.
- Unwrap returns a 128-bit HMAC-classified KEY and preserves the authenticated
  header as its HDR attribute. Header exportability N is protocol information,
  not a hardware-enforced export policy in Workbench.

KBPK metadata expects AES and WrapKey/UnwrapKey usage. Transported KEY metadata
expects HMAC, following header algorithm H; no cryptographic role is inferred
from this classification. Declared conflicts warn without blocking; Unknown
algorithm and unspecified usage remain usable. Technical byte lengths, HEX
validity and inconsistent known size remain blocking. Original KEY identities
and metadata remain unchanged; errors do not include secret values.

## Normative vector

The complete block below is copied from EP2 8.3.0 section 8.7 page 9-63.
Intermediate binding values were calculated independently, not generated from
CryptoScript test output.

| Value | HEX / ASCII |
| --- | --- |
| KBPK | 0123456789ABCDEF23456789ABCDEF01 |
| Key | 00112233445566778899AABBCCDDEEFF |
| Filler | E5EA76E4B74217D867F034AD99C1 |
| Header | D011299HX00N0000 |
| KBEK | 04A20656E2F4FF9CB37D1131AE2DE276 |
| KBAK | 5107101DB9D158DE994A9A8C93DA3FDC |
| Confidential data | 008000112233445566778899AABBCCDDEEFFE5EA76E4B74217D867F034AD99C1 |
| Ciphertext | BF671CBAAF2250BA849EB05E2A2BBF060B1B021B99ABD7D3DB5DF4BCE553E777 |
| MAC | 659CC2356DF2F011D8EAA1375B0D4334 |

`D011299HX00N0000BF671CBAAF2250BA849EB05E2A2BBF060B1B021B99ABD7D3DB5DF4BCE553E777659CC2356DF2F011D8EAA1375B0D4334`

```cryptoscript
KEY kbpk = GenerateKey(AES-ECB, 0x(0123456789ABCDEF23456789ABCDEF01))
KEY material = GenerateKey(HMAC-SHA256, 0x(00112233445566778899AABBCCDDEEFF))
PARAM wrapParams = Parameters(EP2-TR31-KEY-BLOCK, #BLKH:"D011299HX00N0000", #RND:0x(E5EA76E4B74217D867F034AD99C1))
VAR block = Wrap(wrapParams, kbpk, material)
PARAM unwrapParams = Parameters(EP2-TR31-KEY-BLOCK)
KEY restored = Unwrap(unwrapParams, kbpk, block)
```

## Session-key integration

```cryptoscript
KEY session = GenerateKey(HMAC-SHA256, 0x(0123456789ABCDEF23456789ABCDEF01))
PARAM kdf = Parameters(KDF-EP2-SESSION, #SALT:0x(0123456789ABCDEF23456789ABCDEF01456789ABCDEF01236789ABCDEF012345), #VARIANT:KEY-ENCRYPTION)
KEY kbpk = Derive(kdf, session, "")
KEY material = GenerateKey(HMAC-SHA256, 0x(00112233445566778899AABBCCDDEEFF))
PARAM wrapParams = Parameters(EP2-TR31-KEY-BLOCK, #BLKH:"D01129AHG00N0000")
VAR block = Wrap(wrapParams, kbpk, material)
PARAM unwrapParams = Parameters(EP2-TR31-KEY-BLOCK, #BLKH:"D01129AHG00N0000")
KEY restored = Unwrap(unwrapParams, kbpk, block)
```

See [KDF-EP2-SESSION](cryptoscript-info://mechanism/KDF-EP2-SESSION) and
[WRAP-AES-TR31](cryptoscript-info://mechanism/WRAP-AES-TR31).

## Norm editions and generic compatibility

EP2 explicitly cites ANSI X9.143-2021 section 5.3.2.3. The available ANSI
X9.143-2022 document describes AES binding in section 7.2.1, Table 25, using
CMAC derivation inputs `0100000000020080` and `0100010000020080` for AES-128.
The independently checked EP2 vector matches these primitives exactly. This is
evidence for this profile, not a claim that the two entire editions are equivalent:
the 2021 source was unavailable for direct edition comparison. The 2022 header
table defines byte 14 as Key Context (0 permits legacy interoperability), while
EP2 Table 11 mandates reserved 00; this profile always retains EP2's 00.

Generic WRAP-AES-TR31 retains its existing A/T Wrap contracts, AES key-length
obfuscation, optional blocks, variable KBPK lengths and authenticated historical
H Unwrap compatibility. EP2 H generation is available only through this separate
strict mechanism. Generic DES3 Version A/B/C behavior is unchanged.

Workbench is not a complete EP2 terminal or acquirer protocol stack, secure key
store or certified SCD. Session lifecycle, key separation, key distribution,
revocation, replay protection, protocol role selection and secure deletion of
long-lived secrets remain the caller's responsibility. Metadata warnings alone
do not establish PCI or EP2 compliance. Fixed filler is for reproducible tests;
omit RND for normal key transport.
