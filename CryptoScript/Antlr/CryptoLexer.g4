lexer grammar CryptoLexer;

// Lexer rules
T_KEY         : 'KEY';
T_VAR         : 'VAR';
T_PARAMETER   : 'PARAM';
T_PATH        : 'PATH';
PATH          : ( [a-zA-Z] ':' ( '\\' [^\\]* (ESC | ~["\\])*)* '\\'? )                                                          
              ;
FN            : [A-Z] [a-z] [a-zA-Z]*;
INFO		  : 'functions' | 'mechanisms' | 'types';
ID            : [a-zA-Z] [a-zA-Z0-9]*;
HEX_STRING    : '0x(' [0-9a-fA-F]+ ')'
              ;
BASE64_STRING : 'b64(' [A-Za-z0-9+/=]+ ')'
              ;
NORMAL_STRING : '"' (ESC | ~["\\])* '"';
fragment ESC  : '\\' [btnrf"'\\];
INT           : [0-9]+;
MECHANISM     : M_AES_ECB | M_AES_CBC | M_AES_CTR | M_AES_CMAC | M_AES_GCM | M_AES_GMAC | M_AES_CCM
              | M_DES3_ECB| M_DES3_CBC| M_DES3_CMAC
              | M_WRAP_AES_TR31 | M_WRAP_DES3_TR31 | M_WRAP_AES | M_WRAP_DES3 
              | M_BIND_XOR | M_BIND_CMAC;

M_AES_ECB           : 'AES-ECB';
M_AES_CBC           : 'AES-CBC';
M_AES_CTR           : 'AES-CTR';
M_AES_CMAC          : 'AES-CMAC';
M_AES_GCM           : 'AES-GCM';
M_AES_CCM           : 'AES-CCM';
M_AES_GMAC          : 'AES-GMAC';
M_DES3_ECB          : 'DES3-ECB';
M_DES3_CBC          : 'DES3-CBC';
M_DES3_RETAIL       : 'DES3-RETAIL';
M_DES3_CMAC         : 'DES3-CMAC';
M_WRAP_AES_TR31     : 'WRAP-AES-TR31';
M_WRAP_DES3_TR31    : 'WRAP-DES3-TR31';
M_WRAP_AES          : 'WRAP-AES';
M_WRAP_DES3         : 'WRAP-DES3';
M_BIND_XOR          : 'BIND-XOR';
M_BIND_CMAC         : 'BIND-CMAC';

PADDING	      :  PAD_ISO7816 | PAD_PKCS7 | PAD_ISO9797 | PAD_ANSI_X923 | PAD_NONE; 
PAD_ISO7816   : 'ISO-7816';
PAD_PKCS7     : 'PKCS-7';
PAD_ISO9797   : 'ISO-9797';
PAD_ANSI_X923 : 'ANSI-X923';
PAD_NONE      : 'NONE';

PARAM_TYPE	  : P_MECHANISM | P_IV | P_PADDING | P_NONCE | P_COUNTER|P_ADATA | P_BLKHDR | P_KEYBIND;
P_MECHANISM   : '#MECH';
P_IV          : '#IV';
P_PADDING     : '#PAD';
P_NONCE       : '#NONCE';
P_COUNTER     : '#COUNTER';
P_ADATA       : '#ADATA';
P_BLKHDR	  : '#BLKH';
P_KEYBIND     : '#BIND'; 
WS            : [ \t\r\n]+ -> skip;
