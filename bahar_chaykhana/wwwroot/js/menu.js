// menu.js — страница меню: загрузка блюд с сервера, фильтр по категориям,
// группировка по разделам с заголовками, кнопка "Добавить в корзину",
// модальное окно с полной карточкой блюда по клику.

document.addEventListener('DOMContentLoaded', async () => {
    const container = document.getElementById('dishes-container');
    if (!container) return; // не на странице меню

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
        // ИЗМЕНЕНО: добавлен data-id на саму карточку — нужен, чтобы по клику
        // на карточку (не на кнопку) можно было найти блюдо и открыть модалку.
        return `
            <div class="dish-card" data-category="${dish.category}" data-id="${dish.id}">
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
            container.innerHTML = `<div class="dishes-grid">${list.map(dishCardHtml).join('')}</div>`;
            return;
        }

        let html = '';
        for (const category of CATEGORY_ORDER) {
            const items = list.filter((d) => d.category === category);
            if (items.length === 0) continue;

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

    // ИЗМЕНЕНО: делегирование клика теперь обрабатывает два случая —
    // клик по кнопке "В корзину" (как раньше) и клик по остальной части
    // карточки (открывает модалку с полной информацией о блюде).
    container.addEventListener('click', (e) => {
        const addBtn = e.target.closest('.add-to-cart-btn');
        if (addBtn) {
            const id = addBtn.dataset.id;
            const dish = dishes.find((d) => String(d.id) === id);
            if (!dish) return;

            addToCart({ id: dish.id, name: dish.name, price: dish.price });

            const originalText = addBtn.textContent;
            addBtn.textContent = 'Добавлено ✓';
            addBtn.disabled = true;
            setTimeout(() => {
                addBtn.textContent = originalText;
                addBtn.disabled = false;
            }, 900);
            return; // не открываем модалку при клике именно на кнопку
        }

        const card = e.target.closest('.dish-card');
        if (card) {
            const dish = dishes.find((d) => String(d.id) === card.dataset.id);
            if (dish) openDishModal(dish);
        }
    });

    setupDishModal();
});

// ---------- Модальное окно с полной карточкой блюда ----------

function openDishModal(dish) {
    const overlay = document.getElementById('dish-modal-overlay');
    if (!overlay) return;

    document.getElementById('dish-modal-name').textContent = dish.name;
    document.getElementById('dish-modal-weight').textContent =
        dish.category === 'banquet' ? 'за персону' : `${dish.weightG} г`;
    // Полное описание — то же поле dishes.description из базы, которое
    // раньше нигде не отображалось на странице меню.
    document.getElementById('dish-modal-description').textContent =
        dish.description || 'Подробное описание уточняется у администратора.';
    document.getElementById('dish-modal-price').textContent = `${dish.price} ₽`;

    const imageContainer = document.getElementById('dish-modal-image');
    imageContainer.innerHTML = dish.imageUrl
        ? `<img src="${dish.imageUrl}" alt="${dish.name}">`
        : '';

    const addBtn = document.getElementById('dish-modal-add-btn');
    addBtn.textContent = 'В корзину';
    addBtn.disabled = false;
    addBtn.onclick = () => {
        addToCart({ id: dish.id, name: dish.name, price: dish.price });
        addBtn.textContent = 'Добавлено ✓';
        addBtn.disabled = true;
        setTimeout(() => {
            addBtn.textContent = 'В корзину';
            addBtn.disabled = false;
        }, 900);
    };

    overlay.style.display = 'flex';
    document.body.style.overflow = 'hidden'; // блокируем прокрутку фона под модалкой
}

function closeDishModal() {
    const overlay = document.getElementById('dish-modal-overlay');
    if (!overlay) return;
    overlay.style.display = 'none';
    document.body.style.overflow = '';
}

function setupDishModal() {
    const overlay = document.getElementById('dish-modal-overlay');
    const closeBtn = document.getElementById('dish-modal-close');
    if (!overlay || !closeBtn) return;

    closeBtn.addEventListener('click', closeDishModal);

    // Закрытие по клику на затемнённый фон (но не на саму карточку внутри)
    overlay.addEventListener('click', (e) => {
        if (e.target === overlay) closeDishModal();
    });

    // Закрытие по Escape
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closeDishModal();
    });
}