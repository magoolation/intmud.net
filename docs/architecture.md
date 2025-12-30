# Arquitetura do IntMUD.NET

> **Nota**: Este documento descreve a arquitetura interna do IntMUD.NET, o porte para .NET do interpretador IntMUD original criado por **Edward Martin**.

---

## Visão Geral

O IntMUD.NET é composto por vários projetos que trabalham juntos para compilar e executar programas na linguagem IntMUD:

```
┌─────────────────────────────────────────────────────────────────┐
│                        IntMud.Console                           │
│                    (Aplicação Principal)                        │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                        IntMud.Hosting                           │
│              (Engine, Sessions, Event Handler)                  │
└─────────────────────────────────────────────────────────────────┘
                               │
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  IntMud.Compiler │  │  IntMud.Runtime  │  │ IntMud.Networking│
│   (ANTLR4 Parser)│  │  (Interpretador) │  │   (TCP/Telnet)   │
└──────────────────┘  └──────────────────┘  └──────────────────┘
          │                    │
          ▼                    ▼
┌──────────────────┐  ┌──────────────────┐
│   IntMud.Core    │  │   IntMud.Types   │
│  (Tipos Base)    │  │   (Handlers)     │
└──────────────────┘  └──────────────────┘
                               │
                               ▼
                      ┌──────────────────┐
                      │IntMud.Builtin    │
                      │   Functions      │
                      └──────────────────┘
```

---

## Projetos

### IntMud.Core

Contém os tipos e interfaces fundamentais usados por todos os outros projetos.

**Componentes principais:**
- `OpCode` - Enum com todos os opcodes de bytecode
- `VariableType` - Tipos de variáveis em runtime
- `IVariableTypeHandler` - Interface para handlers de tipos
- `IExecutionContext` - Interface para contexto de execução

```csharp
public enum OpCode : byte
{
    Herda = 0,
    Expr = 1,
    Se = 4,
    Senao1 = 5,
    FimSe = 7,
    Enquanto = 8,
    EPara = 9,
    // ... 67 opcodes total
}
```

### IntMud.Compiler

Responsável pela análise léxica, sintática e geração de bytecode.

**Pipeline de compilação:**

```
Código Fonte (.int)
        │
        ▼
┌──────────────────┐
│      Lexer       │ ← ANTLR4
│ (IntMudLexer.g4) │
└──────────────────┘
        │ Tokens
        ▼
┌──────────────────┐
│      Parser      │ ← ANTLR4
│(IntMudParser.g4) │
└──────────────────┘
        │ Parse Tree
        ▼
┌──────────────────┐
│   AST Visitor    │
│ParseTreeToAst    │
└──────────────────┘
        │ AST
        ▼
┌──────────────────┐
│ Bytecode Compiler│
└──────────────────┘
        │ CompiledUnit
        ▼
    Execução
```

**Arquivos de gramática:**
- `IntMudLexer.g4` - Definição de tokens (palavras-chave em português)
- `IntMudParser.g4` - Gramática da linguagem

```antlr
// Exemplos de regras do parser
classDeclaration
    : CLASSE IDENTIFIER inheritanceClause? classBody
    ;

functionDeclaration
    : FUNC IDENTIFIER functionBody
    ;

ifStatement
    : SE expression THEN? statement* elseClause* FIMSE
    ;
```

### IntMud.Runtime

O interpretador de bytecode que executa os programas compilados.

**Componentes principais:**

#### BytecodeInterpreter

A máquina virtual que executa o bytecode:

```csharp
public class BytecodeInterpreter
{
    private readonly Stack<RuntimeValue> _valueStack;
    private readonly Stack<VariableFrame> _variableStack;
    private readonly Stack<CallFrame> _callStack;

    public RuntimeValue Execute(CompiledFunction function);
    public RuntimeValue ExecuteFunctionWithThis(
        CompiledFunction function,
        BytecodeRuntimeObject thisObject,
        RuntimeValue[] args);
}
```

#### RuntimeValue

Representa valores em tempo de execução:

```csharp
public readonly struct RuntimeValue
{
    public RuntimeValueType Type { get; }
    public int IntValue { get; }
    public double DoubleValue { get; }
    public string? StringValue { get; }
    public object? ObjectValue { get; }
    public List<RuntimeValue>? ArrayValue { get; }
}
```

#### Pilhas de Execução

O interpretador usa três pilhas, similar ao IntMUD original:

1. **Value Stack** - Valores temporários durante expressões
2. **Variable Stack** - Variáveis locais de cada escopo
3. **Call Stack** - Frames de chamada de função

