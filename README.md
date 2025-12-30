# IntMUD.NET

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--2.0-blue.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-164%20passing-brightgreen)]()

**IntMUD.NET** é um porte completo do interpretador IntMUD original, escrito em C++, para a plataforma .NET.

---

## Créditos e Reconhecimento

Este projeto é um **porte** do extraordinário trabalho de **Edward Martin** (edx2martin@gmail.com), criador do IntMUD original.

O IntMUD original é uma obra impressionante de engenharia de software que inclui:
- Um interpretador completo para uma linguagem de programação própria
- Suporte a múltiplos protocolos de rede (Telnet, IRC, Papovox)
- Um sistema completo para criação de MUDs com salas, personagens, itens, habilidades e efeitos
- Documentação extensa e tutoriais detalhados
- Anos de desenvolvimento e refinamento

**Nossos sinceros agradecimentos a Edward Martin** por criar e disponibilizar este projeto incrível que inspirou e possibilitou este porte para .NET.

- **Projeto Original**: [IntMUD C++](https://intervox.nce.ufrj.br/~e2mar/)
- **Autor Original**: Edward Martin
- **Contato**: edx2martin@gmail.com
- **Lista de Discussão**: [Google Groups - IntMUD](http://groups.google.com/group/intmud/)

---

## Sobre o IntMUD

O objetivo principal do IntMUD é a criação de **MUDs** (Multi-User Dungeon), mas também é possível criar outros tipos de jogos e aplicações baseados em texto.

O IntMUD é um interpretador de uma linguagem de programação própria, projetada especificamente para o desenvolvimento de jogos multiplayer baseados em texto. A linguagem utiliza palavras-chave em **português**, tornando-a especialmente acessível para desenvolvedores lusófonos.

### Características da Linguagem

- **Palavras-chave em português**: `classe`, `func`, `se`, `senao`, `enquanto`, `para cada`, etc.
- **Orientada a objetos**: Classes com herança e polimorfismo
- **99 funções builtin**: Manipulação de texto, matemática, objetos e mais
- **Tipos de dados completos**: Inteiros, reais, texto, referências, arrays
- **Sistema de eventos**: Para jogos interativos em tempo real
- **Suporte a rede**: Sockets TCP/IP para conexões multiplayer

---

## Por que .NET?

Este porte para .NET oferece várias vantagens:

| Característica | Benefício |
|----------------|-----------|
| **Cross-platform** | Roda em Windows, Linux e macOS |
| **Performance** | JIT compilation e otimizações do runtime .NET |
| **Modernidade** | Async/await, LINQ, e recursos modernos de C# |
| **Ecosistema** | Integração com bibliotecas .NET e NuGet |
| **Tooling** | Suporte completo em IDEs como Visual Studio e VS Code |
| **Manutenibilidade** | Código limpo, tipado e bem estruturado |

---

## Requisitos

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) ou superior
- Windows, Linux ou macOS

---

## Instalação

### Compilando do Código Fonte

```bash
# Clonar o repositório
git clone https://github.com/seu-usuario/intmud.net.git
cd intmud.net

# Restaurar dependências
dotnet restore

# Compilar
dotnet build

# Executar testes
dotnet test
```

### Executando o Servidor MUD

```bash
# Navegar para o diretório do console
cd src/IntMud.Console

# Executar com o MUD de exemplo
dotnet run -- --source ../../samples/simple-mud --port 4000
```

### Conectando ao MUD

```bash
# Via Telnet
telnet localhost 4000

# Ou via PuTTY, MUSHclient, ou qualquer cliente Telnet
```

---

## Estrutura do Projeto

```
intmud.net/
├── src/
│   ├── IntMud.Core/              # Tipos e interfaces fundamentais
│   ├── IntMud.Compiler/          # Lexer, Parser e Compilador (ANTLR4)
│   ├── IntMud.Runtime/           # Interpretador de bytecode
│   ├── IntMud.Types/             # Handlers de tipos de variáveis
│   ├── IntMud.BuiltinFunctions/  # 99 funções builtin
│   ├── IntMud.Networking/        # Camada de rede TCP/IP
│   ├── IntMud.Hosting/           # Host do servidor MUD
│   └── IntMud.Console/           # Aplicação console
├── tests/
│   ├── IntMud.Compiler.Tests/    # Testes do compilador
│   ├── IntMud.Runtime.Tests/     # Testes do runtime
│   └── IntMud.Integration.Tests/ # Testes de integração
├── samples/
│   └── simple-mud/               # MUD de exemplo
├── docs/                         # Documentação
└── tools/
    └── IntMud.EncodingMigrator/  # Ferramenta de migração de encoding
```

---

## Documentação

| Documento | Descrição |
|-----------|-----------|
| [Manual da Linguagem](docs/manual.md) | Referência completa da linguagem IntMUD |
| [Tutorial](docs/tutorial.md) | Guia passo a passo para iniciantes |
| [Arquitetura](docs/architecture.md) | Arquitetura interna do IntMUD.NET |
| [Protocolos](docs/protocols.md) | Especificações de rede e terminal |
| [FAQ](docs/faq.md) | Perguntas frequentes |

### Documentação do MUD

| Documento | Descrição |
|-----------|-----------|
| [Ativar o MUD](docs/mud/01-ativar-o-mud.md) | Como iniciar o servidor |
| [Classes](docs/mud/04-classes.md) | Sistema de classes |
| [Variáveis](docs/mud/05-variaveis.md) | Tipos de variáveis |
| [Salas](docs/mud/07-salas.md) | Sistema de salas/locais |
| [Personagens](docs/mud/08-personagens.md) | Sistema de personagens |
| [Itens](docs/mud/09-itens.md) | Sistema de itens |
| [Eventos](docs/mud/13-eventos.md) | Sistema de eventos |
| [Comandos](docs/mud/14-comandos.md) | Sistema de comandos |

---

## Exemplo Rápido

### Olá Mundo

```intmud
classe main

func inicializar
  escrevaln("Olá, Mundo!")
  ret 1
```

### Servidor de Chat Simples

```intmud
classe chat

serv servidor
int32 clientes

func inicializar
  clientes = vetor(100)
  servidor.iniciar(4000)
  escrevaln("Servidor iniciado na porta 4000")
  ret 1

func aoconectar
  int32 id
  id = arg0
  clientes[id] = 1
  escrevaln("Cliente " + id + " conectou")
  ret 1

func aocomando
  int32 id
  txt256 msg
  id = arg0
  msg = arg1

  # Enviar mensagem para todos
  para cada cliente em clientes
    se cliente != nulo
      enviar(cliente, msg)
    fimse
  proximo
  ret 1
```

### MUD com Salas

```intmud
classe main

int32 salas

func inicializar
  salas = vetor(10)
  salas[0] = "Entrada do Castelo"
  salas[1] = "Salão Principal"
  salas[2] = "Cozinha"
  ret 1

func aoconectar
  escrevaln("{cyan}=== Bem-vindo ao MUD! ==={reset}")
  escrevaln("Você está na entrada de um antigo castelo.")
  escrevaln("Digite {yellow}olhar{reset} para ver a sala.")
  ret 1

func aocomando
  txt256 cmd
  cmd = arg1

  se cmd == "olhar"
    escrevaln("Você olha ao redor...")
    escrevaln("Uma porta leva ao {yellow}norte{reset}.")
    ret 1
  fimse

  ret 0
```

---

## Compatibilidade

O IntMUD.NET mantém **100% de compatibilidade** com a linguagem do IntMUD original:

- ✅ Todas as palavras-chave da linguagem
- ✅ Todos os operadores (aritméticos, lógicos, bitwise)
- ✅ Todas as 99 funções builtin
- ✅ Todos os tipos de variáveis
- ✅ Todas as estruturas de controle
- ✅ Sistema de arquivos (arqtxt, arqmem, arqdir, arqlog, arqsav, arqexec, arqprog)
- ✅ Sistema de depuração (debug)
- ✅ Protocolos de rede (Telnet com cores ANSI)

Scripts escritos para o IntMUD original devem funcionar no IntMUD.NET sem modificações.

---

## Contribuindo

Contribuições são bem-vindas! Por favor:

1. Faça um fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/nova-feature`)
3. Commit suas mudanças (`git commit -m 'Adiciona nova feature'`)
4. Push para a branch (`git push origin feature/nova-feature`)
5. Abra um Pull Request

---

## Licença

Este projeto é licenciado sob a **GNU General Public License v2.0** - veja o arquivo [LICENSE](LICENSE) para detalhes.

O IntMUD original também é distribuído sob GPL-2.0, e este porte mantém a mesma licença em respeito ao trabalho original de Edward Martin.

---

## Links Úteis

- **IntMUD Original**: https://intervox.nce.ufrj.br/~e2mar/
- **Lista de Discussão**: http://groups.google.com/group/intmud/
- **Documentação Original**: Incluída no diretório `doc/` do IntMUD original

---

<p align="center">
  <i>Este projeto é dedicado a Edward Martin, cuja visão e trabalho duro tornaram o IntMUD uma realidade.</i>
</p>
