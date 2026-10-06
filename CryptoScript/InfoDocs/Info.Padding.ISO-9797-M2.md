# PADDING ISO-9797-M2

`ISO-9797-M2` is the canonical CryptoScript identifier for ISO/IEC 9797-1 Padding Method 2. For byte-aligned data, padding begins with `0x80` and continues with zero bytes up to the block boundary. M2 always adds at least one padding byte, so aligned data receives a complete additional padding block.

The `0x80` marker allows CryptoScript to locate and structurally validate the padding boundary during decryption.

---

## Syntax

Use the canonical, case-sensitive literal `ISO-9797-M2` as the value of `#PAD`:

```text
#PAD:ISO-9797-M2
```

Non-canonical spellings are not registered padding values. In particular, `ISO9797M2` is parsed as a general identifier and then rejected as an unknown parameter value by the public CryptoScript path.

## Padding Method 2

ISO/IEC 9797-1:2011, 6.3.3 defines Method 2 in bits. One `1` bit is appended to the data, followed by the minimum number of `0` bits required to produce a positive integer number of complete blocks. For empty data, this produces one `1` bit followed by `n - 1` zero bits, where `n` is the block size in bits.

For CryptoScript's byte-aligned inputs, this rule produces one byte `0x80` followed by zero bytes. The normative rule belongs to the MAC model defined by ISO/IEC 9797-1; CryptoScript also exposes its byte-oriented form on the supported encryption paths listed below.

## CryptoScript Padding Rule

For a block size `B` and input length `L`, both measured in bytes, the number of padding bytes is:

```text
N = B - (L mod B)
1 <= N <= B
```

CryptoScript appends one byte with the value `0x80`, followed by `N - 1` bytes with the value `0x00`. AES uses `B = 16`; DES3 uses `B = 8`.

The result is deterministic. M2 has no length byte and no separate length block; its padding boundary is identified by the `0x80` marker and the trailing zero bytes.

## Partial, N=1, Aligned, and Empty Input

For five input bytes and an 8-byte DES3 block:

```text
Input:   01 02 03 04 05
Padding: 80 00 00
Result:  01 02 03 04 05 80 00 00
```

With seven input bytes, `N = 1`; no zero byte is needed:

```text
Input:   01 02 03 04 05 06 07
Padding: 80
Result:  01 02 03 04 05 06 07 80
```

For aligned input, `N = B`, so M2 appends a complete additional block. DES3 appends `80 00 00 00 00 00 00 00`; AES appends `80` followed by fifteen `00` bytes. This differs from M1, which adds no padding to aligned non-empty input.

Empty input also becomes exactly one complete padding block: `80` followed by seven `00` bytes for DES3, or fifteen `00` bytes for AES. This agrees with the verified empty-data rule in the 2011 edition.

## Unpadding and Validation

For M2, CryptoScript uses `Iso7816Padding.Unpad`. The padded input must be non-empty and block-aligned. CryptoScript examines only the final block, moves backwards over trailing `0x00` bytes, and requires the first non-zero byte reached to be `0x80`. It removes that marker and every following zero byte.

Endings such as `80`, `80 00`, and `80 00 00` are valid. Endings such as `80 00 01` and `80 01 00` are invalid, as is a final block containing only zero bytes. A marker outside the final block is insufficient.

If the final block is:

```text
01 02 80 00 80 00 00 00
```

unpadding returns:

```text
01 02 80 00
```

The relevant marker is the `0x80` immediately before the final zero sequence; earlier `0x80` bytes remain data.

This is structural padding validation only. It does not by itself establish authenticity, integrity, or freedom from manipulation.

## Relationship to ISO-7816

For CryptoScript's byte-aligned inputs, `ISO-9797-M2` and `ISO-7816` are byte-for-byte identical. Both use `Iso7816Padding` and therefore have the same padding formula, byte form, `N = 1` behavior, full-block behavior, empty-input behavior, unpadding, structural validation, and failure cases on their shared AES and DES3 paths.

