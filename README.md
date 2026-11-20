## Ultracar Inventory API

API para consulta de peças e gerenciamento de reservas de estoque por oficina, desenvolvida com .NET 10, ASP.NET Core, PostgreSQL e Dapper.

# Como executar

Pré-requisitos: Docker com suporte a containers Linux e Docker Compose. Os comandos abaixo devem ser executados pelo Bash/WSL.

Na primeira execução, prepare o banco com os scripts da pasta `database-scripts`, na raiz do repositório, antes de iniciar a API. O Compose não executa esses scripts automaticamente.

1. A partir da raiz do repositório, acesse a pasta da API e crie o arquivo de configuração:

   ```sh
   cd src/UltracarInventoryApi
   cp .env.example .env
   ```

   No `.env`, preencha `POSTGRES_PASSWORD` e configure `POSTGRES_DB=UltracarInventory`, nome exigido pelo seed. Se o `.env` já existir, mantenha-o e confira esses valores.

2. Suba somente o PostgreSQL e aguarde o health check indicar que está pronto:

   ```sh
   docker compose up -d --wait postgres
   ```

3. Crie as tabelas, os índices e as restrições com o script de schema:

   ```sh
   docker compose exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < ../../database-scripts/01-schema.sql
   ```

4. Execute o seed para cadastrar os dados de exemplo:

   ```sh
   docker compose exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < ../../database-scripts/02-seed.sql
   ```

   O seed de 2% gera dados para 50 oficinas: 4.000 peças, 4.000 reservas históricas e 40.000 itens de reserva.

5. Depois que os dois scripts terminarem sem erros, suba a API:

   ```sh
   docker compose up --build -d api
   ```

Execute o schema apenas em um banco sem as tabelas da aplicação e o seed apenas com essas tabelas vazias. Se o schema já tiver sido aplicado, pule a etapa 3. Se o banco já estiver populado, pule também a etapa 4. Não é necessário repetir os scripts ao reiniciar os containers.

- API: http://localhost:5087
- Documentação interativa: http://localhost:5087/scalar/v1
- PostgreSQL: localhost:5433

Para encerrar os serviços, preservando os dados:

```sh
docker compose down
```

## DECISÕES E TRADE-OFFS

- Arquitetura limpa com DDD

O projeto separa responsabilidades em Domain, Application, Infrastructure e Controllers, mantendo as camadas em pastas de um único projeto.

Isso simplifica a solução e facilita a navegação. Como contrapartida, os limites entre camadas dependem de disciplina, pois não são impostos por projetos separados.

- Dapper e SQL explícito
O Dapper permite controlar consultas, transações e bloqueios diretamente, especialmente nos fluxos de estoque.

O custo é manter manualmente o SQL, os mapeamentos e a evolução do schema.

o status persistido pode continuar como ativo até uma operação registrar a expiração.

- Paginação por cursor

A busca retorna até 20 peças, ordenadas por ID, e calcula os saldos após limitar a página.

Isso evita o custo crescente de grandes offsets.

- Isolamento por oficina

As consultas e alterações incluem o identificador da oficina, e o schema possui relacionamentos compostos para impedir vínculos entre oficinas.


## O que eu melhoraria com mais tempo:

- Eu adicionaria autenticação e autorização, associando usuário e oficina com as permissões da identidade que foi autenticada.

- Ampliaria os testes em geral.

- Criaria testes end to end.

- Criaria um job para atualização periódica dos status expirados.

- Adicionaria health checks da API, logs e métricas.

- Adicionaria politicas de retry.