# PADDING ISO-9797-M3

`ISO-9797-M3` is the canonical CryptoScript identifier for ISO/IEC 9797-1 Padding Method 3. Its data area is padded with zero bits in the same way as Method 1, but M3 additionally prepends a complete block containing the original message length in bits. This explicit length lets CryptoScript reconstruct genuine trailing zero bytes and distinguishes M3 fundamentally from M1 and M2.

---

## Syntax

Use the canonical, case-sensitive literal `ISO-9797-M3` as the value of `#PAD`:

```text
#PAD:ISO-9797-M3
```

The non-canonical spelling `ISO9797M3` is parsed as a general identifier and then rejected semantically with `Unknown parameter value : ISO9797M3`.

## Padding Method 3

ISO/IEC 9797-1:2011, 6.3.4 defines Method 3 in bits. The original data is right-padded with the minimum number of `0` bits needed to form a positive integer number of complete `n`-bit data blocks. A complete `n`-bit length block is then placed to the left of that padded data.

The length block contains the original, unpadded data length in bits as a binary number. The number is left-padded with zero bits to fill the block, and the rightmost bit is its least significant bit. The resulting order is:

```text
length block || zero-padded data
```

The length block is not appended after the data.

## Length Block

For an `n`-bit block cipher, the length block is also exactly `n` bits wide. At byte level, the bit length is represented right-aligned as an unsigned big-endian value within that complete block.

For DES3, `n = 64` and the length block is 8 bytes. Five input bytes have a length of 40 bits (`0x28`), represented as:

```text
00 00 00 00 00 00 00 28
```

For AES, `n = 128` and the complete length block is 16 bytes. CryptoScript represents the practically required value in the rightmost 64 bits and requires the leftmost eight bytes to be zero during unpadding. Five input bytes are represented as:

```text
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 28
```

This is the normative right-aligned, zero-extended 128-bit representation for values in CryptoScript's supported range; it is not a claim that CryptoScript accepts arbitrary 128-bit length values. Byte arrays and output-size limits impose much smaller practical bounds.

## CryptoScript Padding Rule

Let `L` be the original data length in bytes and `B` the block size in bytes. The data area length is:

```text
D(L,B) =
    B                  if L = 0
    B * ceil(L / B)    if L > 0
```

CryptoScript copies the original data into that area and fills the remaining bytes with `0x00`. The total M3 length includes the additional length block:

```text
T(L,B) = B + D(L,B)
```

Therefore, empty input has length `2B`; aligned non-empty input has length `B + L`; and partial input has length `B + B * ceil(L / B)`.

## Partial, Aligned, and Empty Input

Partial input receives only enough `0x00` bytes in the data area to reach the next block boundary. Aligned, non-empty input receives no additional data block, but still receives the complete length block before its data.

Empty input receives a zero length block followed by one complete zero data block:

```text
zero length block || full zero data block
```

For DES3 this is two 8-byte zero blocks, for a total of 16 bytes. For AES it is two 16-byte zero blocks, for a total of 32 bytes. The additional length block is present in every case.

## Unpadding and Validation

CryptoScript requires an M3 value to contain at least two complete, block-aligned blocks. It reads the first block as the length block; for block sizes greater than 8 bytes, the bytes before the rightmost eight must all be zero. The rightmost 64 bits are read as an unsigned big-endian bit length and must describe a whole number of bytes.

From the declared byte length, CryptoScript derives the only canonical number of data blocks. The complete input must have exactly that structure, the declared length must fit in the data area, and every byte after the declared message must be `0x00`. After validation, CryptoScript removes the length block and zero padding and returns exactly the declared number of original bytes.

Rejected structures include:

- fewer than two blocks or input that is not block-aligned;
- a bit length that is not divisible by eight;
- a declared length greater than the available data capacity;
- extra, non-canonical data blocks;
- a non-zero byte in the zero-padding area;
- for AES, a non-zero byte in the left half of the length block.

A smaller declared length followed exclusively by the corresponding canonical zero padding can instead be a valid encoding of that shorter message. These checks are structural format and padding validation only; they do not prove authenticity, integrity, or freedom from manipulation.

## Trailing Zero Bytes

M3 stores the original length explicitly, so genuine trailing zero bytes remain data rather than being confused with padding. The public CryptoScript encryption and decryption path preserves both of these verified values exactly:

```text
0x(010200)   -> Encrypt -> Decrypt -> 0x(010200)
0x(01020000) -> Encrypt -> Decrypt -> 0x(01020000)
```

This is a central difference from M1, which has no stored original length.

