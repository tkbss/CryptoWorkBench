# PADDING TLS-CBC

`TLS-CBC` is the canonical CryptoScript identifier for the padding byte format used by the classic TLS CBC record formats. CryptoScript makes this byte format available as a general padding selection: it appends the padding bytes to the data supplied to `Encrypt` and removes them after `Decrypt`. It does not create or process a complete TLS record. In particular, it does not add a record header or MAC, manage sequence numbers, derive TLS keys, or perform version-specific record-IV handling. TLS 1.3 does not use this classic CBC padding format.

---

## Syntax

Use the canonical, case-sensitive literal `TLS-CBC` as the value of `#PAD`:

```text
#PAD:TLS-CBC
```

The non-canonical spelling `TLSCBC` is parsed as a general identifier and then rejected semantically with `Unknown parameter value : TLSCBC`.

## TLS-CBC Padding Format

The classic TLS CBC record structure ends with:

```text
padding || padding_length
```

If the one-byte `padding_length` value is `P`, the preceding `padding` vector contains `P` bytes and is followed by the `padding_length` byte itself. All `P + 1` bytes have the value `P`:

```text
P = 2  ->  02 02 02
```

The final `02` is conceptually the `padding_length` field. This off-by-one convention is part of the TLS format: the encoded value is one less than the total number of padding bytes.

## Padding-Length Semantics

Let `N` be the total number of bytes in `padding || padding_length`. Then:

```text
N = P + 1
P = N - 1
```

Consequently:

```text
N = 1  ->  00
N = 2  ->  01 01
N = 3  ->  02 02 02
```

## CryptoScript Padding Rule

For a block size `B`, input length `L`, and remainder `r = L mod B`, all measured in bytes, CryptoScript calculates the minimal block-aligning padding length:

```text
N = B          if r = 0
N = B - r      otherwise
P = N - 1
```

It appends exactly `N` bytes, each with the value `P`. CryptoScript `Pad` generates only this minimal padding, so `1 <= N <= B` on encryption.

## Partial, Aligned, and Empty Input

For the 8-byte DES3 block size, the final block is formed as follows:

```text
01 02 03 04 05 06 07     + 00
01 02 03 04 05 06        + 01 01
01 02 03 04 05           + 02 02 02
```

Already aligned data receives a complete padding block:

```text
07 07 07 07 07 07 07 07
```

Empty input becomes the same complete DES3 padding block. With AES, `B = 16`, so aligned and empty input receive sixteen `0x0F` bytes. TLS-CBC therefore always adds at least one byte, including for aligned and empty data.

## Extended TLS Padding

The classic TLS RFCs allow padding longer than the minimum needed for block alignment. Because `padding_length` is a `uint8`, `P` can range from 0 through 255 and a valid structure can contain from 1 through 256 total padding bytes.

CryptoScript deliberately behaves asymmetrically:

```text
Pad    -> generates minimal block-aligning padding
Unpad  -> accepts minimal or extended TLS-conforming padding
```

For example, a verified public AES-CBC decryption test uses 16 bytes of application data followed by 32 bytes of `0x1F`. Here `P = 31`, `N = 32`, and the total 48 bytes occupy three AES blocks. CryptoScript removes all 32 padding bytes. This asymmetry is intentional, not a product error.

## Unpadding and Validation

CryptoScript requires the padded input to be present, non-empty, and block-aligned. It reads the last byte as `P`, calculates `N = P + 1`, and requires `N` not to exceed the available input length. Every one of the final `N` bytes must equal `P`; only then are exactly those bytes removed.

The boundary encodings are valid when the complete structure is present and block-aligned:

```text
P = 0    -> one final 00 byte
P = 255  -> 256 final FF bytes
```

Unpadding is intentionally not restricted to one cipher block.

An ending such as `01 02 02` is invalid: the last byte declares three bytes with value `02`, but one of those bytes has value `01`. The public CryptoScript decryption path reports a `SemanticErrorException` with `Invalid TLS-CBC padding bytes.` Padding validation is structural validation; it is not cryptographic integrity or authenticity verification.

## TLS Version Context

- **TLS 1.0:** RFC 2246 defines the classic CBC padding format and chains the IV between records after the first record. It uses the same sender encoding, but Section 6.2.3.2 does not state the same explicit receiver requirement to check every padding byte that appears in later versions.
- **TLS 1.1:** RFC 4346 retains the padding format, introduces an explicit IV for each record, and requires receivers to check every padding byte.
- **TLS 1.2:** RFC 5246 retains the format and uses an explicit, unpredictable IV. Invalid padding is handled through the record error path.
- **TLS 1.3:** RFC 8446 uses AEAD rather than the classic CBC record construction. Its optional zero-valued record padding is a different format and is not `#PAD:TLS-CBC`.