### IntMud.Types

Handlers para tipos especiais de variáveis.

**Estrutura de um handler:**

```csharp
public sealed class ArqTxtHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqTxt;
    public override string TypeName => "arqtxt";

    public override void Initialize(Span<byte> memory, ...);
    public override bool GetBool(ReadOnlySpan<byte> memory);
    public override int GetInt(ReadOnlySpan<byte> memory);
    public override string GetText(ReadOnlySpan<byte> memory);
    public override bool ExecuteFunction(
        Span<byte> memory,
        string functionName,
        IExecutionContext context);
}
```

**Handlers implementados:**
- `ArqTxtHandler` - Arquivos de texto
- `ArqMemHandler` - Buffer de memória
- `ArqDirHandler` - Diretórios
- `ArqLogHandler` - Arquivos de log
- `ArqSavHandler` - Save/Load de estado
- `ArqExecHandler` - Execução externa
- `ArqProgHandler` - Includes de programa
- `DebugHandler` - Depuração

### IntMud.BuiltinFunctions

Implementação das 99 funções builtin.

**Organização:**

```csharp
// TextFunctions.cs - 47 funções de texto
public static class TextFunctions
{
    public static RuntimeValue TxtMai(RuntimeValue[] args);
    public static RuntimeValue TxtMin(RuntimeValue[] args);
    public static RuntimeValue TxtTam(RuntimeValue[] args);
    public static RuntimeValue TxtCopia(RuntimeValue[] args);
    // ...
}

// MathFunctions.cs - 28 funções matemáticas
public static class MathFunctions
{
    public static RuntimeValue MatAbs(RuntimeValue[] args);
    public static RuntimeValue MatSeno(RuntimeValue[] args);
    public static RuntimeValue MatCos(RuntimeValue[] args);
    // ...
}

// ObjectFunctions.cs - Funções de objetos
public static class ObjectFunctions
{
    public static RuntimeValue Criar(RuntimeValue[] args);
    public static RuntimeValue Apagar(RuntimeValue[] args);
    public static RuntimeValue ObjAntes(RuntimeValue[] args);
    // ...
}
```

### IntMud.Networking

Camada de rede TCP/IP.

**Componentes:**
- `TcpListener` wrapper para conexões entrantes
- Suporte a protocolo Telnet
- Buffer de I/O assíncrono

### IntMud.Hosting

Engine principal e gerenciamento de sessões.

**Componentes principais:**

#### IntMudEngine

Orquestra toda a execução:

```csharp
public class IntMudEngine
{
    private readonly ScriptEventHandler _eventHandler;
    private readonly SessionManager _sessionManager;

    public async Task StartAsync();
    public async Task StopAsync();
}
```

#### ScriptEventHandler

Ponte entre eventos do servidor e funções do script:

```csharp
public class ScriptEventHandler
{
    public async Task<bool> OnConnectAsync(PlayerSession session);
    public async Task<bool> OnDisconnectAsync(PlayerSession session);
    public async Task<bool> OnCommandAsync(
        PlayerSession session,
        string command,
        string args);
    public async Task OnTickAsync();
}
```

#### SessionManager

Gerencia sessões de jogadores:

```csharp
public class SessionManager
{
    private readonly Dictionary<int, PlayerSession> _sessions;

    public PlayerSession CreateSession(TcpClient client);
    public void RemoveSession(int sessionId);
    public IEnumerable<PlayerSession> GetAllSessions();
}
```

#### PlayerSession

Representa uma conexão de jogador:

```csharp
public class PlayerSession
{
    public int Id { get; }
    public TcpClient Client { get; }
    public StringBuilder OutputBuffer { get; }

    public void QueueOutput(string text);
    public async Task FlushOutputAsync();
}
```

---

## Fluxo de Execução

### 1. Inicialização

```
1. Carregar arquivos .int do diretório fonte
2. Compilar cada arquivo:
   a. Lexer → Tokens
   b. Parser → Parse Tree
   c. Visitor → AST
   d. Compiler → CompiledUnit
3. Criar BytecodeInterpreter com todas as classes
4. Iniciar servidor TCP
5. Executar função 'inicializar' da classe 'main'
```

### 2. Conexão de Jogador

```
1. TcpListener aceita conexão
2. SessionManager cria PlayerSession
3. ScriptEventHandler.OnConnectAsync() chamado
4. BytecodeInterpreter executa 'aoconectar'
5. Output enviado para o cliente
```

### 3. Comando de Jogador

