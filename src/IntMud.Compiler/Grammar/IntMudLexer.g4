lexer grammar IntMudLexer;

// ============================================================================
// Keywords - Control Flow
// Accepts both accented and non-accented forms for Portuguese compatibility
// ============================================================================
SE          : 'se';
SENAO       : 'senao' | 'sen\u00E3o';  // senão
FIMSE       : 'fimse';
ENQUANTO    : 'enquanto';
EPARA       : 'epara';
PARA        : 'para';
CADA        : 'cada';
EM          : 'em';
EFIM        : 'efim';
CASOVAR     : 'casovar';
CASOSE      : 'casose';
CASOFIM     : 'casofim';
RET         : 'ret' | 'retorne';  // Alternative: retorne
SAIR        : 'sair';
CONTINUAR   : 'continuar';
TERMINAR    : 'terminar';

// ============================================================================
// Keywords - Class/Function Definition
// Accepts both abbreviated and full forms for better readability
// ============================================================================
CLASSE      : 'classe';
HERDA       : 'herda';
FUNC        : 'func' | 'fun\u00E7\u00E3o';  // função
VARFUNC     : 'varfunc' | 'varfun\u00E7\u00E3o';  // varfunção
VARCONST    : 'varconst';
CONST       : 'const' | 'constante';  // Alternative: constante

// ============================================================================
// Keywords - Modifiers
// ============================================================================
COMUM       : 'comum';
SAV         : 'sav';

// ============================================================================
// Keywords - Special Values
// ============================================================================
NULO        : 'nulo';
ESTE        : 'este';
REFVAR      : 'refvar';
NOVO        : 'novo';
APAGAR      : 'apagar';

// ============================================================================
// Keywords - Basic Variable Types
// ============================================================================
INT1        : 'int1';
INT8        : 'int8';
INT16       : 'int16';
INT32       : 'int32';
UINT8       : 'uint8';
UINT16      : 'uint16';
UINT32      : 'uint32';
REAL2       : 'real2';  // Must come before REAL
REAL        : 'real';
TXT         : 'txt' [1-9][0-9]*;  // txt1 to txt512
REF         : 'ref';
INTINC      : 'intinc';
INTDEC      : 'intdec';

// ============================================================================
// Keywords - Complex Variable Types
// ============================================================================
LISTAOBJ    : 'listaobj';
LISTAITEM   : 'listaitem';
TEXTOTXT    : 'textotxt';
TEXTOPOS    : 'textopos';
TEXTOVAR    : 'textovar';
TEXTOOBJ    : 'textoobj';
NOMEOBJ     : 'nomeobj';
ARQDIR      : 'arqdir';
ARQLOG      : 'arqlog';
ARQPROG     : 'arqprog';
ARQEXEC     : 'arqexec';
ARQSAV      : 'arqsav';
ARQTXT      : 'arqtxt';
ARQMEM      : 'arqmem';
INTTEMPO    : 'inttempo';
INTEXEC     : 'intexec';
TELATXT     : 'telatxt';
SOCKET      : 'socket';
SERV        : 'serv';
PROG        : 'prog';
DEBUG       : 'debug';
INDICEOBJ   : 'indiceobj';
INDICEITEM  : 'indiceitem';
DATAHORA    : 'datahora';

// ============================================================================
// File Options
// ============================================================================
INCLUIR     : 'incluir';
EXEC        : 'exec';
LOG         : 'log';
ERR         : 'err';
COMPLETO    : 'completo';

// ============================================================================
// Operators - Arithmetic
// ============================================================================
PLUS        : '+';
MINUS       : '-';
STAR        : '*';
SLASH       : '/';
PERCENT     : '%';

// ============================================================================
// Operators - Increment/Decrement
// ============================================================================
PLUSPLUS    : '++';
MINUSMINUS  : '--';

// ============================================================================
// Operators - Comparison
// ============================================================================
LT          : '<';
LE          : '<=';
GT          : '>';
GE          : '>=';
EQEQ        : '==';
EQEQEQ      : '===';
NE          : '!=';
NEE         : '!==';

// ============================================================================
// Operators - Logical
// ============================================================================
AND         : '&&';
OR          : '||';
NOT         : '!';

// ============================================================================
// Operators - Bitwise
// ============================================================================
AMPERSAND   : '&';
PIPE        : '|';
CARET       : '^';
TILDE       : '~';
SHL         : '<<';
SHR         : '>>';

// ============================================================================
// Operators - Assignment
// ============================================================================
EQ          : '=';
PLUSEQ      : '+=';
MINUSEQ     : '-=';
STAREQ      : '*=';
SLASHEQ     : '/=';
PERCENTEQ   : '%=';
SHLEQ       : '<<=';
SHREQ       : '>>=';
AMPEQ       : '&=';
PIPEEQ      : '|=';
CARETEQ     : '^=';

// ============================================================================
// Operators - Conditional
// ============================================================================
QUESTION    : '?';
COLON       : ':';
QUESTIONQUESTION : '??';

// ============================================================================
// Punctuation
// ============================================================================
LPAREN      : '(';
RPAREN      : ')';
LBRACKET    : '[';
RBRACKET    : ']';
LBRACE      : '{';
RBRACE      : '}';
DOT         : '.';
COMMA       : ',';
DOLLAR      : '$';
AT          : '@';
SEMICOLON   : ';';
BACKSLASH   : '\\';

// ============================================================================
// Literals
// ============================================================================
// Note: Negative numbers are handled by unary minus in the parser, not here
HEX_NUMBER
    : '0' [xX] [0-9a-fA-F]+
    ;

DECIMAL_NUMBER
    : [0-9]+ ('.' [0-9]+)?
    ;

STRING
    : '"' ( ESCAPE_SEQUENCE | STRING_LINE_CONTINUATION | ~["\\\r\n] )* '"'
    ;

fragment ESCAPE_SEQUENCE
    : '\\' [nbcd\\"']
    | '\\' [0-9a-fA-F]  // color codes
    ;

fragment STRING_LINE_CONTINUATION
    : '\\' ('\r'? '\n' | '\r')  // backslash at end of line continues string
    ;

// ============================================================================
// Identifiers
// ============================================================================
// Argument keywords - must come before IDENTIFIER
ARGS
    : 'args'
    ;

ARG
    : 'arg' [0-9]
    ;

IDENTIFIER
    : [a-zA-Z_\u00C0-\u00FF] [a-zA-Z0-9_\u00C0-\u00FF@]*
    ;

// ============================================================================
// Comments - Skip
// ============================================================================
COMMENT
    : '#' ~[\r\n]* -> channel(HIDDEN)
    ;

// ============================================================================
// Whitespace - Skip
// ============================================================================
WS
    : [ \t]+ -> skip
    ;

// ============================================================================
// Newlines - Important for statement separation
// ============================================================================
NEWLINE
    : ('\r'? '\n' | '\r')+ -> channel(HIDDEN)
    ;

// Line continuation
LINE_CONTINUATION
    : '\\' [ \t]* ('\r'? '\n' | '\r') -> skip
    ;
