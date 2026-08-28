-- seed_customers.sql
-- Тестовый зарегистрированный гость — для проверки личного кабинета
-- и программы лояльности (уже с ненулевым балансом бонусов, чтобы сразу
-- было видно, как работает списание при оформлении заказа).
-- Email:  guest@example.com
-- Пароль: guest123

INSERT INTO customers (name, phone, email, password_hash, bonus_balance) VALUES
('Наталья Соколова', '+79001234567', 'guest@example.com', '$2b$12$z5SUkqjvHA98509VZkaTWOEGKhwVHKLdLOBJciUmot5C6KR9rZefy', 320);
