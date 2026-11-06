# TR-31 attributes

This reference describes **the CreateHeader runtime profile**, using ANSI X9.143-2022 sections 6.2, 6.3 and 6.4. It is not a claim that the Workbench implements every algorithm or policy in that standard.

To create a data encryption header, choose version D for an AES KBPK, `KEYU:D0`, `ALGO:A` for an AES transported key, `MODEU:B` for both directions, and your required export policy, for example `EXP:E`:

```cryptoscript
TR31H header = CreateHeader(KBVID:D, KEYU:D0, ALGO:A, MODEU:B, EXP:E)
```

For a TDEA KBPK choose A, B or C, `ALGO:T` and `WRAP-DES3-TR31`. A is legacy/deprecated. D uses `WRAP-AES-TR31` and accepts either A or T as the transported algorithm. See [the complete guide](cryptoscript-info://function/CreateHeader).

## Fixed fields and builder controls

| Attribute | Wire width | Required/default | Values and meaning |
|---|---:|---|---|
| KBVID | 1 | Required | A/C: TDEA variant binding; B: TDEA derivation binding; D: AES derivation binding |
| KBLEN | 4 | Automatic at Wrap | Complete wire length in decimal, at most 9999. Explicit 16..9999 is an expected length, never an override |
| KEYU | 2 | Required | Usage from the complete supported table below |
| ALGO | 1 | Required | A: AES (D only); T: TDEA (A/B/C/D) |
| MODEU | 1 | Required | B: encrypt and decrypt/wrap and unwrap; D: decrypt/unwrap; E: encrypt/wrap; C: generate and verify; G: generate; V: verify; X: derive; Y: create variants. Must match KEYU |
| KEYVN | 2 | `00` | Two ASCII letters/digits. `00`: no versioning; leading lower-case `c`: key component; other pairs identify a local key version |
| EXP | 1 | Required | E: export under X9.24-compliant protection; N: non-exportable; S: export under protection not necessarily meeting X9.24. Recipient policy is separate from cryptographic processing |
| NUMOPTB | 2 | Automatic | Decimal count 0..99 including generated PB. Explicit count must equal the final count |
| KEYCTX | 1 | `0` | 0: storage or exchange; 1: storage only; 2: exchange only. The 2022 context field is independent of binding version; all three supported for A/B/C/D |
| RSV | 1 | `0` | Reserved byte must be ASCII zero; other explicit values are rejected |
| OPTID | none | Optional, repeatable pairs | Optional Block identifier, see inventory below |
| OPTBD | none | Required after each OPTID | Data for that block; format depends on the ID |

Names and coded values are case sensitive. `WKL` exists in the lexer but is not a header field accepted by CreateHeader: actual key length comes from the transported key.

## Supported KEYU combinations

The following table is the runtime's complete allowlist. Algorithm and version columns must both match. Exportability E/N/S is accepted for these combinations. The allowlist is intentionally narrower than the standard: no asymmetric, HMAC, proprietary numeric, IV or reserved PIN-generation profile is advertised. Historical A derivation mappings are not accepted by this builder; use B/C/D for X or Y. The existing raw reference-vector path is unchanged.

| KEYU | Purpose | MODEU | KBVID | ALGO |
|---|---|---|---|---|
| B0 | DUKPT base derivation key | X | BCD | AT |
| B1 | DUKPT initial key | X | BCD | AT |
| B2 | Base key for variants (deprecated) | Y | BCD | T |
| B3 | Non-DUKPT derivation key | X | BCD | AT |
| C0 | Card verification key | CGV | ABCD | T |
| D0 | Symmetric data encryption key | BDE | ABCD | AT |
| D2 | Decimalization table encryption key | BDE | BCD | AT |
| D3 | Sensitive data encryption key | BDE | D | A |
| E0 | EMV issuer master: application cryptograms | X | BCD | AT |
| E1 | EMV issuer master: messaging confidentiality | X | BCD | AT |
| E2 | EMV issuer master: messaging integrity | X | BCD | AT |
| E3 | EMV issuer master: authentication code | X | BCD | AT |
| E4 | EMV issuer master: dynamic numbers | X | BCD | AT |
| E5 | EMV issuer master: personalization | X | BCD | AT |
| E6 | EMV issuer master: other | X | BCD | AT |
| F0 | EMV card: application cryptograms | X | BCD | AT |
| F1 | EMV card: messaging confidentiality | X | BCD | AT |
| F2 | EMV card: messaging integrity | X | BCD | AT |
| F3 | EMV card: authentication code | X | BCD | AT |
| F4 | EMV card: dynamic numbers | X | BCD | AT |
| F5 | EMV card: personalization | X | BCD | AT |
| F6 | EMV card: other | X | BCD | AT |
| K0 | Key encryption/wrapping key | BDE | ABCD | AT |
| K1 | X9.143 key block protection key | BDE | BCD | AT |
| K4 | ISO 20038 key block protection key | BDE | D | A |
| M0 | ISO 16609 MAC algorithm 1 | CGV | ABCD | T |
| M1 | ISO 9797-1 MAC algorithm 1 | CGV | ABCD | T |
| M2 | ISO 9797-1 MAC algorithm 2 | CGV | ABCD | T |
| M3 | ISO 9797-1 MAC algorithm 3 | CGV | ABCD | T |
| M4 | ISO 9797-1 MAC algorithm 4 | CGV | ABCD | T |
| M5 | ISO 9797-1:1999 MAC algorithm 5 | CGV | ABCD | T |
| M6 | CMAC / ISO 9797-1:2011 algorithm 5 | CGV | BCD | AT |
| M8 | ISO 9797-1:2011 MAC algorithm 6 | CGV | BCD | AT |
| P0 | PIN encryption key | BDE | ABCD | AT |
| P2 | Other PIN generation algorithm | G | BD | AT |
| V0 | Other PIN verification algorithm | CGV | ABCD | AT |
| V1 | IBM 3624 PIN verification | CGV | ABCD | T |
| V2 | VISA PVV PIN verification | CGV | ABCD | T |
| V3 | X9.132 PIN verification algorithm 1 | CGV | D | A |
| V4 | X9.132 PIN verification algorithm 2 | CGV | D | A |
| V5 | X9.132 PIN verification algorithm 3 | CGV | D | A |

`MODEU:N` (no additional operation restriction), `S` (signature) and `T` (sign and decrypt) are lexically known but have no supported symmetric builder combination. `ALGO:D` (single DES), `E` (elliptic curve), `H` (HMAC), `R` (RSA) and `S` (DSA) are lexically known but not supported by the implemented transport profile.

The lexer separately lists KEYU values B0-B3, C0, D0-D3, E0-E3, F3-F6, I0, K0-K4, M0-M1, P2 and V0-V5. Its combined field-value token also includes single letters and Optional Block IDs, which are **not** thereby valid KEYU values. Other supported codes in the table are parsed as identifiers. Lexically known D1, I0, K2 and K3 are rejected by this profile; the standard defines them respectively for asymmetric data encryption, IV data, TR-34 asymmetric keys and asymmetric wrapping/agreement.

## Optional Block inventory

All 17 IDs in TR31Lexer are inventoried below. Every supported block is stored in the supplied order, with no duplicate IDs. Quotes mean actual printable ASCII text. For hex fields, `"0017EB36"` and `0x(0017EB36)` supply the same hex-ASCII data. Hex fields may have an odd number of digits where the field format requires it (WP). There is no automatic conversion of binary data to label text or base64.

| OPTID | Meaning | Runtime support and exact OPTBD format |
|---|---|---|
| AL | Asymmetric key lifetime | Unsupported: requires an asymmetric profile. Standard structure: version `01` followed by lifetime `00` (ephemeral) or `01` (static) |
| BI | DUKPT base identifier | Supported for KEYU B1. TDEA: `00` + 10 hex digits of KSI; AES: `01` + 8 hex digits of BDK ID |
| CT | Public certificate or chain | Unsupported: private asymmetric key and certificate matching required. Standard uses a certificate-format prefix and base64 certificate data; chains add individual format/length fields |
| DA | Allowed derivation attributes | Supported only for B3 (ANSI X9.143-2022 section 6.3.6.4). Quoted `01` plus one or more 5-character groups: 2-char KEYU, ALGO, MODEU, EXP. Each group must satisfy this profile's combination table. Example `"01P0TENM0TGN"` |
| HM | HMAC hash selection | Unsupported: needs ALGO H / KEYU M7. Standard hash IDs: 10 SHA-1; 20/21/22/23 SHA-224/256/384/512; 24/25 SHA-512/224 and /256; 30/31/32/33 SHA3-224/256/384/512; 40/41 SHAKE128/256 |
| IK | AES DUKPT initial key identifier | Supported for B1/A: exactly 16 hex digits, BDK ID followed by derivation ID |
| KC | Transported key check value | Supported hex: `00` + 6 hex digits for legacy TDEA KCV; `01` + 10 hex digits for CMAC KCV. Legacy KC is rejected for AES transported keys |
| KP | Current KBPK check value | Same KCV format. Legacy `00` is allowed only for A/B/C; D requires CMAC prefix `01` |
| KS | TDEA DUKPT initial serial number | Supported for B1/T: exactly 20 hex digits; the low 21 transaction-counter bits must be zero |
| KV | Informational field-values version | Supported legacy/deprecated form: exactly `0000` |
| LB | User label | Supported: any quoted printable ASCII, including empty text |
| PA | Proprietary/regional algorithm description | Supported for B3/K0/P2/V0: nonempty quoted printable ASCII identifying the agreed algorithm/issuer |
| PB | Header padding | Generated automatically when needed. Explicit quoted printable ASCII, possibly empty, must be last and align the complete Optional Block area |
| PK | Required future export KBPK check value | Supported structure: `00` + 6 or `01` + 10 hex digits. This describes a future export key, not necessarily the current wrapping key |
| TC | Key creation time | Supported quoted UTC timestamp in one of the four formats below |
| TS | Key-block creation time | Supported quoted UTC timestamp in one of the four formats below |
| WP | Wrapping pedigree | Supported: `000`, `001`, `002`, `003`. First two digits select format 00; final digit means respectively no known weakness, previously weaker wrapping key, quantum-vulnerable asymmetric wrapping, or both latter conditions |

KC/KP/PK data is structurally validated; CreateHeader has no key material and does not calculate or verify the check value. Workbench transports DA, PK and WP as authenticated metadata; it does not implement an SCD policy engine that enforces future derivation/export restrictions or certifies wrapping history. These data must truthfully describe the key and be enforced by the consuming system.

TC and TS accept exactly:

- `yyyyMMddHHmmssZ`, for example `"20261020120000Z"`
- `yyyyMMddHHmmssffZ`, for example `"2026102012000012Z"`
- `yyyy-MM-ddTHH:mm:ssZ`, for example `"2026-10-20T12:00:00Z"`
- `yyyy-MM-ddTHH:mm:ss.ffZ`, for example `"2026-10-20T12:00:00.12Z"`

Calendar dates and times are validated; Z is mandatory and fractional precision, when present, is two digits.

## Length and encoding

Each Optional Block serializes as ID (2 characters), total length (2 hexadecimal characters), and data. The total includes the ID and all length fields. Up to 255 characters use the short form; longer blocks use `ID` + `00` + `04` + a four-hex-digit total length + data. This is the extended format supported by the existing reader. The complete key block still cannot exceed 9999 characters.

All Optional Blocks together must align to 16 characters for D or 8 for A/B/C. If required, the builder adds one final PB filled with ASCII zero characters; its minimum serialized length is 4. This block counts toward NUMOPTB. Explicit PB data is preserved and must already provide correct alignment. Padding is never inserted between user blocks.

For example, `OPTID:LB, OPTBD:"Test"` produces `LB08Test`. Under version D the builder adds `PB080000`, so NUMOPTB is 02. Under B the label is already aligned and NUMOPTB is 01.

```cryptoscript
TR31H header = CreateHeader(
    KBVID:D, KEYU:B1, ALGO:A, MODEU:X, EXP:N,
    OPTID:BI, OPTBD:0x(0112345678),
    OPTID:IK, OPTBD:0x(1234567812345678),
    OPTID:LB, OPTBD:"Initial terminal key"
)
```

An explicit KBLEN is checked against the actual Wrap result, and an explicit NUMOPTB against the padded block count. Missing values, duplicate fields, unsupported IDs, wrong formats or limits are blocking errors, never silent corrections.

See [TR-31](cryptoscript-info://overview/tr31), [CreateHeader](cryptoscript-info://function/CreateHeader), [AES wrapping](cryptoscript-info://mechanism/WRAP-AES-TR31), [TDEA wrapping](cryptoscript-info://mechanism/WRAP-DES3-TR31) and [EP2 restrictions](cryptoscript-info://mechanism/EP2-TR31-KEY-BLOCK).
