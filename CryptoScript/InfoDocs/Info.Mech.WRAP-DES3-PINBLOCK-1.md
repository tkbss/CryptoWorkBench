# WRAP-DES3-PINBLOCK-1

ISO 9564 format 1 PIN-block wrapping and unwrapping with TDEA.

`Wrap(PARAM, KEY, VAR)` accepts a 4-to-12-digit PIN stored as a hexadecimal nibble sequence such as `0x(0123)` and returns an 8-byte encrypted PIN block as `VAR`. Only decimal PIN nibbles `0` through `9` are permitted. Leading zeros and odd PIN lengths are preserved. Quoted strings are not valid PIN inputs. `Unwrap(PARAM, KEY, VAR)` accepts an 8-byte encrypted PIN block and returns the PIN as a hexadecimal `VAR`.

The key must contain 16 or 24 bytes of usable TDEA key material. DES3-ECB processes exactly one block without padding or an IV.

`#TRANSACTION` is optional for Wrap and must contain exactly `14 - PIN length` hexadecimal nibbles. When omitted, a fresh field is generated with a cryptographically secure random-number generator. After successful Wrap, the value actually used is stored in the original PARAM variable. An explicit value is accepted only to support reproducible development, analysis, and tests; the caller is responsible for suitable freshness and entropy, and reuse impairs the intended transaction diversification.

Unwrap validates the format identifier, PIN length, and decimal PIN digits, extracts the complete transaction field, and stores it in the original PARAM variable. An existing `#TRANSACTION` value is overwritten only after successful processing.
Structural validation is not authentication and provides no authenticity guarantee for a PIN block.

## Security and lifecycle

CryptoScript processes cleartext PINs and key material in ordinary process memory. It is neither a secure cryptographic device (SCD) nor a hardware security module (HSM). These PIN-block functions are exclusively for development, analysis, interoperability testing, and education; they are not a substitute for standards-compliant production PIN processing. Processing real cardholder PINs in production requires appropriate secure infrastructure and compliance with all applicable requirements. No PCI compliance or certification is implied.

TDEA/DES3 is a legacy algorithm. This mechanism is retained for analysis and interoperability with existing systems. New systems should evaluate a current, approved AES-based solution, including format 4 where it is suitable after assessment of the complete payment infrastructure; format 4 is not automatically suitable for every existing environment.
