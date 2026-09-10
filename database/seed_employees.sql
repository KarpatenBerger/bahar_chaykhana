-- seed_employees.sql
-- Тестовый сотрудник для входа в административную панель.
-- Логин:  admin
-- Email:  admin@bahar.ru  (для входа через общую форму login.html)
-- Пароль: admin123

INSERT INTO employees (login, email, password_hash, name) VALUES
('admin', 'admin@bahar.ru', '$2b$12$BuGJ7hWK5DqbxRMbQeCEkOhYHEatavKkhNCyav/oW.m7.6tdXOA3K', 'Тестовый администратор');