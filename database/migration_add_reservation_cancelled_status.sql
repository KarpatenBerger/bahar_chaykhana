-- migration_add_reservation_cancelled_status.sql
-- Выполнить один раз в Query Tool на базе bahar_chaickana.
-- Добавляет статус "отменено" в разрешённый список для reservations.status —
-- он нужен для отличения самоотмены брони гостем по ссылке из письма
-- (см. CancelEndpoints.cs) от отклонения администратором ("отклонено").

ALTER TABLE reservations DROP CONSTRAINT reservations_status_check;

ALTER TABLE reservations ADD CONSTRAINT reservations_status_check
    CHECK (status IN ('ожидает', 'подтверждено', 'отклонено', 'отменено'));

-- Если первая строка выдаст ошибку "constraint does not exist" — это значит,
-- что PostgreSQL назвал ограничение иначе. В таком случае сначала выполните:
--   SELECT conname FROM pg_constraint WHERE conrelid = 'reservations'::regclass;
-- и подставьте реальное имя в DROP CONSTRAINT вместо reservations_status_check.