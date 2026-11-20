BEGIN;

-- ============================================================
-- Validate the target database.
-- ============================================================

DO $database_check$
BEGIN
    IF current_database() <> 'UltracarInventory' THEN
        RAISE EXCEPTION
            'Connect to database UltracarInventory before running this seed. Current database: %',
            current_database();
    END IF;
END;
$database_check$;

-- ============================================================
-- Prevent concurrent writes during the seed.
-- ============================================================

LOCK TABLE
    public.parts,
    public.reservations,
    public.reservation_items,
    public.reservation_idempotency
IN ACCESS EXCLUSIVE MODE;

-- ============================================================
-- Require empty application tables.
-- Existing records are preserved.
-- ============================================================

DO $empty_tables_check$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM public.parts
    )
    OR EXISTS (
        SELECT 1
        FROM public.reservations
    )
    OR EXISTS (
        SELECT 1
        FROM public.reservation_items
    )
    OR EXISTS (
        SELECT 1
        FROM public.reservation_idempotency
    ) THEN
        RAISE EXCEPTION
            'Seed requires empty application tables.';
    END IF;
END;
$empty_tables_check$;

-- ============================================================
-- Parts
--
-- 50 workshops.
-- 80 parts per workshop.
-- 4,000 parts in total.
--
-- Physical stock represents the current stock balance.
-- ============================================================

INSERT INTO public.parts (
    id,
    workshop_id,
    code,
    description,
    physical_stock
)
SELECT
    source.part_id,

    (
        (source.part_id - 1) / 80
    ) + 1 AS workshop_id,

    'PART-' || lpad(
        (
            ((source.part_id - 1) % 80) + 1
        )::text,
        4,
        '0'
    ) AS code,

    format(
        'Seed part %s for workshop %s',
        ((source.part_id - 1) % 80) + 1,
        ((source.part_id - 1) / 80) + 1
    ) AS description,

    (
        100 + (source.part_id % 400)
    )::integer AS physical_stock

FROM generate_series(
    1::bigint,
    4000::bigint
) AS source(part_id)

ORDER BY source.part_id;

-- ============================================================
-- Historical reservations
--
-- 80 reservations per workshop.
-- 4,000 reservations in total.
--
-- Status:
-- 2 = Confirmed
-- 3 = Cancelled
-- 4 = Expired
--
-- All expiration dates are in the past.
-- ============================================================

WITH seed_reservations AS (
    SELECT
        source.reservation_id,

        (
            (source.reservation_id - 1) / 80
        ) + 1 AS workshop_id,

        transaction_timestamp()
            - INTERVAL '90 days'
            + (
                ((source.reservation_id - 1) % 60)
                * INTERVAL '1 day'
            ) AS created_at

    FROM generate_series(
        1::bigint,
        4000::bigint
    ) AS source(reservation_id)
)
INSERT INTO public.reservations (
    id,
    workshop_id,
    service_order_id,
    user_id,
    status,
    created_at,
    expires_at
)
SELECT
    seed.reservation_id,

    seed.workshop_id,

    'SEED-OS-' || seed.reservation_id::text,

    1000 + seed.workshop_id AS user_id,

    (
        2 + ((seed.reservation_id - 1) % 3)
    )::integer AS status,

    seed.created_at,

    seed.created_at + INTERVAL '30 minutes'

FROM seed_reservations seed

ORDER BY seed.reservation_id;

-- ============================================================
-- Historical reservation items
--
-- 10 distinct parts per reservation.
-- Every part belongs to the reservation's workshop.
-- 40,000 items in total.
-- ============================================================

INSERT INTO public.reservation_items (
    reservation_id,
    workshop_id,
    part_id,
    quantity
)
SELECT
    reservation.id,

    reservation.workshop_id,

    (
        (reservation.workshop_id - 1) * 80
    )
    + (
        (
            ((reservation.id - 1) % 80)
            + item_offset.value
        ) % 80
    )
    + 1 AS part_id,

    (
        1 + (item_offset.value % 3)
    )::integer AS quantity

FROM public.reservations reservation

CROSS JOIN generate_series(
    0,
    9
) AS item_offset(value)

ORDER BY
    reservation.id,
    item_offset.value;

-- ============================================================
-- Synchronize sequences after inserting explicit IDs.
-- ============================================================

SELECT setval(
    pg_get_serial_sequence(
        'public.parts',
        'id'
    )::regclass,
    (
        SELECT MAX(id)
        FROM public.parts
    ),
    true
);

SELECT setval(
    pg_get_serial_sequence(
        'public.reservations',
        'id'
    )::regclass,
    (
        SELECT MAX(id)
        FROM public.reservations
    ),
    true
);

COMMIT;

-- ============================================================
-- Update query planner statistics.
-- ============================================================

ANALYZE public.parts;

ANALYZE public.reservations;

ANALYZE public.reservation_items;

-- ============================================================
-- Load summary.
-- ============================================================

SELECT
    (
        SELECT COUNT(DISTINCT workshop_id)
        FROM public.parts
    ) AS workshop_count,

    (
        SELECT COUNT(*)
        FROM public.parts
    ) AS part_count,

    (
        SELECT COUNT(*)
        FROM public.reservations
    ) AS reservation_count,

    (
        SELECT COUNT(*)
        FROM public.reservation_items
    ) AS reservation_item_count;