## TLS Record Context

For a classic TLS CBC cipher suite, the plaintext record fragment is conceptually:

```text
content || MAC || padding || padding_length
```

TLS calculates its MAC before CBC encryption and incorporates record context including sequence information and record metadata. CryptoScript receives only the data that the script supplies. Selecting `TLS-CBC` does not calculate or append that TLS MAC and does not construct the surrounding TLS record.

## CryptoScript Scope

`#PAD:TLS-CBC` does not automatically generate or manage:

- a TLS record header, content type, protocol version, or record length;
- a TLS HMAC or other MAC, or a MAC attachment;
- the TLS record sequence number;
- a TLS PRF or TLS key derivation;
- TLS-specific IV generation or serialization of an explicit IV.

The `#IV` used by AES-CBC or DES3-CBC is an ordinary CryptoScript mechanism parameter. It is not automatic TLS record-IV processing.

## Relationship to PKCS-7

For `N` total padding bytes, TLS-CBC appends `N` bytes with value `N - 1`; PKCS-7 appends `N` bytes with value `N`:

```text
                         TLS-CBC       PKCS-7
three padding bytes      02 02 02      03 03 03
one padding byte         00            01
full AES padding block   16 x 0F       16 x 10
```

Classic TLS normatively permits extended padding of up to 256 total bytes. PKCS-7 padding is limited to the cipher block size. CryptoScript TLS-CBC encryption nevertheless generates only the minimal `N` needed to reach the next block boundary.

For comparison, with `N` total bytes ANSI-X923 uses `N - 1` zero bytes followed by `N`, while ISO-10126 uses `N - 1` arbitrary or random filler bytes followed by `N`.

## Supported Mechanisms

`TLS-CBC` is selectable and effective for:

- AES-CBC encryption and decryption
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with `#MECH:DES3-CBC`

DES3-ECB merely reuses the byte format; it is not a normative TLS use of ECB. `TLS-CBC` is not effective external padding for AES-ECB, AES-CMAC, DES3-CMAC, AES-CTR, AES-GCM, AES-CCM, or AES-GMAC. Depending on the mechanism, `PAD` is overridden with `NONE`, retained but not applied, or CMAC performs its own internal final-block processing.

`TLS-CBC` is not the default for any CryptoScript mechanism.

### DES3-CBC-MAC

CryptoScript can apply the TLS-CBC byte format before its generic DES3-CBC-MAC calculation. This path uses an 8-byte block size, a zero IV, and minimal TLS-CBC padding. Empty, partial, and aligned input are supported, and the MAC path performs no unpadding.

This is CryptoScript CBC-MAC plus TLS-CBC byte padding. It is not a TLS-defined “TLS-CBC-MAC”.

### DES3-RETAIL

DES3-RETAIL accepts only `ISO-9797-M1` and `ISO-9797-M2`, defaults to M2, and explicitly rejects `TLS-CBC`.

## Example Usage

### Byte-Level Example

For five input bytes and an 8-byte DES3 block:

```text
Input:    01 02 03 04 05
N:        3
P:        N - 1 = 2
Padding:  02 02 02
Result:   01 02 03 04 05 02 02 02
```

The one-byte minimum is encoded with zero:

```text
01 02 03 04 05 06 07 00
```

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:TLS-CBC)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The three input bytes are followed by thirteen `0x0C` bytes:

```text
01 02 03 0C 0C 0C 0C 0C 0C 0C 0C 0C 0C 0C 0C 0C
```

The resulting `ciphertext` is `0x(85D4DDE2744135C1BCBFE68B4505AD36)`, and `decrypted` is `0x(010203)`.

## Security Note

Padding validation alone does not establish authenticity or integrity. Classic TLS CBC record constructions have known historical timing and padding-oracle problems. Selecting `#PAD:TLS-CBC` by itself provides none of the security properties of a complete TLS protocol implementation.

## References

- [RFC 2246, TLS 1.0, Section 6.2.3.2](https://www.rfc-editor.org/rfc/rfc2246.html#section-6.2.3.2)
- [RFC 4346, TLS 1.1, Section 6.2.3.2](https://www.rfc-editor.org/rfc/rfc4346.html#section-6.2.3.2)
- [RFC 5246, TLS 1.2, Section 6.2.3.2](https://www.rfc-editor.org/rfc/rfc5246.html#section-6.2.3.2)
- [RFC 8446, TLS 1.3, Section 5.2](https://www.rfc-editor.org/rfc/rfc8446.html#section-5.2)