The public identifiers remain separate because they refer to different normative contexts. `ISO-9797-M2` denotes Padding Method 2 from ISO/IEC 9797-1 in its MAC model. CryptoScript's `ISO-7816` identifier refers to the byte form from the ISO/IEC 7816-4 Secure Messaging context. Byte identity does not make the normative concepts synonyms or the identifiers aliases.

This distinction is externally visible for DES3-RETAIL. That mechanism accepts only `ISO-9797-M1` and `ISO-9797-M2`; it rejects `ISO-7816` even though M2 and ISO-7816 use the same `Iso7816Padding(8)` implementation and byte form. The default padding for DES3-RETAIL is `ISO-9797-M2`.

## Relationship to ISO-9797-M1

M1 appends only `00` bytes when required. It adds no block to aligned non-empty data, represents empty data as one zero block, has no distinct padding boundary, and is not unpadded by CryptoScript.

M2 begins with `80`, always adds at least one padding byte, adds a complete block to aligned data, and represents empty data as one complete `80 00 ... 00` block. Its marker gives CryptoScript a boundary that can be structurally validated and removed.

M2 has no length block. M3 additionally prepends a complete block containing the original message length in bits.

## Supported Mechanisms

`ISO-9797-M2` is selectable and effective for:

- AES-CBC encryption and decryption
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with `#MECH:DES3-CBC`
- DES3-RETAIL MAC calculation

DES3-RETAIL accepts M1 and M2 only, and defaults to M2. DES3-CBC-MAC applies M2 before MAC calculation: partial input receives `80` and zero bytes, aligned input receives an additional full block, and empty input becomes one full M2 block. A MAC operation does not unpad data. The default of the underlying DES3-CBC parameter set remains `PKCS-7`, not M2.

M2 is not effective external padding for AES-ECB, AES-CMAC, DES3-CMAC, AES-CTR, AES-GCM, AES-CCM, or AES-GMAC. Depending on the mechanism, `#PAD` is overridden with `NONE` or the stored value is ignored; CMAC uses its own internal final-block processing.

## Example Usage

### Byte-Level Example

For a partial 8-byte DES3 block:

```text
Input:   01 02 03 04 05
Padding: 80 00 00
Result:  01 02 03 04 05 80 00 00
```

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:ISO-9797-M2)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting `ciphertext` is `0x(BDCA8D32BBE6732533C7FA16A9F0C3FC)`, and `decrypted` is `0x(010203)`. The ciphertext matches the corresponding ISO-7816 example because the two identifiers produce the same padded plaintext on this shared AES-CBC path.

## Historical and Source Note

ISO/IEC 9797-1:2011, *Information technology - Security techniques - Message Authentication Codes (MACs) - Part 1: Mechanisms using a block cipher*, defines Padding Method 2 in Section 6.3.3 as part of its MAC model. CryptoScript applies the verified byte-oriented rule more broadly through `#PAD:ISO-9797-M2` on the supported encryption and MAC paths listed above. This does not mean that ISO/IEC 9797-1 generally specifies padding for AES-CBC, DES3-CBC, or DES3-ECB encryption.

ISO/IEC 9797-1:2011/Amd 1:2023 exists and is listed as a published amendment in the official ISO catalogue. Its normative amendment text was not available locally for this documentation. The concrete Method 2 rule on this page is therefore attributed only to the locally verified 2011 edition; no claim is made that the amendment changed or left that rule unchanged.

## References

- ISO/IEC 9797-1:2011, Section 6.3.3, *Padding Method 2*
- [ISO catalogue: ISO/IEC 9797-1:2011](https://www.iso.org/standard/50375.html)
- [ISO catalogue: ISO/IEC 9797-1:2011/Amd 1:2023](https://www.iso.org/standard/78748.html)
- [CryptoScript detail page: ISO-7816](cryptoscript-info://padding/ISO-7816)
