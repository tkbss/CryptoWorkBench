grammar CryptoScript;
import  CryptoLexer;



// Parser rules
program       : statement* EOF;

statement     : declaration              
              | functionCall              
              ; 

declaration   : type ID '=' expression
              | type ID '=' functionCall
              | T_PARAMETER ID '=' (declareparam)*
              ;
declareparam  :  MECHANISM
              |  PARAM_TYPE ':' PADDING
              |  PARAM_TYPE ':' HEX_STRING
              |  PARAM_TYPE ':' ID
			  ;
type          : T_KEY
              | T_VAR
              | T_PARAMETER
              ;


expression    : HEX_STRING
              | BASE64_STRING 
              | INT              
              ;


functionCall  : ID '(' arguments? ')'
              ; 
arguments     : argument (',' argument)*
              ;
argument      : MECHANISM 
              | declareparam              
              | ID
              | functionCall
              | expression
              ;
                         
			 



