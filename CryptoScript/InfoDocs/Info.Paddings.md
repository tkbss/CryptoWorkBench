# Padding List

List of all padding schemas supported by CRYPTO-SCRIPT. A padding is defined in the parameter ***PAD*** .  
### Padding Schemes Overview

| Padding Scheme | Byte Pattern (conceptual) | Standard / Reference | Self-removing | Adds block if aligned | Typical usage |
|----------------|---------------------------|----------------------|--------------|----------------------|---------------|
| ISO-9797-M1 | DATA 00 ... 00 | ISO/IEC 9797-1 | No | No | MAC (CBC-MAC variants) |
| ISO-9797-M2 | DATA 80 00 ... 00 | ISO/IEC 9797-1 | Yes | Yes | MAC, financial protocols |
| ISO-9797-M3 | DATA 00 ... 00 LEN64 | ISO/IEC 9797-1 | Yes | Yes | MAC with length binding |
| ISO-7816 | DATA 80 00 ... 00 | ISO/IEC 7816-4 | Yes | Yes | Smartcards, EMV |
| PKCS-7 | DATA NN NN ... NN | RFC 5652 (CMS) | Yes | Yes | Encryption (CBC) |
| ANSI-X923 | DATA 00 ... 00 NN | ANSI X9.23 | Yes | Yes | Encryption (CBC) |
| TLS-CBC | DATA PP PP ... PP | RFC 5246 §6.2.3.2 | Yes | Yes | TLS record encryption |
| NONE | DATA | – | – | – | Stream / AEAD modes |


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
> * **CryptoScript usage**: Additional 0x0s are automatically added by CryptoScript when using parameter PAD:ISO-9797-M1 if the length of the plain text is not a multiple of the block size. Decryption with parameter ***#PAD:ISO-9797-M1*** cannot remove padding, because the padding is not self-describing.    
> * **Interoperability**: Due to its ambiguity, ISO-9797-M1 should only be used when the receiver can reliably determine the original message length.
--- 

  **ISO-9797-M2:  ISO/IEC 9797-1 Padding Method 2 (ISO-9797-M2, 0X80 PADDING)**   

> * **Standard / Reference**: ISO/IEC 9797-1 (MAC algorithms) -Padding method 2.
> * **Mechanism**: Is equivalent to ISO/IEC 7816-4 padding. Append a single byte ***0X80***. Then append ***0X00*** bytes until the message length becomes a multiple of the block size. Example (block size 8 bytes): DATA || 80 00 00 00 00 00 00 00.
> * **Alignment**: Even if the input length is already an exact multiple of the block size, an additional full block is added.
> * **Remove padding**: ISO-9797-M2 is **self-describing** (Self-removing). Scan backwards for the first ***0x80*** byte. All following bytes up to the block boundary must be ***0x00***. 
> * **Typical usage**: Widely used for **MAC padding** (e.g., retail MAC, CBC-MAC variants). Also suitable for **encryption**, especially in financial and smartcard contexts.
> * **Modes/ algorithms**: Applicable to block-based constructions such as CBC-MAC and block cipher modes requiring full blocks (e.g. **AES-CBC**). Not required for streaming modes (CTR, CFB, OFB).
> * **CryptoScript usage**: Adding and removing padding is done automatically by CryptoScript when using parameter PAD:ISO-9797-M2.
> * **Interoperability**: Preferred over M1 when reversible padding is required. Interoperable with ISO/IEC 7816-4 padding definitions.
---
  **ISO-9797-M3:  ISO/IEC 9797-1 Padding Method 3 (ISO-9797-M3, LENGTH BLOCK PADDING)**
