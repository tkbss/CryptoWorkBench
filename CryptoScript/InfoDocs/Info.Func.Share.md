# Share(PARAM, KEY) → SECRET

Shares the complete symmetric key using [SECRET_SPLIT](cryptoscript-info://mechanism/SECRET_SPLIT) or [SECRET_SHAMIR](cryptoscript-info://mechanism/SECRET_SHAMIR). Both arguments are typed variable references. The result contains all Parts shares and an independent descriptor. Known type/usage/derivation/optional attributes are copied without altering the input. Literal KEY declarations with no metadata remain Unknown/Unspecified.

```cryptoscript
KEY k = GenerateKey(AES-ECB, 128)
PARAM p = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 3)
SECRET s = Share(p, k)
KEY r = Restore(s)
```

The default field profile is SHAMIR-GF256-11B. Nonempty whole-byte KEY material in HEX or Base64 form is required; Restore returns HEX. Invalid counts, asymmetric key material, size conflicts and invalid parameter combinations fail. No new key usage restriction is invented for splitting. Existing semantic key diagnostics for other operations remain unchanged. [Print and SECRET](cryptoscript-info://type/SECRET) describe intentional disclosure of all generated shares. See mechanism documents for ISO scope, memory limitations and PCI ceremony requirements.
