# SECRET

SECRET contains a descriptor (Mechanism, Parts, MinParts, Field, ReductionPolynomial, SecretPosition and KeySizeBits), an immutable collection of SecretShare objects (X, Value), PreferredShareFormat, and optional independent original key metadata. Shares are not KEY variables. Empty collections are representable by the C# model when length is known; the script importer needs one share to infer length. Containers may be incomplete. Construct script variables with [Share](cryptoscript-info://function/Share) or [Secret](cryptoscript-info://function/Secret), then [Restore](cryptoscript-info://function/Restore).

Ordinary display uses the label SECRET. Explicit `Print(s)` emits every full payload, index and reconstruction parameter through the existing PrintEvent and GUI output path. This intentionally discloses sensitive material. To copy the final two statements back into an editor, remove their `out: ` display prefix; choose fresh variable identifiers if needed. External import does not retain key attributes or usage metadata.

For `s = Secret(p, 0x(01ABB9), 0x(03A9BD))`, with Shamir Parts=5 and MinParts=2, the complete X-Y output is:

```text
out: Mechanism: SECRET_SHAMIR
out: Polynomial: SHAMIR-GF256-11B
out: ShareFormat: X-Y
out: Parts: 5
out: MinParts: 2
out: AvailableShares: 2
out: KeySizeBits: 16
out: Share 1: 0x(ABB9)
out: Share 3: 0x(A9BD)
out: PARAM importedParameters = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2, #POLYNOM: "SHAMIR-GF256-11B", #SHAREFORMAT: "X-Y")
out: SECRET importedSecret = Secret(importedParameters, 0x(01ABB9), 0x(03A9BD))
```

The complete Y-X output for the same shares is:

```text
out: Mechanism: SECRET_SHAMIR
out: Polynomial: SHAMIR-GF256-11B
out: ShareFormat: Y-X
out: Parts: 5
out: MinParts: 2
out: AvailableShares: 2
out: KeySizeBits: 16
out: Share 1: 0x(ABB9)
out: Share 3: 0x(A9BD)
out: PARAM importedParameters = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2, #POLYNOM: "SHAMIR-GF256-11B", #SHAREFORMAT: "Y-X")
out: SECRET importedSecret = Secret(importedParameters, 0x(ABB901), 0x(A9BD03))
```

The complete SEPARATE-X output is:

```text
out: Mechanism: SECRET_SHAMIR
out: Polynomial: SHAMIR-GF256-11B
out: ShareFormat: SEPARATE-X
out: Parts: 5
out: MinParts: 2
out: AvailableShares: 2
out: KeySizeBits: 16
out: Share 1: 0x(ABB9)
out: Share 3: 0x(A9BD)
out: PARAM importedParameters = Parameters(SECRET_SHAMIR, #PARTS: 5, #MINPARTS: 2, #POLYNOM: "SHAMIR-GF256-11B", #SHAREFORMAT: "SEPARATE-X")
out: SECRET importedSecret = Secret(importedParameters, 1, 0x(ABB9), 3, 0x(A9BD))
```

XOR prints Polynomial as N/A and omits MINPARTS/POLYNOM from the generated Parameters statement. Print is text output, not file persistence or serialization. Inspect the mechanism documents for authentication limits and the scope of the ISO review.
