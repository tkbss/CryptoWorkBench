grammar CryptoScript;
import  CryptoLexer;



// Parser rules
program       : statement* EOF;

statement     : declaration
              | functionCall
              ; 

declaration   : type ID '=' expression
              | type ID '=' functionCall
              ;
type          : T_KEY
              | T_VAR
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
              | ID
              | functionCall
              | expression
              ;



