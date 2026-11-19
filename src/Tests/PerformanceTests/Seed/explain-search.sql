\set ON_ERROR_STOP on
\if :{?workshop_id}
\else
    \set workshop_id 1
\endif
-- Keep the SQL synchronized with PartRepository.SearchAsync for each measured version.
PREPARE performance_search(bigint, bigint, text, integer) AS
WITH reference_time AS MATERIALIZED (
    SELECT clock_timestamp() AS now
),
page AS MATERIALIZED (
    SELECT id, workshop_id, code, description, physical_stock
    FROM parts
    WHERE workshop_id = $1
      AND id > COALESCE($2::bigint, 0)
      AND (
          $3::text IS NULL
          OR code ILIKE $3 ESCAPE '\'
          OR description ILIKE $3 ESCAPE '\'
      )
    ORDER BY id
    LIMIT $4
)
SELECT p.id AS "Id", p.code AS "Code", p.description AS "Description",
       p.physical_stock AS "PhysicalStock",
       s.reserved_stock AS "ReservedStock",
       p.physical_stock - s.reserved_stock AS "AvailableStock"
FROM page p
CROSS JOIN reference_time t
CROSS JOIN LATERAL (
    SELECT COALESCE(SUM(ri.quantity::bigint), 0)::bigint AS reserved_stock
    FROM reservation_items ri
    JOIN reservations r
      ON r.id = ri.reservation_id AND r.workshop_id = ri.workshop_id
    WHERE ri.workshop_id = p.workshop_id
      AND ri.part_id = p.id
      AND r.status = 1
      AND r.expires_at > t.now
) s
ORDER BY p.id;

\echo 'Busca vazia / primeira pagina / 21 registros para decidir nextCursor'
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
EXECUTE performance_search(:workshop_id, NULL, NULL, 21);

\echo 'Busca textual complementar / filtro'
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
EXECUTE performance_search(:workshop_id, NULL, '%filtro%', 21);
DEALLOCATE performance_search;
