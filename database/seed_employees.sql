-- seed_employees.sql
-- Тестовый сотрудник для входа в административную панель.
-- Логин:  admin
-- Пароль: admin123  (хеш ниже сгенерирован через bcrypt заранее для теста;
--                     когда появится код регистрации сотрудников, реальные
--                     пароли всегда должны хешироваться самим приложением,
--                     а не вставляться напрямую в базу)

INSERT INTO employees (login, password_hash, name) VALUES
('admin', '$2b$12$BuGJ7hWK5DqbxRMbQeCEkOhYHEatavKkhNCyav/oW.m7.6tdXOA3K', 'Тестовый администратор');
