# Protocolos e Especificações

> **Nota**: Esta documentação é baseada no documento original `protocolos.txt` do IntMUD, escrito por **Edward Martin**.

---

## Índice

1. [Codificação de Caracteres](#codificação-de-caracteres)
2. [Sistema de Cores IntMUD](#sistema-de-cores-intmud)
3. [Cores ANSI (Telnet)](#cores-ansi-telnet)
4. [Protocolo Telnet](#protocolo-telnet)
5. [Comandos de Terminal](#comandos-de-terminal)
6. [Protocolo IRC](#protocolo-irc)
7. [Protocolo Papovox](#protocolo-papovox)

---

## Codificação de Caracteres

### Caracteres Modificados por txtcod()

A função `txtcod()` converte caracteres especiais para sequências de escape:

| Caractere | Codificado |
|-----------|------------|
| `@` `/b` `/c` `/d` `/n` | `@@` `@b` `@c` `@d` `@n` |
| `"` `!` `#` `$` `%` | `@a` `@e` `@f` `@g` `@h` |
| `&` `'` `(` `)` `*` | `@i` `@j` `@k` `@l` `@m` |
| `+` `,` `-` `.` `/` | `@o` `@p` `@q` `@r` `@s` |
| `:` `;` `<` `=` `>` | `@t` `@u` `@v` `@w` `@x` |
| `?` `[` `\` `]` `^` | `@y` `@z` `@0` `@1` `@2` |
| `` ` `` `{` `|` `}` `~` | `@3` `@4` `@5` `@6` `@7` |

---

## Sistema de Cores IntMUD

### Códigos de Cor Simplificados

O IntMUD.NET suporta códigos de cor em chaves:

```intmud
escrevaln("{red}Texto vermelho{reset}")
escrevaln("{green}Texto verde{reset}")
escrevaln("{blue}Texto azul{reset}")
```

### Tabela de Cores Disponíveis

| Código | Cor | ANSI |
|--------|-----|------|
| `{black}` | Preto | 30 |
| `{red}` | Vermelho | 31 |
| `{green}` | Verde | 32 |
| `{yellow}` | Amarelo | 33 |
| `{blue}` | Azul | 34 |
| `{magenta}` | Magenta | 35 |
| `{cyan}` | Ciano | 36 |
| `{white}` | Branco | 37 |
| `{brightblack}` | Cinza | 90 |
| `{brightred}` | Vermelho Brilhante | 91 |
| `{brightgreen}` | Verde Brilhante | 92 |
| `{brightyellow}` | Amarelo Brilhante | 93 |
| `{brightblue}` | Azul Brilhante | 94 |
| `{brightmagenta}` | Magenta Brilhante | 95 |
| `{brightcyan}` | Ciano Brilhante | 96 |
| `{brightwhite}` | Branco Brilhante | 97 |

### Modificadores

| Código | Efeito |
|--------|--------|
| `{reset}` | Resetar todas as cores |
| `{bold}` | Negrito |
| `{dim}` | Escuro |
| `{underline}` | Sublinhado |
| `{blink}` | Piscando |
| `{reverse}` | Inverter cores |

### Exemplo Prático

```intmud
func mostrar_status
  escrevaln("{bold}{cyan}=== Status do Jogador ==={reset}")
  escrevaln("")
  escrevaln("Nome: {yellow}Aventureiro{reset}")
  escrevaln("Vida: {red}100{reset}/{green}100{reset}")
  escrevaln("Nível: {brightcyan}5{reset}")
  escrevaln("")
  ret 1
```

### Cores Originais do IntMUD

O sistema original usava códigos `\c` e `\d`:

| Código | Cor |
|--------|-----|
| `\b` | Branco com fundo preto |
| `\c0` | Preto |
| `\c1` | Vermelho |
| `\c2` | Verde |
| `\c3` | Marrom |
| `\c4` | Azul |
| `\c5` | Magenta |
| `\c6` | Ciano |
| `\c7` | Branco |
| `\c8` | Cinza |
| `\c9` | Vermelho intenso |
| `\cA` | Verde intenso |
| `\cB` | Amarelo |
| `\cC` | Azul intenso |
| `\cD` | Magenta intenso |
| `\cE` | Ciano intenso |
| `\cF` | Branco intenso |
| `\d0` - `\d7` | Cor de fundo |

---

## Cores ANSI (Telnet)

### Formato

As cores ANSI usam sequências de escape:

```
ESC [ <parâmetros> m
```

Onde `ESC` é o caractere 0x1B (27 decimal).

### Parâmetros

| Código | Efeito |
|--------|--------|
| `0` | Reset (cores padrão) |
| `1` | Negrito |
| `4` | Sublinhado |
| `5` | Piscando |
| `30-37` | Cor de frente (0-7) |
| `40-47` | Cor de fundo (0-7) |
| `90-97` | Cor de frente brilhante |

### Exemplos

```
\x1b[31m      # Vermelho
\x1b[1;32m    # Verde negrito
\x1b[34;47m   # Azul com fundo branco
\x1b[0m       # Reset
```

### Implementação .NET

```csharp
public static class AnsiColors
{
    public const string Reset = "\x1b[0m";
    public const string Red = "\x1b[31m";
    public const string Green = "\x1b[32m";
    public const string Yellow = "\x1b[33m";
    public const string Blue = "\x1b[34m";
    public const string Magenta = "\x1b[35m";
    public const string Cyan = "\x1b[36m";
    public const string White = "\x1b[37m";

    public static string ParseColorCodes(string text)
    {
        return text
            .Replace("{red}", Red)
            .Replace("{green}", Green)
            .Replace("{yellow}", Yellow)
            .Replace("{blue}", Blue)
            .Replace("{magenta}", Magenta)
            .Replace("{cyan}", Cyan)
            .Replace("{white}", White)
            .Replace("{reset}", Reset);
    }
}
```

---

## Protocolo Telnet

### Mensagens Especiais

| Hex | Descrição |
|-----|-----------|
| `FF FB 01` | Echo OFF (não ecoar o que usuário digita) |
| `FF FC 01` | Echo ON (ecoar o que usuário digita) |
| `FF F9` | Go Ahead (identificador de prompt) |
| `07` | Beep (som) |
| `FF FF` | Caractere FF literal |

### Negociação de Opções

```
IAC WILL ECHO    = FF FB 01  (servidor vai ecoar)
IAC WONT ECHO    = FF FC 01  (servidor não vai ecoar)
IAC DO ECHO      = FF FD 01  (cliente quer que servidor ecoe)
IAC DONT ECHO    = FF FE 01  (cliente não quer eco)
```

### Implementação .NET

```csharp
public static class TelnetCommands
{
    public static readonly byte[] EchoOff = { 0xFF, 0xFB, 0x01 };
    public static readonly byte[] EchoOn = { 0xFF, 0xFC, 0x01 };
    public static readonly byte[] GoAhead = { 0xFF, 0xF9 };
    public static readonly byte[] Beep = { 0x07 };
}
```

---

## Comandos de Terminal

### Movimentação do Cursor

| Sequência | Descrição |
|-----------|-----------|
| `\r` (CR) | Início da linha atual |
| `\b` (C-b) | Um caractere para trás |
| `ESC[R;CH` | Move para linha R, coluna C |
| `ESC[NA` | Move N linhas para cima |
| `ESC[NB` | Move N linhas para baixo |
| `ESC[NC` | Move N colunas para direita |
| `ESC[ND` | Move N colunas para esquerda |

### Limpeza de Tela

| Sequência | Descrição |
|-----------|-----------|
| `ESC[J` ou `ESC[0J` | Limpa do cursor até o fim da tela |
| `ESC[1J` | Limpa do início até o cursor |
| `ESC[2J` | Limpa tela inteira |
| `ESC[K` ou `ESC[0K` | Limpa do cursor até fim da linha |
| `ESC[1K` | Limpa do início da linha até cursor |
| `ESC[2K` | Limpa linha inteira |

### Inserção e Deleção

| Sequência | Descrição |
|-----------|-----------|
| `ESC[NL` | Insere N linhas em branco |
| `ESC[NM` | Deleta N linhas |
| `ESC[NP` | Deleta N caracteres |
| `ESC[N@` | Insere N espaços |

---

## Protocolo IRC

### Formato de Cores

```
Ctrl-C         -> Limpa cores
Ctrl-C N       -> Cor de frente N
Ctrl-C NN      -> Cor de frente NN
Ctrl-C N,M     -> Frente N, fundo M
Ctrl-C NN,MM   -> Frente NN, fundo MM
```

Onde `Ctrl-C` é o caractere 0x03.

### Tabela de Cores IRC

| Código | Cor |
|--------|-----|
| 0 | Branco |
| 1 | Preto |
| 2 | Azul (navy) |
| 3 | Verde |
| 4 | Vermelho |
| 5 | Marrom |
| 6 | Roxo |
| 7 | Laranja |
| 8 | Amarelo |
| 9 | Verde claro |
| 10 | Teal |
| 11 | Ciano claro |
| 12 | Azul claro |
| 13 | Rosa |
| 14 | Cinza |
| 15 | Cinza claro |

---

## Protocolo Papovox

O Papovox é um protocolo binário proprietário usado pelo IntMUD original.

### Formato de Conexão

1. Cliente conecta
2. Servidor responde com `+OK\r\n` ou `-ERRO\r\n`
3. Cliente envia identificação terminada com `\r\n`
4. Servidor confirma com `+OK\r\n`

### Formato de Mensagens

Após a conexão, todas as mensagens seguem o formato:

```
[1 byte: tipo] [2 bytes: tamanho] [N bytes: dados]
```

| Campo | Tamanho | Descrição |
|-------|---------|-----------|
| Tipo | 1 byte | Tipo da mensagem (0x01 = texto) |
| Tamanho LSB | 1 byte | Byte menos significativo |
| Tamanho MSB | 1 byte | Byte mais significativo |
| Dados | N bytes | Conteúdo da mensagem |

**Tamanho** = MSB × 256 + LSB

### Exemplo

```
Servidor envia: 01 05 00 4E 6F 6D 65 3F
                │  │  │  └─────────────── "Nome?"
                │  │  └── Tamanho MSB (0)
                │  └── Tamanho LSB (5)
                └── Tipo (texto)

Tamanho = 0 × 256 + 5 = 5 bytes
```

---

## Referências

- **Terminal ANSI**: http://www.xemacs.org/Documentation/21.5/html/term_1.html
- **Cores IRC**: http://www.mirc.co.uk/help/color.txt
- **Telnet RFC**: https://tools.ietf.org/html/rfc854

---

## Créditos

Esta documentação é baseada no trabalho original de **Edward Martin**, criador do IntMUD.

- **Autor Original**: Edward Martin (edx2martin@gmail.com)
- **Website**: https://intervox.nce.ufrj.br/~e2mar/
