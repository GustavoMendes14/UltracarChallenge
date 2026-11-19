# Postman — regras de negócio R1 a R7

Importe estes arquivos pelo botão **Import** do Postman:

- `UltracarInventoryApi.regras.postman_collection.json`: 64 requisições com cenários de sucesso e erro.
- `UltracarInventoryApi.expiracao-natural.postman_collection.json`: 9 etapas, incluindo a espera pelos 30 minutos reais de R4.
- `UltracarInventoryApi.smoke.postman_environment.json`: environment das duas instâncias e dos dados preparados.

As collections seguem os controllers, DTOs e códigos de negócio implementados na API.
Por exemplo, falta de saldo usa `INSUFFICIENT_STOCK`, o código atual do projeto.

## Preparar o ambiente isolado

No terminal WSL, na pasta da API (`src/UltracarInventoryApi`):

Se utilizar o ZIP, mantenha a pasta `postman` dentro dessa pasta da API, ao lado do `Dockerfile` existente.

```sh
docker compose -p ultracar-smoke -f postman/compose.smoke.yml up -d --build
```

Esse compose cria um PostgreSQL próprio, duas instâncias independentes da API e os dados de teste:

| Recurso | Endereço |
| --- | --- |
| API A | `http://localhost:15087` |
| API B | `http://localhost:15088` |
| PostgreSQL de smoke | `localhost:15433`, banco e usuário `ultracar_smoke` |

As duas APIs usam o mesmo banco `ultracar_smoke`. O compose e o volume são separados do ambiente local existente.
As credenciais desse compose são exclusivas do ambiente descartável de testes.

Na primeira inicialização, o PostgreSQL executa `setup/schema.sql` e `setup/reset.sql`.
Antes de cada nova execução completa da collection rápida, restaure os dados:

```sh
docker compose -p ultracar-smoke -f postman/compose.smoke.yml exec -T postgres \
  psql -v ON_ERROR_STOP=1 -U ultracar_smoke -d ultracar_smoke < postman/setup/reset.sql
```

O reset verifica o nome do banco e afeta somente os dados das oficinas de teste `990001` e `990002`.
Não execute reset durante uma collection em andamento, incluindo a espera de 30 minutos.
O esquema SQL é uma cópia do utilizado nos testes de integração e deve acompanhar alterações de schema da API.

## Executar no Postman

1. Importe os três JSONs e selecione **Ultracar - Smoke local - Duas instancias**.
2. Abra a collection **Ultracar - Regras de negocio R1 a R7 - Rapida**.
3. Selecione **Run collection**, mantenha todas as requisições na ordem e use uma iteração.
4. Confira **Test Results**. Preparação inválida ou erro de script interrompem o fluxo. Na suíte rápida, falhas de assertivas de negócio ficam registradas e os próximos cenários continuam; na collection natural, qualquer falha interrompe a espera.
5. Execute a collection de expiração natural separadamente para validar a passagem real do tempo.

Os IDs, saldos iniciais e as oficinas do environment correspondem a `setup/reset.sql`.
Ordens de serviço e chaves de idempotência recebem um identificador novo em cada execução.
As repetições de R3 reutilizam intencionalmente a mesma chave e o mesmo corpo.
Execute a collection inteira; requisições isoladas podem depender de respostas anteriores.

## Cobertura

| Regra | Cenários e assertivas |
| --- | --- |
| R1 | Um lote com um item disponível e dois em falta retorna 409; `details` lista todos e somente os itens em falta, quantidades e saldos; consultas posteriores comprovam ausência de reserva parcial. O sucesso com dois itens também é validado. |
| R2 | Vinte POSTs de uma unidade são disparados concorrentemente, alternando entre duas APIs. Exige exatamente 5 respostas 201 e 15 respostas 409, IDs distintos, erro `INSUFFICIENT_STOCK` e saldo final `[físico: 5, reservado: 5, disponível: 0]` em ambas as instâncias. Consulta saldos também durante a disputa. |
| R3 | Mesma chave e corpo retornam o mesmo JSON e ID, inclusive depois do cancelamento; o saldo não é reservado novamente. Corpo diferente com a mesma chave retorna 422 sem alterar os saldos. A repetição de uma resposta 409 também é validada. |
| R4 | A rápida usa uma reserva Active criada há 31 minutos, com vencimento exatamente 30 minutos depois da criação; confirma a liberação do saldo, a rejeição da confirmação vencida e a possibilidade de reutilizar o saldo. A collection natural aguarda os 30 minutos reais. |
| R5 | Active para Confirmed e Active para Cancelled funcionam. Confirmed/Cancelled para confirmação ou cancelamento retornam 409. Active vencida passa pelo fluxo de expiração; Expired para confirmação ou cancelamento retorna 409. Saldos comprovam ausência de baixa dupla e liberação no cancelamento. |
| R6 | As oficinas enxergam somente suas peças. Saldo, criação, confirmação e cancelamento sobre recursos de outra oficina retornam 404; o recurso permanece utilizável pela proprietária. Também rejeita um lote misturando peças das duas oficinas sem reservar parcialmente. |
| R7 | Todas as respostas negativas validam `application/problem+json`, `status`, `title` e `code`. Valida `detail`, `type` e `instance` quando presentes. Exercita respostas 400, 404, 409 e 422, incluindo headers ausentes, corpo inválido, itens duplicados e cursor malformado. |

