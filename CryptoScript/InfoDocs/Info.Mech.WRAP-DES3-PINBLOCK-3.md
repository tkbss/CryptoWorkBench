# WRAP-DES3-PINBLOCK-3

ISO 9564 format 3 PIN-block wrapping and unwrapping with TDEA.

`Wrap(PARAM, KEY, VAR)` accepts a 4-to-12-digit PIN stored as a hexadecimal nibble sequence such as `0x(0123)` and returns an 8-byte encrypted PIN block as `VAR`. Only decimal PIN nibbles `0` through `9` are permitted. Leading zeros and odd PIN lengths are preserved. Quoted strings are not valid PIN inputs. `Unwrap(PARAM, KEY, VAR)` accepts an 8-byte encrypted PIN block and returns the PIN as a hexadecimal `VAR`.

The key must contain 16 or 24 bytes of usable TDEA key material. DES3-ECB processes exactly one block without padding or an IV.

`#PAN` is required and contains the complete 8-to-19-digit decimal PAN, including its check digit, encoded as a hexadecimal value. The check digit is excluded when constructing the format 3 PAN field.

`#FILL` is optional for Wrap and must contain exactly `14 - PIN length` nibbles in the range `A` through `F`. When omitted, independent, uniformly distributed fill nibbles are generated with a cryptographically secure random-number generator. After successful Wrap, the value actually used is stored in the original PARAM variable.

Unwrap validates the format identifier, PIN length, decimal PIN digits, and fill range, then stores the extracted fill field in the original PARAM variable. An existing `#FILL` value is overwritten only after successful processing. A wrong PAN can sometimes produce structurally plausible clear data and is therefore not guaranteed to be detected.
