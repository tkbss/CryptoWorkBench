# Mechanism Roadmap

The mechanisms in this document are planned concepts. They are **not implemented** by CryptoScript and are not accepted as productive mechanism values.

---

## ECDSA

**Status: Not implemented.**

Planned purpose: Create and verify digital signatures with the Elliptic Curve Digital Signature Algorithm. A future implementation must define supported curves, key formats, digest selection and signature encoding before the mechanism can be exposed by the language.

## RSA-OAEP

**Status: Not implemented.**

Planned purpose: Encrypt and decrypt data with RSA Optimal Asymmetric Encryption Padding. A future implementation must define supported key sizes, digest and mask-generation parameters, labels, input-size limits and key-import formats.

## RSA-PSS

**Status: Not implemented.**

Planned purpose: Create and verify RSA Probabilistic Signature Scheme signatures. A future implementation must define supported key sizes, digest and mask-generation parameters, salt-length handling and signature verification behavior.

## WRAP-AES

**Status: Not implemented.**

Planned purpose: Wrap and unwrap cryptographic keys with a general AES key-wrapping construction outside the existing TR-31 Version D mechanism. A future implementation must select the exact wrapping standard and define integrity protection, padding, key sizes and input constraints.

## WRAP-DES3

**Status: Not implemented.**

Planned purpose: Wrap and unwrap cryptographic keys with a general Triple-DES construction outside the existing TR-31 Version A, B and C mechanism. A future implementation must select the exact wrapping standard and define integrity protection, padding, key sizes and input constraints.
