## Ferramenta de IA utilizada:

- Codex

## Onde foi utilizada:

- Utilizei a IA como uma parceira no desenvolvimento, para esclarecer dúvidas técnicas, gerar trechos de codigo repetitivos, gerar documentação, auxiliar na elaboração de testes unitários, de integração e de desempenho, além dos dados de teste. Revisei e adaptei as sugestões incorporadas ao projeto. As decisões técnicas e a validação da solução ficaram sob minha responsabilidade.

# Resumo:

- Duvidas em geral.

- Geração de documentação

- Ajudar na geração de testes unitarios, teste de carga e teste de integração e os seeds para fazer o teste.

- Auxiliar a escrever algumas partes do codigo mais repetitivas.

## Prompts que mais me ajudaram:

# Por eu ja ter visto algo sobre NBomber, mas nunca havia feito teste de carga gerei o prompt pedindo a IA me ajudar a criar os testes

## 1º Prompt:

como fazer esse teste de desempenho utilizando NBomber

Desempenho
■ D1: crie uma carga (seed) com 200 mil peças distribuídas em 50 oficinas e 2 milhões de
itens de reserva históricos.GRUPO OFICINA BRASIL · BU ULTRACAR

■ D2: com 20 usuários simultâneos, GET /api/v1/pecas?busca= deve ter p95 abaixo de 150 ms
na sua máquina.

■ D3: entregue o DESEMPENHO.md com a ferramenta de carga usada (k6, JMeter, Gatling,
NBomber…), o cenário, o resultado antes e depois de cada otimização e o plano de
execução (EXPLAIN ANALYZE) da consulta principal.

Resposta:

Você pode atender aos três requisitos com um seed SQL, um projeto Console com NBomber e um relatório com medições reais.

No seu projeto, a busca exige X-Oficina-Id, retorna 20 peças por página e calcula o saldo considerando as reservas. O teste precisa passar pela API inteira.

D1 — Prepare o banco antes da medição

Monte um script de seed com esta distribuição:
Dado	Quantidade
Oficinas, identificadas por workshop_id de 1 a 50	50
Peças por oficina	4.000
Total de peças	200.000
Reservas históricas por oficina	4.000
Itens por reserva histórica	10
Total de itens históricos	2.000.000


No seu schema, as oficinas são representadas por workshop_id; não há tabela de oficinas.

Use INSERT ... SELECT ... FROM generate_series(...) no PostgreSQL ou importação em lote com Npgsql. Evite milhões de chamadas HTTP para preparar os dados.

O seed deve:
- Variar códigos e descrições, incluindo termos como filtro, pastilha e correia.
- Relacionar peças e reservas da mesma oficina.
- Criar reservas históricas confirmadas, canceladas e expiradas.
- Respeitar expires_at = created_at + interval '30 minutes'.
- Opcionalmente adicionar reservas ativas para exercitar o cálculo de saldo; documente esse volume adicional.
Depois do seed:
ANALYZE parts;
ANALYZE reservations;
ANALYZE reservation_items;

SELECT count(*) AS pecas,
       count(DISTINCT workshop_id) AS oficinas
FROM parts;

SELECT workshop_id, count(*) AS pecas
FROM parts
GROUP BY workshop_id
ORDER BY workshop_id;

SELECT count(*) AS itens_historicos
FROM reservation_items;
O ANALYZE atualiza as estatísticas usadas pelo otimizador. Documentação do PostgreSQL.

D2 — Crie o teste com 20 usuários simultâneos

Crie um projeto Console separado da API. Execute estes comandos na pasta onde deseja guardar o projeto:
dotnet new console -n UltracarInventoryApi.PerformanceTests -f net10.0
cd UltracarInventoryApi.PerformanceTests
dotnet add package NBomber --version 6.7.0
Coloque este código em Program.cs:
using System.Net;
using NBomber.Contracts;
using NBomber.CSharp;

var baseUrl = args.ElementAtOrDefault(0) ?? "http://localhost:5087";
var search = args.ElementAtOrDefault(1) ?? "";
var execution = args.ElementAtOrDefault(2) ?? "baseline";

using var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = 100,
    AllowAutoRedirect = false
};

using var client = new HttpClient(handler)
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(10)
};

var path = $"/api/v1/pecas?busca={Uri.EscapeDataString(search)}";

