# Delete

`Delete(variable1, ...)` removes existing variables from the current dictionary. `Delete(ALL)` removes every variable, including when the dictionary is already empty. Delete is synchronous and has no return value; it cannot initialize a variable or appear inside another function call.

Arguments must be variable identifiers. All six types (VAR, KEY, PARAM, PATH, TR31H and SECRET) are supported. Missing arguments, unknown names, duplicate selectors, literals, parameters and nested calls are rejected before deletion. Only the exact uppercase `ALL` is a contextual selector; `all` is an ordinary variable name. `ALL` cannot be combined with another selector. Function-token names such as `MasterKey` can also identify variables.

All arguments and references are validated before the remaining dictionary is published atomically. A TR31H root cannot be removed while a remaining PARAM refers to that exact header object. Remove the referencing PARAM roots together with the header, or remove them first. Embedded headers that are not dictionary roots are not additional variables. Remaining objects retain their identity, metadata and order; PARAM references are never rebound.

```cryptoscript
Delete(master)
Delete(sessionKey, otherKey)
Delete(p, header)
Delete(ALL)
```

An exported snapshot remains available after deleting its variables:

```cryptoscript
PATH backup = snapshot.json
KEY master = GenerateKey(AES-ECB, 128)
Save(backup, master)
Delete(master)
Load(backup, master)
Print(master)
```

Delete removes dictionary root references only. It does not erase files, zeroize key material or SECRET shares, remove garbage-collector copies, or destroy objects referenced by other components. Removed objects are not mutated. After `Delete(ALL)`, declare a new PATH before loading a saved snapshot.
