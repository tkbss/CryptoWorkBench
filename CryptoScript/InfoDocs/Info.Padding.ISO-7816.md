# PADDING ISO-7816

`ISO-7816` is the canonical CryptoScript identifier for padding that begins with `0x80` and continues with as many `0x00` bytes as are needed to fill the block. The name refers to a byte form whose historical source is the Secure Messaging context of ISO/IEC 7816-4.

---

## Syntax

Use the canonical, case-sensitive literal `ISO-7816` as the value of `#PAD`:

```text
#PAD:ISO-7816
```

Non-canonical spellings are not registered padding values. In particular, `ISO7816` is invalid and is rejected by the public CryptoScript path.

## CryptoScript Padding Rule

For a block size `B` and an input length `L`, both measured in bytes, the number of padding bytes is:

```text
N = B - (L mod B)
```

Therefore, `1 <= N <= B`. CryptoScript appends one byte with the value `0x80` followed by `N - 1` bytes with the value `0x00`. The padding has no length byte; its length is recovered from the marker and the trailing zero bytes.

```text
N = 1: 80
N = 2: 80 00
N = 3: 80 00 00
```

## Full Block and Empty Input

ISO-7816 always adds padding, including when the input is already block-aligned:

- For AES with a 16-byte block, an aligned or empty input receives `0x80` followed by fifteen `0x00` bytes.
- For DES3 with an 8-byte block, an aligned or empty input receives `0x80` followed by seven `0x00` bytes.
- When `N = 1`, the padding consists only of `0x80`.

Thus an aligned input receives one complete additional padding block, and an empty input becomes exactly one padding block.

## Unpadding and Validation

The padded input must be non-empty and block-aligned. CryptoScript examines only the final block and moves backwards from its end over `0x00` bytes. The first non-zero byte reached must be `0x80`. CryptoScript removes that marker and every following zero byte.

Consequently, endings such as `80`, `80 00`, and `80 00 00` are valid. An ending such as `80 00 01` or `80 01 00` is invalid, as is a final block containing only zero bytes or otherwise lacking a suitable marker. A marker outside the final block is insufficient.

If the final block is:

```text
01 02 80 00 80 00 00 00
```

the later `0x80` starts the padding, and unpadding returns:

```text
01 02 80 00
```

The earlier `0x80` remains part of the data. This is a structural padding check, not proof of authenticity or deliberate modification; a wrong key or IV, or damaged ciphertext, can also cause a padding error.

## Relationship to ISO-9797-M2

For CryptoScript's byte-oriented inputs, `ISO-7816` and `ISO-9797-M2` are byte-for-byte identical. Both use `Iso7816Padding` and therefore have the same padding, unpadding, empty-input, and full-block behavior:

```text
80 00 ... 00
```

The public identifiers remain separate because they refer to different normative contexts. CryptoScript's `ISO-7816` name refers to the byte form from the ISO/IEC 7816-4 Secure Messaging context. `ISO-9797-M2` denotes Padding Method 2 from ISO/IEC 9797-1 in its MAC model. Identical bytes do not make the two standards normative synonyms or assign them the same purpose.

## Supported Mechanisms

`ISO-7816` is selectable and effective for:

- AES-CBC encryption and decryption
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with DES3-CBC

It is not the default padding for these mechanisms. AES-CBC, DES3-CBC, and DES3-ECB default to `PKCS-7`.

AES-ECB always uses `NONE`; AES-CMAC and DES3-CMAC use their internal CMAC padding; and AES-CTR, AES-GCM, AES-CCM, and AES-GMAC do not apply effective external ISO-7816 padding. DES3-RETAIL rejects the `ISO-7816` identifier and accepts only `ISO-9797-M1` or `ISO-9797-M2`; byte identity does not make `ISO-7816` an alias there.

## Example Usage

### Byte-Level Example

For a 16-byte AES block:

```text
Input:   00 01 02 03 04 05 06 07 08 09 0A 0B 0C
Padding: 80 00 00
Result:  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 80 00 00
```

With 15 input bytes, `N = 1` and the padding is only `80`.

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:ISO-7816)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting `ciphertext` is `0x(BDCA8D32BBE6732533C7FA16A9F0C3FC)`, and `decrypted` is `0x(010203)`.

## Historical and Source Note

The concrete rule was verified against ISO/IEC 7816-4:2005, 6.2.3.1, *Cryptographic checksum data element*, in the Secure Messaging section. In that context, the padding used for the cryptographic checksum consists of one mandatory byte `0x80`, followed when needed by `0x00` bytes up to the block boundary. CryptoScript exposes the same byte form more generally for the supported block-cipher operations listed above.

ISO/IEC 7816-4:2005 has been withdrawn. The official ISO catalogue identifies ISO/IEC 7816-4:2020, Edition 4, as the current edition, confirmed in 2025, and lists Amendment 1:2023. The complete 2020 text was not available for this documentation, so this page does not claim that the rule or clause number is unchanged in that edition or amendment.

ISO/IEC 9797-1:2011, 6.3.3 defines Padding Method 2 as a single `1` bit followed by the minimum required number of `0` bits to reach a positive number of complete blocks. For byte-aligned CryptoScript inputs, this produces `80 00 ... 00`.

## References

- ISO/IEC 7816-4:2005, *Identification cards — Integrated circuit cards — Part 4: Organization, security and commands for interchange*, 6.2.3.1
- [ISO catalogue: ISO/IEC 7816-4:2020](https://www.iso.org/standard/77180.html)
- ISO/IEC 9797-1:2011, *Information technology — Security techniques — Message Authentication Codes (MACs) — Part 1: Mechanisms using a block cipher*, 6.3.3
