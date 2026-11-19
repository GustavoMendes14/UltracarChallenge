# Testes
Os projetos são separados e cada um referencia diretamente a API:

```text
Tests/
├── UnitTests/
│   ├── Application/
│   ├── Common/
│   ├── Domain/
│   ├── Presentation/
│   └── UltracarInventoryApi.UnitTests.csproj
├── IntegrationTests/
│   ├── Application/Reservation/CreateReservation/
│   │   ├── CreateReservationConcurrencyTest.cs
│   │   └── CreateReservationConcurrencyTestFixture.cs
│   ├── Base/
│   │   ├── BaseFixture.cs
│   │   ├── DatabaseSchema.sql
│   │   └── InventoryApiFactory.cs
│   ├── xunit.runner.json
│   └── UltracarInventoryApi.IntegrationTests.csproj
└── README.md
```
## Executar os testes unitários

```sh
dotnet test Tests/UnitTests/UltracarInventoryApi.UnitTests.csproj
```

Os mocks são criados a cada teste. O relógio é fixo, sem esperas reais.
A reflexão na fixture preenche apenas os IDs privados que seriam atribuídos
pelo mapeador de persistência.

## Executar os testes de integração

Na pasta `src`, com o SDK .NET 10 e o Docker em execução:

```sh
dotnet test Tests/IntegrationTests/UltracarInventoryApi.IntegrationTests.csproj
```

A primeira execução pode baixar pacotes NuGet, a imagem `postgres:18.6`
e a imagem de limpeza do Testcontainers. No WSL, execute na distribuição
com acesso ao Docker. A API é iniciada pela fixture; não precisa estar rodando.

Para gerar cobertura, acrescente `--collect:"XPlat Code Coverage"` ao comando
do projeto desejado.

## Cenários

| Regra | Cobertura |
| --- | --- |
| R1 | Criação de todos os itens; saldo exato; soma de reservas ativas; todos os itens em falta e seus saldos; nenhuma inserção parcial; rollback em falhas. |
| R2 | Integração: 20 reservas paralelas, 5 respostas 201, 15 respostas 409 e saldo disponível final zero em PostgreSQL real. |
| R3 | Repetição de sucesso e erro; mesma resposta após mudança de estado; duas instâncias do serviço; mesma chave com corpo diferente; validação da chave. |
| R4 | Prazo de 30 minutos; antes, exatamente no limite e depois; persistência da expiração sem baixa física; liberação lógica do saldo. |
| R5 | Confirmação e baixa física; cancelamento sem baixa; rejeição de todas as transições a partir de estados finais; proteção contra baixa parcial. |
| R6 | Consultas aos repositórios com oficina; recurso indisponível retorna NotFound; cursor não reutilizável em outra oficina. |
| R7 | ProblemDetails, application/problem+json, status HTTP, código de negócio e detalhes. |
| Busca | 20 itens por página com saldo; próxima página por cursor; página vazia; cursor inválido ou de outra busca. |

## Concorrência com PostgreSQL real — R2

O teste `AllowOnlyFiveOfTwentyParallelReservationsWithStockOfFive` fica no
projeto `UltracarInventoryApi.IntegrationTests`, em
`Application/Reservation/CreateReservation`. Ele usa fixture compartilhada,
`DisplayName = nameof(...)`, `Trait("Integration/Application", ...)`
e Bogus para gerar códigos, ordens e chaves de idempotência.

O Testcontainers inicia um PostgreSQL 18.6 exclusivo da execução, com porta
aleatória, inicializa o esquema SQL e descarta o container no final.
O `WebApplicationFactory<Program>` inicia a aplicação com os controllers,
serviços, repositórios, transações e bloqueios SQL reais. A comunicação HTTP
usa o TestServer em memória. O acesso ao ponto de entrada interno da API
é autorizado somente ao assembly de integração por `InternalsVisibleTo`.

O cenário insere uma peça com estoque físico 5 e dispara 20 requisições de
reserva de uma unidade, cada uma com ordem e chave de idempotência próprias.
As tarefas aguardam uma barreira assíncrona e são reunidas com `Task.WhenAll`.
O `xunit.runner.json` segue o exemplo do Codeflix e evita paralelismo entre
coleções de testes; as 20 requisições dentro deste teste continuam concorrentes.

As assertivas verificam:

- Exatamente 5 respostas HTTP 201 e 15 respostas HTTP 409.
- Cinco IDs distintos nas reservas criadas.
- Código `INSUFFICIENT_STOCK` nas quinze respostas de conflito.
- Consulta HTTP de saldo com estoque físico 5, reservado 5 e disponível 0.
- Exatamente cinco reservas persistidas no PostgreSQL.

O saldo final zero é o **saldo disponível**. Reservar não baixa o estoque físico;
a baixa ocorre somente na confirmação.

O banco local configurado em `appsettings.json` não é utilizado pelo teste.
O esquema embarcado em `Base/DatabaseSchema.sql` reproduz as tabelas, restrições
e índices do banco local atual. Como ainda não existe um script de migração
versionado na API, esse arquivo deve acompanhar mudanças no esquema.

Este cenário cobre R2. Os demais cenários de integração, como expiração de
reservas e o filtro SQL de reservas ativas, continuam fora deste teste.
