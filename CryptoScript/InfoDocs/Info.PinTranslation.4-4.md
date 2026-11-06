# PIN translation 4 to 4

Source format: 4. Target format: 4. **PCI classification: Allowed.**

Translation is permitted subject to the full PAN and parameter contracts.

PCI PIN Security Requirements and Testing Procedures, Version 3.1, Requirement 3-3. See [PCI Information Supplement: Implementing ISO Format 4 PIN Blocks, September 2021, Figure 3.5, page 10](https://www.pcisecuritystandards.org/documents/Implementing_ISO_Format_4_PIN_Blocks_Information_Supplement.pdf).

Source: [ISO format 4 mechanism](cryptoscript-info://mechanism/WRAP-AES-PINBLOCK-4), two-stage AES; AES KEY of 16, 24 or 32 bytes; 16-byte block.

Target: [ISO format 4 mechanism](cryptoscript-info://mechanism/WRAP-AES-PINBLOCK-4), two-stage AES; AES KEY of 16, 24 or 32 bytes; 16-byte block.

Source PAN: required. Target PAN: required. PAN values use 10 to 19 decimal HEX nibbles, including the check digit, preserving leading zeros. The complete source and target PANs must be identical, checked before decryption. No PAN-change exceptions are implemented.

Required PARAM variables: distinct `sourceParams` with `#MECH:WRAP-AES-PINBLOCK-4` and `targetParams` with `#MECH:WRAP-AES-PINBLOCK-4`. Both KEYs and the encrypted input VAR are genuine references. Source output field: #RANDOM. Target variable field: #RANDOM. Source fields are extracted and overwrite stale values; target fields are explicit or securely generated independently. Both PARAMs commit only after complete success.

Expected errors: forbidden transition, missing/invalid or mismatched PAN, unsupported mechanism, incorrect argument types/count or shared PARAM identity, invalid key encoding/size, malformed encrypted PIN block, and invalid or unused target parameters. All errors block the result and leave both PARAMs unchanged. Key metadata/parity/weakness analysis warnings remain non-blocking.

## Executable deterministic example

```cryptoscript
PARAM sourceParams=#MECH:WRAP-AES-PINBLOCK-4 #PAN:0x(1234567890123456) #RANDOM:0x(0011223344556677)
PARAM targetParams=#MECH:WRAP-AES-PINBLOCK-4 #PAN:0x(1234567890123456) #RANDOM:0x(0011223344556677)
KEY sourceKey=0x(000102030405060708090A0B0C0D0E0F)
KEY targetKey=0x(000102030405060708090A0B0C0D0E0F)
VAR encryptedPinBlock=0x(6685B016B0CEC63CB7E0F2663C9551EC)
VAR result=PinBlockTranslate(sourceParams,sourceKey,encryptedPinBlock,targetParams,targetKey)
Print(result)
Print(sourceParams)
Print(targetParams)
```

Expected result: `0x(6685B016B0CEC63CB7E0F2663C9551EC)`, representing the same five-digit PIN (including its leading zero). These deterministic vectors are checked by the translation tests against independent cryptographic calculations.

After success, source #RANDOM and target #RANDOM contain their actual values: TRANSACTION `0x(012345678)`, FILL `0x(ABCDEFABC)`, RANDOM `0x(0011223344556677)` where applicable; formats 0 and 2 have no variable output field. Remove the applicable target field to request fresh secure randomness; the result then varies. Explicit fields are for reproducible tests and carry no freshness guarantee.

[Back to translation matrix](cryptoscript-info://overview/pintranslations). InfoView also provides its existing Back action.
