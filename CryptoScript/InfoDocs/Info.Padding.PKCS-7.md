# PADDING PKCS-7

CryptoScript uses the canonical name `PKCS-7` for padding that always adds at least one byte. The number and values of the padding bytes allow the original data length to be recovered after decryption.

---

## Syntax

Use the canonical, case-sensitive literal `PKCS-7` as the value of `#PAD`:

```text
#PAD:PKCS-7
```

Spellings such as `PKCS7`, `pkcs-7`, and `Pkcs-7` are not valid CryptoScript padding names.

## Padding Rule

For a block size `B` and an input length `L`, both measured in bytes, the number of padding bytes is:

```text
N = B - (L mod B)
```

CryptoScript appends exactly `N` bytes, each with the numeric value `N`. Therefore, `1 <= N <= B`, and the padded length is always a positive multiple of the block size. The rule is defined for block sizes smaller than 256 bytes; CryptoScript uses 16-byte AES blocks and 8-byte DES3 blocks.

## Full Block and Empty Input

PKCS-7 always adds padding:

- A block-aligned input receives one additional full padding block. AES appends sixteen `0x10` bytes; DES3 appends eight `0x08` bytes.
- An empty input becomes exactly one full padding block, using the same values.

This differs from `NONE`, which adds no bytes.

## Unpadding and Validation

The last decrypted byte specifies the padding length `N`. It must be between one and the block size, and all final `N` bytes must contain the value `N`. CryptoScript removes these bytes only when the padding is valid.

Invalid cases include `N = 0`, `N` greater than the block size, and inconsistent padding bytes. CryptoScript reports an error when validation fails. Such an error does not by itself prove that the padding was deliberately modified: a wrong key or IV, or damaged ciphertext, can produce the same result.

## Supported Mechanisms

`PKCS-7` is selectable and effective for:

- AES-CBC encryption and decryption
- [AES-CBC-MAC](cryptoscript-info://mechanism/AES-CBC-MAC) calculation
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with DES3-CBC

It is the default padding for AES-CBC, AES-CBC-MAC, DES3-CBC, and DES3-ECB. For AES-CBC-MAC, padding is materialized with the 16-byte AES block size before the CBC-MAC calculation: empty and aligned messages receive a complete block of sixteen `0x10` bytes, while partial messages are extended to the next block. Padding does not make classic CBC-MAC generally secure for variable-length messages; see the [AES-CBC-MAC mechanism page](cryptoscript-info://mechanism/AES-CBC-MAC).

AES-ECB always uses `NONE`; AES-CMAC and DES3-CMAC do not use external PKCS-7 padding; and DES3-RETAIL explicitly does not support it. AES-CTR, AES-GCM, AES-CCM, and AES-GMAC do not apply effective external PKCS-7 padding.

## Example Usage

### Byte-Level Example

For a 16-byte AES block:

```text
Input:   00 01 02 03 04 05 06 07 08 09 0A 0B 0C
Padding: 03 03 03
Result:  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 03 03 03
```

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:PKCS-7)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting `ciphertext` is `0x(66B68414C193C1C7A3EA6B5A54A786B1)`, and `decrypted` is `0x(010203)`.

## References

CryptoScript calls this selection `PKCS-7`. Its padding rule is the rule described by PKCS #7 version 1.5 and its successor, CMS.

- [PKCS #7 Version 1.5, Section 10.3](https://www.rfc-editor.org/rfc/rfc2315.html#section-10.3)
- [RFC 5652, Cryptographic Message Syntax (CMS), Section 6.3](https://www.rfc-editor.org/rfc/rfc5652.html#section-6.3)
