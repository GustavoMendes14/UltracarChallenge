## Ultracar Inventory API

API para consulta de peças e gerenciamento de reservas de estoque por oficina, desenvolvida com .NET 10, ASP.NET Core, PostgreSQL e Dapper.

# Como executar

Pré-requisitos: Docker com suporte a containers Linux e Docker Compose.

A partir da raiz do repositório, acesse a pasta da API:
cd src/UltracarInventoryApi
Na primeira execução, copie .env.example para .env e preencha POSTGRES_PASSWORD:
cp .env.example .env
Depois dessa configuração, suba a API e o PostgreSQL com um comando:

- docker compose up --build -d

- API: http://localhost:5087
- Documentação interativa: http://localhost:5087/scalar/v1
- PostgreSQL: localhost:5433

Inicialização do banco: atualmente, o Compose não aplica o schema automaticamente. Em um banco novo, após subir os containers, execute pelo Bash/WSL:
docker compose exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < ../Tests/IntegrationTests/Base/DatabaseSchema.sql
Esse script cria as tabelas e os índices, sem cadastrar peças. Execute-o apenas na inicialização de um banco vazio.

Para encerrar os serviços, preservando os dados:

- docker compose down

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