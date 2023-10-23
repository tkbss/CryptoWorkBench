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
type          : KEYWORD_KEY
              | KEYWORD_VAR
              ;
expression    : HEX_STRING
              | BASE64_STRING 
              | INT
              ;


functionCall  : ID '(' arguments? ')'
              ; 
arguments     : argument (',' argument)*
              ;
argument      : expression 
              | ID
              | functionCall
              ;



