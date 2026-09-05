# MECHANISM WRAP-AES-TR31

WRAP-AES-TR31 wraps keys in an AES-based TR-31 key block. It derives encryption and authentication keys from the key block protection key, calculates an AES-CMAC over the header and binary key data, and encrypts the key data with AES-CBC using that MAC as the IV.

---

## Key Features

- **Block Cipher**: AES operates on 128-bit (16-byte) blocks. The protection key supports 128, 192 or 256 bits.
- **Key Requirements**: Use declared KEY variables. GenerateKey with AES-CBC can generate or import the AES protection key and the key to wrap. Wrap constructs binary data for 128-, 192- and 256-bit keys.
- **Authentication**: The complete 16-byte CMAC is included in the block. Unwrap verifies it before returning the recovered key.
- **Return Value**: Wrap returns a VAR in the composite format "header"0x(ciphertext)0x(mac). Unwrap accepts this format or a quoted complete TR-31 block and returns a KEY with hexadecimal Value and KeyValue, recovered KeySize and mechanism WRAP-AES-TR31.

## Functions
The following functions are available for WRAP-AES-TR31 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Wrap | #MECH, #BLKH, #RND | parameters, protection key, key to wrap | VAR key block | 128/192/256 bits |
| Unwrap | #MECH | parameters, protection key, wrapped block | KEY variable | 128/192/256 bits for protection key |

## Parameters
These parameters are used with the WRAP-AES-TR31 mechanism:
- **#MECH**: Specifies WRAP-AES-TR31. Declare PARAM directly as shown below; this wrapper does not implement Parameters or GenerateKey.
- **#BLKH**: Header string required by Wrap. Supply the complete header with the appropriate total block length. Wrap does not generate or correct it. The example uses a 16-character header and a 112-character complete block for a 128-bit wrapped key. Unwrap reads the header from the block.
- **#RND**: Optional hexadecimal filler for Wrap: 14 bytes for a 128- or 256-bit key, 6 bytes for a 192-bit key. If omitted or of a different length, the implementation generates filler instead.
- **#IV**: No external IV is used. The calculated 16-byte MAC serves as the AES-CBC IV.
- **#PAD**: No configurable padding. The wrapper builds a two-byte bit-length field, the key bytes and filler, yielding 32 bytes for 128- or 192-bit keys and 48 bytes for 256-bit keys. CBC then runs without padding.
- **#MACLEN**: Does not change the fixed 16-byte MAC. There is no separate #NONCE, #COUNTER or #ADATA processing; the header is included in authentication.
- **Input Length**: Wrap takes keys rather than arbitrary plaintext or empty messages. Unwrap requires a complete block with block-aligned encrypted key data and a valid MAC. It can also recover other key lengths encoded in authenticated key data; Wrap does not provide a general arbitrary-length key-data builder.

---
## Example Usage
### WRAP-AES-TR31 Key Wrapping and Unwrapping
KEY kbpk       = GenerateKey(AES-CBC, 256)  
KEY keyToWrap  = GenerateKey(AES-CBC, 128)  
PARAM p0       = #MECH:WRAP-AES-TR31 #BLKH:"D0112P0AE00E0000" #RND:0x(00112233445566778899AABBCCDD)  
VAR wrappedKey = Wrap(p0, kbpk, keyToWrap)  
PARAM p1       = #MECH:WRAP-AES-TR31  
KEY recovered  = Unwrap(p1, kbpk, wrappedKey)  

---
