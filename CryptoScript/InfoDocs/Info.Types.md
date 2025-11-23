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
- **KEY**   : Key type used to store cryptographic keys. A key type consists out of:  
    - MECHANISM: Cryptographic algorithm assigned to the key.  
    - LENGTH: Length of the key in bits.
- **PARAM** : Parameter type used to store cryptographic parameters needed for cryptographic algorithms.
    - STRUCTURE: A PARAM type can have the following structure:
        - **#NAME:** : Defines the parameter with a unique identifier. E.g., #MECH:
        - **VALUE**  : Contains the value of the parameter. E.g., AES.
    - NUMBER OF PARAMETERS: A PARAM type can contain multiple parameters, each defined by a #NAME and VALUE pair.
        - EXAMPLE: PARAM p = #MECH:AES-CTR #NONCE:0x(00112233445566778899AABB) #COUNTER:0x(00000000) #PAD:PKCS-7 #IV:0x(12345678)
- **PATH**  : Path type used to specify file paths for loading and saving data. The PATH structure depends on the operating system.
    - EXAMPLE (Windows): "C:\folder\subfolder\file.txt"
    - EXAMPLE (Linux): "/home/user/folder/file.txt"