> * **Standard / Reference**: ISO/IEC 9797-1 (MAC algorithms) – Padding method 3.
> * **Mechanism**: Append a **64-bit (8-byte) representation of the message length in bits**. The length field is encoded as an **unsigned integer in big-endian format**. If required, zero bytes are inserted before the length field so that the final result is a multiple of the block size. Conceptually: DATA || 00 ... 00 || [LENGTH (64-bit, big-endian)]
> * **Alignment**: Even if the input length is already an exact multiple of the block size, an additional full block is added.
> * **Remove padding**: The final 8 bytes explicitly encode the original message length (in bits). This makes the padding fully deterministic and reversible. 
> * **Typical usage**: Designed primarily for **MAC constructions** under ISO/IEC 9797-1. Rarely used for encryption in practice. Useful when the exact message length must be cryptographically bound into the MAC calculation.
> * **Modes/ algorithms**: Applicable to block-based MAC constructions such as CBC-MAC variants, Retail MAC. Can theoretically be used with block cipher modes like **AES-CBC**, but it is not common for general encryption purposes. Not required for streaming modes (CTR, CFB, OFB).
> * **CryptoScript usage**: Adding and removing padding is done automatically by CryptoScript when using parameter PAD:ISO-9797-M3.
> * **Interoperability**: M3 provides stronger structural binding than M1 and M2 because the exact message length is included in the processed data. Common in legacy financial systems that require strict ISO 9797 compliance.
---  
**ISO-7816:  ISO/IEC 7816-4 Padding (0x80 PADDING)**

> * **Standard / Reference**: ISO/IEC 7816-4 (often referenced as “ISO 7816-4 padding” / “0x80..00 padding”).
> * **Mechanism**: Append a single byte ***0x80***. Then append ***0x00*** bytes until the message length becomes a multiple of the block size.
> * **Alignment**: Always adds padding. Even if the input is already block-aligned, an additional full block is added (because at least the ***0x80*** byte must be appended).
> * **Remove padding**: **Self-describing (self-removing)**. Scan backwards for the first ***0x80***; all bytes after it up to the block boundary must be ***0x00***.
> * **Typical usage**: Common in smartcard / EMV-style systems and legacy financial protocols. Used for **MAC** (incl. EMV-style padding) and can also be used for **encryption** when reversible padding is required.
> * **Modes/ algorithms**: Applicable to block-based modes requiring full blocks (e.g. **AES-CBC**, **DES3-CBC**) and MAC constructions that process full blocks. Not required for streaming modes (CTR, CFB, OFB).
> * **CryptoScript usage**: Adding and removing padding is done automatically by CryptoScript when using parameter PAD:ISO-7816.
> * **Interoperability**: Often treated as equivalent to ISO-9797-M2 in padding behavior (0x80 followed by zeros).

---  
**PKCS-7:  PKCS#7 Padding (RFC / PKCS family)**

