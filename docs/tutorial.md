# Tutorial IntMUD.NET

> **Nota**: Este tutorial é baseado no tutorial original do IntMUD escrito por **Edward Martin**. O IntMUD.NET é um porte fiel da implementação original em C++, adaptado para a plataforma .NET.

---

## Sobre Este Tutorial

O objetivo deste tutorial é explicar os primeiros passos de como criar programas usando o IntMUD.NET. Vamos começar com exemplos simples e progressivamente adicionar mais funcionalidades.

---

## Pré-requisitos

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) instalado
- Um editor de texto (VS Code, Notepad++, ou similar)
- Cliente Telnet (PuTTY, Windows Telnet, ou similar)

---

## Como Editar Programas

O IntMUD é um interpretador de uma linguagem projetada principalmente para a criação de jogos do tipo MUD. Os programas são escritos em um editor de texto e salvos com a extensão `.int`.

### Estrutura de Diretórios Recomendada

```
meu-projeto/
├── main.int          # Arquivo principal
├── personagens.int   # Classes de personagens
├── salas.int         # Classes de salas
└── itens.int         # Classes de itens
```

### Editor de Texto

Use qualquer editor de texto simples. Recomendamos:
- **VS Code** com syntax highlighting
- **Notepad++** para Windows
- **nano** ou **vim** para Linux

---

## Organização dos Programas

Um programa IntMUD é formado por:

1. **Opções** (opcional) - Configurações no início do arquivo
2. **Classes** - Definições de tipos de dados e comportamentos
3. **Variáveis** - Dados armazenados em cada objeto
4. **Funções** - Comportamentos e ações

### Estrutura Básica

```intmud
# Opções (opcional)
incluir = area/

# Definição de classe
classe nome_da_classe

# Variáveis
int32 pontos
txt256 nome

# Funções
func inicializar
  pontos = 0
  nome = "Jogador"
  ret 1
```

---

## Exemplo 1 - Olá Mundo

Crie um arquivo `main.int` com o seguinte conteúdo:

```intmud
classe main

func inicializar
  escrevaln("Olá, Mundo!")
  escrevaln("Bem-vindo ao IntMUD.NET!")
  ret 1
```

### Executando

```bash
cd src/IntMud.Console
dotnet run -- --source ./caminho/para/seu/projeto
```

### O Que Faz

- `classe main` - Define a classe principal
- `func inicializar` - Função executada automaticamente quando o programa inicia
- `escrevaln()` - Escreve texto com quebra de linha
- `ret 1` - Retorna 1 (sucesso)

---

## Exemplo 2 - Servidor de Conexão Básico

```intmud
classe main

func inicializar
  escrevaln("Servidor iniciado!")
  ret 1

func aoconectar
  escrevaln("Novo jogador conectou!")
  escrevaln("")
  escrevaln("=== Bem-vindo ao MUD! ===")
  escrevaln("")
  escrevaln("Digite algo e pressione ENTER.")
  ret 1

func aodesconectar
  escrevaln("Jogador desconectou.")
  ret 1

func aocomando
  txt256 comando
  comando = arg1

  escrevaln("Você digitou: " + comando)
  ret 1
```

### Executando

```bash
# Terminal 1: Iniciar o servidor
dotnet run -- --source ./meu-projeto --port 4000

# Terminal 2: Conectar via Telnet
telnet localhost 4000
```

### Explicação

- `aoconectar` - Chamada quando um jogador conecta
- `aodesconectar` - Chamada quando um jogador desconecta
- `aocomando` - Chamada quando o jogador envia um comando
- `arg0` - ID da sessão do jogador
- `arg1` - Comando digitado
- `arg2` - Argumentos do comando

---

## Exemplo 3 - Variáveis e Condições

```intmud
classe main

int32 visitantes

func inicializar
  visitantes = 0
  escrevaln("Servidor iniciado!")
  ret 1

func aoconectar
  visitantes = visitantes + 1

  escrevaln("")
  escrevaln("=== Bem-vindo! ===")
  escrevaln("")
  escrevaln("Você é o visitante número " + visitantes)
  escrevaln("")

  se visitantes == 1
    escrevaln("Você é o primeiro visitante!")
  senao visitantes < 10
    escrevaln("Ainda somos poucos...")
  senao
    escrevaln("O servidor está movimentado!")
  fimse

  ret 1
```

### Explicação

- `int32 visitantes` - Declara variável inteira
- `visitantes = visitantes + 1` - Incrementa o contador
- `se ... senao ... fimse` - Estrutura condicional

---

## Exemplo 4 - Comandos do Jogador

