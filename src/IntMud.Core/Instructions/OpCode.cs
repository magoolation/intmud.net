namespace IntMud.Core.Instructions;

/// <summary>
/// Bytecode operation codes - maps to Instr::Comando enum from the original C++ implementation.
/// These codes define the type of each instruction in the compiled bytecode.
/// </summary>
public enum OpCode : byte
{
    // ========== Common Instructions ==========

    /// <summary>Inheritance declaration (herda)</summary>
    Herda = 0,

    /// <summary>Pure expression statement</summary>
    Expr = 1,

    /// <summary>Comment line</summary>
    Coment = 2,

    /// <summary>Reference variable (refvar)</summary>
    RefVar = 3,

    // ========== Control Flow ==========

    /// <summary>If statement (se)</summary>
    Se = 4,

    /// <summary>Else without condition (senao)</summary>
    Senao1 = 5,

    /// <summary>Else with condition (senao expr)</summary>
    Senao2 = 6,

    /// <summary>End if (fimse)</summary>
    FimSe = 7,

    /// <summary>While loop (enquanto)</summary>
    Enquanto = 8,

    /// <summary>For loop (epara)</summary>
    EPara = 9,

    /// <summary>End while/for (efim)</summary>
    EFim = 10,

    /// <summary>Switch statement (casovar)</summary>
    CasoVar = 11,

    /// <summary>Case with text pattern (casose)</summary>
    CasoSe = 12,

    /// <summary>Default case (casose without pattern)</summary>
    CasoSePadrao = 13,

    /// <summary>End switch (casofim)</summary>
    CasoFim = 14,

    /// <summary>Return without value (ret)</summary>
    Ret1 = 15,

    /// <summary>Return with expression (ret expr)</summary>
    Ret2 = 16,

    /// <summary>Break without condition (sair)</summary>
    Sair1 = 17,

    /// <summary>Break with condition (sair expr)</summary>
    Sair2 = 18,

    /// <summary>Continue without condition (continuar)</summary>
    Continuar1 = 19,

    /// <summary>Continue with condition (continuar expr)</summary>
    Continuar2 = 20,

    /// <summary>Terminate program (terminar)</summary>
    Terminar = 21,

    // ========== Variable Section Marker ==========

    /// <summary>Start of variables section</summary>
    Variaveis = 22,

    // ========== Text Types ==========

    /// <summary>Text 1-256 characters (txt1-txt256)</summary>
    Txt1 = 23,

    /// <summary>Text 257-512 characters (txt257-txt512)</summary>
    Txt2 = 24,

    // ========== Integer Types ==========

    /// <summary>1-bit integer (int1)</summary>
    Int1 = 25,

    /// <summary>8-bit signed integer (int8)</summary>
    Int8 = 26,

    /// <summary>8-bit unsigned integer (uint8)</summary>
    UInt8 = 27,

    /// <summary>16-bit signed integer (int16)</summary>
    Int16 = 28,

    /// <summary>16-bit unsigned integer (uint16)</summary>
    UInt16 = 29,

    /// <summary>32-bit signed integer (int32)</summary>
    Int32 = 30,

    /// <summary>32-bit unsigned integer (uint32)</summary>
    UInt32 = 31,

    // ========== Floating Point Types ==========

    /// <summary>Single precision float (real)</summary>
    Real = 32,

    /// <summary>Double precision float (real2)</summary>
    Real2 = 33,

    // ========== Constants ==========

    /// <summary>Null constant (const x = nulo)</summary>
    ConstNulo = 34,

    /// <summary>Text constant (const x = "text")</summary>
    ConstTxt = 35,

    /// <summary>Numeric constant (const x = 123)</summary>
    ConstNum = 36,

    /// <summary>Expression constant (const x = expr)</summary>
    ConstExpr = 37,

    // ========== Functions ==========

    /// <summary>Function definition (func)</summary>
    Func = 38,

    /// <summary>Variable function (varfunc)</summary>
    VarFunc = 39,

    // ========== Reference Types ==========

    /// <summary>Object reference (ref)</summary>
    Ref = 40,

    /// <summary>Object list (listaobj)</summary>
    ListaObj = 41,

    /// <summary>List iterator (listaitem)</summary>
    ListaItem = 42,

    // ========== Text Container Types ==========

    /// <summary>Multi-line text (textotxt)</summary>
    TextoTxt = 43,

    /// <summary>Text position (textopos)</summary>
    TextoPos = 44,

    /// <summary>Text with variables (textovar)</summary>
    TextoVar = 45,

    /// <summary>Text with object reference (textoobj)</summary>
    TextoObj = 46,

    /// <summary>Object name index (nomeobj)</summary>
    NomeObj = 47,

    // ========== File Types ==========

    /// <summary>Directory handle (arqdir)</summary>
    ArqDir = 48,

    /// <summary>Log file (arqlog)</summary>
    ArqLog = 49,

    /// <summary>Program info (arqprog)</summary>
    ArqProg = 50,

    /// <summary>External execution (arqexec)</summary>
    ArqExec = 51,

    /// <summary>Save file (arqsav)</summary>
    ArqSav = 52,

    /// <summary>Text file (arqtxt)</summary>
    ArqTxt = 53,

    /// <summary>Memory buffer (arqmem)</summary>
    ArqMem = 54,

    // ========== Time/Execution Types ==========

    /// <summary>Timer (inttempo)</summary>
    IntTempo = 55,

    /// <summary>Execution counter (intexec)</summary>
    IntExec = 56,

    /// <summary>Increment counter (intinc)</summary>
    IntInc = 57,

    /// <summary>Decrement counter (intdec)</summary>
    IntDec = 58,

    // ========== I/O Types ==========

    /// <summary>Text screen/terminal (telatxt)</summary>
    TelaTxt = 59,

    /// <summary>Network socket (socket)</summary>
    Socket = 60,

    /// <summary>Server socket (serv)</summary>
    Serv = 61,

    // ========== Special Types ==========

    /// <summary>Program control (prog)</summary>
    Prog = 62,

    /// <summary>Debug info (debug)</summary>
    Debug = 63,

    /// <summary>Object index (indiceobj)</summary>
    IndiceObj = 64,

    /// <summary>Index iterator (indiceitem)</summary>
    IndiceItem = 65,

    /// <summary>Date/time (datahora)</summary>
    DataHora = 66,

    // ========== End Marker ==========

    /// <summary>End of instruction list</summary>
    Fim = 255
}
