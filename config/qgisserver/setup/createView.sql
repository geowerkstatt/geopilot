-- public.diff_sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche source

CREATE OR REPLACE VIEW public.diff_sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche
AS WITH exact_matches AS (
         SELECT c.t_id AS cid,
            n.t_id AS nid,
            c.t_ili_tid AS c_t_ili_tid,
            n.t_ili_tid AS n_t_ili_tid,
            c.rechtsstatus_txt AS c_rechtsstatus_txt,
            n.rechtsstatus_txt AS n_rechtsstatus_txt,
            c.geometrie AS c_geometrie,
            n.geometrie AS n_geometrie
           FROM current.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche c
             JOIN next.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche n ON c.geometrie = n.geometrie
          WHERE NOT (c.t_ili_tid::text IS DISTINCT FROM n.t_ili_tid::text OR c.rechtsstatus_txt::text IS DISTINCT FROM n.rechtsstatus_txt::text)
        ), equal_geometry_matches AS (
         SELECT c.t_id AS cid,
            n.t_id AS nid,
            c.t_ili_tid AS c_t_ili_tid,
            n.t_ili_tid AS n_t_ili_tid,
            c.rechtsstatus_txt AS c_rechtsstatus_txt,
            n.rechtsstatus_txt AS n_rechtsstatus_txt,
            c.geometrie AS c_geometrie,
            n.geometrie AS n_geometrie
           FROM current.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche c
             JOIN next.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche n ON st_equals(c.geometrie, n.geometrie)
          WHERE NOT ((c.t_id IN ( SELECT exact_matches.cid
                   FROM exact_matches)) OR (n.t_id IN ( SELECT exact_matches.nid
                   FROM exact_matches)))
        ), ranked_matches AS (
         SELECT DISTINCT ON (c.geometrie) c.t_id AS cid,
            n.t_id AS nid,
            c.t_ili_tid AS c_t_ili_tid,
            n.t_ili_tid AS n_t_ili_tid,
            c.rechtsstatus_txt AS c_rechtsstatus_txt,
            n.rechtsstatus_txt AS n_rechtsstatus_txt,
            c.geometrie AS c_geometrie,
            n.geometrie AS n_geometrie,
            st_frechetdistance(c.geometrie, n.geometrie) AS distance
           FROM current.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche c
             JOIN next.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche n ON st_overlaps(c.geometrie, n.geometrie)
          WHERE NOT ((c.t_id IN ( SELECT exact_matches.cid
                   FROM exact_matches)) OR (c.t_id IN ( SELECT equal_geometry_matches.cid
                   FROM equal_geometry_matches)))
          ORDER BY c.geometrie, (st_frechetdistance(c.geometrie, n.geometrie))
        ), matched_c_t_id AS (
         SELECT exact_matches.cid
           FROM exact_matches
        UNION
         SELECT equal_geometry_matches.cid
           FROM equal_geometry_matches
        UNION
         SELECT ranked_matches.cid
           FROM ranked_matches
        ), matched_n_t_id AS (
         SELECT exact_matches.nid
           FROM exact_matches
        UNION
         SELECT equal_geometry_matches.nid
           FROM equal_geometry_matches
        UNION
         SELECT ranked_matches.nid
           FROM ranked_matches
        )
 SELECT md5(coalesce(cid::text, '') || coalesce(nid::text, ''))::uuid AS id, -- Add hash as ID
    'unchanged'::text AS operation,
    exact_matches.c_t_ili_tid,
    exact_matches.n_t_ili_tid,
    exact_matches.c_rechtsstatus_txt,
    exact_matches.n_rechtsstatus_txt,
    exact_matches.c_geometrie,
    exact_matches.n_geometrie
   FROM exact_matches
UNION
 SELECT md5(coalesce(cid::text, '') || coalesce(nid::text, ''))::uuid AS id, -- Add hash as ID
    'unchanged (matched by geometry)'::text AS operation,
    equal_geometry_matches.c_t_ili_tid,
    equal_geometry_matches.n_t_ili_tid,
    equal_geometry_matches.c_rechtsstatus_txt,
    equal_geometry_matches.n_rechtsstatus_txt,
    equal_geometry_matches.c_geometrie,
    equal_geometry_matches.n_geometrie
   FROM equal_geometry_matches
  WHERE equal_geometry_matches.c_rechtsstatus_txt::text = equal_geometry_matches.n_rechtsstatus_txt::text
UNION
 SELECT md5(coalesce(cid::text, '') || coalesce(nid::text, ''))::uuid AS id, -- Add hash as ID
    'changed (equal geometry)'::text AS operation,
    equal_geometry_matches.c_t_ili_tid,
    equal_geometry_matches.n_t_ili_tid,
    equal_geometry_matches.c_rechtsstatus_txt,
    equal_geometry_matches.n_rechtsstatus_txt,
    equal_geometry_matches.c_geometrie,
    equal_geometry_matches.n_geometrie
   FROM equal_geometry_matches
  WHERE equal_geometry_matches.c_rechtsstatus_txt::text <> equal_geometry_matches.n_rechtsstatus_txt::text
UNION
 SELECT md5(coalesce(cid::text, '') || coalesce(nid::text, ''))::uuid AS id, -- Add hash as ID
    'changed (close geometry)'::text AS operation,
    ranked_matches.c_t_ili_tid,
    ranked_matches.n_t_ili_tid,
    ranked_matches.c_rechtsstatus_txt,
    ranked_matches.n_rechtsstatus_txt,
    ranked_matches.c_geometrie,
    ranked_matches.n_geometrie
   FROM ranked_matches
UNION
 SELECT md5(c.t_id::text || 'deleted')::uuid AS id, -- Add hash for deleted entries
    'deleted (no close geometry)'::text AS operation,
    c.t_ili_tid AS c_t_ili_tid,
    NULL::character varying AS n_t_ili_tid,
    c.rechtsstatus_txt AS c_rechtsstatus_txt,
    NULL::character varying AS n_rechtsstatus_txt,
    c.geometrie AS c_geometrie,
    NULL::geometry AS n_geometrie
   FROM current.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche c
  WHERE NOT (c.t_id IN ( SELECT matched_c_t_id.cid
           FROM matched_c_t_id))
UNION
 SELECT md5(n.t_id::text || 'added')::uuid AS id, -- Add hash for added entries
    'added (no close geometry)'::text AS operation,
    NULL::character varying AS c_t_ili_tid,
    n.t_ili_tid AS n_t_ili_tid,
    NULL::character varying AS c_rechtsstatus_txt,
    n.rechtsstatus_txt AS n_rechtsstatus_txt,
    NULL::geometry AS c_geometrie,
    n.geometrie AS n_geometrie
   FROM next.sh_ntznng_v5_0geobasisdaten_grundnutzung_zonenflaeche n
  WHERE NOT (n.t_id IN ( SELECT matched_n_t_id.nid
           FROM matched_n_t_id));