# SecureLoad

Imports a password-protected Secure-Container v1 created by [SecureSave](cryptoscript-info://function/SecureSave).

```cryptoscript
PATH vault = C:\Private\snapshot.cssecure
SecureLoad(vault)
SecureLoad(vault, ALL)
SecureLoad(vault, MasterKey, SessionKey)
SecureLoad(vault, MasterKey, #ONCONFLICT:REPLACE)
SecureLoad(vault, ALL, #ONCONFLICT:REJECT)
```

The first argument is an existing PATH variable. Selectors are case-sensitive identifiers stored in the file; they need not exist in the workspace. Omission or ALL selects every root. ALL cannot be combined with other selectors. Duplicate selectors are rejected. The optional #ONCONFLICT occurs once, last. REJECT is the default: any selected existing name aborts the entire import, including a PATH name conflict. Explicit REPLACE replaces complete selected objects and adds missing ones. Unselected variables retain their identity. Replacement is rejected if an unchanged PARAM would retain a reference to a replaced TR31H. Dependencies stay internal and do not become additional roots.

SecureLoad has no return value and cannot be nested or used in an initializer. Hosts must use ExecuteAsync; Execute reports AsyncExecutionRequired. The GUI awaits each Scriptbook line and stops on failure or cancellation. The runner requests a password without confirmation through PasswordRequested; passwords are never script arguments.

The stored, validated KDF chooses Argon2id v1.3 or PBKDF2-HMAC-SHA256 with the original costs. HKDF-SHA256 derives the mode-specific key. AES-256-GCM protects confidentiality and authenticity; AES-256-GMAC authenticates plaintext without hiding it. Root identifiers, types and KEY algorithm, actual bit length and full KCV (when available) are public in both modes. This manifest is untrusted until authentication and is compared against the fully restored snapshot before import.

The path is resolved and the file read once before the dialog. Limits are 96 MiB per envelope, 16 MiB per S01 payload, 1 MiB each for manifest wire/frame, 4096 roots and envelope depth 8. Unknown fields, duplicate fields, malformed Unicode/Base64 and excessive KDF costs are rejected before requesting a password. The complete S01 graph, including unselected roots, is validated after authentication. Source text is restored as data, never executed. Publication is atomic: any failure before commit leaves the workspace unchanged. Cancellation arriving after commit does not undo success.

Errors include InvalidEnvelope, UnsupportedFormat, ResourceLimitExceeded, InvalidSecureSnapshot, InvalidSelection, NameConflict and ReferenceConflict. Wrong passwords and invalid tags share SecureAuthenticationFailed without disclosing the cause. PasswordHostFailure and PasswordHostContractViolation indicate host failures; user cancellation and token cancellation remain distinct. Filesystem errors follow the existing Load policy; there is no plaintext fallback.

Use a trusted directory and ancestors: the existing S02 target-check/open race limitations remain. Managed strings in the serializer, GUI and reconstructed variables cannot be reliably erased; owned byte/character buffers are cleared. Valid older containers can be replayed; the format does not prove freshness. GMAC files expose their entire contents even before authentication.
