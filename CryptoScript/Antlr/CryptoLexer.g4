lexer grammar CryptoLexer;

// Lexer rules
KEYWORD_KEY   : 'KEY';
KEYWORD_VAR   : 'VAR';
ID            : [a-zA-Z_] [a-zA-Z0-9_]*
              ;
HEX_STRING    : '0x(' [0-9a-fA-F]+ ')'
              ;
BASE64_STRING : 'b64(' [A-Za-z0-9+/=]+ ')'
              ;
NORMAL_STRING : '"' (ESC | ~["\\])* '"';
fragment ESC  : '\\' [btnrf"'\\];
INT           : [0-9]+;
MECHANISM     : 'AES-ECB' | 'AES-CBC' | 'AES-CTR' | 'AES-CMAC'
              | 'DES3-ECB' | 'DES3-CBC'  | 'DES3-CMAC'
              ;
WS            : [ \t\r\n]+ -> skip;
