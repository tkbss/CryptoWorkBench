# MECHANISM WRAP-DES3-TR31

WRAP-DES3-TR31 creates and reads ANSI X9.143-2022 TR-31 key blocks protected with TDEA. CryptoWorkBench supports Versions A, B and C; the version in the header selects variant or derivation binding.

---

## Key Features

- **TDEA Keys**: The KBPK and wrapped TDEA key can contain 128 or 192 bits (16 or 24 bytes, including parity bits). Technically valid weak or degenerate keys remain executable for analysis, with quality warnings on the KBPK.
- **Key-Length Obfuscation**: TDEA keys shorter than 192 bits are padded to 24 key bytes before padding to the 8-byte TDEA block boundary.
- **Binding by Version**:

| Version | Binding method | Authentication value | Use |
|---------|----------------|----------------------|-----|
| A | TDEA Key Variant Binding | 4 bytes | Deprecated by ANSI X9.143-2022; retained for compatibility |
| B | TDEA Key Derivation Binding using TDEA-CMAC-derived KBEK and KBAK | 8 bytes | Preferred over Version A for new TDEA applications |
| C | TDEA Key Variant Binding | 4 bytes | Same cryptographic binding as A with the newer header and attribute assignments |

- **Authentication**: Unwrap authenticates the header and encrypted key data before returning the key. A changed header, ciphertext, authentication value or KBPK is rejected.
- **Return Value**: Wrap returns a VAR in composite form: `"header"0x(ciphertext)0x(authentication-value)`. Unwrap accepts this form or a quoted complete TR-31 wire block and returns a KEY.

### Semantic diagnostics

Expected KeyType metadata: Tdea on the KBPK. Expected KeyUsage: `WrapKey for Wrap; UnwrapKey for Unwrap`.
Declarative conflicts emit non-blocking `KeyTypeConflict` then `KeyUsageConflict` warnings where a fixed expected algorithm exists. Correct metadata emits no conflict warning; Unknown KeyType and Unspecified usage impose no diagnostic restriction. These checks do not mutate KEY or PARAM and work without a diagnostic sink. These usages apply to the protection key only. The transported KEY needs neither WrapKey nor UnwrapKey. Structured calls preserve original KEY identity; legacy identifiers or unambiguous projected values are accepted, while ambiguous values fail. Unwrap never mutates the input VAR. Header algorithm and key usage are authenticated protocol fields, not freely replaceable metadata. Version, header integrity, KBEK/KBAK, lengths, obfuscation and authentication remain unchanged. DES quality warnings are reported on the original KBPK. The explicitly enabled analysis operation also processes internal KBEK/KBAK with their actual effective bits; internal keys do not generate additional warnings.

Malformed key bytes, unsupported key lengths, invalid parameters and mandatory protocol or authentication checks remain blocking. Warnings do not establish PCI or EP2 conformity; the EP2 security matrix remains open.

## Functions
The following functions are available for WRAP-DES3-TR31 mechanism:

| Functions | Parameters | Input | Output | Key Length |
|------------|-------------------|-------------------------------------|----------------|------------------|
| Wrap | #MECH, #BLKH, #RND | parameters, TDEA KBPK, TDEA key to wrap | VAR key block | 128/192 bits |
| Unwrap | #MECH | parameters, TDEA KBPK, wrapped block | KEY variable | 128/192 bits |

## Parameters
These parameters are used with the WRAP-DES3-TR31 mechanism:
- **#MECH**: Set to `WRAP-DES3-TR31` in a directly declared PARAM.
- **#BLKH**: Required by Wrap. Supply a complete Version A, B or C header, including Optional Blocks when present. The header is authenticated and its declared total length must describe the resulting wire block.
- **#RND**: Optional hexadecimal filler in wire order: key-length obfuscation first, then TDEA block padding. A 128-bit wrapped TDEA key uses 8 bytes of obfuscation and 6 bytes of block padding, for 14 bytes total. A 192-bit key uses no obfuscation and 6 bytes of block padding, for 6 bytes total. If omitted or supplied with another length, CryptoWorkBench generates the complete filler with a cryptographically secure random generator.
- **Input and Output**: Wrap accepts Versions A, B and C only. No external IV or configurable padding is used by this mechanism.

---
## Example Usage
### Version A Wrap — Derived Reference from ANSI Inputs
KEY kbpkA      = GenerateKey(DES3-CBC, 0x(89E88CF7931444F334BD7547FC3F380C))
KEY keyA       = GenerateKey(DES3-CBC, 0x(F039121BEC83D26B169BDCD5B22AAF8F))
PARAM parametersA = #MECH:WRAP-DES3-TR31 #BLKH:"A0088P0TE00E0000" #RND:0x(249F30A2B39A7D6B720DF563BB07)
VAR wrappedA   = Wrap(parametersA, kbpkA, keyA)
KEY recoveredA = Unwrap(parametersA, kbpkA, wrappedA)
### Version B Wrap — ANSI X9.143-2022 Reference
KEY kbpkB      = GenerateKey(DES3-CBC, 0x(DD7515F2BFC17F85CE48F3CA25CB21F6))
KEY keyB       = GenerateKey(DES3-CBC, 0x(3F419E1CB7079442AA37474C2EFBF8B8))
PARAM parametersB = #MECH:WRAP-DES3-TR31 #BLKH:"B0096P0TE00E0000" #RND:0x(7CB920D261E9F3AA1C2965473CE2)
VAR wrappedB   = Wrap(parametersB, kbpkB, keyB)
KEY recoveredB = Unwrap(parametersB, kbpkB, wrappedB)
### Version C Wrap — Corrected ANSI Reference
KEY kbpkC      = GenerateKey(DES3-CBC, 0x(B8ED59E0A279A295E9F5ED7944FD06B9))
KEY keyC       = GenerateKey(DES3-CBC, 0x(EDB380DD340BC2620247D445F5B8D678))
PARAM parametersC = #MECH:WRAP-DES3-TR31 #BLKH:"C0112B0TX12S0100KS1800604B120F9292800000" #RND:0x(7CB920D261E9F3AA8546A8ED98D1)
VAR wrappedC   = Wrap(parametersC, kbpkC, keyC)
KEY recoveredC = Unwrap(parametersC, kbpkC, wrappedC)
### Version B Unwrap — Complete Wire Block
VAR wireBlockB = "B0096P0TE00E0000D7ED9E189BC6F715125B265B149DF8FE218A396785608923D6197378386A3759308FC49A2AA891BA"
PARAM unwrapParametersB = #MECH:WRAP-DES3-TR31
KEY recoveredWireB = Unwrap(unwrapParametersB, kbpkB, wireBlockB)

---
### TDEA test and analysis keys

`DesParityViolation` (10) and `WeakDes3Key` (11) are non-blocking Warning diagnostics. Weak or semi-weak DES components and effective K1=K2 or K2=K3 are reported independently of parity; K1=K3 alone is not single-DES degeneration. Each quality category is reported at most once per operation on the original KEY, after any KeyTypeConflict and KeyUsageConflict metadata warnings. No KEY bytes or parity bits are changed. Technically valid calls also work without a diagnostic sink.

This support is for test and analysis and makes no suitability claim for production payment systems. Invalid key lengths, malformed data and protocol errors remain blocking. ARQC verification and TR-31 authentication remain mandatory; failed verification stops execution without a successful result. AES behavior and the approved algorithm selectors remain unchanged.
