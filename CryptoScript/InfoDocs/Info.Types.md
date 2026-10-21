# Types

List of all types used in CRYPTO-SCRIPT. Upper case lettersfor every type is mandatory.

---

## Built-in Types
- **VAR**   : Variable type used to store data such as messages, signatures, ciphertexts, etc.
    - FORMATS: A VAR type can have the following formats:
        - **STRING** : Text format for storing plain text data: "TEXT"
        - **HEX**    : Hexadecimal format for storing binary data as hex strings: 0x(123456789abcdef...)
        - **BASE64** : Base64 format for storing binary data as base64 encoded strings: b64(asdf...)
        - **TR31**   : TR31 string format for encoding a TR31 block. It is a combination of STRING and HEX: "TEXT"0x(1234...)
- **KEY**: Cryptographic key material with `KeyType` (algorithm/material kind),
  `KeySizeInBits`, usage policy, derivation mechanism and optional key attributes.
  AES and TDEA secret keys expose the calculated properties `Kcv`, `Kcvc`, `Kcvk`.
  See the KEY check values below and `Info(functions)` for `Print` and `Check`.
- **TR31H**: Validated, structured TR-31 header definition from [CreateHeader](cryptoscript-info://function/CreateHeader).
  Pass a variable reference as `#BLKH:header`. Wrap calculates the total length without changing the definition.
  [All header attributes](cryptoscript-info://overview/tr31attributes) are documented separately.
- **PARAM** : Parameter type used to store cryptographic parameters needed for cryptographic algorithms.
    - STRUCTURE: A PARAM type can have the following structure:
        - **#NAME:** : Defines the parameter with a unique identifier. E.g., #MECH:
        - **VALUE**  : Contains the value of the parameter. E.g., AES.
    - NUMBER OF PARAMETERS: A PARAM type can contain multiple parameters, each defined by a #NAME and VALUE pair.
        - EXAMPLE: PARAM p = #MECH:AES-CTR #NONCE:0x(00112233445566778899AABB) #COUNTER:0x(00000000) #PAD:PKCS-7 #IV:0x(12345678)
- **PATH**  : Path type used to specify file paths for loading and saving data. The PATH structure depends on the operating system.
    - EXAMPLE (Windows): "C:\folder\subfolder\file.txt"
    - EXAMPLE (Linux): "/home/user/folder/file.txt"

## KEY literals

```cryptoscript
KEY k = 0x(00112233445566778899AABBCCDDEEFF)
KEY same = b64(ABEiM0RVZneImaq7zN3u/w==)
PARAM p = Parameters(AES-CBC)
VAR data = 0x(000102030405060708090A0B0C0D0E0F)
VAR encrypted = Encrypt(p, k, data)
VAR decrypted = Decrypt(p, k, encrypted)
```

Nonempty whole-byte HEX and valid Base64 literals create complete KEY objects.
The original literal and format are preserved. `KeySizeInBits` and `KeySize`
describe all decoded bytes (128 bits in this example), including leading zeros.
The defaults are `Secret(Unknown)`, `Usage.Unspecified`, empty derivation
mechanism and empty key attributes. Length does not select an algorithm.
String, numeric, path and malformed binary initializers are rejected before a
variable is published or an existing variable is replaced. VAR declarations and
GenerateKey retain their own behavior; GenerateKey supplies algorithm metadata.

A key without declared algorithm can be used where the existing mechanism
contract supplies the algorithm and the material is technically valid. For
example, AES accepts 16, 24 or 32 bytes. Encryption/decryption does not assign an
algorithm or change KEY metadata. An Unknown key has no KCV, KCVC or KCVK
(`null` in the runtime, `N/A` in Print), even after AES use.

[Save](cryptoscript-info://function/Save) exports these keys as KEY snapshots
without inferring an algorithm. The snapshot roundtrip preserves their material,
format, size and Unknown metadata. Snapshots contain unencrypted key material.

## KEY check values

Let `K` be the complete key and `B` the cipher block size in bytes:

| Algorithm | Key bytes | B | Result bytes (each method) |
|---|---|---|---|
| AES-128 / AES-192 / AES-256 | 16 / 24 / 32 | 16 | 16 |
| TDEA double / triple length | 16 / 24 | 8 | 8 |

- `KCV = ECB_Encrypt(K, 0^B)`.
- `KCVC = CMAC(K, 0^B)`: exactly one all-zero block, **not an empty message**.
- `KCVK = ECB_Encrypt(K, K[0..B])`: encrypt the first block of key bytes with
  the **complete key**. No IV or padding is used for either ECB calculation;
  AES-192 needs no special padding.

All three values are full HEX blocks, with no automatic truncation to 3 or 5 bytes.
They are calculated from current material and algorithm metadata on access, including
imported, generated, derived, unwrapped and deserialized keys. They are not independent
JSON fields. HMAC, Unknown and asymmetric key types have no applicable values (`N/A`).
Generic KDF outputs with Unknown algorithm remain Unknown; length does not imply AES
or TDEA. Calculation does not modify key material, parity, attributes or usage policy.
Weak TDEA analysis keys remain calculable; existing quality warnings still apply.

### Standards context

[PCI PIN Security Requirements and Testing Procedures v3.1, March 2021, Requirement 15-1](https://www.pcisecuritystandards.org/documents/PCI_PIN_Security_Requirements_Testing_v3_1.pdf)
permits the zero-block encryption method for TDEA with a leading result of at most
24 bits (3 bytes). AES must use CMAC; CMAC is permitted for AES and TDEA with a
leading result of at most 40 bits (5 bytes). Thus AES `KCV` is not a PCI KCV method;
`KCVK` is a legacy method outside these permitted methods. The full raw values
displayed here are **not PCI-conforming output KCVs** and do not establish PCI
compliance of CryptoScript Workbench. The user selects any prefix used for analysis.

`KCVK` follows the self-encryption concept of XFS4IoT KeyManagement `kcvSelf`;
`KCV` corresponds to its `kcvZero` concept. Reference edition:
[XFS4IoT Specification, CWA 17852, release v2.0](https://www.cencenelec.eu/media/CEN-CENELEC/AreasOfWork/CEN%20sectors/Digital%20Society/CWA%20Download%20Area/XFS/CWA17852/cwa-17852-xfs4iot-specification-release-v2-0.pdf).
The AES first-16-byte extension is an explicit Workbench legacy-system requirement,
not a claim that XFS4IoT or PCI specifies that extension.

## SECRET

[SECRET](cryptoscript-info://type/SECRET) holds immutable key shares, reconstruction parameters, preferred share format and optional original key metadata. Share/Secret construct it; Restore returns a KEY. Explicit Print displays all shares.
