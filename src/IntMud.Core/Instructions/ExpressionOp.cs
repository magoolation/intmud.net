namespace IntMud.Core.Instructions;

/// <summary>
/// Expression operators - maps to Instr::Expressao enum from the original C++ implementation.
/// These codes are used within expression bytecode to represent operations.
/// </summary>
public enum ExpressionOp : byte
{
    // ========== Control ==========

    /// <summary>End of expression</summary>
    Fim = 0,

    /// <summary>Comment within expression</summary>
    Coment = 1,

    // ========== Escape Sequences ==========

    /// <summary>\n - newline</summary>
    BarraN = 2,

    /// <summary>\b - backspace marker</summary>
    BarraB = 3,

    /// <summary>\c - color code</summary>
    BarraC = 4,

    /// <summary>\d - data marker</summary>
    BarraD = 5,

    // ========== Variable Access ==========

    /// <summary>Start of variable reference</summary>
    VarIni = 6,

    /// <summary>End of variable reference</summary>
    VarFim = 7,

    /// <summary>Class separator (:)</summary>
    DoisPontos = 8,

    /// <summary>Member access (.)</summary>
    Ponto = 9,

    /// <summary>Function arguments ()</summary>
    Arg = 10,

    /// <summary>Array indexer []</summary>
    Colchetes = 11,

    // ========== Operators ==========

    /// <summary>Addition (+)</summary>
    Add = 20,

    /// <summary>Subtraction (-)</summary>
    Sub = 21,

    /// <summary>Multiplication (*)</summary>
    Mul = 22,

    /// <summary>Division (/)</summary>
    Div = 23,

    /// <summary>Modulo (%)</summary>
    Mod = 24,

    /// <summary>Bitwise AND (&amp;)</summary>
    And = 25,

    /// <summary>Bitwise OR (|)</summary>
    Or = 26,

    /// <summary>Bitwise XOR (^)</summary>
    Xor = 27,

    /// <summary>Left shift (&lt;&lt;)</summary>
    Shl = 28,

    /// <summary>Right shift (&gt;&gt;)</summary>
    Shr = 29,

    // ========== Comparison ==========

    /// <summary>Equal (==)</summary>
    Igual = 30,

    /// <summary>Not equal (!=)</summary>
    Diferente = 31,

    /// <summary>Less than (&lt;)</summary>
    Menor = 32,

    /// <summary>Greater than (&gt;)</summary>
    Maior = 33,

    /// <summary>Less than or equal (&lt;=)</summary>
    MenorIgual = 34,

    /// <summary>Greater than or equal (&gt;=)</summary>
    MaiorIgual = 35,

    /// <summary>Strict equal (===)</summary>
    IgualTipo = 36,

    /// <summary>Strict not equal (!==)</summary>
    DiferenteTipo = 37,

    // ========== Logical ==========

    /// <summary>Logical AND (&amp;&amp;)</summary>
    LogicoE = 38,

    /// <summary>Logical OR (||)</summary>
    LogicoOu = 39,

    /// <summary>Logical NOT (!)</summary>
    LogicoNao = 40,

    // ========== Unary ==========

    /// <summary>Unary minus (-x)</summary>
    Neg = 41,

    /// <summary>Bitwise complement (~x)</summary>
    Complemento = 42,

    /// <summary>Pre-increment (++x)</summary>
    PreInc = 43,

    /// <summary>Pre-decrement (--x)</summary>
    PreDec = 44,

    /// <summary>Post-increment (x++)</summary>
    PosInc = 45,

    /// <summary>Post-decrement (x--)</summary>
    PosDec = 46,

    // ========== Assignment ==========

    /// <summary>Assignment (=)</summary>
    Atrib = 50,

    /// <summary>Add assignment (+=)</summary>
    AtribAdd = 51,

    /// <summary>Subtract assignment (-=)</summary>
    AtribSub = 52,

    /// <summary>Multiply assignment (*=)</summary>
    AtribMul = 53,

    /// <summary>Divide assignment (/=)</summary>
    AtribDiv = 54,

    /// <summary>Modulo assignment (%=)</summary>
    AtribMod = 55,

    /// <summary>AND assignment (&amp;=)</summary>
    AtribAnd = 56,

    /// <summary>OR assignment (|=)</summary>
    AtribOr = 57,

    /// <summary>XOR assignment (^=)</summary>
    AtribXor = 58,

    /// <summary>Left shift assignment (&lt;&lt;=)</summary>
    AtribShl = 59,

    /// <summary>Right shift assignment (&gt;&gt;=)</summary>
    AtribShr = 60,

    // ========== Ternary ==========

    /// <summary>Ternary condition (?)</summary>
    Ternario = 61,

    /// <summary>Ternary separator (:)</summary>
    TernarioDois = 62,

    /// <summary>Null coalescing (??)</summary>
    NullCoalesce = 63,

    // ========== Special ==========

    /// <summary>Comma operator (,)</summary>
    Virgula = 64,

    // ========== Literals ==========

    /// <summary>Integer literal</summary>
    NumInt = 70,

    /// <summary>Float/double literal</summary>
    NumReal = 71,

    /// <summary>Text literal</summary>
    Texto = 72,

    /// <summary>Null literal (nulo)</summary>
    Nulo = 73,

    // ========== References ==========

    /// <summary>Class reference ($ClassName)</summary>
    Classe = 80,

    /// <summary>Dynamic class access ([expr])</summary>
    ClasseDinamica = 81,

    /// <summary>This object (este)</summary>
    Este = 82
}
