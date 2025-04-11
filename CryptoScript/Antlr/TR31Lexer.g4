lexer grammar TR31Lexer;
TR31_FIELD_NAME
    : TR31_KB_VERSION_ID
    | TR31_KB_LENGTH
    | TR31_TRANSPORTED_KEYLEN
    | TR31_KU
    | TR31_ALGO
    | TR31_MODEU
    | TR31_KEY_VERSION_NUM
    | TR31_EXPORTABILITY
    | TR31_NUM_OPT_BLOCKS
    | TR31_KEY_CONTEXT
    | TR31_RESERVED_FIELD
    | TR31_OPT_BLOCK_ID
    ;
TR31_FIELD_VALUE
    : KU_B0 | KU_B1 | KU_B2 | KU_B3 | KU_C0 | KU_D0 | KU_D1 | KU_D2 | KU_D3
    | KU_E0 | KU_E1 | KU_E2 | KU_E3 | KU_F3 | KU_F4 | KU_F5 | KU_F6 | KU_I0
    | KU_K0 | KU_K1 | KU_K2 | KU_K3 | KU_K4 | KU_M0 | KU_M1 | KU_P2
    | KU_V0 | KU_V1 | KU_V2 | KU_V3 | KU_V4 | KU_V5
    | H | R | S | T | D | E | A | B | C | G | N | V | X | Y
    | OPT_AL | OPT_BI | OPT_CT | OPT_DA | OPT_HM | OPT_IK
    | OPT_KC | OPT_KP | OPT_KS | OPT_KV | OPT_LB | OPT_PA
    | OPT_PB | OPT_PK | OPT_TC | OPT_TS | OPT_WP 
    ;
// TR31 Key Block Header Field Names
TR31_KB_VERSION_ID			: 'KBVID';
TR31_KB_LENGTH				: 'KBLEN';
TR31_TRANSPORTED_KEYLEN		: 'TKL';
TR31_KU						: 'KEYU';
TR31_ALGO					: 'ALGO';
TR31_MODEU					: 'MODEU';
TR31_KEY_VERSION_NUM		: 'KEYVN';
TR31_EXPORTABILITY			: 'EXP';
TR31_NUM_OPT_BLOCKS			: 'NUMOPTB';
TR31_KEY_CONTEXT			: 'KEYCTX';
TR31_RESERVED_FIELD			: 'RSV';

// Optional Block IDs (examples)
TR31_OPT_BLOCK_ID			: 'OPTID';
TR31_OPT_BLOCK_DATA			: 'OPTBD';
NUM : INT ;
// KEY USAGE (KU) Values (keep only KU-specific literals here)
KU_B0 : 'B0'; KU_B1 : 'B1'; KU_B2 : 'B2'; KU_B3 : 'B3'; KU_C0 : 'C0';
KU_D0 : 'D0'; KU_D1 : 'D1'; KU_D2 : 'D2'; KU_D3 : 'D3'; KU_E0 : 'E0';
KU_E1 : 'E1'; KU_E2 : 'E2'; KU_E3 : 'E3'; KU_F3 : 'F3'; KU_F4 : 'F4';
KU_F5 : 'F5'; KU_F6 : 'F6'; KU_I0 : 'I0'; KU_K0 : 'K0';
KU_K1 : 'K1'; KU_K2 : 'K2'; KU_K3 : 'K3'; KU_K4 : 'K4'; KU_M0 : 'M0';
KU_M1 : 'M1'; KU_P2 : 'P2'; KU_V0 : 'V0'; KU_V1 : 'V1';
KU_V2 : 'V2'; KU_V3 : 'V3'; KU_V4 : 'V4'; KU_V5 : 'V5';

// Optional Block ID values from ANSI X9.143-TR31-2022
OPT_AL : 'AL';  // Asymmetric key life attribute
OPT_BI : 'BI';  // Base Derivation Key Identifier for DUKPT
OPT_CT : 'CT';  // Asymmetric public key certificate or chain
OPT_DA : 'DA';  // Derivations Allowed for Derivation Keys
OPT_HM : 'HM';  // Hash algorithm for HMAC
OPT_IK : 'IK';  // Initial Key identifier for AES DUKPT
OPT_KC : 'KC';  // Key Check value of the wrapped key
OPT_KP : 'KP';  // Key Protection (Key check value of KBPK)
OPT_KS : 'KS';  // Key Serial (TDEA DUKPT Initial Key Serial Number)
OPT_KV : 'KV';  // Key Block Values Version (Informational)
OPT_LB : 'LB';  // Label (variable-length user-defined label)
OPT_PA : 'PA';  // Proprietary Algorithm (regional algorithm definition)
OPT_PB : 'PB';  // Padding Block (variable-length padding)
OPT_PK : 'PK';  // Protection Key Check Value
OPT_TC : 'TC';  // Time of Creation (key creation date/time)
OPT_TS : 'TS';  // Time Stamp (Key Block formation date/time)
OPT_WP : 'WP';  // Wrapping Pedigree (key wrapping security level)


// Shared Literals (defined ONCE)
H : 'H';
R : 'R';
S : 'S';
T : 'T';
D : 'D';
E : 'E';
A : 'A';
B : 'B';
C : 'C';
G : 'G';
N : 'N';
V : 'V';
X : 'X';
Y : 'Y';