var scenario = Scenario.Create("buscar_pecas", async context =>
{
    // Distribui as requisições entre as 50 oficinas do seed.
    var workshopId = context.Random.Next(1, 51);

    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Add("X-Oficina-Id", workshopId.ToString());

    try
    {
        // GetAsync/SendAsync, por padrão, aguarda o corpo completo.
        using var response = await client.SendAsync(request);
        var status = ((int)response.StatusCode).ToString();

        return response.StatusCode == HttpStatusCode.OK
            ? Response.Ok(statusCode: status)
            : Response.Fail(statusCode: status);
    }
    catch (HttpRequestException)
    {
        return Response.Fail(statusCode: "HTTP_ERROR");
    }
    catch (OperationCanceledException)
    {
        return Response.Fail(statusCode: "TIMEOUT");
    }
})
.WithWarmUpDuration(TimeSpan.FromSeconds(30))
.WithLoadSimulations(
    Simulation.KeepConstant(
        copies: 20,
        during: TimeSpan.FromMinutes(3)));

var result = NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFolder($"reports/{execution}")
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .Run();

var stats = result.ScenarioStats
    .Single(x => x.ScenarioName == "buscar_pecas");

var p95 = stats.Ok.Latency.Percent95;
var failures = stats.Fail.Request.Count;
var successes = stats.Ok.Request.Count;

var passed = successes > 0 && failures == 0 && p95 < 150;

Console.WriteLine(
    $"p95={p95:F2} ms; falhas={failures}; aprovado={passed}");

Environment.ExitCode = passed ? 0 : 1;
KeepConstant(copies: 20) mantém 20 usuários virtuais executando requisições continuamente. Isso representa concorrência, e não 20 requisições por segundo. Os 30 segundos de aquecimento ficam fora do resultado. Concorrência, aquecimento.

O código exige zero falhas como critério adicional para evitar aprovar uma execução que apresenta erros.

Com a API iniciada, execute:
 Requisito literal: busca vazia.
dotnet run -c Release -- "http://localhost:5087" "" "baseline-vazia-01"

 Cenários complementares, medidos separadamente.
dotnet run -c Release -- "http://localhost:5087" "filtro" "baseline-filtro-01"
dotnet run -c Release -- "http://localhost:5087" "inexistente-xyz" "baseline-sem-resultados-01"
A porta 5087 corresponde ao seu Docker Compose. Ajuste a URL se executar a API de outra maneira. Como o teste rejeita redirecionamentos, use diretamente a URL final que retorna HTTP 200.

Antes da carga, confirme manualmente que as oficinas possuem dados e que uma busca vazia retorna peças. Durante as medições, use a API em Release, sem depurador, e mantenha constantes a configuração do banco e os recursos do Docker/WSL.

Faça três execuções de cada cenário e registre cada resultado. Não misture buscas vazias, preenchidas e sem resultados em um único p95.

D3 — Capture o plano e compare cada alteração

A consulta principal está em PartRepository.SearchAsync. Copie o SQL completo e acrescente:
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
-- SQL completo de SearchAsync, com valores concretos.
Substitua os parâmetros assim:
Parâmetro	Busca vazia	Busca por filtro
@WorkshopId	1	1
@AfterPartId	NULL::bigint	NULL::bigint
@Pattern	NULL::text	'%filtro%'::text
@Limit	21	21


A API consulta 21 registros para decidir se existe próxima página e devolve 20.

Capture o plano antes e depois de cada otimização, fora da execução do NBomber. O plano mostra o comportamento do banco; a latência do NBomber inclui também HTTP, execução da aplicação e transferência da resposta. Documentação de EXPLAIN.

No seu código, a consulta já pagina antes de calcular os saldos, e o schema dos testes já declara índices para busca textual e reservas. Portanto, registre a implementação atual como baseline e use os planos para decidir a próxima alteração.

O DESEMPENHO.md pode seguir este modelo:
 Desempenho

 Ambiente
- CPU e RAM:
- Sistema operacional:
- Limites de CPU/RAM do Docker/WSL:
- Versões: .NET, PostgreSQL e NBomber:
- Commit avaliado:
- API em Release, sem depurador:

 Seed
- Comando utilizado:
- 200.000 peças em 50 oficinas.
- 2.000.000 itens de reserva históricos.
- Distribuição de descrições, status e reservas ativas:
- Consultas de validação e resultados:

 Cenário
- GET /api/v1/pecas?busca=
- X-Oficina-Id: distribuído entre 1 e 50.
- 20 usuários simultâneos.
- Aquecimento: 30 segundos.
- Medição: 3 minutos.
- Três execuções por versão e cenário.
- Critério: p95 < 150 ms; zero falhas.

 Resultados
| Versão/alteração | Busca | Execução | p95 ms | RPS | Falhas | Relatório |
|---|---|---|---:|---:|---:|---|
| Baseline | vazia | 1 | preencher | preencher | preencher | link |
| Após otimização 1 | vazia | 1 | preencher | preencher | preencher | link |

 Otimização 1
