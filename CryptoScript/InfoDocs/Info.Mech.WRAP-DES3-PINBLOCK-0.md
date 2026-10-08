# WRAP-DES3-PINBLOCK-0

ISO 9564 format 0 PIN-block wrapping and unwrapping with TDEA.

`Wrap(PARAM, KEY, VAR)` accepts a 4-to-12-digit PIN stored as a hexadecimal nibble sequence such as `0x(0123)` and returns an 8-byte encrypted PIN block as `VAR`. Only decimal PIN nibbles `0` through `9` are permitted. Leading zeros and odd PIN lengths are preserved. Quoted strings are not valid PIN inputs. `Unwrap(PARAM, KEY, VAR)` accepts an 8-byte encrypted PIN block and returns the PIN as a hexadecimal `VAR`.

The key must contain 16 or 24 bytes of usable TDEA key material. DES3-ECB processes exactly one block without padding or an IV.

`#PAN` is required and contains the complete 8-to-19-digit decimal PAN, including its check digit, encoded as a hexadecimal value. The check digit is excluded when constructing the format 0 PAN field.

Unwrap validates the format identifier, PIN length, decimal PIN digits, and fixed `F` fill field. A wrong PAN can sometimes produce structurally plausible clear data and is therefore not guaranteed to be detected.