```
1. Dados recebidos do socket
2. Comando parseado (comando + argumentos)
3. ScriptEventHandler.OnCommandAsync() chamado
4. BytecodeInterpreter executa 'aocomando'
   a. arg0 = ID da sessão
   b. arg1 = comando
   c. arg2 = argumentos
5. Retorno indica se comando foi tratado
6. Output enviado para o cliente
```

### 4. Execução de Bytecode

```
while (instructionPointer < bytecode.Length)
{
    opcode = ReadOpCode();

    switch (opcode)
    {
        case OpCode.Push:
            _valueStack.Push(ReadValue());
            break;

        case OpCode.Call:
            args = PopArguments();
            result = CallFunction(functionName, args);
            _valueStack.Push(result);
            break;

        case OpCode.Se:
            condition = _valueStack.Pop();
            if (!condition.IsTruthy)
                JumpToElseOrEndIf();
            break;

        // ... outros opcodes
    }
}
```

---

## Sistema de Tipos

### Tipos Primitivos

| Tipo IntMUD | Tipo .NET | Tamanho |
|-------------|-----------|---------|
| int1 | bool | 1 bit |
| int8 | sbyte | 1 byte |
| uint8 | byte | 1 byte |
| int16 | short | 2 bytes |
| uint16 | ushort | 2 bytes |
| int32 | int | 4 bytes |
| uint32 | uint | 4 bytes |
| real | float | 4 bytes |
| real2 | double | 8 bytes |
| txt1-512 | string | variável |
| ref | object | referência |

### RuntimeValue

Tipo union que pode representar qualquer valor:

```csharp
public readonly struct RuntimeValue
{
    // Tipo discriminador
    public RuntimeValueType Type { get; }

    // Valores (apenas um é válido por vez)
    public int IntValue { get; }
    public double DoubleValue { get; }
    public string? StringValue { get; }
    public object? ObjectValue { get; }
    public List<RuntimeValue>? ArrayValue { get; }

    // Propriedades auxiliares
    public bool IsTruthy => Type switch
    {
        RuntimeValueType.Null => false,
        RuntimeValueType.Integer => IntValue != 0,
        RuntimeValueType.Double => DoubleValue != 0,
        RuntimeValueType.String => !string.IsNullOrEmpty(StringValue),
        _ => ObjectValue != null
    };
}
```

---

## Extensibilidade

### Adicionando Nova Função Builtin

1. Adicionar método em `IntMud.BuiltinFunctions`:

```csharp
public static RuntimeValue MinhaFuncao(RuntimeValue[] args)
{
    // Validar argumentos
    if (args.Length < 1)
        return RuntimeValue.Null;

    // Implementar lógica
    var resultado = args[0].IntValue * 2;

    return RuntimeValue.FromInt(resultado);
}
```

2. Registrar no `BuiltinFunctionRegistry`:

```csharp
_functions["minhafuncao"] = TextFunctions.MinhaFuncao;
```

### Adicionando Novo Tipo de Variável

1. Criar handler em `IntMud.Types`:

```csharp
public sealed class MeuTipoHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.MeuTipo;
    public override string TypeName => "meutipo";

    // Implementar métodos necessários...
}
```

2. Adicionar OpCode em `IntMud.Core`:

```csharp
public enum OpCode : byte
{
    // ...
    MeuTipo = 67,
}
```

3. Registrar no handler registry.

---

## Performance

### Otimizações Implementadas

1. **Bytecode Compilation**
   - Código é compilado uma vez e reutilizado
   - Bytecode é compacto e eficiente

2. **Stack-based VM**
   - Operações de pilha são O(1)
   - Sem alocações durante expressões simples

3. **String Interning**
   - Strings literais são internadas
   - Comparações por referência quando possível

4. **Pooling**
   - Reutilização de buffers de I/O
   - Pool de RuntimeValue arrays

### Comparação com Original C++

| Aspecto | IntMUD C++ | IntMUD.NET |
|---------|------------|------------|
| Startup | Mais rápido | GC warmup |
| Steady-state | Comparável | Comparável |
| Memória | Menor footprint | Mais headroom |
| Threading | Manual | Async/await |

---

## Créditos

A arquitetura do IntMUD.NET é inspirada diretamente no design original do IntMUD C++ criado por **Edward Martin**. Os conceitos fundamentais - pilhas de execução, sistema de tipos, handlers de variáveis - foram preservados para manter compatibilidade total.

- **Autor Original**: Edward Martin (edx2martin@gmail.com)
- **Website**: https://intervox.nce.ufrj.br/~e2mar/
