# PADDING ISO-9797-M1

`ISO-9797-M1` is the canonical CryptoScript identifier for ISO/IEC 9797-1 Padding Method 1. It appends zero-valued bytes only as needed to reach the block boundary. A non-empty aligned input receives no padding block, while empty input becomes one full zero block.

Method 1 does not encode the original data length and therefore cannot be removed unambiguously. CryptoScript consequently performs no M1 unpadding during decryption.

---

## Syntax

Use the canonical, case-sensitive literal `ISO-9797-M1` as the value of `#PAD`:

```text
#PAD:ISO-9797-M1
```

Non-canonical spellings are not registered padding values. In particular, `ISO9797M1` is parsed as a general identifier and then rejected as an unknown parameter value by the public CryptoScript path.

## Padding Method 1

ISO/IEC 9797-1:2011, 6.3.2 defines Method 1 in bits. The data is right-padded with the minimum number of `0` bits needed to produce a positive integer number of complete blocks. This number may be zero for a non-empty input that is already block-aligned. Note 2 specifies that an empty data string receives one complete block of `0` bits.

This normative rule belongs to the MAC model defined by ISO/IEC 9797-1. CryptoScript applies its byte-oriented form to the supported encryption and MAC operations listed below.

## CryptoScript Padding Rule

For CryptoScript's byte-oriented input, let `B` be the block size in bytes, `L` the input length in bytes, and `P` the number of appended bytes:

```text
P(L,B) =
    B                 if L = 0
    0                 if L > 0 and L mod B = 0
    B - (L mod B)     otherwise
```

CryptoScript appends `P` bytes with the value `0x00`. AES uses `B = 16`; DES3 uses `B = 8`. The result is deterministic.

## Partial, Aligned, and Empty Input

For partial input, CryptoScript appends only the zero bytes needed to reach the next block boundary. With an 8-byte DES3 block:

```text
Input:   01 02 03 04 05
Padding: 00 00 00
Result:  01 02 03 04 05 00 00 00
```

For aligned, non-empty input, `P = 0`: no padding byte and no additional block is appended.

```text
Input:   01 02 03 04 05 06 07 08
Padding: none
Result:  01 02 03 04 05 06 07 08
```

Empty input is the special case required to produce a positive number of blocks. AES produces sixteen `0x00` bytes; DES3 produces eight:

```text
Input:   empty
Padding: 00 00 00 00 00 00 00 00
Result:  00 00 00 00 00 00 00 00
```

## Decryption and Missing Length Information

Method 1 contains neither a length value nor a distinct padding marker. After decryption, it is therefore impossible to determine which trailing zero bytes belonged to the original data and which were appended by M1.

CryptoScript deliberately does not guess this boundary. For `ISO-9797-M1`, Decrypt performs no padding removal, removes no trailing zero bytes, and returns the complete decrypted block-aligned value. This is a consequence of Method 1 not encoding the original length, not an additional loss introduced by CryptoScript unpadding.

## Trailing-Zero Ambiguity

For an 8-byte DES3 block, each of these original inputs:

```text
01 02
01 02 00
01 02 00 00
```

can produce the same padded value:

```text
01 02 00 00 00 00 00 00
```

The padded value does not reveal which original length was used. CryptoScript preserves the complete value instead of treating any suffix of zero bytes as removable padding.

## Relationship to ISO-9797-M2

M1 appends only `00` bytes when padding is needed. It adds no block to aligned non-empty data, represents empty data as one zero block, and has no unambiguous padding boundary.

M2 begins its padding with `80` and follows it with `00` bytes. It always adds at least one padding byte, adds a complete block to aligned input, and represents empty input as `80` followed by zero bytes. The marker gives CryptoScript an unambiguous boundary that can be validated and removed. CryptoScript's byte-oriented `ISO-7816` representation is byte-for-byte identical to M2, not to M1.

`ISO-9797-M3` also zero-pads its data portion, but additionally prepends a complete block containing the original message length in bits. M1 has no such length block.

## Supported Mechanisms

`ISO-9797-M1` is selectable and effective for:

- AES-CBC encryption and decryption
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with `DES3-CBC`
- DES3-RETAIL MAC calculation

DES3-RETAIL accepts only `ISO-9797-M1` and `ISO-9797-M2`; its default is M2. Its M1 path uses the same 8-byte M1 padding logic, including one zero block for empty input. DES3-CBC-MAC likewise applies M1 before MAC calculation; no unpadding concept is needed because a MAC operation does not return plaintext.

M1 is not effective external padding for AES-ECB, AES-CMAC, DES3-CMAC, AES-CTR, AES-GCM, AES-CCM, or AES-GMAC. Depending on the mechanism, `#PAD` is overridden or ignored and the mechanism uses no external padding or its own internal final-block processing. `ISO-9797-M1` is not the default for any CryptoScript mechanism.

## Example Usage

### Byte-Level Example

For a partial 8-byte DES3 block:

```text
Input:   01 02 03 04 05
Padding: 00 00 00
Result:  01 02 03 04 05 00 00 00
```

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:ISO-9797-M1)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting `ciphertext` is `0x(ACD3C7AE20D0D53E7D22A92B13181A9F)`. Decrypt returns the complete padded plaintext, `0x(01020300000000000000000000000000)`, rather than claiming that `decrypted` equals the original three-byte input. The same rule preserves a genuine trailing `0x00`, but its position cannot be distinguished from subsequently appended M1 zero bytes without separate length information.

## Historical and Source Note

ISO/IEC 9797-1:2011, *Information technology — Security techniques — Message Authentication Codes (MACs) — Part 1: Mechanisms using a block cipher*, defines Padding Method 1 in Section 6.3.2 as part of its MAC model. CryptoScript exposes the verified byte-oriented padding contract more broadly through `#PAD:ISO-9797-M1` for the supported encryption and MAC paths listed above. This does not mean that ISO/IEC 9797-1 generally specifies padding for AES-CBC, DES3-CBC, or DES3-ECB encryption.

ISO/IEC 9797-1:2011/Amd 1:2023 exists and is listed as a published amendment in the official ISO catalogue. Its normative amendment text was not available locally for this documentation. The concrete Method 1 rule on this page is therefore attributed only to the locally verified 2011 edition; no claim is made that the amendment changed or left that rule unchanged.

## References

- ISO/IEC 9797-1:2011, Section 6.3.2, *Padding Method 1*
- [ISO catalogue: ISO/IEC 9797-1:2011](https://www.iso.org/standard/50375.html)
- [ISO catalogue: ISO/IEC 9797-1:2011/Amd 1:2023](https://www.iso.org/standard/78748.html)
