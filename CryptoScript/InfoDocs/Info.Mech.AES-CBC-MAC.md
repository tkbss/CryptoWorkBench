# MECHANISM AES-CBC-MAC

AES-CBC-MAC calculates a message authentication code by applying AES in CBC mode with a fixed all-zero initialization vector and returning the leftmost bytes of the final ciphertext block. It supports Mac rather than encryption or decryption.

---

## Key Features

- **Block Cipher**: AES has a 128-bit (16-byte) block size and accepts 128-, 192-, or 256-bit keys.
- **Key Requirements**: GenerateKey accepts 128, 192, or 256 for a new key, or hexadecimal key data of exactly 16, 24, or 32 bytes.
- **Initialization Vector**: CBC-MAC always starts with a 16-byte all-zero block. There is no configurable #IV parameter.
- **Padding**: Padding is applied before CBC-MAC processing. The default is PKCS-7.
- **MAC Length**: #MACLEN selects 8 through 16 leftmost bytes. The default is 16 bytes.
- **Return Value**: Mac returns a VAR containing hexadecimal MAC bytes in 0x(...) format.

## Functions

| Function | Parameters | Input | Output |
|----------|------------|-------|--------|
| GenerateKey | AES-CBC-MAC, key length or hexadecimal key | 128/192/256 bits or 16/24/32 bytes | KEY variable |
| Parameters | #MECH, optional #PAD and #MACLEN | Mechanism parameters | PARAM variable |
| Mac | parameters, key, data | Message bytes | VAR MAC |

Encrypt and Decrypt reject AES-CBC-MAC. Use AES-CBC for encryption and decryption.

## Parameters

- **#MECH**: Must be AES-CBC-MAC.
- **#PAD**: Optional. Supported values are NONE, PKCS-7, ANSI-X923, ISO-7816, ISO-9797-M1, ISO-9797-M2, ISO-9797-M3, and TLS-CBC. The default is PKCS-7. ISO-10126 is rejected because its random filler would make identical inputs produce different MACs.
- **#MACLEN**: Optional quoted integer from "8" through "16". The default is "16". Truncation retains the leftmost, most-significant bytes.
- **#IV**: Not supported. The all-zero IV is fixed by the mechanism.

With #PAD:NONE, the message must be non-empty and its length must be a multiple of 16 bytes. Empty and partial-block messages are rejected.

### MAC Computation

After padding, split the message into 16-byte blocks M1 through Mn. AES-CBC-MAC computes:

```text
C0 = 0^128
Ci = AES_K(Mi XOR C(i-1))
MAC = MSB_MACLEN(Cn)
```

The final operation returns the selected number of leftmost bytes from Cn.

## Example Usage

The following value is derived from the official AES-CBC examples in NIST SP 800-38A by adapting the first input block for an all-zero IV. It is not an official NIST CBC-MAC test vector.

```cryptoscript
KEY k = GenerateKey(AES-CBC-MAC,0x(2B7E151628AED2A6ABF7158809CF4F3C))
PARAM p = Parameters(#MECH:AES-CBC-MAC,#PAD:NONE,#MACLEN:"16")
VAR mac = Mac(p,k,0x(6BC0BCE12A459991E134741A7F9E1925AE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710))
```

Expected result:

```text
0x(3FF1CAA1681FAC09120ECA307586E1A7)
```

### Security Considerations

AES-CBC-MAC is a low-level workbench mechanism. Classic CBC-MAC is not generally secure for variable-length messages, and padding alone does not turn CBC-MAC into CMAC. For new general-purpose applications, AES-CMAC is normally preferred.

The fixed zero IV is part of the CBC-MAC construction and must not be replaced by a random or caller-selected IV. Shorter MAC lengths reduce resistance to guessing and forgery. TLS-CBC padding does not make the result a TLS MAC or implement TLS record processing. ISO-9797-M3 binds the original message length into its padded representation, but it does not turn this mechanism into CMAC.

### References

- NIST FIPS 197, Advanced Encryption Standard (AES): AES block and key sizes.
- NIST SP 800-38A, Recommendation for Block Cipher Modes of Operation: CBC recursion and the AES-CBC material from which the example value is derived.
- NIST SP 800-38B, Recommendation for Block Cipher Modes of Operation: The CMAC Mode for Authentication: CMAC security context and truncation guidance.

---