## Relationship to ISO-9797-M1

M1 and the M3 data area both use `00` bytes to reach a block boundary. Neither adds a data block to aligned non-empty input, and each represents an empty data area as one zero block.

M1 has no length block, so trailing zeros are ambiguous and CryptoScript performs no M1 unpadding. M3 always prepends a complete length block, uses two zero blocks for empty input, and can return exactly the declared original bytes during unpadding.

## Relationship to ISO-9797-M2

M2 uses `80 00 ...` padding and no length block. Its `0x80` marker identifies the padding boundary, and aligned data receives a complete padding block; empty input becomes one M2 padding block.

M3 instead uses `length block || zero-padded data`. Its boundary comes from the explicit original bit length, aligned data receives no additional data block, and empty input becomes two zero blocks.

## Supported Mechanisms

`ISO-9797-M3` is selectable and effective for:

- AES-CBC encryption and decryption
- [AES-CBC-MAC](cryptoscript-info://mechanism/AES-CBC-MAC) calculation
- DES3-CBC encryption and decryption
- DES3-ECB encryption and decryption
- DES3-CBC-MAC through `Mac` with `#MECH:DES3-CBC`

For DES3-CBC-MAC, the complete 8-byte-block M3 value—length block followed by the M1-padded data area—is processed with the zero IV. Empty, partial, and aligned messages are supported. A MAC operation does not unpad data, and the default of the DES3-CBC parameter set remains `PKCS-7`, not M3.

For AES-CBC-MAC, the complete M3 value is prepared with 16-byte blocks before MAC calculation. The first block contains the original bit length as an unsigned, right-aligned big-endian value: its upper eight bytes are zero and its lower eight bytes contain the UInt64 bit length. The original data follows and is extended according to M1 with zero bytes to a positive number of complete blocks. Empty input therefore becomes a 16-byte zero length block followed by a 16-byte zero data block, for 32 zero bytes in total.

M3 binds the original message length into the prepared input, but AES-CBC-MAC with M3 does not become CMAC and does not acquire the general security guarantees of AES-CMAC. See the [AES-CBC-MAC mechanism page](cryptoscript-info://mechanism/AES-CBC-MAC) for the security boundary.

DES3-RETAIL explicitly rejects M3. It accepts only `ISO-9797-M1` and `ISO-9797-M2` and defaults to M2.

M3 is not effective external padding for AES-ECB, AES-CMAC, DES3-CMAC, AES-CTR, AES-GCM, AES-CCM, or AES-GMAC. Depending on the mechanism, `PAD` is overridden with `NONE`, the stored value is not applied, or CMAC uses its own internal final-block processing. M3 is not the default for any CryptoScript mechanism.

## Example Usage

### Byte-Level Example

For five input bytes and an 8-byte DES3 block:

```text
Input:               01 02 03 04 05
Original bit length: 40 = 0x28
Length block:        00 00 00 00 00 00 00 28
Padded data:         01 02 03 04 05 00 00 00
Complete value:      00 00 00 00 00 00 00 28
                     01 02 03 04 05 00 00 00
```

### CryptoScript Example

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:ISO-9797-M3)
VAR input = 0x(010203)
VAR ciphertext = Encrypt(p,k,input)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting `ciphertext` is `0x(162A3722741DD4C363DD19595518B73603BD3721388CA93069B7F22322B360F5)`, and `decrypted` is `0x(010203)`.

## Historical and Source Note

ISO/IEC 9797-1:2011, *Information technology - Security techniques - Message Authentication Codes (MACs) - Part 1: Mechanisms using a block cipher*, defines Padding Method 3 in Section 6.3.4 as part of its MAC model. CryptoScript applies the same verified format contract additionally on the supported encryption and decryption paths listed above. This does not mean that ISO/IEC 9797-1 generally specifies padding for AES-CBC, DES3-CBC, or DES3-ECB encryption.

ISO/IEC 9797-1:2011/Amd 1:2023 exists and is listed as a published amendment in the official ISO catalogue. Its normative amendment text was not available locally for this documentation. The concrete M3 rule on this page is therefore attributed only to the locally verified 2011 edition; no claim is made that the amendment changed or left that rule unchanged.

## References

- ISO/IEC 9797-1:2011, Section 6.3.4, *Padding Method 3*
- [ISO catalogue: ISO/IEC 9797-1:2011](https://www.iso.org/standard/50375.html)
- [ISO catalogue: ISO/IEC 9797-1:2011/Amd 1:2023](https://www.iso.org/standard/78748.html)