> * **Standard / Reference**: RFC 5652 – Cryptographic Message Syntax (CMS) Section 6.3 (Content-encryption process).
> * **Mechanism**: Let ***N*** be the number of padding bytes needed (1..blockSize). Append ***N*** bytes, each containing the value ***0xN***. Example (AES block size 16, 4 bytes padding): DATA || 04 04 04 04
> * **Alignment**: **Always adds padding**. If the input is already block-aligned, a full block of padding is appended (e.g., for AES block size 16: 0x10 repeated 16 times).
> * **Remove padding**: **Self-describing (self-removing)**. The last byte defines ***N***; validate that the last ***N*** bytes are all equal to ***N***, then remove them.
> * **Typical usage**: Very common for **encryption** with CBC (classic “PKCS#7 + CBC”). Not typically used for ISO 9797 MAC padding schemes.
> * **Modes/ algorithms**: Used with block cipher modes requiring full blocks (e.g. **AES-CBC**, **DES3-CBC**). Not required for streaming modes (CTR, CFB, OFB).
> * **CryptoScript usage**: Adding and removing padding is done automatically by CryptoScript when using parameter PAD:PKCS-7.
> * **Interoperability**: Widely supported across crypto libraries and protocols for CBC encryption.

---  
**ANSI-X923:  ANSI X9.23 Padding (a.k.a. “X9.23 padding”)**

> * **Standard / Reference**: ANSI X9.23 – Financial Institution Encryption of Wholesale Financial Messages (ANSI X9, withdrawn).
> * **Mechanism**: Let ***N*** be the number of padding bytes needed (1..blockSize). Append ***N-1*** bytes of ***0x00***, then append a final byte ***0xN*** (the padding length). Example: DATA || 00 00 00 04
> * **Alignment**: **Always adds padding**. If the input is already block-aligned, a full block is appended: 00 ... 00 || 0xN (where N = block size).
> * **Remove padding**: **Self-describing (self-removing)**. Read last byte as ***N***; validate that the preceding ***N-1*** bytes are ***0x00***, then remove ***N*** bytes.
> * **Typical usage**: Mostly used for **encryption** (CBC) in some legacy or interoperability scenarios. Rare for ISO 9797 MAC padding.
> * **Modes/ algorithms**: Used with block cipher modes requiring full blocks (e.g. **AES-CBC**, **DES3-CBC**). Not required for streaming modes (CTR, CFB, OFB).
> * **CryptoScript usage**: Adding and removing padding is done automatically by CryptoScript when using parameter PAD:ANSI-X923.
> * **Interoperability**: Commonly supported by major crypto libraries (sometimes named “X923”). Similar to PKCS#7 but uses zero bytes except for the last length byte.

---
**TLS-CBC:  TLS CBC Record Padding**

> * **Standard / Reference**: TLS record-layer CBC padding (RFC 5246, Section 6.2.3.2 – CBC Block Cipher).
> * **Mechanism**: Append padding bytes so that the record becomes block-aligned. The padding may be any length up to 255 bytes. The last byte is the ***padding_length***. In TLS CBC padding, the padding bytes (including the last byte) have the same value (the padding length). Example (conceptual): ... || PP PP ... PP || where each PP equals padLen.
> * **Alignment**: **Always adds at least 1 padding byte** (because the padding_length byte is always present). If already aligned, at least one byte is still appended.
> * **Remove padding**: **Self-describing (self-removing)**. The last byte indicates how many padding bytes are present; validate the padding pattern, then remove.
> * **Typical usage**: **TLS-specific encryption framing** for CBC-based TLS ciphersuites (historically “MAC-then-encrypt” TLS record construction). Not a general-purpose padding choice outside TLS interoperability.
> * **Modes/ algorithms**: Intended for **CBC** modes as used in TLS record encryption (e.g. AES-CBC / 3DES-CBC in TLS).
> * **CryptoScript usage**: Adding and removing padding is done automatically by CryptoScript when using parameter PAD:TLS-CBC.
> * **Interoperability**: Use only when emulating / interoperating with TLS CBC record formatting. This is what distinguishes TLS-CBC somewhat from classic PKCS#7. TLS allows variable padding lengths (even more than the minimum required). 

---  
**NONE:  No padding**

> * **Standard / Reference**: No padding applied.
> * **Mechanism**: No bytes are appended.
> * **Alignment**: For block-based modes that require full blocks (e.g. **AES-CBC**, **DES3-CBC**, ECB), the input must already be a multiple of the block size, otherwise encryption/MAC processing cannot be performed without additional rules.
> * **Remove padding**: Not applicable.
> * **Typical usage**: Used when the message is already block-aligned, or when using modes that do not require padding (streaming / AEAD contexts).
> * **Modes/ algorithms**: **OK without padding** for streaming modes (**CTR**, CFB, OFB) and typically for AEAD modes (**GCM**, **CCM**) which operate on arbitrary-length plaintext (record format still has its own rules). **Requires block-aligned input** for ECB/CBC style block modes.
> * **CryptoScript usage**: CryptoScript does not add or remove any padding when using parameter PAD:NONE. If a block mode requires alignment and the plaintext is not block-aligned, CryptoScript should raise a semantic error.
> * **Interoperability**: Suitable when the protocol defines its own framing/length or already provides block-aligned data.

---  


     








