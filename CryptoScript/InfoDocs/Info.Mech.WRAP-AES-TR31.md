# MECHANISM WRAP-AES-TR31

WRAP-AES-TR31 creates and reads ANSI X9.143-2022 TR-31 Version D key blocks. Version D uses AES Key Derivation Binding: it derives an encryption key and an authentication key from an AES key block protection key (KBPK), authenticates the header and confidential data with AES-CMAC, and encrypts the confidential data with AES-CBC.

---

## Key Features

- **Version and Binding**: CryptoWorkBench supports Version D with AES Key Derivation Binding. The version in the TR-31 header selects the binding method.
- **KBPK Requirements**: The AES KBPK must contain 128, 192 or 256 bits and can be generated or imported with an AES mechanism such as AES-CBC.
- **AES Key-Length Obfuscation**: An AES key is padded with random key-length-obfuscation bytes to 32 key bytes, followed by random padding to the 16-byte AES block boundary.

| AES key | Key-length obfuscation | Block padding | Total `#RND` | Confidential data / ciphertext |
|---------|------------------------|---------------|--------------|--------------------------------|
| 128 bits | 16 bytes | 14 bytes | 30 bytes | 48 bytes |
| 192 bits | 8 bytes | 14 bytes | 22 bytes | 48 bytes |
| 256 bits | 0 bytes | 14 bytes | 14 bytes | 48 bytes |

- **Wrapped TDEA Keys**: Version D still uses AES Key Derivation Binding and an AES KBPK, but CryptoWorkBench can wrap supported TDEA keys as the contained key.

| TDEA key | Key-length obfuscation | Block padding | Total `#RND` | Confidential data / ciphertext | Wire length without Optional Blocks |
|----------|------------------------|---------------|--------------|--------------------------------|-------------------------------------|
| 128 bits | 8 bytes | 6 bytes | 14 bytes | 32 bytes | 112 characters (`D0112...`) |
| 192 bits | 0 bytes | 6 bytes | 6 bytes | 32 bytes | 112 characters (`D0112...`) |

- **Block Length**: With a 16-character header and no Optional Blocks, the AES key layouts above produce a 144-character key block and require a `D0144...` header. The supported TDEA key layouts produce a structurally correct 112-character Version D block and require `D0112...`. Optional Blocks change the total length.
- **Authentication**: Version D includes the complete 16-byte AES-CMAC authentication value. Unwrap verifies it before returning the key.
- **Compatibility**: Unwrap can still read historical CryptoWorkBench AES-128 `D0112` blocks that omitted the required AES key-length obfuscation; those blocks are not ANSI-conformant. New AES-128 wraps use the `D0144` layout. This legacy case is distinct from the correct `D0112` layout for a contained TDEA-128 or TDEA-192 key described above.
- **Return Value**: Wrap returns a VAR in composite form: `"header"0x(ciphertext)0x(authentication-value)`. Unwrap accepts this form or a quoted complete TR-31 wire block and returns a KEY.

## Functions
The following functions are available for WRAP-AES-TR31 mechanism:

| Functions | Parameters | Input | Output | KBPK Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Wrap | #MECH, #BLKH, #RND | parameters, AES KBPK, key to wrap | VAR key block | 128/192/256 bits |
| Unwrap | #MECH | parameters, AES KBPK, wrapped block | KEY variable | 128/192/256 bits |

## Parameters
These parameters are used with the WRAP-AES-TR31 mechanism:
- **#MECH**: Set to `WRAP-AES-TR31` in a directly declared PARAM.
- **#BLKH**: Required by Wrap. Supply the complete authenticated TR-31 header, including Optional Blocks when present. The four-digit declared total length must equal the generated wire-block length; a contradictory header is rejected rather than rewritten.
- **#RND**: Optional hexadecimal random filler in wire order: key-length obfuscation first, then block padding. For AES-128, AES-192 and AES-256 use exactly 30, 22 and 14 bytes respectively for deterministic output; for TDEA-128 and TDEA-192 use 14 and 6 bytes. If omitted or supplied with another length, CryptoWorkBench generates the complete filler with a cryptographically secure random generator.
- **Input and Output**: Wrap receives declared KEY variables. Unwrap accepts a composite TR-31 value or a quoted complete wire block. No external IV or configurable padding is used; the calculated authentication value is the AES-CBC IV.

---
## Example Usage
### Wrap — ANSI X9.143-2022 Section 8.1 AES-128 Vector
KEY kbpk       = GenerateKey(AES-CBC, 0x(88E1AB2A2E3DD38C1FA039A536500CC8A87AB9D62DC92C01058FA79F44657DE6))
KEY keyToWrap  = GenerateKey(AES-CBC, 0x(3F419E1CB7079442AA37474C2EFBF8B8))
PARAM wrapParameters = #MECH:WRAP-AES-TR31 #BLKH:"D0144P0AE00E0000" #RND:0x(1A87BBFA2CFE78D383E5F4C6AA83473C1C2965473CE206BB855B01533782)
VAR wrappedKey = Wrap(wrapParameters, kbpk, keyToWrap)
### Unwrap — Complete Version D Wire Block
VAR normativeBlock = "D0144P0AE00E00002C77FA3F4A553BED6E88AE5C172A4166E3D4ACA8E2AC71C158A476FAC12C13C3829DE55D3AB54C48F4C4FEF7AC75E90FC47F1B77E7B19A73ED46E64410082557"
PARAM unwrapParameters = #MECH:WRAP-AES-TR31
KEY recovered = Unwrap(unwrapParameters, kbpk, normativeBlock)

---
