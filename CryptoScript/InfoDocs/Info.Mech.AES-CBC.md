# MECHANISM AES-CBC 

AES-CBC is a mode of operation for symmetric key cryptographic block ciphers with the AES algorithm. It uses a secret key and an initialization vector (IV) to encrypt data in fixed-size blocks. The CBC mode provides confidentiality by XORing each plaintext block with the previous ciphertext block before encryption.

## Key Features

- **Block Cipher**: AES operates on fixed-size blocks (128 bits size) and supports key sizes of 128, 192, or 256 bits.
- **Chaining**: Each block of plaintext is XORed with the previous ciphertext block, creating a dependency between blocks.
- **IV Usage**: An initialization vector (IV) is used to ensure that identical plaintext blocks produce different ciphertext blocks.

## Functions
The following functions are available for AES-CBC mechanism:   


| Functions  | Parameters        | Input                               | Output         | Key Length       |
|------------|-------------------|-------------------------------------|----------------|------------------|
| GenerateKey| N/A               | AES-CBC, Key Length                 | KEY variable   | 128/192/256 bits |
| Parameters | N/A               | #MECH, #IV, #PAD                    | PARAM variable | N/A              |
| Encrypt    | #MECH, #IV, #PAD  | parameters, key, data               | VAR cyphertext | 128/192/256 bits |
| Decrypt    | #MECH, #IV, #PAD  | parameters, key, encrypted data     | VAR data       | 128/192/256 bits |
| Wrap	     | #MECH, #IV, #PAD  | parameters, wraper key, key to wrap | VAR Wraped     | 128/192/256 bits |
| Unwrap     | #MECH, #IV, #PAD  | parameters, wraper key, wraped key  | KEY Unwraped   | 128/192/256 bits |

## Parameters
These parameters are used with the AES-CBC mechanism:
- **#MECH**: Specifies the AES-CBC mechanism.
- **#IV**: Initialization vector used for encryption and decryption. In case of AES-CBC, it should be 16 bytes (128 bits) long.
- **#PAD**: Padding scheme to ensure input data is a multiple of the block size.
    - NONE: No padding if input must is a multiple of block size.
    - PKCS7: Default padding scheme for AES-CBC. Any other padding scheme defined in CRYPTO-SCRIPT can be used.
    
## Example Usage  
### AES-CBC Encryption and Decryption
KEY k0         = GenerateKey(AES-CBC, 256)  
PARAM p0       = Parameters(#MECH=AES-CBC, #IV:0x(00112233445566778899AABBCCDDEEFF), #PAD:PKCS7)  
VAR cleartext  = "This is a secret message."  
VAR ciphertext = Encrypt(p0, k0, cleartext)  
VAR decrypted  = Decrypt(p0, k0, ciphertext) 
### AES-CBC Key Wrapping and Unwrapping
KEY k1          = GenerateKey(AES-CBC, 256)  
KEY keyToWrap   = GenerateKey(AES-CBC, 256)  
PARAM p1        = Parameters(#MECH=WRAP-AES, #IV:0x(00112233445566778899AABBCCDDEEFF), #PAD:PKCS7)  
VAR wrappedKey  = Wrap(p1, k1, keyToWrap)  
