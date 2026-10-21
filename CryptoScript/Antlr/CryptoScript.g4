grammar CryptoScript;
import  CryptoLexer;


INT           : [0-9]+;
// Parser rules
program       : statement* EOF;

statement     : declaration              
              | functionCall              
              ; 

declaration   : type ID '=' expression              
              | type ID '=' functionCall
              | type ID '=' (declareparam)*
              | type ID '=' tr31Header
              ;
declareparam  :  P_POLYNOM ':' polynomialValue
              |  P_SHAREFORMAT ':' shareFormatValue
              |  PARAM_TYPE ':' MECHANISM
              |  PARAM_TYPE ':' PADDING
              |  PARAM_TYPE ':' HEX_STRING
              |  PARAM_TYPE ':' BASE64_STRING
              |  PARAM_TYPE ':' ID
              |  PARAM_TYPE ':' VARIANT_VALUE
              |  PARAM_TYPE ':' NORMAL_STRING
			  |  PARAM_TYPE ':' INT
			  ;
polynomialValue : {TokenStream.LT(1).Text == "\"SHAMIR-GF256-11B\"" || TokenStream.LT(1).Text == "\"SHAMIR-GF256-11D\""}? NORMAL_STRING;
shareFormatValue : {TokenStream.LT(1).Text == "\"X-Y\"" || TokenStream.LT(1).Text == "\"Y-X\"" || TokenStream.LT(1).Text == "\"SEPARATE-X\""}? NORMAL_STRING;
type          : T_SECRET
              | T_KEY
              | T_VAR
              | T_PARAMETER
              | T_PATH
              | T_TR31H
              ;

tr31Header    : '{' tr31Field+ '}';
tr31Field     : headerAttribute ';';
headerAttribute : (ID | TR31_FIELD_NAME | TR31_OPT_BLOCK_DATA) ':'
                  (ID | FN | TR31_FIELD_VALUE | INT | NUM | KV_00 | NORMAL_STRING | HEX_STRING);


expression    : HEX_STRING
              | BASE64_STRING 
              | NORMAL_STRING
              | INT 
              | PATH_VALUE
              | TR31_STRING
              ;


functionCall  : FN '(' ({$FN.text == "CreateHeader"}? headerArguments
                      | {$FN.text == "Info"}? functionTopic
                      | {$FN.text == "Load"}? loadArguments
                      | {$FN.text == "SecureSave"}? secureArguments
                      | arguments)? ')'
              ; 
functionTopic : FN (',' arguments)?;
// FN-shaped snapshot identifiers (e.g. MasterKey) are contextual to Load.
loadArguments : loadArgument (',' loadArgument)*;
loadArgument : FN | argument;
// Keep the raw value for Load's semantic validation; never resolve it as a variable.
onConflictParameter : P_ONCONFLICT ':' (ID | FN | expression | MECHANISM | PADDING | VARIANT_VALUE);
headerArguments : headerAttribute (',' headerAttribute)*;
arguments     : argument (',' argument)*
              ;
argument      : MECHANISM 
              | onConflictParameter
              | declareparam              
              | ID
              | functionCall
              | expression
              | INFO
              ;
                         
			 




secureArguments : secureArgument (',' secureArgument)*;
secureArgument : secureOption | FN | argument;
secureOption : (SOPT_SECUREMODE | SOPT_SECUREKDF | SOPT_ARGONMEM | SOPT_ARGONITER | SOPT_ARGONLANES | SOPT_PBKDFITER)
    ':' (ID | FN | expression | MECHANISM | PADDING | VARIANT_VALUE);