R2 usa `pm.sendRequest` para iniciar as vinte chamadas antes de aguardar `Promise.all`; o Runner sequencial sozinho não produziria essa concorrência.
As URLs precisam ter origens diferentes. O compose garante dois processos independentes, compartilhando o PostgreSQL.
O teste exercita a concorrência observada e amostra os saldos; uma execução finita não prova a ausência de qualquer corrida possível.
A proibição de locks em memória também exige revisão de código: testes HTTP não identificam sozinhos o mecanismo de sincronização.
Os repositórios atuais usam `SELECT ... FOR UPDATE` no PostgreSQL.

## Expiração natural — aproximadamente 30 minutos

A collection **Ultracar - R4 - Expiracao natural em 30 minutos**:

1. Cria uma reserva de uma unidade na peça dedicada, com estoque 1, e valida o TTL retornado de 30 minutos.
2. Confere o bloqueio do saldo e consulta a outra instância a cada até 15 segundos.
3. Verifica que o saldo não é liberado antes do vencimento, respeitando a precisão de um segundo do header HTTP `Date`.
4. Somente depois de `expiraEm`, com margem de dois segundos, exige saldo reservado 0 e disponível 1.
5. Exige 409 ao confirmar ou cancelar a reserva expirada, cria outra reserva com o saldo liberado e a cancela.

O loop usa o prazo retornado pela API e o relógio HTTP do servidor. Existe limite de espera de 35 minutos.
Não altere o TTL da API nem o relógio do sistema. O teste precisa do **Runner**; o botão **Send** não executa os desvios de fluxo.
No Runner, permita que a collection fique em execução durante todo o período.
Para validar a persistência da transição da fixture vencida da collection rápida, pode consultar depois:

```sh
docker compose -p ultracar-smoke -f postman/compose.smoke.yml exec -T postgres \
  psql -U ultracar_smoke -d ultracar_smoke \
  -c 'SELECT id, status FROM reservations WHERE id = 99000001;'
```

O status deve ser `4` (`Expired`). A API expira essa linha ao tentar confirmar; o saldo já ignora reservas vencidas na consulta.
A API não expõe um GET individual da reserva; a confirmação da coluna de status persistida usa essa consulta SQL complementar.

## Efeitos e repetição

A collection rápida confirma uma unidade da peça `SMOKE-CONFIRM` e deixa cinco reservas ativas na peça de concorrência, com saldo disponível zero.
Use o reset antes de executar novamente. As demais reservas criadas pela suíte rápida são canceladas ou já estão expiradas.
Uma falha pode interromper o fluxo antes do cancelamento; o reset restaura as fixtures.
A collection natural usa outra peça e termina liberando seu saldo.

Para encerrar somente esse ambiente:

```sh
docker compose -p ultracar-smoke -f postman/compose.smoke.yml down
```

## Execução por linha de comando

Os arquivos usam collection v2.1, sem pacotes privados. Também podem ser executados por um runner compatível, como Newman:

```sh
newman run postman/UltracarInventoryApi.regras.postman_collection.json \
  -e postman/UltracarInventoryApi.smoke.postman_environment.json --timeout-script 120000
```

Para a collection natural, use tempo total suficiente para 35 minutos e tempo por script superior a 15 segundos:

```sh
newman run postman/UltracarInventoryApi.expiracao-natural.postman_collection.json \
  -e postman/UltracarInventoryApi.smoke.postman_environment.json \
  --timeout 2200000 --timeout-script 120000
```

Referências: [requisições assíncronas](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-send-request/)
e [controle de fluxo no Runner](https://learning.postman.com/docs/tests-and-scripts/running-collections/building-workflows/).

## Validação destes arquivos

A collection rápida foi executada com Newman contra as duas instâncias e o PostgreSQL real do compose:
64 requisições principais, 29 chamadas auxiliares e **214 assertivas aprovadas, sem falhas** na rodada final.
R2 produziu exatamente 5 respostas 201, 15 respostas 409 e saldo disponível zero nas duas instâncias;
o monitor também coletou 8 amostras de saldo durante a disputa.

Os 93 scripts das duas collections passaram pela verificação de sintaxe.
O fluxo da collection natural foi verificado com respostas e relógio simulados, incluindo a rejeição de liberação antecipada.
**A espera real de 30 minutos não foi executada nesta validação.** Para verificá-la contra a API, rode a collection natural pelo Runner.