- Problema observado:
- Alteração realizada:
- Resultado antes e depois:
- EXPLAIN (ANALYZE, BUFFERS, VERBOSE) antes:
- EXPLAIN (ANALYZE, BUFFERS, VERBOSE) depois:

 Conclusão
- D2 atendido ou não, conforme as medições.
- Limitações e próximos passos.
Os relatórios HTML/Markdown são gerados pelo NBomber. Preencha o documento com os resultados obtidos; nenhuma carga foi executada nesta análise. Relatórios.



# Esse segundo prompt foi muito util pois a IA me mostrou uma forma muito interessante de implementar idempotencia.

## 2º Prompt

Preciso implementar a chave de idempotencia qual a melhor forma de fazer ? Utilizando Redis ou um banco sql?

# Resposta:

Para essa API, eu recomendo usar o PostgreSQL que você já utiliza. O principal motivo é conseguir salvar a reserva e o registro de idempotência na mesma transação. Não alterei nenhum arquivo.

Por que eu escolheria SQL nesse caso?

Considere esta falha usando Redis como único registro de idempotência:
1. A API cria a reserva no PostgreSQL.
2. O PostgreSQL confirma a transação.
3. A aplicação cai antes de salvar o resultado no Redis.
4. O cliente repete a chamada, e o Redis não tem o registro da conclusão.
Sem outra proteção no banco, a reserva pode ser criada novamente. Gravar primeiro no Redis também exige tratar o caso inverso: a chave existe, mas a reserva falhou.

No PostgreSQL, você pode confirmar reserva + registro + resultado juntos, ou desfazer tudo. Essa atomicidade é a vantagem central para seu cenário. Documentação de transações.

O Redis pode fazer sentido como cache de respostas em um cenário com necessidade comprovada de desempenho. Porém, utilizá-lo como única garantia exigiria resolver a coordenação com o PostgreSQL e definir cuidadosamente persistência e recuperação. A durabilidade do Redis depende da configuração utilizada. Documentação de persistência.

Como eu manteria o desenho:
1. O cliente gera uma chave por nova operação e reutiliza a mesma chave nas tentativas dessa operação.
2. A API tenta registrar a chave dentro da transação, protegida por uma restrição única.
3. Se conseguir, executa a operação, salva o resultado e confirma tudo.
4. Se a chave já existir e o conteúdo for igual, devolve o resultado original.
5. Se a chave existir com conteúdo diferente, rejeita a reutilização.
Para duas requisições simultâneas, a proteção precisa estar no banco; apenas consultar se a chave existe antes de executar não resolve a concorrência.

# Esse terceiro prompt foi muito util pois a IA gerou testes unitarios utilizando um padrão que sempre
# uso nos meus projetos, garantindo que as regras de negocio foram aplicadas e economizando muito tempo de
# escrita, apenas gastei tempo revisando para garantir que os testes estavam corretos.

## 3º Prompt

pegue como base esse estilo de teste unitarios e faça o mesmo para atender as regras de negocio do pdf. Use apenas esse estilo e as camadas não precisam ser separadas por projeto apenas por pastas

# Resposta:
Implementei 91 testes unitários, todos passando, seguindo o padrão de referência: xUnit, FluentAssertions, Moq, Bogus, fixtures, Fact/`Theory` e Trait.

Um único projeto de testes, com camadas por pastas. Cobertura de R1, R3, R4 e R5, além de isolamento por oficina, paginação e erros RFC 7807.

dotnet test Tests/UltracarInventoryApi.UnitTests/UltracarInventoryApi.UnitTests.csproj
O teste de concorrência com PostgreSQL permanece separado, pois exige integração real.



## Caso em que a IA Errou:

A IA sugeriu inicialmente um projeto Console dedicado para executar os testes de desempenho. Preferi utilizar um projeto com xUnit e NBomber, separando cenários, configuração e validações, para manter consistência com a organização dos testes da solução.

# Resposta errada da IA:

Coloque este código em Program.cs:
using System.Net;
using NBomber.Contracts;
using NBomber.CSharp;

var baseUrl = args.ElementAtOrDefault(0) ?? "http://localhost:5087";
var search = args.ElementAtOrDefault(1) ?? "";
var execution = args.ElementAtOrDefault(2) ?? "baseline";

using var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = 100,
    AllowAutoRedirect = false
};

using var client = new HttpClient(handler)
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(10)
};

var path = $"/api/v1/pecas?busca={Uri.EscapeDataString(search)}";

