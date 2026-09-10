-- migration_add_employee_email.sql
-- Выполнить один раз в Query Tool на базе bahar_chaickana.
-- Добавляет email сотрудникам — нужен для входа в админ-панель через общую
-- форму авторизации (login.html) по корпоративному домену @bahar.ru.

ALTER TABLE employees ADD COLUMN email VARCHAR(150);

-- Backfill: подставьте реальный корпоративный email для уже существующего
-- тестового администратора (см. seed_employees.sql). Пример ниже — замените
-- на настоящий адрес перед использованием в продакшене.
UPDATE employees SET email = 'admin@bahar.ru' WHERE login = 'admin';

-- После бэкфилла делаем поле обязательным и уникальным
ALTER TABLE employees ALTER COLUMN email SET NOT NULL;
ALTER TABLE employees ADD CONSTRAINT employees_email_unique UNIQUE (email);