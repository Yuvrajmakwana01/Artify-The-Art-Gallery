BEGIN;

-- 1) Category (safe re-run)
INSERT INTO t_category (c_category_name, c_category_description, c_is_active)
SELECT 'Demo Category', 'Seed category for admin UI testing', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM t_category WHERE c_category_name = 'Demo Category'
);

-- 2) Buyer user (safe re-run)
INSERT INTO t_user (
    c_email, c_password_hash, c_full_name, c_username, c_gender, c_mobile, c_profile_image
)
SELECT
    'buyer.demo@artify.com',
    'demo_hash',
    'Buyer Demo',
    'buyer_demo',
    'Male',
    '9999999999',
    NULL
WHERE NOT EXISTS (
    SELECT 1 FROM t_user WHERE c_email = 'buyer.demo@artify.com'
);

-- 3) Artist base user (required by t_artist_profile FK)
INSERT INTO t_user (
    c_email, c_password_hash, c_full_name, c_username, c_gender, c_mobile, c_profile_image
)
SELECT
    'artist.demo@artify.com',
    'demo_hash',
    'Artist Demo',
    'artist_demo',
    'Female',
    '8888888888',
    NULL
WHERE NOT EXISTS (
    SELECT 1 FROM t_user WHERE c_email = 'artist.demo@artify.com'
);

-- 4) Artist profile (safe re-run)
INSERT INTO t_artist_profile (
    c_artist_id,
    c_artist_name,
    c_artist_email,
    c_password,
    c_biography,
    c_cover_image,
    c_rating_avg,
    c_is_verified,
    c_url,
    c_rejected_count
)
SELECT
    u.c_user_id,
    'Artist Demo',
    'artist.demo@artify.com',
    'demo_pass',
    'Seed artist for admin testing',
    NULL,
    4.50,
    TRUE,
    ARRAY['https://demo.artify.local']::TEXT[],
    0
FROM t_user u
WHERE u.c_email = 'artist.demo@artify.com'
  AND NOT EXISTS (
      SELECT 1 FROM t_artist_profile ap WHERE ap.c_artist_id = u.c_user_id
  );

-- 5) Artwork by demo artist under demo category
INSERT INTO t_artwork (
    c_artist_id,
    c_category_id,
    c_title,
    c_description,
    c_price,
    c_preview_path,
    c_original_path,
    c_approval_status,
    c_admin_note,
    c_likes_count,
    c_sell_count
)
SELECT
    ap.c_artist_id,
    c.c_category_id,
    'Demo Artwork',
    'Seed artwork for admin sales/orders test',
    1200.00,
    '/preview/demo-art.jpg',
    '/original/demo-art.jpg',
    'Approved',
    'Seed record',
    5,
    1
FROM t_artist_profile ap
CROSS JOIN t_category c
WHERE ap.c_artist_email = 'artist.demo@artify.com'
  AND c.c_category_name = 'Demo Category'
  AND NOT EXISTS (
      SELECT 1 FROM t_artwork aw WHERE aw.c_title = 'Demo Artwork'
  );

-- 6) One completed order by demo buyer
INSERT INTO t_order (c_buyer_id, c_total_amount, c_order_status)
SELECT
    u.c_user_id,
    1200.00,
    'Completed'
FROM t_user u
WHERE u.c_email = 'buyer.demo@artify.com'
  AND NOT EXISTS (
      SELECT 1
      FROM t_order o
      WHERE o.c_buyer_id = u.c_user_id
        AND o.c_total_amount = 1200.00
  );

-- 7) Order item linking demo order and demo artwork
INSERT INTO t_order_item (c_order_id, c_artwork_id, c_price_at_purchase)
SELECT
    o.c_order_id,
    aw.c_artwork_id,
    1200.00
FROM t_order o
JOIN t_user u ON u.c_user_id = o.c_buyer_id
JOIN t_artwork aw ON aw.c_title = 'Demo Artwork'
WHERE u.c_email = 'buyer.demo@artify.com'
  AND NOT EXISTS (
      SELECT 1 FROM t_order_item oi
      WHERE oi.c_order_id = o.c_order_id
        AND oi.c_artwork_id = aw.c_artwork_id
  )
LIMIT 1;

-- 8) Payment row for same order
INSERT INTO t_payment (
    c_order_id,
    c_transaction_id,
    c_method,
    c_amount_paid,
    c_commission_deducted,
    c_artist_payout_amount,
    c_payment_status,
    c_currency
)
SELECT
    o.c_order_id,
    'TXN-DEMO-0001',
    'Card',
    1200.00,
    120.00,
    1080.00,
    'Paid',
    'USD'
FROM t_order o
JOIN t_user u ON u.c_user_id = o.c_buyer_id
WHERE u.c_email = 'buyer.demo@artify.com'
  AND NOT EXISTS (
      SELECT 1 FROM t_payment p WHERE p.c_order_id = o.c_order_id
  )
LIMIT 1;

COMMIT;
