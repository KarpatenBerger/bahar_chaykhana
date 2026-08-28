// menu.js — страница меню: загрузка блюд с сервера, фильтр по категориям,
// кнопка "Добавить в корзину" на каждой карточке.

document.addEventListener('DOMContentLoaded', async () => {
    const container = document.getElementById('dishes-container');
    if (!container) return; // не на странице меню

    let dishes = [];

    try {
        dishes = await api.get('/menu');
    } catch (err) {
        container.innerHTML = `<p class="error-message">Не удалось загрузить меню: ${err.message}</p>`;
        return;
    }

    function renderDishes(list) {
        if (list.length === 0) {
            container.innerHTML = '<p class="empty-message">В этой категории пока нет блюд.</p>';
            return;
        }

        container.innerHTML = list.map((dish) => `
            <div class="dish-card" data-category="${dish.category}">
                <div class="dish-image">
                    ${dish.imageUrl ? `<img src="${dish.imageUrl}" alt="${dish.name}">` : ''}
                </div>
                <h4>${dish.name}</h4>
                <p class="dish-weight">${dish.category === 'banquet' ? 'за персону' : dish.weight + ' г'}</p>
                <div class="dish-footer">
                    <span class="price">${dish.price} ₽</span>
                    <button class="btn btn-small add-to-cart-btn" data-id="${dish.id}">В корзину</button>
                </div>
            </div>
        `).join('');
    }

    renderDishes(dishes);

    // Фильтр по категориям
    const categoryButtons = document.querySelectorAll('.category-btn');
    categoryButtons.forEach((btn) => {
        btn.addEventListener('click', () => {
            categoryButtons.forEach((b) => b.classList.remove('active'));
            btn.classList.add('active');

            const category = btn.dataset.category;
            const filtered = category === 'all'
                ? dishes
                : dishes.filter((d) => d.category === category);

            renderDishes(filtered);
        });
    });

    // Добавление в корзину (делегирование, т.к. карточки перерисовываются)
    container.addEventListener('click', (e) => {
        if (!e.target.classList.contains('add-to-cart-btn')) return;

        const id = e.target.dataset.id;
        const dish = dishes.find((d) => d.id === id);
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
