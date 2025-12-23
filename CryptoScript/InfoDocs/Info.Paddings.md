# Padding List

List of all padding schemas supported by CRYPTO-SCRIPT. A padding is defined in the parameter ***PAD*** . 


---   

## Padding Schemas   

---  

  **ISO-9797-M1:  ISO/IEC 9797-1 Padding Method 1 (ISO-9797-M1, ZERO PADDING)**   
> * **Standard / Reference**: ISO/IEC 9797-1 (MAC algorithms) -Padding method 1.   
> * **Mechanism**: Append ***0x00*** bytes until the message length becomes a multiple of the block size.   
> * **Alignment**: If the input length is already an exact multiple of the block size, no bytes are appended. The output length remains unchanged.
> * **Remove padding**: Zero padding is ambiguous. It is not possible to distinguish between padding bytes and legitimate trailing ***0x00*** bytes of the original message. Safe removal is only possible if the original length is known out-of-band or the data format guarantees the plaintext never ends with ***0x00***.   
> * **Typical usage**: Primarily used for **MAC padding** in ISO/IEC 9797-1 MAC constructions. Can be used for **encryption**, but only if the original plaintext length is known by other means.
> * **Modes/ algorithms**: Applicable to block-based constructions such as CBC-MAC and block cipher modes requiring full blocks (e.g. **AES-CBC**). Not required for streaming modes (CTR, CFB, OFB).   
> * **CryptoScript usage**: Decryption with parameter ***#PAD:ISO-9797-M1*** cannot remove padding, because the padding is not self-describing. Prefer self-removing paddings such as ISO/IEC 7816-4 or PKCS#7.   
> * **Interoperability**: Due to its ambiguity, ISO-9797-M1 should only be used when the receiver can reliably determine the original message length.
--- 

  **ISO-9797-M2:  ISO/IEC 9797-1 Padding Method 2 (ISO-9797-M2, Bit padding)**   

> * **Standard / Reference**: ISO/IEC 9797-1 (MAC algorithms) -Padding method 2.
     








