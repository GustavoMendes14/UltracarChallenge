# Desempenho

Status: preparação, seed, carga e limpeza automatizados com Testcontainers.
Uma execução completa passou: p95 de 3,92 ms, 1.393.728 respostas válidas e zero falhas.
É uma medição de validação da automação; não foram realizadas três repetições nem
comparações de otimização nesta alteração.

## Ferramenta e reprodução

NBomber 6.7.0, xUnit 2.9.3 e Testcontainers.PostgreSql 4.16.0 em projeto .NET 10 separado da API:
`../Tests/PerformanceTests/UltracarInventoryApi.PerformanceTests.csproj`.
O projeto usa HTTP real contra uma API temporária em Docker, sem TestServer.
Preparação e comandos completos: [README dos testes](../Tests/PerformanceTests/README.md).

O teste exige somente Docker Linux acessível e `RUN_PERFORMANCE_TESTS=1`.
Sem ativação explícita, a carga fica ignorada. Nenhuma URL ou credencial de banco
precisa ser fornecida. Na pasta da API, pelo Bash/WSL:

```sh
RUN_PERFORMANCE_TESTS=1 dotnet test ../Tests/PerformanceTests/UltracarInventoryApi.PerformanceTests.csproj \
  -c Release --filter 'Category=Performance' --logger 'console;verbosity=detailed'
```

Cada execução cria uma rede privada, PostgreSQL `postgres:17`, banco e credenciais
temporários. Aplica schema e seed, constrói a imagem Release da API pelo Dockerfile
existente e inicia uma instância em Production com logs em Warning e porta aleatória.
O teste valida D1 e as 50 oficinas, captura os planos e executa a carga. Ao terminar,
remove containers, dados, rede e imagem temporária da API, inclusive em falhas.
Os relatórios são preservados; imagens base e cache de build podem permanecer.
Nenhum banco, container ou volume da aplicação já existente é utilizado.

## D1 — Dados

Script: [performance-seed.sql](../Tests/PerformanceTests/Seed/performance-seed.sql).
O seed roda automaticamente no banco temporário e só aceita tabelas vazias.

| Dado | Distribuição |
| --- | --- |
| Oficinas | IDs de 1 a 50, representados por workshop_id |
| Peças | 200.000: 4.000 por oficina |
| Estoque físico | 200 por peça |
| Descrições | Filtro, pastilha, correia, vela e amortecedor |
| Reservas históricas | 200.000: 4.000 por oficina |
| Itens históricos | 2.000.000: 10 peças distintas por reserva |
| Status | Confirmada, cancelada e expirada |
| Datas | Criação entre 1 e 365 dias atrás; expiração após 30 minutos |
| Reservas ativas | Nenhuma neste cenário; saldo reservado esperado = 0 |

Cada peça tem exatamente dez itens históricos. O script atualiza as estatísticas
com ANALYZE. Antes do aquecimento, o teste verifica as contagens no PostgreSQL
e valida por HTTP a primeira página das 50 oficinas, incluindo IDs e saldos.

## D2 — Cenário e aprovação

- Endpoint: `GET /api/v1/pecas?busca=`, sem cursor, página de 20 peças.
- `X-Oficina-Id`: sorteado de 1 a 50 em cada requisição.
- Concorrência: `Simulation.KeepConstant(copies: 20)`; sem espera entre chamadas.
- Aquecimento: 30 segundos, fora das estatísticas finais.
- Medição: 180 segundos; corpo completo e validação da página incluídos.
- HTTP: cliente compartilhado, timeout de 10 segundos, sem retries ou redirects.
- Aprovação: respostas válidas > 0, zero falhas e p95 estritamente menor que 150 ms.
- Preparação, validação do seed e EXPLAIN ficam fora da medição.
- API em Release, sem depurador e sem cobertura de código.

São gerados HTML, Markdown, CSV, summary.json, explain-search.txt e logs dos containers
em uma pasta única por execução, antes de descartar o ambiente.
O rótulo PERF_RUN_LABEL identifica baseline/otimização e o número da repetição.
Faça três execuções por versão, preservando dados e condições da máquina.

## Ambiente medido

Preencher na máquina em que a carga for executada:

