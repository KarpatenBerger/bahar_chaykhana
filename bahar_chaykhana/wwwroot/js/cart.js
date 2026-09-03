// cart.js — корзина: хранится в localStorage браузера до оформления заказа.
// Подключается на страницах, где можно добавлять товары или смотреть корзину:
// index (через api.js), menu, cart, about, checkout, contacts, login, register.

const CART_KEY = 'bahar_cart';

function getCart() {
    const raw = localStorage.getItem(CART_KEY);
    if (!raw) return [];
    try {
        return JSON.parse(raw);
    } catch {
        return [];
    }
}

function saveCart(cart) {
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
    updateCartBadge(); // функция определена в api.js
}

/**
 * Добавить блюдо в корзину. Если оно уже есть — увеличить количество.
 * dish: { id, name, price }
 */
function addToCart(dish) {
    const cart = getCart();
    // dish.id может быть числом (пришло с сервера) или строкой (из dataset) —
    // сравниваем как строки, чтобы не промахнуться из-за разных типов
    const existing = cart.find((item) => String(item.id) === String(dish.id));

    if (existing) {
        existing.quantity += 1;
    } else {
        cart.push({
            id: dish.id,
            name: dish.name,
            price: dish.price,
            quantity: 1
        });
    }

    saveCart(cart);
}

function removeFromCart(dishId) {
    const cart = getCart().filter((item) => String(item.id) !== String(dishId));
    saveCart(cart);
}

function changeQuantity(dishId, delta) {
    const cart = getCart();
    const item = cart.find((i) => String(i.id) === String(dishId));
    if (!item) return;

    item.quantity += delta;
    if (item.quantity <= 0) {
        removeFromCart(dishId);
        return;
    }
    saveCart(cart);
}

function getCartTotal(cart) {
    return cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
}

// ---------- Отрисовка страницы корзины (cart.html) ----------

function renderCartPage() {
    const container = document.getElementById('cart-items-container');
    if (!container) return; // мы не на странице корзины

    const cart = getCart();
    const checkoutBtn = document.getElementById('checkout-btn');

    if (cart.length === 0) {
        container.innerHTML = '<p class="empty-cart-message">Ваша корзина пуста. <a href="menu.html">Перейти в меню</a></p>';
        if (checkoutBtn) checkoutBtn.disabled = true;
        updateCartSummary(cart);
        return;
    }

    container.innerHTML = cart.map((item) => `
        <div class="cart-item" data-id="${item.id}">
            <div class="cart-item-info">
                <h4>${item.name}</h4>
                <span class="cart-item-price">${item.price} ₽ × ${item.quantity}</span>
            </div>
            <div class="cart-item-controls">
                <button class="qty-btn" data-action="decrease" data-id="${item.id}">−</button>
                <span class="qty-value">${item.quantity}</span>
                <button class="qty-btn" data-action="increase" data-id="${item.id}">+</button>
                <button class="remove-btn" data-id="${item.id}">Удалить</button>
            </div>
        </div>
    `).join('');

    if (checkoutBtn) checkoutBtn.disabled = false;
    updateCartSummary(cart);
}

// Обработчики кнопок +/-/удалить (делегирование на контейнер).
// Важно: вешаем ОДИН раз при загрузке страницы, а не внутри renderCartPage —
// иначе при каждой перерисовке добавлялся бы ещё один обработчик поверх
// старых, и один клик срабатывал бы сразу несколько раз (эффект накапливался
// бы экспоненциально: 1 обработчик → 2 → 4 → 8 → 16...).
function setupCartItemHandlers() {
    const container = document.getElementById('cart-items-container');
    if (!container) return;

    container.addEventListener('click', (e) => {
        const id = e.target.dataset.id;
        if (!id) return;

        if (e.target.dataset.action === 'increase') {
            changeQuantity(id, 1);
            renderCartPage();
        } else if (e.target.dataset.action === 'decrease') {
            changeQuantity(id, -1);
            renderCartPage();
        } else if (e.target.classList.contains('remove-btn')) {
            removeFromCart(id);
            renderCartPage();
        }
    });
}

function updateCartSummary(cart) {
    const subtotalEl = document.getElementById('cart-subtotal');
    const finalTotalEl = document.getElementById('cart-final-total');
    const bonusCheckbox = document.getElementById('use-bonus');
    const bonusBalanceEl = document.getElementById('user-bonus-balance');

    const subtotal = getCartTotal(cart);

    // Бонусы доступны только вошедшим в личный кабинет гостям
    const session = getGuestSession(); // функция из api.js
    let bonusBalance = 0;
    let finalTotal = subtotal;

    if (session && bonusCheckbox) {
        bonusBalance = session.bonusBalance || 0;
        if (bonusBalanceEl) bonusBalanceEl.textContent = bonusBalance;
        bonusCheckbox.disabled = subtotal === 0 || bonusBalance === 0;

        if (bonusCheckbox.checked) {
            const maxDiscount = Math.min(bonusBalance, Math.round(subtotal * 0.9));
            finalTotal = subtotal - maxDiscount;
        }
    }

    if (subtotalEl) subtotalEl.textContent = `${subtotal} ₽`;
    if (finalTotalEl) finalTotalEl.textContent = `${finalTotal} ₽`;
}

// Переход к оформлению заказа
document.addEventListener('DOMContentLoaded', () => {
    const bonusCheckbox = document.getElementById('use-bonus');
    if (bonusCheckbox) {
        bonusCheckbox.addEventListener('change', () => renderCartPage());
    }

    const checkoutBtn = document.getElementById('checkout-btn');
    if (checkoutBtn) {
        checkoutBtn.addEventListener('click', () => {
            if (checkoutBtn.disabled) return;
            window.location.href = 'checkout.html';
        });
    }

    renderCartPage();
    setupCartItemHandlers();
});