var scenario = Scenario.Create("buscar_pecas", async context =>
{
    // Distribui as requisições entre as 50 oficinas do seed.
    var workshopId = context.Random.Next(1, 51);

    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Add("X-Oficina-Id", workshopId.ToString());

    try
    {
        // GetAsync/SendAsync, por padrão, aguarda o corpo completo.
        using var response = await client.SendAsync(request);
        var status = ((int)response.StatusCode).ToString();

        return response.StatusCode == HttpStatusCode.OK
            ? Response.Ok(statusCode: status)
            : Response.Fail(statusCode: status);
    }
    catch (HttpRequestException)
    {
        return Response.Fail(statusCode: "HTTP_ERROR");
    }
    catch (OperationCanceledException)
    {
        return Response.Fail(statusCode: "TIMEOUT");
    }
})
.WithWarmUpDuration(TimeSpan.FromSeconds(30))
.WithLoadSimulations(
    Simulation.KeepConstant(
        copies: 20,
        during: TimeSpan.FromMinutes(3)));

var result = NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFolder($"reports/{execution}")
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .Run();

var stats = result.ScenarioStats
    .Single(x => x.ScenarioName == "buscar_pecas");

var p95 = stats.Ok.Latency.Percent95;
var failures = stats.Fail.Request.Count;
var successes = stats.Ok.Request.Count;

var passed = successes > 0 && failures == 0 && p95 < 150;

Console.WriteLine(
    $"p95={p95:F2} ms; falhas={failures}; aprovado={passed}");

Environment.ExitCode = passed ? 0 : 1;


## Proximos prompts para corrigir o erro:

Não faça esse teste na program, qual a forma mais recomendada de fazer esse tipo de teste ?

# Resposta

Não faça esse teste na program, qual a forma mais recomendada de fazer esse tipo de teste ?

Para o seu projeto, recomendo um projeto dedicado de testes de desempenho com xUnit + NBomber, separado dos testes unitários e de integração. O NBomber documenta essa integração com xUnit para executar a carga e validar os resultados com assertions. Documentação oficial.

Como você já usa xUnit, ficaria assim:
Tests/
├── UnitTests/
├── IntegrationTests/
└── PerformanceTests/
    ├── PerformanceTests.csproj
    ├── Scenarios/
    │   └── PartSearchScenario.cs
    ├── Tests/
    │   └── PartSearchPerformanceTests.cs
    ├── Seed/
    │   └── performance-seed.sql
    └── xunit.runner.json
Cada parte teria uma responsabilidade:
- Seed: prepara as 200 mil peças e os 2 milhões de itens históricos antes das medições.
- Scenario: configura HTTP, X-Oficina-Id, 20 usuários, aquecimento e duração.
- Teste xUnit: executa o cenário e verifica p95, quantidade de respostas e falhas.
- Relatórios: guardam as evidências para o DESEMPENHO.md.

O método de teste executaria o cenário e faria estas verificações:
var result = NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .Run();

var stats = result.ScenarioStats.Single();

Assert.True(
    stats.Ok.Request.Count > 0,
    "O teste precisa produzir respostas válidas.");

Assert.Equal(0L, stats.Fail.Request.Count);

Assert.True(
    stats.Ok.Latency.Percent95 < 150,
    $"p95 obtido: {stats.Ok.Latency.Percent95:F2} ms.");
Para a medição representar o requisito, a API deve estar em outro processo, em Release, acessível por HTTP real — por exemplo, pelo seu Docker Compose. O NBomber chama http://localhost:5087/api/v1/pecas?busca=. Usar WebApplicationFactory com o servidor em memória muda o caminho medido e não representa a mesma execução.

Eu adotaria este fluxo:
1. Subir PostgreSQL e API.
2. Executar o seed uma vez e validar as contagens.
3. Executar o projeto de desempenho, sem depurador e sem cobertura de código.
4. Guardar os relatórios e capturar o EXPLAIN ANALYZE.
5. Aplicar uma otimização e repetir nas mesmas condições.
Desabilite o paralelismo entre os testes desse projeto: o NBomber já controla os 20 usuários; dois testes de carga executados juntos alterariam a concorrência.

Execute a suíte por um comando próprio:
dotnet test "<caminho-do-PerformanceTests.csproj>" -c Release
Mantenha essa execução em uma etapa específica da pipeline. Um atributo [Trait("Category", "Performance")] ajuda a filtrar, mas não impede sozinho que o teste rode no dotnet test geral; isso precisa ser configurado nos comandos da suíte.

Essa organização permite manter o cenário reutilizável e transformar o requisito p95 < 150 ms em um teste automatizado, com relatórios e aprovação ou falha explícita.

## Como você validou o que foi gerado ?

Validei lendo o codigo gerado pela IA e vendo se fazia sentido e mudando o que fosse necessario.

No final pedi para ela gerar cenarios de smoke testes para garantir o fluxo completo end to end das regras de negocio.