# Perguntas Frequentes (FAQ)

> **Nota**: Esta FAQ inclui perguntas do documento original `perguntas.txt` do IntMUD, escrito por **Edward Martin**, além de perguntas específicas sobre o IntMUD.NET.

---

## Índice

### IntMUD.NET
1. [O que é o IntMUD.NET?](#o-que-é-o-intmudnet)
2. [Qual a diferença entre IntMUD.NET e IntMUD original?](#qual-a-diferença-entre-intmudnet-e-intmud-original)
3. [Scripts do IntMUD original funcionam no .NET?](#scripts-do-intmud-original-funcionam-no-net)
4. [Como instalo o IntMUD.NET?](#como-instalo-o-intmudnet)
5. [Quais sistemas operacionais são suportados?](#quais-sistemas-operacionais-são-suportados)

### Programação
6. [Como debugar meu código?](#como-debugar-meu-código)
7. [Por que minha função não retorna valor?](#por-que-minha-função-não-retorna-valor)
8. [Como criar vetores multidimensionais?](#como-criar-vetores-multidimensionais)
9. [Como salvar dados do jogador?](#como-salvar-dados-do-jogador)

### Mecânicas de Jogo
10. [Como funciona o sistema de experiência?](#como-funciona-o-sistema-de-experiência)
11. [Como funciona o peso e velocidade?](#como-funciona-o-peso-e-velocidade)
12. [Como recuperar senha perdida?](#como-recuperar-senha-perdida)

---

## IntMUD.NET

### O que é o IntMUD.NET?

O IntMUD.NET é um **porte completo** do interpretador IntMUD original (escrito em C++ por Edward Martin) para a plataforma .NET. Ele mantém 100% de compatibilidade com a linguagem original, permitindo executar scripts IntMUD sem modificações.

### Qual a diferença entre IntMUD.NET e IntMUD original?

| Aspecto | IntMUD Original (C++) | IntMUD.NET |
|---------|----------------------|------------|
| Plataforma | Windows/Linux nativo | .NET (cross-platform) |
| Linguagem | C++ | C# |
| Compilação | Código nativo | JIT/AOT .NET |
| Threading | Manual | Async/await |
| Memória | Manual | Garbage Collected |
| Extensibilidade | Modificar C++ | Plugins .NET |

**Funcionalidades idênticas:**
- Todas as 99 funções builtin
- Todos os tipos de variáveis
- Sistema de eventos
- Protocolos de rede
- Sistema de cores

### Scripts do IntMUD original funcionam no .NET?

**Sim!** O IntMUD.NET foi projetado para manter compatibilidade total. Scripts escritos para o IntMUD original devem funcionar sem modificações.

```intmud
# Este script funciona em ambas as versões
classe main

func inicializar
  escrevaln("Olá, Mundo!")
  ret 1
```

### Como instalo o IntMUD.NET?

```bash
# 1. Instale o .NET 10 SDK
# https://dotnet.microsoft.com/download

# 2. Clone o repositório
git clone https://github.com/seu-usuario/intmud.net.git
cd intmud.net

# 3. Compile
dotnet build

# 4. Execute
cd src/IntMud.Console
dotnet run -- --source ./seu-projeto --port 4000
```

### Quais sistemas operacionais são suportados?

O IntMUD.NET roda em qualquer sistema que suporte .NET 10:

- **Windows** 10/11 (x64, ARM64)
- **Linux** (Ubuntu, Debian, Fedora, etc.)
- **macOS** (Intel e Apple Silicon)
- **Docker** (qualquer arquitetura)

---

## Programação

### Como debugar meu código?

Use a função `escrevaln()` para debug:

```intmud
func calcular
  int32 valor
  valor = arg0 * 2

  escrevaln("[DEBUG] valor = " + valor)

  ret valor
```

Ou use a variável `debug`:

```intmud
debug dbg

func diagnostico
  dbg.ini()
  escrevaln("Memória: " + dbg.mem() + " KB")
  escrevaln("Tempo: " + dbg.stempo() + " ms")
  ret 1
```

### Por que minha função não retorna valor?

Certifique-se de usar `ret` para retornar:

```intmud
# ERRADO - não retorna nada
func somar
  int32 resultado
  resultado = arg0 + arg1

# CORRETO - retorna o valor
func somar
  int32 resultado
  resultado = arg0 + arg1
  ret resultado
```

### Como criar vetores multidimensionais?

Crie vetores de vetores:

```intmud
int32 matriz

func criar_matriz
  int32 i

  # Criar vetor de 10 elementos
  matriz = vetor(10)

  # Cada elemento é outro vetor
  epara i = 0; i < 10; i++
    matriz[i] = vetor(10)
  efim

  # Acessar
  matriz[5][5] = 42

  ret 1
```

### Como salvar dados do jogador?

Use variáveis marcadas com `sav`:

```intmud
classe jogador
  sav txt256 nome
  sav int32 nivel
  sav int32 experiencia
  sav int32 sala_atual
```

Ou use `arqsav` para controle manual:

```intmud
arqsav save

func salvar_jogo
  txt256 arquivo
  arquivo = "saves/" + jogador.nome + ".sav"
  save.salvar(arquivo, "senha123")
  ret 1

func carregar_jogo
  txt256 arquivo
  arquivo = "saves/" + jogador.nome + ".sav"

  se save.existe(arquivo)
    save.ler(arquivo, "senha123")
    escrevaln("Jogo carregado!")
  senao
    escrevaln("Arquivo não encontrado.")
  fimse
  ret 1
```

---

## Mecânicas de Jogo

### Como funciona o sistema de experiência?

A experiência necessária para subir de nível segue a fórmula:

```
exp_necessaria = fator0 + (fator1 × nivel) + (fator2 × nivel²)
```

**Exemplos:**

| Fatores | Nível 1 | Nível 5 | Nível 10 |
|---------|---------|---------|----------|
| f0=100, f1=0, f2=0 | 100 | 100 | 100 |
| f0=0, f1=100, f2=0 | 100 | 500 | 1000 |
| f0=0, f1=0, f2=10 | 10 | 250 | 1000 |
| f0=100, f1=50, f2=5 | 155 | 475 | 1100 |

### Experiência ao Matar Monstros

A experiência ganha é calculada assim:

```
exp_base = exp_nivel0 + (exp_por_nivel × nivel_monstro)
```

**Modificadores:**

| Situação | Modificador |
|----------|-------------|
| Monstro mesmo nível | 100% |
| Monstro 1 nível abaixo | 75% |
| Monstro 2 níveis abaixo | 50% |
| Monstro 3 níveis abaixo | 25% |
| Monstro 4+ níveis abaixo | 0% |
| Monstro nível acima | +10% por nível |

### Divisão de Experiência em Grupo

1. **Dano proporcional**: Quem causou mais dano ganha mais exp
2. **Grupo formal**: Experiência dividida igualmente
3. **Múltiplos personagens**: `exp × 2 / (num_personagens + 1)`
4. **Desmaiados**: Não ganham experiência

### Como funciona o peso e velocidade?

O peso afeta a velocidade de ataque:

```
perda_velocidade = peso_carregado / 1500 + peso_vestido / 3000
```

**Penalidades de peso:**

| Peso | Efeito |
|------|--------|
| Normal | Sem penalidade |
| Sobrecarregado | -50% velocidade |
| Muito pesado | -75% velocidade + perda de força |

**Verificação de sobrecarga:**
```
peso_total = peso_carregado + (peso_vestido / 2) + (peso_arma × 4)
se peso_total > capacidade_maxima
  # Penalidade de ataque
fimse
```

### Como recuperar senha perdida?

**Opção 1: Editar arquivo .sav**

Abra o arquivo de save do jogador e remova a linha de senha:
```
senha=%Vk-"ZS;7dHo@vZ2fpq<ME4\c)
```

Sem essa linha, qualquer senha será aceita.

**Opção 2: Copiar senha de outro save**

Copie a linha `senha=...` de outro arquivo cujo password você conhece.

**Opção 3: Via código (administrador)**

```intmud
func resetar_senha
  txt256 jogador_nome
  txt256 nova_senha

  jogador_nome = arg0
  nova_senha = arg1

  # Codificar nova senha
  arqsav save
  txt256 senha_codificada
  senha_codificada = save.senha(nova_senha)

  # Atualizar no arquivo
  # ... (depende da implementação)

  escrevaln("Senha alterada para " + jogador_nome)
  ret 1
```

---

## Perguntas Técnicas

### O servidor suporta quantas conexões?

O IntMUD.NET usa I/O assíncrono, podendo suportar milhares de conexões simultâneas. O limite prático depende da memória disponível e complexidade do jogo.

### Posso usar banco de dados?

Sim! Como o IntMUD.NET roda em .NET, você pode integrar com qualquer banco de dados via:

- Entity Framework
- Dapper
- ADO.NET direto
- MongoDB, Redis, etc.

### O código é thread-safe?

O interpretador processa comandos sequencialmente por sessão. Para operações entre sessões, use mecanismos de sincronização do .NET.

### Posso criar extensões em C#?

Sim! O IntMUD.NET foi projetado para extensibilidade:

1. Adicione novas funções builtin
2. Crie handlers de tipos personalizados
3. Integre com APIs externas

---

## Precisa de Mais Ajuda?

### Recursos

- **Documentação**: [docs/manual.md](manual.md)
- **Tutorial**: [docs/tutorial.md](tutorial.md)
- **Arquitetura**: [docs/architecture.md](architecture.md)

### Comunidade

- **Lista de Discussão**: http://groups.google.com/group/intmud/
- **IntMUD Original**: https://intervox.nce.ufrj.br/~e2mar/

### Créditos

O IntMUD foi criado por **Edward Martin** (edx2martin@gmail.com). Este FAQ inclui informações do documento original `perguntas.txt` e foi expandido para cobrir aspectos específicos do IntMUD.NET.
