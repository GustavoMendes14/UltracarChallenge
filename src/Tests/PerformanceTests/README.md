# Testes de desempenho com NBomber e ambiente temporário

Projeto xUnit independente, sem referência à API. O Testcontainers cria um
PostgreSQL exclusivo e uma instância da API em Docker. O NBomber usa HTTP real
contra essa instância, com os dados exigidos por D1. Não precisa configurar URL,
credenciais, preparar seed manualmente ou subir o Docker Compose da aplicação.

## Pré-requisitos

- SDK .NET 10 ou superior.
- Docker com suporte a containers Linux, acessível ao processo de testes.
- Checkout do repositório, incluindo o Dockerfile da API e o schema de integração.
- Na primeira execução, acesso aos pacotes NuGet e imagens Docker.

No WSL, execute na distribuição que tem acesso ao Docker. No Windows, é necessário
que o Docker também esteja acessível pelo processo Windows.

## Executar tudo automaticamente

Na pasta `src/UltracarInventoryApi`, com Docker em execução:

```powershell
$env:RUN_PERFORMANCE_TESTS = '1'
try {
    dotnet test ../Tests/PerformanceTests/UltracarInventoryApi.PerformanceTests.csproj `
        -c Release --filter 'Category=Performance' --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:RUN_PERFORMANCE_TESTS -ErrorAction SilentlyContinue
}
```

No Bash/WSL, na mesma pasta:

```sh
RUN_PERFORMANCE_TESTS=1 dotnet test ../Tests/PerformanceTests/UltracarInventoryApi.PerformanceTests.csproj \
  -c Release --filter 'Category=Performance' --logger 'console;verbosity=detailed'
```

A variável vale somente para essa execução no exemplo Bash. O teste permanece
ignorado em uma execução comum da solução sem `RUN_PERFORMANCE_TESTS=1`.
Os onze testes leves de classificação das respostas continuam executando sem Docker.

## Ciclo de vida

1. Cria rede privada e container PostgreSQL `postgres:17`, com banco e credenciais
   exclusivos do teste, sem usar o PostgreSQL da aplicação.
2. Aplica o schema compartilhado de `../IntegrationTests/Base/DatabaseSchema.sql`.
3. Executa `Seed/performance-seed.sql` via `psql` dentro do container. Não é
   necessário instalar `psql` na máquina nem conhecer a senha do banco real.
4. Constrói uma imagem temporária da API usando o Dockerfile existente: Release,
   ambiente Production e logs em Warning.
5. Inicia o container da API apontando para o banco temporário, com porta aleatória.
6. Valida D1 no banco e as primeiras páginas/saldos das 50 oficinas por HTTP.
7. Captura `EXPLAIN (ANALYZE, BUFFERS, VERBOSE)` fora da medição.
8. Executa 30 segundos de aquecimento e 180 segundos de carga com 20 usuários.
9. Salva relatórios e logs, depois remove os containers, dados, rede e imagem
   temporária da API, inclusive quando o teste falha nas assertions.

O ambiente é criado dentro do método de teste, após a ativação explícita. Se o
setup falhar, o descarte também é tentado. Cada recurso tem limpeza independente;
uma falha de limpeza não impede a tentativa de remover os demais. O Resource
Reaper do Testcontainers complementa essa limpeza. Imagens base e cache de build
podem permanecer no Docker; containers/volumes da aplicação existente não são usados.

O tempo de preparar o seed e construir a API é adicional aos 3 minutos e 30 segundos
da carga. A preparação tem timeout de 15 minutos. Não execute duas cargas ao mesmo
tempo nem use depurador/cobertura de código durante as medições.

## D1: dados determinísticos

- 50 oficinas, representadas por `workshop_id` de 1 a 50.
- 200.000 peças: 4.000 por oficina, com estoque físico 200.
- IDs consecutivos e códigos `PERF-0000001` etc.
- Descrições alternadas: filtro, pastilha, correia, vela e amortecedor.
- 200.000 reservas com 10 peças distintas cada: 2.000.000 itens históricos.
- Cada peça aparece em exatamente dez itens históricos, sempre na mesma oficina.
- Status confirmada/cancelada/expirada, criação entre 1 e 365 dias atrás e
  expiração exatamente 30 minutos após a criação.
- Sem reservas ativas neste cenário: saldo reservado 0 e disponível 200.

O seed roda em transação, exige tabelas vazias e executa `ANALYZE` após o commit.
Todos os SQLs são embarcados no assembly. O schema usa um link ao arquivo existente
para evitar manter duas cópias diferentes do schema.

## D2: cenário e aprovação

- `GET /api/v1/pecas?busca=`, primeira página, 20 resultados.
- `X-Oficina-Id` sorteado entre as 50 oficinas em cada chamada.
- `KeepConstant(copies: 20)`, sem espera entre requisições.
- 30 segundos de aquecimento excluídos das estatísticas; medição de 180 segundos.
- Corpo completo e validação da página incluídos na latência.
- Cliente HTTP compartilhado, timeout de 10 segundos, sem retries ou redirects.
- Aprovação: respostas válidas > 0, zero falhas e **p95 < 150 ms**.

## Relatórios e comparação das versões

São preservados HTML, Markdown, CSV, `summary.json`, `explain-search.txt`, `api.log`
e `postgres.log`. Os resultados ficam em uma subpasta única com rótulo e horário
UTC. Por padrão: `bin/Release/net10.0/reports`, dentro do projeto de testes.
A conexão e a senha do banco não são gravadas nos relatórios.

Variáveis opcionais:

| Variável | Uso |
| --- | --- |
| `PERF_RUN_LABEL` | Rótulo: baseline-01, otimização-01 etc.; padrão baseline |
| `PERF_REPORTS_DIR` | Pasta de saída dos relatórios |
| `PERF_API_DIRECTORY` | Pasta com csproj e Dockerfile, caso a API não seja localizada no checkout |

`PERF_BASE_URL` e `PERF_POSTGRES` não são usados: os endereços e credenciais são
obtidos dos recursos temporários. Um container da aplicação já em execução pode
continuar no ar; o teste cria sua própria instância com outro banco e outra porta.

Faça três execuções por versão, mantendo hardware, recursos do Docker/WSL e
configurações constantes. Cada execução recebe um banco novo com o mesmo seed.
Mantenha `Seed/explain-search.sql` sincronizado com `PartRepository.SearchAsync`
em cada otimização. O script captura a busca vazia e a busca textual `filtro`
separadamente; somente a busca vazia compõe a carga D2.

Registre resultados, ambiente e comparação antes/depois em
`src/UltracarInventoryApi/DESEMPENHO.md`. A pasta `reports/` é ignorada pelo Git;
copie as evidências selecionadas para uma pasta versionada ou anexe-as à entrega.
O tempo SQL de EXPLAIN não substitui o p95 HTTP medido pelo NBomber.

## Visualizar os resultados no navegador

O arquivo `busca-vazia.html` é o relatório visual do NBomber, com gráficos,
latência e percentis (p50/p95/p99), RPS, requisições e falhas. Ele pode ser aberto
no navegador após o teste, mesmo com os containers temporários já removidos.

Documentação: [Testcontainers](https://dotnet.testcontainers.org/),
[NBomber com xUnit](https://nbomber.com/docs/nbomber/asserts_and_thresholds/).


