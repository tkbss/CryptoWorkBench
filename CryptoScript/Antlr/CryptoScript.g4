grammar CryptoScript;
import  CryptoLexer;



// Parser rules
program       : statement* EOF;

statement     : declaration              
              | functionCall              
              ; 

declaration   : type ID '=' expression              
              | type ID '=' functionCall
              | type ID '=' (declareparam)*
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
              ;


expression    : HEX_STRING
              | BASE64_STRING 
              | INT 
              | PATH
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
                         
			 



