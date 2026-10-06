# PADDING NONE

`NONE` means that CryptoScript does not add or remove padding bytes. It is not a padding scheme defined by an external standard.

---

## Syntax

Use the canonical, case-sensitive literal `NONE` as the value of `#PAD`:

```text
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:NONE)
```

## Behavior

- CryptoScript passes the input to the cryptographic operation without adding padding bytes.
- Decryption does not remove any bytes as padding. All decrypted bytes are retained, including trailing bytes that resemble another padding format.
- For classic block-cipher modes, plaintext and ciphertext have the same length when `NONE` is used.
- Any required block alignment comes from the selected mechanism, not from a padding scheme named `NONE`.

## Input Requirements

The CryptoScript paths where `NONE` is selectable require non-empty, block-aligned input:

| Mechanism or operation | Required input length |
|------------------------|-----------------------|
| AES-CBC | A non-zero multiple of 16 bytes |
| [AES-CBC-MAC](cryptoscript-info://mechanism/AES-CBC-MAC) | A non-zero multiple of 16 bytes |
| DES3-CBC | A non-zero multiple of 8 bytes |
| DES3-ECB | A non-zero multiple of 8 bytes |
| DES3-CBC-MAC through `Mac` with `DES3-CBC` | A non-zero multiple of 8 bytes |

These are requirements of the respective block-cipher operations. `NONE` does not itself prohibit empty input.

## Supported Mechanisms

- **Selectable `NONE`**: AES-CBC, [AES-CBC-MAC](cryptoscript-info://mechanism/AES-CBC-MAC), DES3-CBC, DES3-ECB, and DES3-CBC-MAC through `Mac` with `DES3-CBC`.
- **Fixed or internal no-padding behavior**: AES-ECB always uses no external padding. AES-CMAC and DES3-CMAC handle final blocks internally rather than through selectable external padding.
- **Padding-free modes**: AES-CTR, AES-GCM, AES-CCM, and AES-GMAC do not use this external block-padding operation.
- **Not supported**: DES3-RETAIL accepts only ISO-9797-M1 or ISO-9797-M2 and rejects `NONE`.

With AES-CBC-MAC, `NONE` adds no bytes and the MAC is calculated over exactly the supplied complete 16-byte blocks. The message must contain at least one block. Selecting `NONE` does not make classic CBC-MAC generally secure for variable-length messages; see the [AES-CBC-MAC mechanism page](cryptoscript-info://mechanism/AES-CBC-MAC) for the security boundary.

## Example Usage

This AES-CBC example uses one complete 16-byte plaintext block:

```cryptoscript
KEY k = GenerateKey(AES-CBC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090A0B0C0D0E0F),#PAD:NONE)
VAR cleartext = 0x(6BC1BEE22E409F96E93D7E117393172A)
VAR ciphertext = Encrypt(p,k,cleartext)
VAR decrypted = Decrypt(p,k,ciphertext)
```

The resulting ciphertext is `0x(7649ABAC8119B246CEE98E9B12E9197D)`, and `decrypted` contains the complete original plaintext block. A 15-byte plaintext would be rejected because AES-CBC requires complete 16-byte blocks when no padding is added.

## References

- [NIST SP 800-38A](https://doi.org/10.6028/NIST.SP.800-38A) specifies the general complete-block requirements of CBC and ECB. It does not define a padding scheme named `NONE`.
