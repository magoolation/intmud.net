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
    : INCLUIR EQ (STRING | filePath)
    | EXEC EQ DECIMAL_NUMBER
    | TELATXT EQ DECIMAL_NUMBER
    | LOG EQ DECIMAL_NUMBER
    | ERR EQ DECIMAL_NUMBER
    | COMPLETO EQ DECIMAL_NUMBER
    | ARQEXEC EQ STRING
    ;

filePath
    : (IDENTIFIER | SLASH)+ SLASH?   // path like adm/ or obj/config/
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
    : CONST identifier EQ expression (COMMA expression)*
    ;

varConstDefinition
    : VARCONST identifier EQ expression (COMMA expression)*
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
    : REFVAR extendedIdentifier EQ expression
    ;

// Extended identifier that allows FUNC as variable name (used in refvar and expressions)
extendedIdentifier
    : identifier
    | FUNC
    ;

// Expression that can appear inside brackets, allowing FUNC as identifier
bracketExpression
    : expression
    | FUNC       // FUNC used as variable name inside brackets
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
    : CASOVAR expression caseClause* CASOFIM
    ;

caseClause
    : CASOSE caseValue? statement*   // caseValue is optional - without value it's a default case
    ;

caseValue
    : STRING
    | DECIMAL_NUMBER
    | HEX_NUMBER
    ;

returnStatement
    : RET (expression (COMMA expression)?)?
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
    : leftHandSide assignmentOperator assignmentExpression
    | conditionalExpression
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
    | nullCoalesceExpression COLON expression  // Shortened: expr : default means expr ? expr : default
    ;

nullCoalesceExpression
    : logicalOrExpression (QUESTIONQUESTION logicalOrExpression)*
    ;

logicalOrExpression
    : logicalAndExpression (OR assignmentExpression)*
    ;

logicalAndExpression
    : bitwiseOrExpression (AND assignmentExpression)*
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

// Postfix expression: primary expression followed by postfixOps
postfixExpression
    : primaryExpression postfixOp*
    ;

// Postfix operations: member access, increment/decrement, function calls
// Note: In IntMud, arr[expr] is NOT array access - it's dynamic name construction
// Vector access uses dot notation: arr.0, arr.1, arr.[expr]
postfixOp
    : PLUSPLUS
    | MINUSMINUS
    | DOT dynamicMemberName arguments?
    | DOT DECIMAL_NUMBER                          // Vector element access: v.0, v.1
    | DOT LBRACKET bracketExpression RBRACKET     // Dynamic vector access: v.[expr] or v.[func]
    | arguments
    ;

// Dynamic member name: supports patterns like:
//   member, cmd_[arg1], cmd_[arg1]_suffix, [arg1], [arg1]_suffix, [x]_[y]
// Key: identifiers can only follow brackets to avoid consuming separate tokens
// Uses memberIdentifier to allow keywords like FUNC as member names
// Optional @ suffix for countdown variables
dynamicMemberName
    : memberIdentifier (LBRACKET bracketExpression RBRACKET memberIdentifier?)* AT?
    | LBRACKET bracketExpression RBRACKET (memberIdentifier (LBRACKET bracketExpression RBRACKET memberIdentifier?)*)? AT?
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
    | dynamicIdentifierRef
    ;

// Dynamic identifier reference: supports dynamic name construction with brackets
// Examples: x, x[y], x[y]_suffix, [expr], [expr]_suffix
// In IntMud, [expr] makes expr part of the variable/function name
// Example: x["1"] = 10 is the same as x1 = 10
// Example: x[y] = 20 where y="_teste" is the same as x_teste = 20
dynamicIdentifierRef
    : identifier (LBRACKET bracketExpression RBRACKET identifier?)* AT?
    | LBRACKET bracketExpression RBRACKET (identifier (LBRACKET bracketExpression RBRACKET identifier?)*)? AT?
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
    : identifier COLON dynamicMemberName                           // config:modo or config:modo_[expr]
    | identifier LBRACKET bracketExpression RBRACKET COLON dynamicMemberName  // t_[tipo]:nmin (dynamic class reference)
    | LBRACKET bracketExpression RBRACKET COLON dynamicMemberName         // [expr]:member (fully dynamic class reference)
    ;

dollarReference
    : DOLLAR identifier                                     // $jog
    | DOLLAR LBRACKET bracketExpression RBRACKET            // $[expr] (dynamic global variable)
    | DOLLAR identifier LBRACKET bracketExpression RBRACKET // $t_[tipo] (dynamic global with index)
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
    | SERV       // Can be used as variable name
    | PROG       // Can be used as variable name
    | DEBUG      // Can be used as variable name
    | SOCKET     // Can be used as variable name (e.g., "se socket")
    | TXT        // txt1(), txt2() are built-in functions
    | REF        // ref() is a built-in function
    | APAGAR     // apagar() is a method name
    | CLASSE     // classe can be used in expressions like .classe
    | SAV        // sav can be used as identifier
    | NOVO       // novo can be used as identifier
    | ARG        // arg0-arg9 can be used as member name (e.g., contr.arg0)
    | COMUM      // comum can be used as class name in class references (comum:member)
    | PARA       // para can be used as function name (e.g., func para)
    ;

// Identifiers that can be used as member names (after .)
memberIdentifier
    : IDENTIFIER
    | contextualKeyword
    | FUNC       // func can be used as member name (e.g., arg0.func)
    ;
