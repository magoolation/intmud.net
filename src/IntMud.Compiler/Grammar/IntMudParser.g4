parser grammar IntMudParser;

options {
    tokenVocab = IntMudLexer;
}

// ============================================================================
// Top-level structure
// ============================================================================

compilationUnit
    : fileOption* classDefinition* EOF
    ;

fileOption
    : INCLUIR EQ STRING
    | EXEC EQ DECIMAL_NUMBER
    | TELATXT EQ DECIMAL_NUMBER
    | LOG EQ DECIMAL_NUMBER
    | ERR EQ DECIMAL_NUMBER
    | COMPLETO EQ DECIMAL_NUMBER
    | ARQEXEC EQ STRING
    ;

// ============================================================================
// Class Definition
// ============================================================================

classDefinition
    : CLASSE identifier inheritClause? classMember*
    ;

inheritClause
    : HERDA identifierList
    ;

identifierList
    : identifier (COMMA identifier)*
    ;

classMember
    : variableDeclaration
    | functionDefinition
    | constantDefinition
    | varFuncDefinition
    | varConstDefinition
    ;

// ============================================================================
// Variable Declaration
// ============================================================================

variableDeclaration
    : modifier* variableType identifier vectorSize? (EQ expression)?
    ;

modifier
    : COMUM
    | SAV
    ;

variableType
    : INT1
    | INT8
    | INT16
    | INT32
    | UINT8
    | UINT16
    | UINT32
    | REAL
    | REAL2
    | TXT
    | REF
    | INTINC
    | INTDEC
    | LISTAOBJ
    | LISTAITEM
    | TEXTOTXT
    | TEXTOPOS
    | TEXTOVAR
    | TEXTOOBJ
    | NOMEOBJ
    | ARQDIR
    | ARQLOG
    | ARQPROG
    | ARQEXEC
    | ARQSAV
    | ARQTXT
    | ARQMEM
    | INTTEMPO
    | INTEXEC
    | TELATXT
    | SOCKET
    | SERV
    | PROG
    | DEBUG
    | INDICEOBJ
    | INDICEITEM
    | DATAHORA
    ;

vectorSize
    : DOT DECIMAL_NUMBER
    ;

// ============================================================================
// Function Definition
// ============================================================================

functionDefinition
    : FUNC identifier statement*
    ;

varFuncDefinition
    : VARFUNC identifier statement*
    ;

// ============================================================================
// Constant Definition
// ============================================================================

constantDefinition
    : CONST identifier EQ expression
    ;

varConstDefinition
    : VARCONST identifier EQ expression
    ;

// ============================================================================
// Statements
// ============================================================================

statement
    : variableDeclaration
    | refVarDeclaration
    | ifStatement
    | whileStatement
    | forStatement
    | foreachStatement
    | switchStatement
    | returnStatement
    | exitStatement
    | continueStatement
    | terminateStatement
    | expressionStatement
    ;

refVarDeclaration
    : REFVAR identifier EQ expression
    ;

// ============================================================================
// Control Flow Statements
// ============================================================================

ifStatement
    : SE expression statement* elseIfClause* elseClause? FIMSE?
    ;

elseIfClause
    : SENAO expression statement*
    ;

elseClause
    : SENAO statement*
    ;

whileStatement
    : ENQUANTO expression statement* EFIM
    ;

forStatement
    : EPARA expression COMMA expression COMMA expression statement* EFIM
    ;

foreachStatement
    : PARA CADA identifier EM expression statement* EFIM
    ;

switchStatement
    : CASOVAR expression caseClause* defaultClause? CASOFIM
    ;

caseClause
    : CASOSE STRING statement*
    ;

defaultClause
    : CASOSE statement*
    ;

returnStatement
    : RET expression?
    ;

exitStatement
    : SAIR expression?
    ;

continueStatement
    : CONTINUAR expression?
    ;

terminateStatement
    : TERMINAR
    ;

// ============================================================================
// Expression Statement
// ============================================================================

expressionStatement
    : expression (COMMA expression)*
    ;

// ============================================================================
// Expressions
// ============================================================================

expression
    : assignmentExpression
    ;

assignmentExpression
    : conditionalExpression
    | leftHandSide assignmentOperator assignmentExpression
    ;

assignmentOperator
    : EQ
    | PLUSEQ
    | MINUSEQ
    | STAREQ
    | SLASHEQ
    | PERCENTEQ
    | SHLEQ
    | SHREQ
    | AMPEQ
    | PIPEEQ
    | CARETEQ
    ;

conditionalExpression
    : nullCoalesceExpression
    | nullCoalesceExpression QUESTION expression? (COLON expression)?
    ;

nullCoalesceExpression
    : logicalOrExpression (QUESTIONQUESTION logicalOrExpression)*
    ;

logicalOrExpression
    : logicalAndExpression (OR logicalAndExpression)*
    ;

logicalAndExpression
    : bitwiseOrExpression (AND bitwiseOrExpression)*
    ;

bitwiseOrExpression
    : bitwiseXorExpression (PIPE bitwiseXorExpression)*
    ;

bitwiseXorExpression
    : bitwiseAndExpression (CARET bitwiseAndExpression)*
    ;

bitwiseAndExpression
    : equalityExpression (AMPERSAND equalityExpression)*
    ;

equalityExpression
    : relationalExpression ((EQEQ | EQEQEQ | NE | NEE) relationalExpression)*
    ;

relationalExpression
    : shiftExpression ((LT | LE | GT | GE) shiftExpression)*
    ;

shiftExpression
    : additiveExpression ((SHL | SHR) additiveExpression)*
    ;

additiveExpression
    : multiplicativeExpression ((PLUS | MINUS) multiplicativeExpression)*
    ;

multiplicativeExpression
    : unaryExpression ((STAR | SLASH | PERCENT) unaryExpression)*
    ;

unaryExpression
    : PLUSPLUS unaryExpression
    | MINUSMINUS unaryExpression
    | MINUS unaryExpression
    | NOT unaryExpression
    | TILDE unaryExpression
    | postfixExpression
    ;

postfixExpression
    : primaryExpression postfixOp*
    ;

postfixOp
    : PLUSPLUS
    | MINUSMINUS
    | DOT identifier arguments?
    | DOT DECIMAL_NUMBER
    | LBRACKET expression RBRACKET
    | arguments
    ;

primaryExpression
    : NULO
    | ESTE
    | ARG
    | ARGS
    | DECIMAL_NUMBER
    | HEX_NUMBER
    | STRING
    | LPAREN expression RPAREN
    | classReference
    | dollarReference
    | newExpression
    | deleteExpression
    | identifier
    ;

newExpression
    : NOVO identifier arguments?
    ;

deleteExpression
    : APAGAR expression
    ;

leftHandSide
    : postfixExpression
    ;

// ============================================================================
// References
// ============================================================================

classReference
    : identifier COLON identifier
    ;

dollarReference
    : DOLLAR identifier
    ;

// ============================================================================
// Function Call
// ============================================================================

arguments
    : LPAREN argumentList? RPAREN
    ;

argumentList
    : expression (COMMA expression)*
    ;

// ============================================================================
// Identifier
// ============================================================================

identifier
    : IDENTIFIER
    | contextualKeyword
    ;

// Keywords that can be used as identifiers in certain contexts
contextualKeyword
    : INCLUIR
    | EXEC
    | LOG
    | ERR
    | COMPLETO
    ;
