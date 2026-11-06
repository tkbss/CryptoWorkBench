# MECHANISM AES-CBC 

AES-CBC is a mode of operation for symmetric key cryptographic block ciphers with the AES algorithm. It uses a secret key and an initialization vector (IV) to encrypt data in fixed-size blocks. The CBC mode provides confidentiality by XORing each plaintext block with the previous ciphertext block before encryption.   

---

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits size) and supports key sizes of 128, 192, or 256 bits.
- **Chaining**: Each block of plaintext is XORed with the previous ciphertext block, creating a dependency between blocks.
- **IV Usage**: An initialization vector (IV) is used to ensure that identical plaintext blocks produce different ciphertext blocks.

### Key Generation and Import

GenerateKey(AES-CBC, ...) supports random generation and explicit hexadecimal import. The registry fixes the resulting KeyType to Secret(Aes) for both variants. Valid key lengths: 128, 192 or 256 bits (16, 24 or 32 bytes). Imported bytes are preserved exactly. The mechanism selects the key algorithm and does not restrict the resulting KEY to its original mode or HMAC digest; other compatible mechanisms of the same family may consume it.

## Functions
The following functions are available for AES-CBC mechanism:   


| Functions  | Parameters        | Input                               | Output         | Key Length       |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | AES-CBC, Key Length                 | KEY variable   | 128/192/256 bits |
| Parameters | N/A               | #MECH, #IV, #PAD                    | PARAM variable | N/A              |
| Encrypt    | #MECH, #IV, #PAD  | parameters, key, data               | VAR cyphertext | 128/192/256 bits |
| Decrypt    | #MECH, #IV, #PAD  | parameters, key, encrypted data     | VAR data       | 128/192/256 bits |
| Wrap	     | #MECH, #IV, #PAD  | parameters, wrapper key, key to wrap | VAR encrypted key   | 128/192/256 bits |
| Unwrap     | #MECH, #IV, #PAD  | parameters, wrapper key, wrapped key  | KEY key      | 128/192/256 bits |

## Parameters
These parameters are used with the AES-CBC mechanism:
- **#MECH**: Selects AES-CBC processing through PARAM. A KEY generated or imported with another AES mechanism remains compatible; its original mode is not a restriction.
- **#IV**: Initialization vector used for encryption and decryption. In case of AES-CBC, it should be 16 bytes (128 bits) long.
- **#PAD**: Padding scheme to ensure input data is a multiple of the block size.
    - NONE: No padding if input must is a multiple of block size.
    - PKCS-7: Default padding scheme for AES-CBC. Any other padding scheme defined in CRYPTO-SCRIPT can be used.

### Binary file encryption

`PATH encrypted = Encrypt(p, k, source)` selects file processing when `source`
is a declared PATH. It returns the absolute name of a completed `.cwe` container.
The source and existing outputs remain unchanged; repeated calls use `.1.cwe`,
`.2.cwe`, etc., choosing the first free number. All nine CryptoScript paddings are supported
for files; NONE requires a nonzero multiple of 16 bytes. A supplied #IV is preserved;
an automatically generated IV is refreshed for every file without mutating PARAM.
The actual IV is stored in the versioned header. CBC provides confidentiality only:
the header and ciphertext have **no integrity protection or authentication**.
See [Encrypt](cryptoscript-info://function/Encrypt) for contracts and error behavior.

### Binary file decryption

`PATH cleartext = Decrypt(p, k, encrypted)` streams CryptoWorkBenchEncryptedFile
1.0 containers with AES-128/192/256-CBC and all nine paddings. The header supplies IV
and padding; explicit PARAM values must agree, while generated/default values are
ignored. PARAM is unchanged. A final `.cwe` is removed from the output name;
existing outputs cause the first free numeric suffix to be used. The container
is preserved. Invalid containers, padding, cancellation and I/O errors publish no
partial plaintext. CBC cannot authenticate the header or ciphertext, and a wrong
key with NONE can silently produce incorrect plaintext. See
[Decrypt](cryptoscript-info://function/Decrypt) for names, contracts and filesystem
security boundaries.
    
---
### File processing (F05)

Encrypt/Decrypt with a declared PATH stream this mechanism using CWE 1.0.
See [Encrypt](cryptoscript-info://function/Encrypt) and
[Decrypt](cryptoscript-info://function/Decrypt) for the file contract.
This mechanism does not authenticate file contents or metadata. It is provided
for reference/legacy analysis, not as a preferred modern file encryption choice.

## Example Usage  
### AES-CBC Encryption and Decryption
KEY k0         = GenerateKey(AES-CBC, 256)  
PARAM p0       = Parameters(#MECH:AES-CBC, #IV:0x(00112233445566778899AABBCCDDEEFF), #PAD:PKCS-7)
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext) 

---
