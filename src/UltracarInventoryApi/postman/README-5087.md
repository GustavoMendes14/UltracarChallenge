# Execucao no ambiente local 5087

API principal: http://localhost:5087

Segunda instancia: http://localhost:5088, conectada ao mesmo PostgreSQL.

## Resultado observado

- 64 requisicoes principais e 39 chamadas auxiliares.
- 210 assertivas aprovadas e 2 falhas.
- R2: 5 respostas 201, 15 respostas 409, 18 amostras de saldo e saldo final zero nas duas instancias.
- Expiracao de 30 minutos naturais: nao executada. O vencimento com fixture preparada foi exercitado.

## Falhas da rodada

A requisicao 46, cancelamento pela proprietaria B, esperava HTTP 200 e recebeu HTTP 409:

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Operation time cannot precede reservation creation.",
  "code": "INVALID_OPERATION_TIME"
}
```

Na requisicao 47, o saldo retornou fisico 5, reservado 1 e disponivel 4; o esperado depois do cancelamento era 5, 0 e 5.
A segunda falha e consequencia do cancelamento rejeitado. As assertivas continuam exigindo o comportamento previsto pelas regras.

## Importar no Postman

1. Importe o arquivo `UltracarInventoryApi.local-5087.postman_environment.json`.
2. Selecione o environment **Ultracar - Local 5087 e 5088**.
3. Use a collection de 64 requisicoes `Ultracar - Regras de negocio R1 a R7 - Rapida` ja entregue.
4. Execute via **Run collection**, em ordem, com uma iteracao.

O environment contem todos os campos, incluindo `oficinaExternaId=990002`.
Foram adicionadas dez pecas de teste e uma reserva vencida somente nas oficinas 990001 e 990002. As 200 mil pecas anteriores foram preservadas.

Antes de repetir a suite inteira, aplique `reset-local-5087.sql` ao banco local. O script verifica o banco e recusa reposicao caso encontre registros fora do padrao SMOKE nessas oficinas.

A segunda instancia iniciada nesta tarefa esta na porta 5088. Se ela for encerrada, inicie outra instancia da mesma API, com a mesma conexao PostgreSQL e a URL http://localhost:5088 antes de executar R2.

No terminal WSL, na pasta da API, a reposicao pode ser feita pelo compose existente:

```sh
docker compose exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < postman/setup/reset-local-5087.sql
```

Se a segunda instancia desta tarefa for encerrada e a porta 5088 estiver livre, pode iniciar outra com a configuracao do compose existente:

```sh
docker compose run -d --no-deps -p 127.0.0.1:5088:8080 api
```
