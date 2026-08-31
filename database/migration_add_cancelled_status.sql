-- migration_add_cancelled_status.sql
-- Выполнить один раз в Query Tool на базе bahar_chaickana.
-- Добавляет статус "отменён" в разрешённый список для orders.status —
-- он нужен для отмены заказа гостем по ссылке из письма (CancelEndpoints.cs).

ALTER TABLE orders DROP CONSTRAINT orders_status_check;

ALTER TABLE orders ADD CONSTRAINT orders_status_check
    CHECK (status IN ('новый', 'принят', 'готовится', 'готов', 'выдан', 'отменён'));

-- Если первая строка выдаст ошибку "constraint does not exist" — это значит,
-- что PostgreSQL назвал ограничение иначе. В таком случае сначала выполните:
--   SELECT conname FROM pg_constraint WHERE conrelid = 'orders'::regclass;
-- и подставьте реальное имя в DROP CONSTRAINT вместо orders_status_check.
