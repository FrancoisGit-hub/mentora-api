-- Lot 14.1 precheck (READ ONLY). Run on PROD and api-demo BEFORE deploying migration Lot14_1_UserEmailCi.
-- Query 1 must return ZERO rows: the unique index UX_USERS_EMAIL_CI on lower("USER_EMAIL") cannot be
-- created while two USERS rows differ only by case (the migration would fail and the API would not start).
-- Query 2 is informational: an email stored with a leading or trailing space would never match a login,
-- because every lookup now compares lower("USER_EMAIL") with the trimmed, lowercased value.

-- 1. Emails duplicated case-insensitively
SELECT lower("USER_EMAIL")      AS normalized_email,
       count(*)                 AS rows_count,
       array_agg("USER_ID")     AS user_ids,
       array_agg("USER_EMAIL")  AS stored_values
FROM "USERS"
GROUP BY lower("USER_EMAIL")
HAVING count(*) > 1;

-- 2. Emails with surrounding whitespace (expected: zero rows)
SELECT "USER_ID", "USER_EMAIL"
FROM "USERS"
WHERE "USER_EMAIL" <> btrim("USER_EMAIL");
