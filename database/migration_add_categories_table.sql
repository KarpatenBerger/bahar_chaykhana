-- migration_add_categories_table.sql
-- Выполнить один раз в Query Tool на базе bahar_chaickana.
-- Добавляет отдельную таблицу категорий меню — до этого категории существовали
-- только как текстовые значения в dishes.category и были захардкожены кнопками
-- в menu.html/menu.js. Теперь админ сможет добавлять/удалять категории через
-- панель, а меню на сайте будет подтягивать их динамически через /api/categories.

CREATE TABLE categories (
    id SERIAL PRIMARY KEY,
    slug VARCHAR(50) NOT NULL UNIQUE,   -- техническое имя, как в dishes.category (soups, drinks и т.д.)
    label VARCHAR(100) NOT NULL,        -- то, что видит гость на сайте ("Супы", "Напитки")
    sort_order INT NOT NULL DEFAULT 0   -- порядок кнопок-фильтров на menu.html
);

-- Переносим 8 категорий, которые уже реально используются в dishes.category
-- (см. seed_dishes.sql), в том порядке, в котором они сейчас идут на сайте.
INSERT INTO categories (slug, label, sort_order) VALUES
('soups', 'Супы', 1),
('starters', 'Закуски', 2),
('shashlik', 'Шашлык', 3),
('hot', 'Горячие блюда', 4),
('salads', 'Салаты', 5),
('pastries', 'Выпечка', 6),
('drinks', 'Напитки', 7),
('desserts', 'Десерты', 8);

-- Привязываем dishes.category к этому справочнику ПО ЗНАЧЕНИЮ (slug), а не по id —
-- так не нужно менять тип и данные в самой dishes. ON DELETE RESTRICT означает:
-- пока в категории есть хотя бы одно блюдо, удалить её нельзя — Postgres сам
-- откажет с ошибкой, а API вернёт админу понятное сообщение
-- (см. AdminMenuEndpoints.cs, обработка Npgsql-исключения 23503).
ALTER TABLE dishes ADD CONSTRAINT dishes_category_fkey
    FOREIGN KEY (category) REFERENCES categories(slug) ON DELETE RESTRICT;