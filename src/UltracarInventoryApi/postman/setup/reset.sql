-- Executar somente no banco descartavel criado por compose.smoke.yml.
DO $$
BEGIN
    IF current_database() <> 'ultracar_smoke' THEN
        RAISE EXCEPTION 'Reset permitido somente no banco ultracar_smoke.';
    END IF;
END $$;

BEGIN;

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
