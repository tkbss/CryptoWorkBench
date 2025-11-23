# Function List

List of all functions of CRYPTO-SCRIPT. Upper and lower case letters in the function name are mandatory.  
Possible Parameters are given in overlay window during typing.

---

## Built-in Functions
- Sign a message: **VAR s = Sign(parameters, key, data)**
- Encrypt a message: **VAR e = Encrypt(parameters, key, data)**
- Decrypt a message: **VAR d = Decrypt(parameters, key, data)**
- Compute a message authentication code (MAC): **VAR m = Mac(parameters, key, data)**
- Generate a key: **KEY k = GenerateKey(mechanism, length)**
- Generate parameters: **PARAM p = Parameters(mechanism, p1, ..., pn)**
- Print variable to output: **Print(s)**
- Show Crypto Script information: **Info(type)**
- Load variable from file: **VAR d = Load(VAR, path)**
- Load parameters from file: **PARAM p = Load(PARAM, path)**
- Load key from file: **KEY k = Load(KEY, path)**




