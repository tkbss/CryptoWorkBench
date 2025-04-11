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
declareparam  :  PARAM_TYPE ':' MECHANISM
              |  PARAM_TYPE ':' PADDING
              |  PARAM_TYPE ':' HEX_STRING
              |  PARAM_TYPE ':' ID
              |  PARAM_TYPE ':' NORMAL_STRING
			  ;
type          : T_KEY
              | T_VAR
              | T_PARAMETER
              | T_PATH
              | T_TR31H
              ;

tr31Header    : '{' tr31Field+ '}';
tr31Field     : TR31_FIELD_NAME ':' (TR31_FIELD_VALUE | INT) ';';


expression    : HEX_STRING
              | BASE64_STRING 
              | NORMAL_STRING
              | INT 
              | PATH
              | TR31_STRING
              ;


functionCall  : FN '(' arguments? ')'
              ; 
arguments     : argument (',' argument)*
              ;
argument      : MECHANISM 
              | declareparam              
              | ID
              | functionCall
              | expression
              | INFO
              ;
                         
			 



