-- schema.sql
-- Полная схема базы данных чайханы «Бахар».
-- Выполнять один раз на пустой базе (например, bahar_chaickana), от владельца bahar_admin.
-- Порядок таблиц важен из-за внешних ключей: сначала независимые таблицы,
-- затем те, что на них ссылаются.

-- Блюда меню
CREATE TABLE dishes (
    id SERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    category VARCHAR(50) NOT NULL, -- soups, shashlik, hot, salads, drinks, desserts
    weight_g INT NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    image_url VARCHAR(255),
    description TEXT,
    is_available BOOLEAN NOT NULL DEFAULT TRUE
);

-- Зарегистрированные гости (личный кабинет)
CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    email VARCHAR(150) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    bonus_balance INT NOT NULL DEFAULT 0,
    registered_at TIMESTAMP NOT NULL DEFAULT now()
);

-- Сотрудники (вход в админ-панель)
CREATE TABLE employees (
    id SERIAL PRIMARY KEY,
    login VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(150) NOT NULL UNIQUE, -- ДОБАВЛЕНО: для входа через общую форму по домену @bahar.ru
    password_hash VARCHAR(255) NOT NULL,
    name VARCHAR(150) NOT NULL
);

-- Заказы
CREATE TABLE orders (
    id SERIAL PRIMARY KEY,
    customer_id INT REFERENCES customers(id), -- NULL, если гость без регистрации
    customer_name VARCHAR(150) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    email VARCHAR(150),                        -- необязательное поле
    delivery_type VARCHAR(20) NOT NULL CHECK (delivery_type IN ('pickup', 'delivery')),
    address VARCHAR(255),                       -- только при delivery
    comment TEXT,
    -- ИЗМЕНЕНО: добавлен статус 'отменён' (см. migration_add_cancelled_status.sql)
    status VARCHAR(20) NOT NULL DEFAULT 'новый'
        CHECK (status IN ('новый', 'принят', 'готовится', 'готов', 'выдан', 'отменён')),
    subtotal DECIMAL(10,2) NOT NULL,
    delivery_cost DECIMAL(10,2) NOT NULL DEFAULT 0,
    bonus_used INT NOT NULL DEFAULT 0,
    total DECIMAL(10,2) NOT NULL,
    cancel_token VARCHAR(64) UNIQUE,             -- токен для отмены по ссылке из письма
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

-- Состав заказа (блюда внутри конкретного заказа)
CREATE TABLE order_items (
    id SERIAL PRIMARY KEY,
    order_id INT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    dish_id INT NOT NULL REFERENCES dishes(id),
    quantity INT NOT NULL CHECK (quantity > 0),
    price_at_order DECIMAL(10,2) NOT NULL -- цена на момент заказа
);

-- Бронирования столиков
CREATE TABLE reservations (
    id SERIAL PRIMARY KEY,
    customer_id INT REFERENCES customers(id),
    customer_name VARCHAR(150) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    email VARCHAR(150),
    reservation_date DATE NOT NULL,
    reservation_time TIME NOT NULL,
    guests INT NOT NULL CHECK (guests > 0),
    comment TEXT,
    -- ИЗМЕНЕНО: добавлен статус 'отменено' — самоотмена гостем по ссылке из письма,
    -- отдельно от 'отклонено' (решение администратора)
    -- (см. migration_add_reservation_cancelled_status.sql)
    status VARCHAR(20) NOT NULL DEFAULT 'ожидает'
        CHECK (status IN ('ожидает', 'подтверждено', 'отклонено', 'отменено')),
    cancel_token VARCHAR(64) UNIQUE,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

-- Индексы для частых запросов
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_reservations_status ON reservations(status);
CREATE INDEX idx_reservations_date ON reservations(reservation_date);