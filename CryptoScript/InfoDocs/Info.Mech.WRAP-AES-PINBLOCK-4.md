# WRAP-AES-PINBLOCK-4

ISO 9564 format 4 PIN-block wrapping and unwrapping with AES.

`Wrap(PARAM, KEY, VAR)` accepts a PIN stored as a hexadecimal `VAR`, for example `0x(01234)`, and returns a 16-byte encrypted PIN block as a hexadecimal `VAR`. The PIN must contain 4 to 12 decimal nibbles. Leading zeros and odd PIN lengths are preserved; quoted strings are not valid PIN inputs.

`Unwrap(PARAM, KEY, VAR)` accepts a 16-byte encrypted PIN block and returns the recovered PIN as a hexadecimal `VAR`.

The key must contain 16, 24 or 32 bytes of AES key material. Format 4 uses two AES-ECB operations without padding or an IV. The encoded PIN field is encrypted, XORed with the encoded PAN field, and encrypted again. Unwrap applies the inverse construction and validates the recovered PIN field.

`#PAN` is required and contains the complete 10-to-19-digit decimal PAN, including its check digit, encoded as a hexadecimal value.

`#RANDOM` is optional for Wrap and must contain exactly 16 hexadecimal nibbles. When omitted, eight bytes are generated with a cryptographically secure random-number generator. After successful Wrap, the value actually used is stored in the original PARAM variable. Unwrap extracts the random field and overwrites any existing `#RANDOM` value in the original PARAM variable.

The PARAM variable is mutated only after all validation, field processing and AES operations have completed successfully. `#MECH` and `#PAN` remain unchanged.

A wrong PAN can produce an invalid recovered PIN field, but is not guaranteed to be detected when the resulting field happens to be structurally valid.
