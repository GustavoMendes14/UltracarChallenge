-- Reposicao somente das fixtures SMOKE das oficinas 990001 e 990002.
-- Nao altera pecas nem reservas das oficinas existentes antes destes testes.
DO $$
BEGIN
    IF current_database() <> 'UltracarInventory' THEN
        RAISE EXCEPTION 'Banco diferente do ambiente local esperado para estes testes.';
    END IF;
END $$;

BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM parts WHERE workshop_id IN (990001,990002) AND (id NOT BETWEEN 99000001 AND 99000010 OR code NOT LIKE 'SMOKE-%'))
       OR EXISTS (SELECT 1 FROM reservations WHERE workshop_id IN (990001,990002) AND service_order_id NOT LIKE 'SMOKE-%') THEN
        RAISE EXCEPTION 'Encontrados registros alheios as fixtures; reset cancelado.';
    END IF;
END $$;

DELETE FROM reservation_idempotency WHERE workshop_id IN (990001, 990002);
DELETE FROM reservation_items WHERE workshop_id IN (990001, 990002);
DELETE FROM reservations WHERE workshop_id IN (990001, 990002);
DELETE FROM parts WHERE workshop_id IN (990001, 990002);

INSERT INTO parts (id, workshop_id, code, description, physical_stock) VALUES
    (99000001, 990001, 'SMOKE-R1-OK', 'Item com saldo para atomicidade', 5),
    (99000002, 990001, 'SMOKE-R1-ZERO', 'Item sem saldo', 0),
    (99000003, 990001, 'SMOKE-R1-LOW', 'Item com saldo menor que o solicitado', 1),
    (99000004, 990001, 'SMOKE-IDEMPOTENCY', 'Item para idempotencia', 5),
    (99000005, 990001, 'SMOKE-CONFIRM', 'Item para confirmacao', 5),
    (99000006, 990001, 'SMOKE-CANCEL', 'Item para cancelamento', 5),
    (99000007, 990001, 'SMOKE-EXPIRED', 'Item com reserva vencida preparada', 1),
    (99000008, 990001, 'SMOKE-CONCURRENCY', 'Item com estoque 5 para 20 reservas paralelas', 5),
    (99000009, 990002, 'SMOKE-FOREIGN', 'Item exclusivo da oficina B', 5),
    (99000010, 990001, 'SMOKE-NATURAL-EXPIRY', 'Item para aguardar os 30 minutos reais', 1);

-- Mantem o TTL real: criadaEm e expiraEm sao deslocadas igualmente.
-- A linha comeca Active para exercitar a expiracao na tentativa de transicao.
WITH reference_time AS (
    SELECT clock_timestamp() - interval '31 minutes' AS created_at
)
INSERT INTO reservations (id, workshop_id, service_order_id, user_id, status, created_at, expires_at)
SELECT 99000001, 990001, 'SMOKE-EXPIRED-SQL', 990001, 1,
    created_at, created_at + interval '30 minutes'
FROM reference_time;

INSERT INTO reservation_items (id, reservation_id, workshop_id, part_id, quantity)
VALUES (99000001, 99000001, 990001, 99000007, 1);

COMMIT;
