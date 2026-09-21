lexer grammar CryptoLexer;
import TR31Lexer;

// Lexer rules for variable declaration
T_KEY         : 'KEY';
T_VAR         : 'VAR';
T_PARAMETER   : 'PARAM';
T_PATH        : 'PATH';
T_TR31H       : 'TR31H';

PATH_VALUE    : [a-zA-Z] ':' ~[\r\n]*      // C:\...
                | '\\\\' ~[\r\n]*           // \\server\share\...
                | '/' ~[\r\n]*              // /usr/local/...
                ;
FN            : [A-Z] [a-z] [a-zA-Z]*;
INFO		  : 'functions' | 'mechanisms' | 'types' | 'parameters' | 'paddings' | 'mechanism' | 'keymap';
ID            : [a-zA-Z] [a-zA-Z0-9]*;
VARIANT_VALUE : 'MAC-SEND' | 'MAC-RECEIVE' | 'KEY-ENCRYPTION';
HEX_STRING    : '0x(' [0-9a-fA-F]+ ')';
BASE64_STRING : 'b64(' [A-Za-z0-9+/=]+ ')';
NORMAL_STRING : '"' (ESC | ~["\\])* '"';

// Composite token using fragments
TR31_STRING     : NORMAL_STRING_FRAG HEX_STRING_FRAG HEX_STRING_FRAG;

// Fragment definitions (do not produce tokens on their own)
fragment NORMAL_STRING_FRAG : '"' (ESC | ~["\\])* '"';
fragment HEX_STRING_FRAG    : '0x(' [0-9a-fA-F]+ ')';
fragment ESC                : '\\' [btnrf"'\\];

MECHANISM     : M_AES_ECB | M_AES_CBC | M_AES_CTR | M_AES_CMAC | M_AES_GCM | M_AES_GMAC | M_AES_CCM
              | M_HMAC_SHA1 | M_HMAC_SHA224 | M_HMAC_SHA256 | M_HMAC_SHA384 | M_HMAC_SHA512
              | M_HMAC_SHA512_224 | M_HMAC_SHA512_256
              | M_HMAC_SHA3_224 | M_HMAC_SHA3_256 | M_HMAC_SHA3_384 | M_HMAC_SHA3_512
              | M_HASH_SHA1 | M_HASH_SHA224 | M_HASH_SHA256 | M_HASH_SHA384 | M_HASH_SHA512
              | M_HASH_SHA512_224 | M_HASH_SHA512_256
              | M_HASH_SHA3_224 | M_HASH_SHA3_256 | M_HASH_SHA3_384 | M_HASH_SHA3_512
              | M_DES3_ECB| M_DES3_CBC| M_DES3_RETAIL | M_DES3_CMAC
              | M_WRAP_AES_TR31 | M_WRAP_DES3_TR31 | M_WRAP_AES | M_WRAP_DES3
              | M_KDF_HKDF | M_HKDF_EXTRACT | M_HKDF_EXPAND | M_KDF_EP2_SESSION
              | M_KDF_SP800_108_COUNTER
              | M_KDF_EP2_PAN_SURROGATE_TRX | M_KDF_EP2_PAN_RECEIPT_TRX | M_KDF_EP2_PAN_RECEIPT_TRM
              ;

M_AES_ECB           : 'AES-ECB';
M_AES_CBC           : 'AES-CBC';
M_AES_CTR           : 'AES-CTR';
M_AES_CMAC          : 'AES-CMAC';
M_AES_GCM           : 'AES-GCM';
M_AES_CCM           : 'AES-CCM';
M_AES_GMAC          : 'AES-GMAC';
M_HMAC_SHA1         : 'HMAC-SHA1';
M_HMAC_SHA224       : 'HMAC-SHA224';
M_HMAC_SHA256       : 'HMAC-SHA256';
M_HMAC_SHA384       : 'HMAC-SHA384';
M_HMAC_SHA512       : 'HMAC-SHA512';
M_HMAC_SHA512_224   : 'HMAC-SHA512-224';
M_HMAC_SHA512_256   : 'HMAC-SHA512-256';
M_HMAC_SHA3_224     : 'HMAC-SHA3-224';
M_HMAC_SHA3_256     : 'HMAC-SHA3-256';
M_HMAC_SHA3_384     : 'HMAC-SHA3-384';
M_HMAC_SHA3_512     : 'HMAC-SHA3-512';
M_HASH_SHA1         : 'HASH-SHA1';
M_HASH_SHA224       : 'HASH-SHA224';
M_HASH_SHA256       : 'HASH-SHA256';
M_HASH_SHA384       : 'HASH-SHA384';
M_HASH_SHA512       : 'HASH-SHA512';
M_HASH_SHA512_224   : 'HASH-SHA512-224';
M_HASH_SHA512_256   : 'HASH-SHA512-256';
M_HASH_SHA3_224     : 'HASH-SHA3-224';
M_HASH_SHA3_256     : 'HASH-SHA3-256';
M_HASH_SHA3_384     : 'HASH-SHA3-384';
M_HASH_SHA3_512     : 'HASH-SHA3-512';
M_DES3_ECB          : 'DES3-ECB';
M_DES3_CBC          : 'DES3-CBC';
M_DES3_RETAIL       : 'DES3-RETAIL';
M_DES3_CMAC         : 'DES3-CMAC';
M_WRAP_AES_TR31     : 'WRAP-AES-TR31';
M_WRAP_DES3_TR31    : 'WRAP-DES3-TR31';
M_WRAP_AES          : 'WRAP-AES';
M_WRAP_DES3         : 'WRAP-DES3';
M_KDF_HKDF          : 'KDF-HKDF';
M_HKDF_EXTRACT      : 'HKDF-EXTRACT';
M_HKDF_EXPAND       : 'HKDF-EXPAND';
M_KDF_SP800_108_COUNTER : 'KDF-SP800-108-COUNTER';
M_KDF_EP2_SESSION   : 'KDF-EP2-SESSION';
M_KDF_EP2_PAN_SURROGATE_TRX : 'KDF-EP2-PAN-SURROGATE-TRX';
M_KDF_EP2_PAN_RECEIPT_TRX : 'KDF-EP2-PAN-RECEIPT-TRX';
M_KDF_EP2_PAN_RECEIPT_TRM : 'KDF-EP2-PAN-RECEIPT-TRM';
PADDING	      :  PAD_ISO7816 | PAD_PKCS7 | PAD_ISO9797M1 | PAD_ISO9797M2 | PAD_ISO9797M3 | PAD_ANSI_X923 | PAD_TLS_CBC | PAD_NONE; 
PAD_ISO7816   : 'ISO-7816';
PAD_PKCS7     : 'PKCS-7';
PAD_ISO9797M1 : 'ISO-9797-M1';
PAD_ISO9797M2 : 'ISO-9797-M2';
PAD_ISO9797M3 : 'ISO-9797-M3';
PAD_ANSI_X923 : 'ANSI-X923';
PAD_TLS_CBC   : 'TLS-CBC';
PAD_NONE      : 'NONE';

PARAM_TYPE	  : P_MECHANISM | P_IV | P_PADDING | P_MAC_LENGTH | P_NONCE | P_COUNTER|P_ADATA | P_BLKHDR | P_RND | P_HASH | P_SALT | P_OUT_LENGTH | P_VARIANT | P_PRF | P_LABEL;
P_MECHANISM   : '#MECH';
P_IV          : '#IV';
P_PADDING     : '#PAD';
P_MAC_LENGTH  : '#MACLEN';
P_NONCE       : '#NONCE';
P_COUNTER     : '#COUNTER';
P_ADATA       : '#ADATA';
P_BLKHDR	  : '#BLKH';
P_RND         : '#RND';
P_HASH        : '#HASH';
P_SALT        : '#SALT';
P_OUT_LENGTH  : '#OUTLEN';
P_VARIANT     : '#VARIANT';
P_PRF         : '#PRF';
P_LABEL       : '#LABEL';
WS            : [ \t\r\n]+ -> skip;
