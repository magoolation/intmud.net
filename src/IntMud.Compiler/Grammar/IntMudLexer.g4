lexer grammar IntMudLexer;

// ============================================================================
// Keywords - Control Flow
// ============================================================================
SE          : 'se';
SENAO       : 'senao';
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
RET         : 'ret';
SAIR        : 'sair';
CONTINUAR   : 'continuar';
TERMINAR    : 'terminar';

// ============================================================================
// Keywords - Class/Function Definition
// ============================================================================
CLASSE      : 'classe';
HERDA       : 'herda';
FUNC        : 'func';
VARFUNC     : 'varfunc';
VARCONST    : 'varconst';
CONST       : 'const';

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
HEX_NUMBER
    : '-'? '0' [xX] [0-9a-fA-F]+
    ;

DECIMAL_NUMBER
    : '-'? [0-9]+ ('.' [0-9]+)?
    ;

STRING
    : '"' ( ESCAPE_SEQUENCE | ~["\\\r\n] )* '"'
    ;

fragment ESCAPE_SEQUENCE
    : '\\' [nbcd\\"']
    | '\\' [0-9a-fA-F]  // color codes
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
