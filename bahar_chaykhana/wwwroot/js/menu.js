// menu.js — страница меню: загрузка блюд с сервера, фильтр по категориям,
// группировка по разделам с заголовками, кнопка "Добавить в корзину".

document.addEventListener('DOMContentLoaded', async () => {
    const container = document.getElementById('dishes-container');
    if (!container) return; // не на странице меню

    // Порядок и подписи категорий — тот же, что и на кнопках-фильтрах
    const CATEGORY_LABELS = {
        soups: 'Супы',
        shashlik: 'Шашлык',
        hot: 'Горячие блюда',
        salads: 'Салаты',
        bread: 'Хлеб',
        drinks: 'Напитки',
        desserts: 'Десерты',
        banquet: 'Банкеты'
    };
    const CATEGORY_ORDER = Object.keys(CATEGORY_LABELS);

    let dishes = [];

    try {
        dishes = await api.get('/menu');
    } catch (err) {
        container.innerHTML = `<p class="error-message">Не удалось загрузить меню: ${err.message}</p>`;
        return;
    }

    function dishCardHtml(dish) {
        return `
            <div class="dish-card" data-category="${dish.category}">
                <div class="dish-image">
                    ${dish.imageUrl ? `<img src="${dish.imageUrl}" alt="${dish.name}">` : ''}
                </div>
                <h4>${dish.name}</h4>
                <p class="dish-weight">${dish.category === 'banquet' ? 'за персону' : dish.weightG + ' г'}</p>
                <div class="dish-footer">
                    <span class="price">${dish.price} ₽</span>
                    <button class="btn btn-small add-to-cart-btn" data-id="${dish.id}">В корзину</button>
                </div>
            </div>
        `;
    }

    function renderDishes(list, groupByCategory) {
        if (list.length === 0) {
            container.innerHTML = '<p class="empty-message">В этой категории пока нет блюд.</p>';
            return;
        }

        if (!groupByCategory) {
            // Выбрана конкретная категория — просто плитка без заголовков
            container.innerHTML = `<div class="dishes-grid">${list.map(dishCardHtml).join('')}</div>`;
            return;
        }

        // "Все блюда" — группируем по категориям в заданном порядке, с заголовком раздела
        let html = '';
        for (const category of CATEGORY_ORDER) {
            const items = list.filter((d) => d.category === category);
            if (items.length === 0) continue; // раздел пуст — пропускаем, не показываем пустой заголовок

            html += `
                <section class="menu-category-section">
                    <h3 class="menu-category-title">${CATEGORY_LABELS[category] || category}</h3>
                    <div class="dishes-grid">${items.map(dishCardHtml).join('')}</div>
                </section>
            `;
        }
        container.innerHTML = html;
    }

    renderDishes(dishes, true);

    // Фильтр по категориям
    const categoryButtons = document.querySelectorAll('.category-btn');
    categoryButtons.forEach((btn) => {
        btn.addEventListener('click', () => {
            categoryButtons.forEach((b) => b.classList.remove('active'));
            btn.classList.add('active');

            const category = btn.dataset.category;
            if (category === 'all') {
                renderDishes(dishes, true);
            } else {
                renderDishes(dishes.filter((d) => d.category === category), false);
            }
        });
    });

    // Добавление в корзину (делегирование на контейнер, т.к. разметка перерисовывается целиком)
    container.addEventListener('click', (e) => {
        if (!e.target.classList.contains('add-to-cart-btn')) return;

        const id = e.target.dataset.id;
        const dish = dishes.find((d) => String(d.id) === id);
        if (!dish) return;

        addToCart({ id: dish.id, name: dish.name, price: dish.price }); // функция из cart.js

        const originalText = e.target.textContent;
        e.target.textContent = 'Добавлено ✓';
        e.target.disabled = true;
        setTimeout(() => {
            e.target.textContent = originalText;
            e.target.disabled = false;
        }, 900);
    });
});