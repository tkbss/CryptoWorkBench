# PADDING ANSI-X923

CryptoScript uses the canonical name `ANSI-X923` for padding that fills the final block with zero-valued bytes and records the total padding length in the last byte.

---

## Syntax

Use the canonical, case-sensitive literal `ANSI-X923` as the value of `#PAD`:

```text
#PAD:ANSI-X923
```

The spelling `ANSIX923` is not a valid CryptoScript padding name.

## Padding Rule

For a block size `B` and an input length `L`, both measured in bytes, the number of padding bytes is:

```text
N = B - (L mod B)
```

CryptoScript appends `N - 1` bytes with the value `0x00`, followed by one byte with the numeric value `N`. Therefore, `1 <= N <= B`, and the padded length is always a positive multiple of the block size.

## Full Block and Empty Input

ANSI-X923 always adds padding:

- A block-aligned input receives one additional full padding block. AES appends fifteen `0x00` bytes followed by `0x10`; DES3 appends seven `0x00` bytes followed by `0x08`.
- An empty input becomes exactly one full padding block, using the same layouts.

## Unpadding and Validation

The last decrypted byte specifies the padding length `N`. It must be between one and the block size, and the preceding `N - 1` padding bytes must all be `0x00`. When `N = 1`, there are no preceding filler bytes. CryptoScript removes the `N` bytes only after successful validation.

Invalid cases include `N = 0`, `N` greater than the block size, and a non-zero byte within the expected zero-filled area. CryptoScript reports an execution error for invalid padding. Empty padded data and data whose length is not a multiple of the block size are also rejected by the applicable block-cipher operations. A padding error does not by itself prove that the padding was deliberately modified: a wrong key or IV, or damaged ciphertext, can produce the same result.

## Supported Mechanisms

`ANSI-X923` is selectable and effective for:

- AES-CBC encryption and decryption
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with DES3-CBC

It is not the default padding for these mechanisms. AES-CBC, DES3-CBC, and DES3-ECB default to `PKCS-7`. AES-ECB always uses `NONE`; AES-CMAC and DES3-CMAC do not use external ANSI-X923 padding; and DES3-RETAIL explicitly does not support it. AES-CTR, AES-GCM, AES-CCM, and AES-GMAC do not apply effective external ANSI-X923 padding.

## Example Usage

### Byte-Level Example

For a 16-byte AES block:

```text
Input:   00 01 02 03 04 05 06 07 08 09 0A 0B 0C
Padding: 00 00 03
Result:  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 00 00 03
```

For the same input, `PKCS-7` would append `03 03 03`; ANSI-X923 instead uses zero-valued filler bytes and places the padding length only in the final byte.

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:ANSI-X923)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting `ciphertext` is `0x(DB9E61C7DA230568E1A5F0C0B83BDBD9)`, and `decrypted` is `0x(010203)`.

## Historical and Source Note

The name `ANSI-X923` refers historically to ANSI X9.23. CryptoScript implements the zero-filled behavior and validation described above. A historical ANSI X9.23 original containing the exact padding definition was not available for this documentation. This page therefore documents behavior verified from the CryptoScript implementation and tests, with current platform documentation and source code as supporting references; it does not attribute CryptoScript's concrete zero-filling or validation rules to the historical standard as normative requirements.

## References

- [Microsoft .NET `PaddingMode` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.paddingmode)
- [.NET `SymmetricPadding` source code](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/SymmetricPadding.cs)
- [FIPS PUB 140-1, Appendix C](https://csrc.nist.gov/files/pubs/fips/140-1/upd1/final/docs/fips1401.pdf), which identifies ANSI X9.23-1988 among the referenced standards
- [ASC X9 TR 37-2010](https://webstore.ansi.org/standards/ascx9/ascx9tr372010), which records later migration guidance for X9.23
