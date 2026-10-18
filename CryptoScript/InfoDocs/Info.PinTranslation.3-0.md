# PIN translation 3 to 0

Source format: 3. Target format: 0. **PCI classification: Allowed.**

Translation is permitted subject to the full PAN and parameter contracts.

PCI PIN Security Requirements and Testing Procedures, Version 3.1, Requirement 3-3. See [PCI Information Supplement: Implementing ISO Format 4 PIN Blocks, September 2021, Figure 3.5, page 10](https://www.pcisecuritystandards.org/documents/Implementing_ISO_Format_4_PIN_Blocks_Information_Supplement.pdf).

Source: [ISO format 3 mechanism](cryptoscript-info://mechanism/WRAP-DES3-PINBLOCK-3), TDEA-ECB; TDEA KEY of 16 or 24 bytes; 8-byte block.

Target: [ISO format 0 mechanism](cryptoscript-info://mechanism/WRAP-DES3-PINBLOCK-0), TDEA-ECB; TDEA KEY of 16 or 24 bytes; 8-byte block.

Source PAN: required. Target PAN: required. PAN values use 10 to 19 decimal HEX nibbles, including the check digit, preserving leading zeros. The complete source and target PANs must be identical, checked before decryption. No PAN-change exceptions are implemented.

Required PARAM variables: distinct `sourceParams` with `#MECH:WRAP-DES3-PINBLOCK-3` and `targetParams` with `#MECH:WRAP-DES3-PINBLOCK-0`. Both KEYs and the encrypted input VAR are genuine references. Source output field: #FILL. Target variable field: none. Source fields are extracted and overwrite stale values; target fields are explicit or securely generated independently. Both PARAMs commit only after complete success.

Expected errors: forbidden transition, missing/invalid or mismatched PAN, unsupported mechanism, incorrect argument types/count or shared PARAM identity, invalid key encoding/size, malformed encrypted PIN block, and invalid or unused target parameters. All errors block the result and leave both PARAMs unchanged. Key metadata/parity/weakness analysis warnings remain non-blocking.

## Executable deterministic example

```cryptoscript
PARAM sourceParams=#MECH:WRAP-DES3-PINBLOCK-3 #PAN:0x(1234567890123456) #FILL:0x(ABCDEFABC)
PARAM targetParams=#MECH:WRAP-DES3-PINBLOCK-0 #PAN:0x(1234567890123456)
KEY sourceKey=0x(0123456789ABCDEFFEDCBA9876543210)
KEY targetKey=0x(0123456789ABCDEFFEDCBA9876543210)
VAR encryptedPinBlock=0x(6694CAF82B1C2994)
VAR result=PinBlockTranslate(sourceParams,sourceKey,encryptedPinBlock,targetParams,targetKey)
Print(result)
Print(sourceParams)
Print(targetParams)
```

Expected result: `0x(2D8F0FF09AFDCCD0)`, representing the same five-digit PIN (including its leading zero). These deterministic vectors are checked by the translation tests against independent cryptographic calculations.

After success, source #FILL and target none contain their actual values: TRANSACTION `0x(012345678)`, FILL `0x(ABCDEFABC)`, RANDOM `0x(0011223344556677)` where applicable; formats 0 and 2 have no variable output field. Remove the applicable target field to request fresh secure randomness; the result then varies. Explicit fields are for reproducible tests and carry no freshness guarantee.

[Back to translation matrix](cryptoscript-info://overview/pintranslations). InfoView also provides its existing Back action.
