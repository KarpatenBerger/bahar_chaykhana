// main.js — логика главной страницы (index.html).
// Важно: index.html подключает только этот файл, без api.js и cart.js,
// поэтому счётчик корзины в шапке обновляется здесь самостоятельно,
// по тому же ключу localStorage, что использует cart.js на остальных страницах.

document.addEventListener('DOMContentLoaded', () => {
    updateCartBadgeOnIndex();
});

function updateCartBadgeOnIndex() {
    const badge = document.getElementById('cart-count');
    if (!badge) return; // сейчас в шапке index.html ссылки на корзину нет

    const raw = localStorage.getItem('bahar_cart');
    if (!raw) {
        badge.textContent = '0';
        return;
    }

    try {
        const cart = JSON.parse(raw);
        const count = cart.reduce((sum, item) => sum + item.quantity, 0);
        badge.textContent = count;
    } catch {
        badge.textContent = '0';
    }
}
