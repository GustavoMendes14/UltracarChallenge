\set ON_ERROR_STOP on
-- Run once against an empty, dedicated performance database with the API schema.
-- No TRUNCATE or DELETE: reruns fail without replacing existing data.
BEGIN;
LOCK TABLE parts, reservations, reservation_items, reservation_idempotency IN ACCESS EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM parts) OR EXISTS (SELECT 1 FROM reservations)
        OR EXISTS (SELECT 1 FROM reservation_items) OR EXISTS (SELECT 1 FROM reservation_idempotency) THEN
        RAISE EXCEPTION 'O seed exige tabelas vazias em um banco exclusivo de desempenho.';
    END IF;
END $$;

INSERT INTO parts (id, workshop_id, code, description, physical_stock)
SELECT n, ((n - 1) / 4000) + 1,
       'PERF-' || lpad(n::text, 7, '0'),
       (ARRAY['Filtro de oleo', 'Pastilha de freio', 'Correia dentada',
              'Vela de ignicao', 'Amortecedor'])[((n - 1) % 5 + 1)::integer]
           || ' modelo ' || ((n - 1) % 100)::text,
       200
FROM generate_series(1::bigint, 200000::bigint) AS g(n);

INSERT INTO reservations
    (id, workshop_id, service_order_id, user_id, status, created_at, expires_at)
SELECT n, ((n - 1) / 4000) + 1, 'PERF-OS-' || n::text,
       1 + ((n - 1) % 20), (2 + ((n - 1) % 3))::integer,
       CURRENT_TIMESTAMP - interval '1 day' - ((n - 1) % 365) * interval '1 day',
       CURRENT_TIMESTAMP - interval '1 day' - ((n - 1) % 365) * interval '1 day'
           + interval '30 minutes'
FROM generate_series(1::bigint, 200000::bigint) AS g(n);

-- 200,000 reservations x 10 distinct parts, all from the same workshop.
-- Each of the 200,000 parts appears in exactly ten historical items.
INSERT INTO reservation_items (reservation_id, workshop_id, part_id, quantity)
SELECT r.id, r.workshop_id,
       (r.workshop_id - 1) * 4000 + ((r.id - 1 + item_offset) % 4000) + 1,
       (1 + item_offset % 3)::integer
FROM reservations r CROSS JOIN generate_series(0, 9) AS g(item_offset);

SELECT setval(pg_get_serial_sequence('parts', 'id'), (SELECT max(id) FROM parts));
SELECT setval(pg_get_serial_sequence('reservations', 'id'), (SELECT max(id) FROM reservations));
COMMIT;

ANALYZE parts;
ANALYZE reservations;
ANALYZE reservation_items;

SELECT count(*) AS parts, count(DISTINCT workshop_id) AS workshops FROM parts;
SELECT workshop_id, count(*) AS parts FROM parts GROUP BY workshop_id ORDER BY workshop_id;
SELECT count(*) AS historical_items FROM reservation_items;
SELECT status, count(*) AS reservations FROM reservations GROUP BY status ORDER BY status;
