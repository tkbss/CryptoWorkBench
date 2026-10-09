# WRAP-DES3-PINBLOCK-2

ISO 9564 format 2 PIN-block wrapping and unwrapping with TDEA.

`Wrap(PARAM, KEY, VAR)` accepts a 4-to-12-digit PIN stored as a hexadecimal nibble sequence such as `0x(0123)` and returns an 8-byte encrypted PIN block as `VAR`. Only decimal PIN nibbles `0` through `9` are permitted. Leading zeros and odd PIN lengths are preserved. Quoted strings are not valid PIN inputs. `Unwrap(PARAM, KEY, VAR)` accepts an 8-byte encrypted PIN block and returns the PIN as a hexadecimal `VAR`.

The key must contain 16 or 24 bytes of usable TDEA key material. DES3-ECB processes exactly one block without padding or an IV.

Unwrap validates the format identifier, PIN length, decimal PIN digits, and fixed `F` fill field.
Structural validation is not authentication and provides no authenticity guarantee for a PIN block.

Format 2 is intended for offline PIN verification or PIN change in an ICC context and must not be used for online PIN verification. CryptoScript does not model the complete EMV protocol context and therefore cannot determine whether an external use is offline or online. The user or integrating system is responsible for using this mechanism only in a permitted context; no artificial runtime restriction is imposed.

## Security and lifecycle

CryptoScript processes cleartext PINs and key material in ordinary process memory. It is neither a secure cryptographic device (SCD) nor a hardware security module (HSM). These PIN-block functions are exclusively for development, analysis, interoperability testing, and education; they are not a substitute for standards-compliant production PIN processing. Processing real cardholder PINs in production requires appropriate secure infrastructure and compliance with all applicable requirements. No PCI compliance or certification is implied.

TDEA/DES3 is a legacy algorithm. This mechanism is retained for analysis and interoperability with existing systems. New systems should evaluate a current, approved AES-based solution, including format 4 where it is suitable after assessment of the complete payment infrastructure; format 4 is not automatically suitable for every existing environment.