```intmud
classe main

func inicializar
  escrevaln("Servidor MUD iniciado!")
  ret 1

func aoconectar
  escrevaln("")
  escrevaln("{cyan}=== Bem-vindo ao MUD! ==={reset}")
  escrevaln("")
  escrevaln("Comandos disponíveis:")
  escrevaln("  {yellow}ajuda{reset}  - Mostra esta mensagem")
  escrevaln("  {yellow}olhar{reset}  - Olha ao redor")
  escrevaln("  {yellow}sair{reset}   - Desconecta do jogo")
  escrevaln("")
  ret 1

func aocomando
  txt256 cmd
  cmd = arg1

  se cmd == "ajuda"
    chamar mostrar_ajuda()
    ret 1
  fimse

  se cmd == "olhar"
    chamar olhar()
    ret 1
  fimse

  se cmd == "sair"
    escrevaln("{red}Até logo!{reset}")
    ret 1
  fimse

  escrevaln("{red}Comando desconhecido: {reset}" + cmd)
  ret 0

func mostrar_ajuda
  escrevaln("")
  escrevaln("{brightcyan}=== Ajuda ==={reset}")
  escrevaln("")
  escrevaln("  {yellow}ajuda{reset}  - Mostra esta mensagem")
  escrevaln("  {yellow}olhar{reset}  - Olha ao redor")
  escrevaln("  {yellow}sair{reset}   - Desconecta do jogo")
  escrevaln("")
  ret 1

func olhar
  escrevaln("")
  escrevaln("{brightcyan}[ Sala Inicial ]{reset}")
  escrevaln("")
  escrevaln("Você está em uma sala vazia.")
  escrevaln("Não há saídas visíveis.")
  escrevaln("")
  ret 1
```

### Cores Disponíveis

| Código | Cor |
|--------|-----|
| `{red}` | Vermelho |
| `{green}` | Verde |
| `{blue}` | Azul |
| `{yellow}` | Amarelo |
| `{cyan}` | Ciano |
| `{magenta}` | Magenta |
| `{white}` | Branco |
| `{brightred}` | Vermelho brilhante |
| `{brightgreen}` | Verde brilhante |
| `{brightcyan}` | Ciano brilhante |
| `{dim}` | Texto escuro |
| `{reset}` | Resetar cor |

---

## Exemplo 5 - Sistema de Salas

```intmud
classe main

int32 salas
int32 jogadores

func inicializar
  # Criar arrays
  salas = vetor(10)
  jogadores = vetor(100)

  # Definir nomes das salas
  salas[0] = "Entrada"
  salas[1] = "Corredor"
  salas[2] = "Sala de Estar"

  escrevaln("{green}Mundo criado com 3 salas!{reset}")
  ret 1

func aoconectar
  int32 id
  id = arg0

  # Colocar jogador na sala 0
  jogadores[id] = 0

  escrevaln("{cyan}=== Bem-vindo! ==={reset}")
  chamar olhar(id)
  ret 1

func aocomando
  int32 id
  txt256 cmd
  int32 sala

  id = arg0
  cmd = arg1
  sala = jogadores[id]

  se cmd == "olhar"
    chamar olhar(id)
    ret 1
  fimse

  se cmd == "norte"
    se sala == 0
      jogadores[id] = 1
      escrevaln("{green}Você vai para o norte.{reset}")
      chamar olhar(id)
    senao
      escrevaln("{red}Não há saída para o norte.{reset}")
    fimse
    ret 1
  fimse

  se cmd == "sul"
    se sala == 1
      jogadores[id] = 0
      escrevaln("{green}Você vai para o sul.{reset}")
      chamar olhar(id)
    senao
      escrevaln("{red}Não há saída para o sul.{reset}")
    fimse
    ret 1
  fimse

  ret 0

func olhar
  int32 id
  int32 sala
  txt256 nome

  id = arg0
  sala = jogadores[id]
  nome = salas[sala]

  escrevaln("")
  escrevaln("{brightcyan}[ " + nome + " ]{reset}")
  escrevaln("")

  casovar sala
    casose 0
      escrevaln("Você está na entrada do castelo.")
      escrevaln("Uma porta leva ao {yellow}norte{reset}.")
    casose 1
      escrevaln("Um longo corredor com tochas nas paredes.")
      escrevaln("A entrada está ao {yellow}sul{reset}.")
      escrevaln("Uma porta leva ao {yellow}leste{reset}.")
    casose 2
      escrevaln("Uma sala aconchegante com sofás.")
      escrevaln("O corredor está a {yellow}oeste{reset}.")
  casofim

  escrevaln("")
  ret 1
```

---

## Exemplo 6 - Herança de Classes

