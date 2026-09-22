# MECHANISM DUKPT-TDEA-INITIAL-KEY

DUKPT-TDEA-INITIAL-KEY derives the Initial Key for the legacy double-length TDEA DUKPT procedure from a Base Derivation Key (BDK) and a complete Key Serial Number (KSN). The procedure is specified in ANSI X9.24-1:2009 Annex A and retained for compatibility in ANSI X9.24-3:2017 Annex C.

This CryptoScript mechanism implements only:

```text
BDK + KSN -> Initial Key
```

It does not implement transaction key derivation, Transaction Counter advancement, future or intermediate key management, PIN key variants, MAC key variants, data-encryption key variants, device or key-register state management, or TR-31 key distribution.

---

## Key Features

- **Legacy TDEA DUKPT Initial Key**: Implements only the Initial Key derivation used by the legacy TDEA DUKPT procedure.
- **Double-Length TDEA**: The BDK and resulting Initial Key are each exactly 16 bytes / 128 bits. This mechanism does not accept a 24-byte triple-length TDEA BDK.
- **Complete KSN Input**: The caller supplies the complete 10-byte / 80-bit KSN. CryptoScript clears the 21-bit Transaction Counter internally.
- **Two TDEA-ECB Operations**: Each half is produced by encrypting one 8-byte block, without an IV and without padding.
- **Return Value**: `Derive` returns a generic 16-byte `KEY` in hexadecimal `0x(...)` form. `Mechanism` is empty and `DerivationMechanism` is `DUKPT-TDEA-INITIAL-KEY`.

## Functions

| Function | Arguments | Input | Output | Key Length |
|----------|-----------|-------|--------|------------|
| Parameters | mechanism | `DUKPT-TDEA-INITIAL-KEY` | PARAM variable | N/A |
| Derive | PARAM, KEY, DATA | parameters, BDK, complete 80-bit KSN | Generic KEY variable | 16 bytes / 128 bits |

`Derive(PARAM, KEY, DATA)` uses:

| Argument | Meaning |
|----------|---------|
| PARAM | Parameter block containing `#MECH:DUKPT-TDEA-INITIAL-KEY` |
| KEY | Base Derivation Key (BDK) |
| DATA | Complete 80-bit Key Serial Number (KSN) |
| Return value | Derived double-length TDEA Initial Key |

## Parameters

The only required and permitted parameter is:

- **#MECH**: `DUKPT-TDEA-INITIAL-KEY`

No additional mechanism-specific parameters are used. Parameters such as `#OUTLEN`, `#KEYTYPE`, `#PRF` and `#COUNTER` are not accepted.

```text
PARAM dukpt = Parameters(DUKPT-TDEA-INITIAL-KEY)
```

## BDK and Initial Key Requirements

This legacy DUKPT procedure uses double-length TDEA:

| Value | Required length |
|-------|-----------------|
| Base Derivation Key (BDK) | Exactly 16 bytes / 128 bits |
| Initial Key | Exactly 16 bytes / 128 bits |

TDEA as an algorithm is not generally restricted to 128-bit key representations. The 16-byte requirement applies specifically to this legacy double-length TDEA DUKPT procedure. A 24-byte triple-length TDEA BDK is rejected by this mechanism.

## KSN and Transaction Counter

CryptoScript requires the canonical complete KSN:

```text
KSN = exactly 10 bytes / 80 bits
```

The 21 least-significant bits contain the Transaction Counter. Initial Key derivation clears exactly these 21 bits and retains all other KSN bits:

```text
KSN:              FFFF9876543210E00001
Counter cleared:  FFFF9876543210E00000
TDEA input block: FFFF9876543210E0
```

Callers provide the complete KSN and must not clear the Transaction Counter themselves. The counter-cleared KSN is also commonly called the Initial Key Serial Number (IKSN).

The ANSI X9.24 derivation description permits a shorter KSN representation to be left-padded with `FF`. CryptoScript deliberately exposes a stricter API: `DATA` must already contain the canonical complete 10-byte / 80-bit KSN. This is a CryptoScript API decision, not a general limitation of the ANSI procedure.

## Derivation Algorithm

First clear the 21-bit Transaction Counter and select the eight most-significant bytes:

```text
I = MSB8(KSN with the 21-bit Transaction Counter cleared)
```

Calculate the left half with the original BDK:

```text
IKL = TDEA-ECB(BDK, I)
```

For the right half, use the fixed key variant mask:

```text
C0C0C0C000000000C0C0C0C000000000
```

```text
VariantBDK =
BDK XOR C0C0C0C000000000C0C0C0C000000000

IKR = TDEA-ECB(VariantBDK, I)
```

The final Initial Key is:

```text
InitialKey = IKL || IKR
```

Both operations use double-length TDEA in ECB mode. Each operation encrypts exactly one 8-byte block. No IV and no padding are used. This derivation is not SP 800-108 and does not use `KDF-SP800-108-COUNTER`.

## Official Annex Test Vector

The following TDEA DUKPT Initial Key example is provided by the ANSI X9.24 annex material:

| Value | Hexadecimal data |
|-------|------------------|
| BDK | `0123456789ABCDEFFEDCBA9876543210` |
| KSN | `FFFF9876543210E00001` |
| Counter-cleared KSN | `FFFF9876543210E00000` |
| TDEA input block | `FFFF9876543210E0` |
| Variant mask | `C0C0C0C000000000C0C0C0C000000000` |
| Variant BDK | `C1E385A789ABCDEF3E1C7A5876543210` |
| Initial Key | `6AC292FAA1315B4D858AB3A3D7D5933A` |

## Example Usage

```text
KEY bdk = GenerateKey(DES3-ECB, 0x(0123456789ABCDEFFEDCBA9876543210))
PARAM dukpt = Parameters(DUKPT-TDEA-INITIAL-KEY)
KEY initialKey = Derive(dukpt, bdk, 0x(FFFF9876543210E00001))
```

The resulting `initialKey` is:

```text
0x(6AC292FAA1315B4D858AB3A3D7D5933A)
```

## Invalid Inputs

The mechanism rejects:

- BDKs shorter or longer than exactly 16 bytes.
- A 24-byte triple-length TDEA BDK.
- KSNs shorter or longer than exactly 10 bytes.
- Additional parameters, including `#OUTLEN`, `#KEYTYPE`, `#PRF` and `#COUNTER`.

## Comparison with DUKPT-AES-INITIAL-KEY

`DUKPT-TDEA-INITIAL-KEY` implements the legacy double-length TDEA DUKPT Initial Key derivation. It uses the classic 80-bit KSN containing a 21-bit Transaction Counter and derives two 8-byte halves with TDEA-ECB.

`DUKPT-AES-INITIAL-KEY` implements the newer AES-based procedure. It uses a 64-bit Initial Key ID and the AES DUKPT derivation-data structure. The two mechanisms intentionally use different standardized cryptographic derivation algorithms and cannot be substituted for one another.

## Reference

- ANSI X9.24-1:2009, Annex A, especially A.6, Derivation of the Initial Key.
- ANSI X9.24-3:2017, Annex C, legacy TDEA DUKPT compatibility procedure.

---
