# Parameters

List of all parameters used in CRYPTO-SCRIPT. A parameter is defined by a '#' followed by the parameter name and a colon: '#'PARAMETER_NAME ':' PARAMETER_VALUE

---   
    
- MECH: Mechanism parameter specifying the cryptographic algorithm used. Information about each mechanism can be obtained through Info(mechanism).
    - MECHANISM VALUES:
        - RSA-PSS
        - RSA-OAEP
        - ECDSA
        - AES-CBC
        - AES-CTR
        - AES-GCM
        - AES-ECB
        - AES-CBC
        - AES-CTR
        - AES-CMAC
        - AES-CCM
        - AES-GMAC
        - DES3-ECB
        - DES3-CBC
        - DES3-RETAIL
        - DES3-CMAC
        - WRAP-AES-TR31
        - WRAP-DES3-TR31
        - WRAP-AES
        - WRAP-DES3
- IV: Initialization vector for symmetric encryption.
- PAD: Padding scheme to be used in symmetric encryption.
    - PAD VALUES:
        - PKCS-7
        - ANSI-X923
        - ISO-7816
        - ISO-9797
        - NONE
- MACLEN: Output length in bytes for DES3-RETAIL; valid values are 4 through 8 (default 8).
- NONCE: Unique nonce value for symmetric encryption used in certain modes.
- COUNTER: Counter value for symmetric encryption for certain modes.
- ADATA: Additional authenticated data used in certain modes.
- BLKH: TR-31 Key Block Header used by Wrap. The header is authenticated, and its declared total length must match the resulting key-block structure.
- BIND: Binding parameter for mechanisms that explicitly consume it. For WRAP-AES-TR31 and WRAP-DES3-TR31, the TR-31 header version selects the binding method; BIND does not select Version A, B, C or D.
    - BIND VALUES:
        - BIND-XOR : Key derivation in TR31 with XOR operation and predifined constant value.
        - BIND-CMAC: Key derivation in TR31 with symmetric encryption in CMAC mode.
- RND: Optional random bytes used by TR-31 Wrap for key-length obfuscation and cipher-block padding. The required length depends on the wrapped-key algorithm and size; an omitted or wrong-sized value is replaced with generated random filler.