| Campo | Valor |
| --- | --- |
| Data e horário | 19/11/2026, 16:17–16:21 (America/Sao_Paulo); horários UTC completos no summary.json |
| HEAD do checkout local | 17dce0f9187b24ecace48b97c66653bbba8266ce, com alterações locais |
| CPU | Intel Core i9-14900HX |
| Processadores lógicos visíveis ao gerador | 32 |
| RAM física | Não coletada; memória visível ao WSL: 15,47 GiB |
| Sistema operacional e WSL | Ubuntu 24.04.5 LTS; kernel 6.18.58.1-microsoft-standard-WSL2 |
| Docker | Engine 29.10.0; sem limites adicionais configurados nos containers do teste |
| SDK/runtime .NET e PostgreSQL | SDK 10.0.115; runtime 10.0.15; imagem postgres:17 |
| API e banco | Containers Linux exclusivos do teste; API Release/Production |
| Configuração de conexão/pool e logs | Pool Npgsql padrão; API com logs em Warning; sem credenciais na evidência |

## D3 — Resultados antes e depois

Primeira medição real abaixo. Ainda não foi realizada alteração de consulta ou
índice como parte desta implementação. As evidências da validação ficam em
[Evidence/automation-validation](../Tests/PerformanceTests/Evidence/automation-validation/summary.json).

| Versão/alteração | Execução | p95 (ms) | RPS | Falhas | Relatório |
| --- | --- | --- | --- | --- | --- |
| Baseline: automation-validation | 1 | 3,92 | 7.742,93 | 0 | [NBomber](../Tests/PerformanceTests/Evidence/automation-validation/busca-vazia.md) |
| Baseline | 2 | Pendente | Pendente | Pendente | Pendente |
| Baseline | 3 | Pendente | Pendente | Pendente | Pendente |

Para cada otimização, registrar o gargalo observado, a alteração, o commit e
as três novas medições. A versão anterior é o antes da próxima alteração.
Não misturar buscas textuais e busca vazia no mesmo p95 nem calcular uma média
de p95 como se fosse o percentil combinado de todas as requisições.

## Planos de execução

Script: [explain-search.sql](../Tests/PerformanceTests/Seed/explain-search.sql).
Ele reproduz a consulta completa de PartRepository.SearchAsync, com oficina,
cursor inicial NULL e limite 21, para gerar `EXPLAIN (ANALYZE, BUFFERS, VERBOSE)`.
Há uma execução para busca vazia e outra complementar para `%filtro%`.

Os planos são capturados automaticamente fora da carga, no banco temporário de
cada execução. Manter o SQL do script sincronizado com o repositório em cada versão.
Registrar execução total, linhas reais, loops, buffers e nós relevantes do plano.

| Versão | Busca | Plano completo |
| --- | --- | --- |
| Baseline | Vazia | [Plano completo](../Tests/PerformanceTests/Evidence/automation-validation/explain-search.txt): Execution Time 0,272 ms |
| Baseline | filtro (complementar) | [Plano completo](../Tests/PerformanceTests/Evidence/automation-validation/explain-search.txt): Execution Time 0,277 ms |

O plano da busca vazia usa `uq_parts_workshop_id` para selecionar 21 peças antes
do cálculo de saldo. O histórico é consultado por
`ix_reservation_items_workshop_part_reservation`, com dez itens por peça, e as
reservas ativas por `ix_reservations_active_workshop_id`. Como o seed contém
somente reservas históricas, esse último índice não retorna reservas ativas.
O plano da busca vazia registrou 685 buffers compartilhados em cache e 12 lidos.

O tempo SQL do plano não substitui o p95 HTTP. Os relatórios gerados são ignorados
pelo Git; copiar as evidências selecionadas para uma pasta versionada ou anexá-las
à entrega e substituir os campos pendentes pelos links correspondentes.

## Conclusão

D1, preparação, captura de planos, D2 e descarte do ambiente foram executados
com sucesso. A medição de 180 segundos, após 30 segundos de aquecimento, teve
1.393.728 respostas HTTP 200, zero falhas e p95 de 3,92 ms, abaixo dos 150 ms.
O ciclo completo, incluindo preparação e limpeza, durou cerca de 4 minutos e 9 segundos.

A limpeza também foi observada em falha de pré-validação. Após a execução, as
consultas ao Docker não encontraram containers, redes ou imagens temporárias da
API com o prefixo do teste. Os onze testes leves passaram e a carga ficou ignorada
quando a variável de ativação não estava definida.

Os relatórios completos selecionados foram preservados em
[Evidence/automation-validation](../Tests/PerformanceTests/Evidence/automation-validation/busca-vazia.html).
Repetições e comparações de otimização continuam pendentes; nenhuma otimização
de consulta ou índice foi realizada nesta alteração de infraestrutura do teste.
