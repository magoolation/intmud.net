parser grammar IntMudParser;

options {
    tokenVocab = IntMudLexer;
}

@parser::members {
    /// <summary>
    /// Check if there's a newline in the hidden channel before the current token.
    /// Used to prevent postfix ++ and -- from consuming tokens on a new line.
    /// </summary>
    private bool NoNewlineBefore()
    {
        var tokenIndex = CurrentToken.TokenIndex;
        var stream = (ITokenStream)InputStream;

        // Look at hidden tokens before current token
        for (int i = tokenIndex - 1; i >= 0; i--)
        {
            var token = stream.Get(i);
            if (token.Channel == Lexer.DefaultTokenChannel)
                break; // Stop at previous visible token
            if (token.Type == IntMudLexer.NEWLINE)
                return false; // Found newline - don't allow postfix
        }
        return true; // No newline found - allow postfix
    }

    /// <summary>
    /// Check if we're NOT at the start of a class definition (CLASSE followed by identifier).
    /// Used to prevent statements from consuming what should be a new class definition.
    /// </summary>
    private bool NotClassDefinitionStart()
    {
        // If current token is not CLASSE, we're definitely not at a class definition start
        if (CurrentToken.Type != IntMudLexer.CLASSE)
            return true;

        // Current token is CLASSE - check if next visible token is an identifier
        var stream = (ITokenStream)InputStream;
        int i = CurrentToken.TokenIndex + 1;

        // Skip hidden tokens to find next visible token
        while (i < stream.Size)
        {
            var token = stream.Get(i);
            if (token.Channel == Lexer.DefaultTokenChannel)
            {
                // Found next visible token - if it's an identifier, this is a class definition
                if (token.Type == IntMudLexer.IDENTIFIER)
                    return false; // This IS a class definition start, don't match as statement
                break;
            }
            i++;
        }
        return true; // Not a class definition start
    }
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
    | ARQEXEC EQ (STRING | arqExecCommand)
    ;

arqExecCommand
    : (IDENTIFIER | contextualKeyword) (IDENTIFIER | contextualKeyword | STAR)*
    ;

filePath
    : (IDENTIFIER | SLASH)+ SLASH?   // path like adm/ or obj/config/
    ;

// ============================================================================
// Class Definition
// ============================================================================

classDefinition
    : CLASSE className inheritClause? classMember*
    ;

inheritClause
    : HERDA classNameList
    ;

// Class names can be multi-word (e.g., "bom dia", "abc def")
// Uses IDENTIFIER+ so keywords stop the match naturally
// COMUM is allowed as a class name (common base class pattern: "herda comum")
className
    : IDENTIFIER+
    | COMUM
    ;

classNameList
    : className (COMMA className)*
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
    : {NotClassDefinitionStart()}? (
        variableDeclaration
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
    )
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
// IMPORTANT: Postfix ++ and -- only match if there's no newline before them.
// This prevents "x++\n++y" from being parsed as "x" with two postfix increments.
postfixOp
    : {NoNewlineBefore()}? PLUSPLUS
    | {NoNewlineBefore()}? MINUSMINUS
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
    | STRING+                          // Adjacent strings are concatenated: "abc" "def" = "abcdef"
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
// CLASSE can be used as identifier (e.g., const classe = "...") but the statement
// rule uses NotClassDefinitionStart() predicate to prevent it from starting statements
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
    | SAV        // sav can be used as identifier
    | NOVO       // novo can be used as identifier
    | ARG        // arg0-arg9 can be used as member name (e.g., contr.arg0)
    | COMUM      // comum can be used as class name in class references (comum:member)
    | PARA       // para can be used as function name (e.g., func para)
    | CLASSE     // Can be used as identifier (e.g., const classe = "name")
    ;

// Identifiers that can be used as member names (after .)
// CLASSE is included via contextualKeyword for expressions like obj.classe
memberIdentifier
    : IDENTIFIER
    | contextualKeyword
    | FUNC       // func can be used as member name (e.g., arg0.func)
    | CONST      // const can be used as member name (e.g., prog.const)
    ;
