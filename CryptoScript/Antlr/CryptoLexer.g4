lexer grammar CryptoLexer;

// Lexer rules
T_KEY         : 'KEY';
T_VAR         : 'VAR';
ID            : [a-zA-Z_] [a-zA-Z0-9_]*
              ;
HEX_STRING    : '0x(' [0-9a-fA-F]+ ')'
              ;
BASE64_STRING : 'b64(' [A-Za-z0-9+/=]+ ')'
              ;
NORMAL_STRING : '"' (ESC | ~["\\])* '"';
fragment ESC  : '\\' [btnrf"'\\];
INT           : [0-9]+;
MECHANISM     : M_AES_ECB | M_AES_CBC | M_AES_CTR | M_AES_CMAC
              | M_DES3_ECB| M_DES3_CBC| M_DES3_CMAC;
M_AES_ECB     : 'AES-ECB';
M_AES_CBC     : 'AES-CBC';
M_AES_CTR     : 'AES-CTR';
M_AES_CMAC    : 'AES-CMAC';
M_DES3_ECB    : 'DES3-ECB';
M_DES3_CBC    : 'DES3-CBC';
M_DES3_CMAC   : 'DES3-CMAC';


WS            : [ \t\r\n]+ -> skip;
