# Manual da Linguagem IntMUD

> **Nota**: Este manual é baseado no manual original do IntMUD escrito por **Edward Martin** e revisado por Paulo Santos Ramos. O IntMUD.NET é um porte fiel da implementação original em C++.

---

## Índice

1. [Sobre o IntMUD](#1-sobre-o-intmud)
2. [Opções dos Arquivos .INT](#2-opções-dos-arquivos-int)
3. [Classes](#3-classes)
4. [Variáveis](#4-variáveis)
5. [Funções](#5-funções)
6. [Constantes](#6-constantes)
7. [Funções do Tipo Variáveis](#7-funções-do-tipo-variáveis)
8. [Herança](#8-herança)
9. [Conteúdo das Funções](#9-conteúdo-das-funções)
10. [Variáveis Básicas](#10-variáveis-básicas)
11. [Identificadores comum e sav](#11-identificadores-comum-e-sav)
12. [Vetores](#12-vetores)
13. [Operadores](#13-operadores)
14. [Instruções de Controle de Fluxo](#14-instruções-de-controle-de-fluxo)
15. [Lista de Funções Builtin](#15-lista-de-funções-builtin)
16. [Tipos Especiais de Variáveis](#16-tipos-especiais-de-variáveis)

---

## 1. Sobre o IntMUD

O IntMUD é um interpretador de comandos que trabalha com um ou mais arquivos textuais de extensão `.int`, cujo conteúdo deverá ser escrito na linguagem IntMUD para sua correta interpretação.

### Executando no IntMUD.NET

```bash
# Sintaxe básica
dotnet run -- --source <diretório> --port <porta>

# Exemplo
dotnet run -- --source ./meu-mud --port 4000
```

O IntMUD.NET procura por arquivos `.int` no diretório especificado e compila todos automaticamente.

---

## 2. Opções dos Arquivos .INT

O arquivo `.int` principal pode conter definições no início:

```intmud
incluir = area/
exec = 10000
telatxt = 1
log = 1
```

### 2.1. Opção INCLUIR

Permite incluir outros arquivos `.int`:

```intmud
incluir = mud-          # Todos os arquivos que começam com "mud-"
incluir = area/         # Todos os arquivos do diretório "area"
incluir = teste/mud     # Arquivos em "teste" que começam com "mud"
```

### 2.2. Opção EXEC

Define o número máximo de instruções executadas antes do controle retornar:

```intmud
exec = 10000
```

**Importante**: Um valor muito alto pode causar travamento em loops infinitos. Um valor muito baixo pode interromper funções longas.

### 2.3. Opção TELATXT

```intmud
telatxt = 0    # Roda em segundo plano (padrão)
telatxt = 1    # Abre janela de terminal
```

### 2.4. Opção LOG

```intmud
log = 0    # Erros na janela (padrão)
log = 1    # Erros em arquivo .log
```

---

## 3. Classes

Classes são a estrutura fundamental do IntMUD. Todo objeto criado é derivado de uma classe.

### Sintaxe

```intmud
classe nome_da_classe

# variáveis da classe
int32 valor
txt256 nome

# funções da classe
func inicializar
  valor = 100
  nome = "teste"
  ret 1
```

### Regras para Nomes

- Podem conter letras, números e underscore (`_`)
- Não podem começar com número
- Não podem conter espaços ou caracteres especiais
- São case-insensitive (maiúsculas = minúsculas)

### Exemplo Completo

```intmud
classe jogador

# Variáveis de instância
txt256 nome
int32 vida
int32 nivel
int32 experiencia

# Construtor
func inicializar
  nome = "Aventureiro"
  vida = 100
  nivel = 1
  experiencia = 0
  ret 1

# Métodos
func atacar
  int32 dano
  dano = nivel * 10
  ret dano

func ganhar_exp
  int32 quantidade
  quantidade = arg0
  experiencia = experiencia + quantidade

  se experiencia >= nivel * 100
    nivel = nivel + 1
    escrevaln("Subiu para o nível " + nivel + "!")
  fimse
  ret 1
```

---

## 4. Variáveis

### Tipos de Variáveis Básicas

| Tipo | Descrição | Faixa/Tamanho |
|------|-----------|---------------|
| `int1` | Inteiro 1 bit | 0 ou 1 |
| `int8` | Inteiro 8 bits com sinal | -128 a 127 |
| `uint8` | Inteiro 8 bits sem sinal | 0 a 255 |
| `int16` | Inteiro 16 bits com sinal | -32768 a 32767 |
| `uint16` | Inteiro 16 bits sem sinal | 0 a 65535 |
| `int32` | Inteiro 32 bits com sinal | -2³¹ a 2³¹-1 |
| `uint32` | Inteiro 32 bits sem sinal | 0 a 2³²-1 |
| `real` | Ponto flutuante simples | ~7 dígitos |
| `real2` | Ponto flutuante duplo | ~15 dígitos |
| `txt1` a `txt512` | Texto | 1 a 512 caracteres |
| `ref` | Referência a objeto | - |

### Declaração de Variáveis

```intmud
# Na classe (variáveis de instância)
int32 pontos
txt256 descricao
real velocidade

# Em funções (variáveis locais)
func calcular
  int32 resultado
  txt128 mensagem
  resultado = 42
  mensagem = "Resposta: " + resultado
  ret resultado
```

### Inicialização

Variáveis numéricas são inicializadas com 0. Variáveis de texto são inicializadas com string vazia.

---

## 5. Funções

### Sintaxe Básica

```intmud
func nome_da_funcao
  # corpo da função
  ret valor_retorno
```

### Argumentos

Funções recebem argumentos através das variáveis especiais `arg0` a `arg9`:

```intmud
func somar
  int32 a
  int32 b
  a = arg0
  b = arg1
  ret a + b

# Chamada
func testar
  int32 resultado
  resultado = chamar somar(10, 20)
  escrevaln("Resultado: " + resultado)
  ret 1
```

### Chamando Funções

```intmud
# Chamar função sem retorno
chamar nome_funcao(arg1, arg2)

# Chamar função com retorno
variavel = chamar nome_funcao(arg1, arg2)

# Chamar função de outro objeto
chamar objeto.funcao(args)
resultado = chamar objeto.funcao(args)
```

---

## 6. Constantes

Constantes são valores fixos definidos na classe:

```intmud
classe configuracao

const MAX_JOGADORES = 100
const NOME_SERVIDOR = "Meu MUD"
const PI = 3.14159
const DEBUG = nulo

func mostrar
  escrevaln("Servidor: " + NOME_SERVIDOR)
  escrevaln("Máximo: " + MAX_JOGADORES)
  ret 1
```

---

## 7. Funções do Tipo Variáveis

Variáveis especiais podem ter funções associadas:

```intmud
arqtxt arquivo

func salvar
  arquivo.abrir("dados.txt", "w")
  arquivo.linha("Dados do jogo")
  arquivo.fechar()
  ret 1
```

---

## 8. Herança

Classes podem herdar de outras classes:

```intmud
classe animal
  txt256 nome
  int32 vida

  func falar
    escrevaln(nome + " faz um som")
    ret 1

classe cachorro
  herda animal

  func falar
    escrevaln(nome + " late: Au au!")
    ret 1

  func buscar
    escrevaln(nome + " busca a bola")
    ret 1
```

### Múltipla Herança

```intmud
classe monstro_mago
  herda monstro
  herda mago
```

---

## 9. Conteúdo das Funções

Funções contêm instruções executadas sequencialmente:

```intmud
func processar_comando
  txt256 comando
  comando = arg0

  # Declarações locais
  int32 resultado

  # Atribuições
  resultado = 0

  # Condicionais
  se comando == "ajuda"
    chamar mostrar_ajuda()
    resultado = 1
  senao
    escrevaln("Comando desconhecido")
  fimse

  # Retorno
  ret resultado
```

---

## 10. Variáveis Básicas

### Tipos Inteiros

```intmud
int1 flag           # 0 ou 1
int8 pequeno        # -128 a 127
uint8 byte          # 0 a 255
int16 medio         # -32768 a 32767
uint16 positivo     # 0 a 65535
int32 grande        # Inteiro padrão
uint32 muitogrande  # 0 a 4 bilhões
```

### Tipos Reais

```intmud
real velocidade     # Precisão simples
real2 precisao      # Precisão dupla
```

### Tipos Texto

```intmud
txt1 letra          # 1 caractere
txt32 curto         # Até 32 caracteres
txt256 medio        # Até 256 caracteres (mais comum)
txt512 longo        # Até 512 caracteres
```

### Referências

```intmud
ref alvo            # Referência a outro objeto
ref sala_atual
```

---

## 11. Identificadores comum e sav

### comum

Variáveis marcadas como `comum` são compartilhadas entre todos os objetos da mesma classe:

```intmud
classe contador
  comum int32 total

  func incrementar
    total = total + 1
    ret total
```

### sav

Variáveis marcadas como `sav` são salvas em arquivos:

```intmud
classe jogador
  sav txt256 nome
  sav int32 nivel
  sav int32 experiencia
```

---

## 12. Vetores

Vetores são criados com a função `vetor()`:

```intmud
func inicializar
  int32 numeros
  txt256 nomes

  # Criar vetores
  numeros = vetor(10)    # 10 elementos numéricos
  nomes = vetor(5)       # 5 elementos de texto

  # Acessar elementos
  numeros[0] = 100
  numeros[1] = 200
  nomes[0] = "Alice"
  nomes[1] = "Bob"

  # Ler elementos
  escrevaln("Primeiro número: " + numeros[0])
  escrevaln("Primeiro nome: " + nomes[0])
  ret 1
```

### Vetores Multidimensionais

```intmud
int32 matriz

func criar_matriz
  int32 i
  matriz = vetor(10)

  epara i = 0; i < 10; i = i + 1
    matriz[i] = vetor(10)
  efim

  # Acessar
  matriz[0][0] = 1
  matriz[5][5] = 25
  ret 1
```

---

## 13. Operadores

### 13.1. Verdadeiro e Falso

- **Verdadeiro**: Qualquer valor diferente de 0, `nulo`, ou string vazia
- **Falso**: 0, `nulo`, ou string vazia

### 13.2. Lista de Operadores

#### Aritméticos

| Operador | Descrição | Exemplo |
|----------|-----------|---------|
| `+` | Adição / Concatenação | `a + b` |
| `-` | Subtração | `a - b` |
| `*` | Multiplicação | `a * b` |
| `/` | Divisão | `a / b` |
| `%` | Módulo (resto) | `a % b` |

#### Comparação

| Operador | Descrição | Exemplo |
|----------|-----------|---------|
| `==` | Igual | `a == b` |
| `!=` | Diferente | `a != b` |
| `<` | Menor que | `a < b` |
| `>` | Maior que | `a > b` |
| `<=` | Menor ou igual | `a <= b` |
| `>=` | Maior ou igual | `a >= b` |

#### Lógicos

| Operador | Descrição | Exemplo |
|----------|-----------|---------|
| `&&` ou `e` | E lógico | `a && b` |
| `\|\|` ou `ou` | OU lógico | `a \|\| b` |
| `!` ou `nao` | NÃO lógico | `!a` |

#### Atribuição

| Operador | Descrição | Equivalente |
|----------|-----------|-------------|
| `=` | Atribuição | `a = b` |
| `+=` | Adicionar e atribuir | `a = a + b` |
| `-=` | Subtrair e atribuir | `a = a - b` |
| `*=` | Multiplicar e atribuir | `a = a * b` |
| `/=` | Dividir e atribuir | `a = a / b` |
| `++` | Incremento | `a = a + 1` |
| `--` | Decremento | `a = a - 1` |

### 13.3. Precedência de Operadores

Da maior para menor precedência:

1. `()` Parênteses
2. `!` `-` (unários)
3. `*` `/` `%`
4. `+` `-`
5. `<` `>` `<=` `>=`
6. `==` `!=`
7. `&&`
8. `||`
9. `?:` (ternário)
10. `=` `+=` `-=` etc.

### 13.4. Operador Ternário

```intmud
resultado = condicao ? valor_verdadeiro : valor_falso

# Exemplo
status = vida > 0 ? "vivo" : "morto"
```

### 13.5. Operador de Coalescência Nula

```intmud
valor = variavel ?? valor_padrao

# Exemplo
nome = jogador.nome ?? "Desconhecido"
```

### 13.6. Operadores de Bit

| Operador | Descrição |
|----------|-----------|
| `&` | E bit a bit |
| `\|` | OU bit a bit |
| `^` | XOR bit a bit |
| `~` | NOT bit a bit |
| `<<` | Shift left |
| `>>` | Shift right |

---

## 14. Instruções de Controle de Fluxo

### Se / Senao / FimSe

```intmud
se condicao
  # código se verdadeiro
fimse

se condicao
  # código se verdadeiro
senao
  # código se falso
fimse

se condicao1
  # código 1
senao condicao2
  # código 2
senao
  # código padrão
fimse
```

### Enquanto / EFim

```intmud
int32 i
i = 0

enquanto i < 10
  escrevaln("i = " + i)
  i = i + 1
efim
```

### EPara / EFim

```intmud
int32 i

epara i = 0; i < 10; i = i + 1
  escrevaln("i = " + i)
efim
```

### Para Cada / Proximo

```intmud
int32 lista
int32 item

lista = vetor(5)
lista[0] = 10
lista[1] = 20
lista[2] = 30

para cada item em lista
  escrevaln("Item: " + item)
proximo
```

### CasoVar / CasoSe / CasoFim

```intmud
int32 opcao
opcao = 2

casovar opcao
  casose 1
    escrevaln("Opção 1")
  casose 2
    escrevaln("Opção 2")
  casose 3
    escrevaln("Opção 3")
  casose
    escrevaln("Opção padrão")
casofim
```

### Sair e Continuar

```intmud
# Sair do loop
enquanto 1
  se condicao
    sair
  fimse
efim

# Sair com condição
enquanto 1
  sair condicao
efim

# Continuar próxima iteração
epara i = 0; i < 10; i++
  continuar i % 2 == 0  # Pula números pares
  escrevaln(i)
efim
```

### Ret (Retorno)

```intmud
func calcular
  se erro
    ret -1
  fimse
  ret resultado
```

---

## 15. Lista de Funções Builtin

O IntMUD possui 99 funções builtin organizadas em categorias:

### 15.1. Funções de Saída

| Função | Descrição |
|--------|-----------|
| `escreva(texto)` | Escreve texto sem quebra de linha |
| `escrevaln(texto)` | Escreve texto com quebra de linha |

### 15.2. Funções Numéricas

| Função | Descrição |
|--------|-----------|
| `int(valor)` | Converte para inteiro |
| `real(valor)` | Converte para real |
| `matabs(n)` | Valor absoluto |
| `matseno(n)` | Seno |
| `matcos(n)` | Cosseno |
| `mattan(n)` | Tangente |
| `matlog(n)` | Logaritmo natural |
| `matexp(n)` | Exponencial (e^n) |
| `matraiz(n)` | Raiz quadrada |
| `matpot(b,e)` | Potência (b^e) |
| `matcima(n)` | Arredonda para cima |
| `matbaixo(n)` | Arredonda para baixo |
| `matrad(n)` | Graus para radianos |
| `matdeg(n)` | Radianos para graus |
| `rand(n)` | Número aleatório 0 a n-1 |

### 15.3. Funções de Texto

| Função | Descrição |
|--------|-----------|
| `txt(valor)` | Converte para texto |
| `txtmai(t)` | Maiúsculas |
| `txtmin(t)` | Minúsculas |
| `txtmaiini(t)` | Primeira letra maiúscula |
| `txtmaimin(t)` | Primeira maiúscula, resto minúsculas |
| `txttam(t)` | Tamanho do texto |
| `txtcopia(t,i,n)` | Copia parte do texto |
| `txtpos(t,sub)` | Posição da substring |
| `txtposf(t,sub)` | Posição da última ocorrência |
| `txtremove(t,i,n)` | Remove caracteres |
| `txtinsere(t,i,s)` | Insere texto |
| `txttroca(t,a,n)` | Substitui texto |
| `txttrim(t)` | Remove espaços |
| `txtcor(t,c)` | Adiciona cor ao texto |
| `txtrev(t)` | Inverte texto |
| `txtrepete(t,n)` | Repete texto n vezes |
| `txtcod(t)` | Codifica caracteres especiais |
| `txtdec(t)` | Decodifica caracteres especiais |
| `txtsha1(t)` | Hash SHA1 |
| `txtmd5(t)` | Hash MD5 |

### 15.4. Funções de Comparação de Texto

| Função | Descrição |
|--------|-----------|
| `intcmp(a,b)` | Compara textos (-1, 0, 1) |
| `intcmpmai(a,b)` | Compara ignorando maiúsculas |
| `txtproc(t,sub)` | Procura substring |
| `txtprocmai(t,sub)` | Procura ignorando maiúsculas |

### 15.5. Funções de Objetos

| Função | Descrição |
|--------|-----------|
| `criar(classe)` | Cria novo objeto |
| `apagar(obj)` | Apaga objeto |
| `classe(obj)` | Nome da classe |
| `objantes(obj)` | Objeto anterior |
| `objdepois(obj)` | Próximo objeto |
| `inttotal(classe)` | Total de objetos da classe |

### 15.6. Funções de Vetores

| Função | Descrição |
|--------|-----------|
| `vetor(n)` | Cria vetor com n elementos |
| `vetortam(v)` | Tamanho do vetor |
| `vetorcopia(v)` | Copia vetor |
| `vetorord(v)` | Ordena vetor |
| `vetorinv(v)` | Inverte vetor |

### 15.7. Outras Funções

| Função | Descrição |
|--------|-----------|
| `tempo()` | Timestamp atual |
| `tempoms()` | Timestamp em milissegundos |
| `esperar(ms)` | Pausa execução |
| `vartroca(a,b)` | Troca valores |

---

## 16. Tipos Especiais de Variáveis

### ListaObj - Lista de Objetos

```intmud
listaobj jogadores

func adicionar_jogador
  ref j
  j = criar("jogador")
  jogadores.adicionar(j)
  ret 1

func listar
  ref j
  para cada j em jogadores
    escrevaln(j.nome)
  proximo
  ret 1
```

### ArqTxt - Arquivos de Texto

```intmud
arqtxt arquivo

func salvar_dados
  arquivo.abrir("dados.txt", "w")
  arquivo.linha("Nome: João")
  arquivo.linha("Pontos: 100")
  arquivo.fechar()
  ret 1

func carregar_dados
  txt256 linha
  arquivo.abrir("dados.txt", "r")
  enquanto arquivo.fim == 0
    linha = arquivo.ler()
    escrevaln(linha)
  efim
  arquivo.fechar()
  ret 1
```

### ArqMem - Buffer de Memória

```intmud
arqmem buffer

func processar
  buffer.limpar()
  buffer.escrever(65)  # 'A'
  buffer.escrever(66)  # 'B'
  buffer.ir(0)
  escrevaln("Conteúdo: " + buffer.texto())
  ret 1
```

### ArqDir - Diretórios

```intmud
arqdir pasta

func listar_arquivos
  txt256 arquivo
  pasta.abrir("./dados", "*.txt")
  enquanto pasta.fim == 0
    arquivo = pasta.proximo()
    escrevaln(arquivo)
  efim
  pasta.fechar()
  ret 1
```

### ArqLog - Arquivos de Log

```intmud
arqlog log

func registrar
  log.abrir("servidor.log")
  log.linha("Servidor iniciado")
  log.linha("Jogador conectou")
  log.fechar()
  ret 1
```

### ArqSav - Salvar/Carregar Estado

```intmud
arqsav save

func salvar_jogo
  save.salvar("jogo.sav", "senha123")
  ret save

func carregar_jogo
  int32 objetos
  objetos = save.ler("jogo.sav", "senha123")
  escrevaln("Carregados " + objetos + " objetos")
  ret 1
```

### Socket - Comunicação TCP/IP

```intmud
socket conexao

func conectar_servidor
  conexao.conectar("servidor.com", 23)
  conexao.enviar("Olá servidor!")
  ret 1
```

### Serv - Servidor TCP/IP

```intmud
serv servidor

func iniciar
  servidor.iniciar(4000)
  escrevaln("Servidor na porta 4000")
  ret 1
```

### Debug - Depuração

```intmud
debug dbg

func diagnostico
  dbg.ini()
  escrevaln("Versão: " + dbg.ver())
  escrevaln("Memória: " + dbg.mem() + " KB")
  escrevaln("Tempo: " + dbg.stempo() + " ms")
  ret 1
```

---

## Referência Rápida de Cores

O IntMUD suporta cores ANSI em texto:

```intmud
escrevaln("{red}Texto vermelho{reset}")
escrevaln("{green}Texto verde{reset}")
escrevaln("{blue}Texto azul{reset}")
escrevaln("{yellow}Texto amarelo{reset}")
escrevaln("{cyan}Texto ciano{reset}")
escrevaln("{magenta}Texto magenta{reset}")
escrevaln("{white}Texto branco{reset}")
escrevaln("{brightred}Vermelho brilhante{reset}")
escrevaln("{dim}Texto escuro{reset}")
escrevaln("{bold}Texto negrito{reset}")
```

---

## Créditos

Este manual é baseado no trabalho original de **Edward Martin**, criador do IntMUD. O IntMUD.NET mantém compatibilidade total com a linguagem original, permitindo que scripts existentes funcionem sem modificações.

Para mais informações sobre o projeto original:
- **Autor**: Edward Martin (edx2martin@gmail.com)
- **Website**: https://intervox.nce.ufrj.br/~e2mar/
- **Lista de Discussão**: http://groups.google.com/group/intmud/
