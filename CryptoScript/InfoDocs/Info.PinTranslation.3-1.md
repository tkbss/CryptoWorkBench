# PIN translation 3 to 1

Source format: 3. Target format: 1. **PCI classification: Forbidden.**

The source format must not be translated to this target format. Runtime rejects this combination before decryption with `PinBlockTranslationForbidden`.

PCI PIN Security Requirements and Testing Procedures, Version 3.1, Requirement 3-3. See [PCI Information Supplement: Implementing ISO Format 4 PIN Blocks, September 2021, Figure 3.5, page 10](https://www.pcisecuritystandards.org/documents/Implementing_ISO_Format_4_PIN_Blocks_Information_Supplement.pdf).

Source: [ISO format 3 mechanism](cryptoscript-info://mechanism/WRAP-DES3-PINBLOCK-3), TDEA-ECB; TDEA KEY of 16 or 24 bytes; 8-byte block.

Target: [ISO format 1 mechanism](cryptoscript-info://mechanism/WRAP-DES3-PINBLOCK-1), TDEA-ECB; TDEA KEY of 16 or 24 bytes; 8-byte block.

Source PAN: required. Target PAN: forbidden. PAN values use 10 to 19 decimal HEX nibbles, including the check digit, preserving leading zeros. Supply PAN only on the side whose construction uses it.

Required PARAM variables: distinct `sourceParams` with `#MECH:WRAP-DES3-PINBLOCK-3` and `targetParams` with `#MECH:WRAP-DES3-PINBLOCK-1`. Both KEYs and the encrypted input VAR are genuine references. Source output field: #FILL. Target variable field: #TRANSACTION. Source fields are extracted and overwrite stale values; target fields are explicit or securely generated independently. Both PARAMs commit only after complete success.

Expected errors: forbidden transition, missing/invalid or mismatched PAN, unsupported mechanism, incorrect argument types/count or shared PARAM identity, invalid key encoding/size, malformed encrypted PIN block, and invalid or unused target parameters. All errors block the result and leave both PARAMs unchanged. Key metadata/parity/weakness analysis warnings remain non-blocking.

[Back to translation matrix](cryptoscript-info://overview/pintranslations). InfoView also provides its existing Back action.
