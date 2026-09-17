-- seed_categories.sql
-- Наполнение таблицы categories для чистой установки (schema.sql уже создал
-- саму таблицу). Выполнять ДО seed_dishes.sql — dishes.category ссылается
-- на categories.slug внешним ключом.

INSERT INTO categories (slug, label, sort_order) VALUES
('soups', 'Супы', 1),
('starters', 'Закуски', 2),
('shashlik', 'Шашлык', 3),
('hot', 'Горячие блюда', 4),
('salads', 'Салаты', 5),
('pastries', 'Выпечка', 6),
('drinks', 'Напитки', 7),
('desserts', 'Десерты', 8);