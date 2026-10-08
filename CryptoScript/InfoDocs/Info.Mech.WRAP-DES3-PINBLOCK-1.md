# WRAP-DES3-PINBLOCK-1

ISO 9564 format 1 PIN-block wrapping and unwrapping with TDEA.

`Wrap(PARAM, KEY, VAR)` accepts a 4-to-12-digit PIN stored as a hexadecimal nibble sequence such as `0x(0123)` and returns an 8-byte encrypted PIN block as `VAR`. Only decimal PIN nibbles `0` through `9` are permitted. Leading zeros and odd PIN lengths are preserved. Quoted strings are not valid PIN inputs. `Unwrap(PARAM, KEY, VAR)` accepts an 8-byte encrypted PIN block and returns the PIN as a hexadecimal `VAR`.

The key must contain 16 or 24 bytes of usable TDEA key material. DES3-ECB processes exactly one block without padding or an IV.

`#TRANSACTION` is optional for Wrap and must contain exactly `14 - PIN length` hexadecimal nibbles. When omitted, a cryptographically secure field is generated. After successful Wrap, the value actually used is stored in the original PARAM variable.

Unwrap validates the format identifier, PIN length, and decimal PIN digits, extracts the complete transaction field, and stores it in the original PARAM variable. An existing `#TRANSACTION` value is overwritten only after successful processing.
