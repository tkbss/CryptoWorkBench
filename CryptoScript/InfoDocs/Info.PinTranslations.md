# PCI PIN-block translations

`Info(pintranslations)` opens this matrix. `PinBlockTranslate(sourceParams, sourceKey, encryptedPinBlock, targetParams, targetKey)` decrypts and validates internally, builds the target format and returns only the encrypted target block as VAR.

| Position | Argument | Contract |
|---|---|---|
| 1 | sourceParams | Genuine source PARAM reference |
| 2 | sourceKey | Genuine source KEY reference |
| 3 | encryptedPinBlock | Genuine encrypted-block VAR reference |
| 4 | targetParams | Genuine target PARAM reference, distinct from sourceParams |
| 5 | targetKey | Genuine target KEY reference; may equal sourceKey if technically compatible |

Allowed means permitted subject to all contracts. ICC-only means exclusively offline IC-card PIN verification or PIN change, including EMV Offline-PIN. Forbidden means a blocking `PinBlockTranslationForbidden` error before decryption. Format 2 is not a general online PIN-block format; 2 to 2 remains ICC-only. CryptoScript cannot verify the external ICC use and does not implement the EMV card protocol or a context parameter.

| From / To | 0 | 1 | 2 | 3 | 4 |
|---|---|---|---|---|---|
| 0 | [Allowed](cryptoscript-info://pintranslation/0/0) | [Forbidden](cryptoscript-info://pintranslation/0/1) | [ICC-only](cryptoscript-info://pintranslation/0/2) | [Allowed](cryptoscript-info://pintranslation/0/3) | [Allowed](cryptoscript-info://pintranslation/0/4) |
| 1 | [Allowed](cryptoscript-info://pintranslation/1/0) | [Allowed](cryptoscript-info://pintranslation/1/1) | [ICC-only](cryptoscript-info://pintranslation/1/2) | [Allowed](cryptoscript-info://pintranslation/1/3) | [Allowed](cryptoscript-info://pintranslation/1/4) |
| 2 | [Forbidden](cryptoscript-info://pintranslation/2/0) | [Forbidden](cryptoscript-info://pintranslation/2/1) | [ICC-only](cryptoscript-info://pintranslation/2/2) | [Forbidden](cryptoscript-info://pintranslation/2/3) | [Forbidden](cryptoscript-info://pintranslation/2/4) |
| 3 | [Allowed](cryptoscript-info://pintranslation/3/0) | [Forbidden](cryptoscript-info://pintranslation/3/1) | [ICC-only](cryptoscript-info://pintranslation/3/2) | [Allowed](cryptoscript-info://pintranslation/3/3) | [Allowed](cryptoscript-info://pintranslation/3/4) |
| 4 | [Allowed](cryptoscript-info://pintranslation/4/0) | [Forbidden](cryptoscript-info://pintranslation/4/1) | [ICC-only](cryptoscript-info://pintranslation/4/2) | [Allowed](cryptoscript-info://pintranslation/4/3) | [Allowed](cryptoscript-info://pintranslation/4/4) |

There are 13 Allowed, 5 ICC-only and 7 Forbidden transitions.

Formats 0, 3 and 4 require a complete PAN of 10 to 19 decimal HEX nibbles including the check digit. Leading zeros remain significant. If both formats use PAN, the entire PAN must match, even for 0 to 0, 3 to 3 and 4 to 4. A mismatch is rejected before decryption; comparing only the twelve selected ISO format-0 digits is insufficient. Formats 1 and 2 reject unused PAN parameters. G02 does not implement issuance or cryptographically bound PAN-tokenization exceptions and therefore supports a stricter subset of PCI rules.

Source output fields are extracted regardless of stale stored values: format 1 TRANSACTION, format 3 FILL, format 4 RANDOM. Target fields are explicit or independently generated with a cryptographically secure random-number generator; source fields are never implicitly reused. Both original PARAMs are updated only after full success. Any failure leaves both unchanged and creates no result VAR. Unsupported parameters are rejected. Wrap and Unwrap remain separate functions.

Formats 0 to 3 use TDEA-ECB with 16/24-byte keys and 8-byte blocks. Format 4 uses the existing two-stage AES construction with 16/24/32-byte keys and 16-byte blocks. Declarative KeyTypeConflict, KeyUsageConflict, DesParityViolation and WeakDes3Key diagnostics remain non-blocking; technical key validation remains blocking.

CryptoScript is a development, test and analysis environment, not an HSM/SCD. These rules do not imply PCI certification. Clear PINs and keys exist in ordinary process memory. Mutable intermediate byte buffers are cleared where possible; complete erasure of managed strings or provider-internal buffers is not guaranteed. Structural validation does not authenticate a PIN block.

Use InfoView's existing Back action to return after following a detail link.

PCI PIN Security Requirements and Testing Procedures, Version 3.1, Requirement 3-3. See [PCI Information Supplement: Implementing ISO Format 4 PIN Blocks, September 2021, Figure 3.5, page 10](https://www.pcisecuritystandards.org/documents/Implementing_ISO_Format_4_PIN_Blocks_Information_Supplement.pdf).