```intmud
classe ser_vivo
  txt256 nome
  int32 vida
  int32 vida_max

  func inicializar
    nome = "Ser Vivo"
    vida_max = 100
    vida = vida_max
    ret 1

  func receber_dano
    int32 dano
    dano = arg0
    vida = vida - dano

    se vida <= 0
      vida = 0
      escrevaln(nome + " morreu!")
    senao
      escrevaln(nome + " recebeu " + dano + " de dano. Vida: " + vida)
    fimse
    ret vida

  func curar
    int32 cura
    cura = arg0
    vida = vida + cura

    se vida > vida_max
      vida = vida_max
    fimse

    escrevaln(nome + " curou " + cura + ". Vida: " + vida)
    ret vida


classe jogador
  herda ser_vivo

  int32 nivel
  int32 experiencia

  func inicializar
    nome = "Aventureiro"
    vida_max = 100
    vida = vida_max
    nivel = 1
    experiencia = 0
    ret 1

  func ganhar_exp
    int32 exp
    exp = arg0
    experiencia = experiencia + exp

    se experiencia >= nivel * 100
      nivel = nivel + 1
      vida_max = vida_max + 20
      vida = vida_max
      escrevaln("{yellow}LEVEL UP! Agora você é nível " + nivel + "!{reset}")
    fimse
    ret nivel


classe monstro
  herda ser_vivo

  int32 dano_min
  int32 dano_max

  func inicializar
    nome = "Monstro"
    vida_max = 50
    vida = vida_max
    dano_min = 5
    dano_max = 15
    ret 1

  func atacar
    int32 dano
    dano = dano_min + rand(dano_max - dano_min + 1)
    escrevaln(nome + " ataca causando " + dano + " de dano!")
    ret dano
```

---

## Exemplo 7 - Loops e Iteração

```intmud
classe main

func inicializar
  # Teste de loops
  chamar teste_enquanto()
  chamar teste_epara()
  chamar teste_para_cada()
  ret 1

func teste_enquanto
  int32 i
  i = 0

  escrevaln("=== Enquanto ===")
  enquanto i < 5
    escrevaln("i = " + i)
    i = i + 1
  efim
  escrevaln("")
  ret 1

func teste_epara
  int32 i

  escrevaln("=== EPara ===")
  epara i = 0; i < 5; i = i + 1
    escrevaln("i = " + i)
  efim
  escrevaln("")
  ret 1

func teste_para_cada
  int32 lista
  int32 item

  escrevaln("=== Para Cada ===")

  lista = vetor(5)
  lista[0] = 10
  lista[1] = 20
  lista[2] = 30
  lista[3] = 40
  lista[4] = 50

  para cada item em lista
    escrevaln("item = " + item)
  proximo

  escrevaln("")
  ret 1
```

---

## Exemplo 8 - Funções com Retorno

```intmud
classe main

func inicializar
  int32 a
  int32 b
  int32 resultado

  a = 10
  b = 5

  resultado = chamar somar(a, b)
  escrevaln("Soma: " + a + " + " + b + " = " + resultado)

  resultado = chamar multiplicar(a, b)
  escrevaln("Multiplicação: " + a + " * " + b + " = " + resultado)

  resultado = chamar fatorial(5)
  escrevaln("Fatorial de 5 = " + resultado)

  ret 1

func somar
  int32 x
  int32 y
  x = arg0
  y = arg1
  ret x + y

func multiplicar
  int32 x
  int32 y
  x = arg0
  y = arg1
  ret x * y

func fatorial
  int32 n
  n = arg0

  se n <= 1
    ret 1
  fimse

  ret n * chamar fatorial(n - 1)
```

---

## Próximos Passos

Agora que você conhece os conceitos básicos, explore:

1. **[Manual da Linguagem](manual.md)** - Referência completa
2. **[Arquitetura](architecture.md)** - Como o IntMUD.NET funciona internamente
3. **[Sistema de Salas](mud/07-salas.md)** - Criando um mundo completo
4. **[Sistema de Personagens](mud/08-personagens.md)** - NPCs e jogadores
5. **[Sistema de Eventos](mud/13-eventos.md)** - Eventos e triggers

---

## Dicas Importantes

### Comentários

```intmud
# Isso é um comentário
int32 valor  # Comentário no final da linha
```

### Indentação

Use indentação consistente para melhor legibilidade:

```intmud
func processar
  se condicao
    # código aqui
    se outra_condicao
      # mais código
    fimse
  fimse
  ret 1
```

### Debug

Use `escrevaln()` para debug:

```intmud
func calcular
  int32 resultado
  resultado = arg0 * 2
  escrevaln("[DEBUG] resultado = " + resultado)
  ret resultado
```

---

## Créditos

Este tutorial é baseado no trabalho original de **Edward Martin**, criador do IntMUD. Agradecemos imensamente por seu trabalho pioneiro no desenvolvimento de linguagens para MUDs.

- **Autor Original**: Edward Martin (edx2martin@gmail.com)
- **Website**: https://intervox.nce.ufrj.br/~e2mar/
- **Lista de Discussão**: http://groups.google.com/group/intmud/
