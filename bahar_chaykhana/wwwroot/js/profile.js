// profile.js — личный кабинет: данные гостя, история заказов, активные брони.

document.addEventListener('DOMContentLoaded', async () => {
    const profileContent = document.getElementById('profile-content');
    if (!profileContent) return; // не на странице личного кабинета

    const notLoggedInBlock = document.getElementById('not-logged-in');
    const session = getGuestSession(); // функция из api.js

    // Локальная сессия — только для быстрого отображения интерфейса.
    // Настоящую проверку прав всё равно делает сервер по cookie.
    if (!session) {
        profileContent.style.display = 'none';
        notLoggedInBlock.style.display = 'block';
        return;
    }

    let profile;
    try {
        profile = await api.get('/account'); // сервер сверяет cookie-сессию
    } catch (err) {
        // сессия на сервере истекла — сбрасываем локальную и просим войти заново
        clearGuestSession();
        profileContent.style.display = 'none';
        notLoggedInBlock.style.display = 'block';
        return;
    }

    // Основные данные
    document.getElementById('user-name').textContent = profile.name;
    document.getElementById('user-email').textContent = profile.email;
    document.getElementById('user-email-info').textContent = profile.email;
    document.getElementById('user-phone').textContent = profile.phone || '—';
    document.getElementById('user-register-date').textContent = formatDate(profile.registeredAt);
    document.getElementById('bonus-balance').textContent = profile.bonusBalance;

    // Обновляем локальную сессию актуальным балансом бонусов
    setGuestSession({ name: profile.name, email: profile.email, bonusBalance: profile.bonusBalance });

    renderReservations(profile.activeReservations || []);
    renderOrdersHistory(profile.orders || []);
});

function formatDate(isoString) {
    if (!isoString) return '—';
    const d = new Date(isoString);
    return d.toLocaleDateString('ru-RU');
}

function renderReservations(reservations) {
    const container = document.getElementById('active-reservations');

    if (reservations.length === 0) {
        container.innerHTML = '<p class="empty-message">У вас пока нет активных бронирований.</p>';
        return;
    }

    container.innerHTML = reservations.map((r) => `
        <div class="reservation-item">
            <span>${formatDate(r.date)} в ${r.time}, ${r.guests} чел.</span>
            <span class="status-badge status-${r.status}">${r.status}</span>
        </div>
    `).join('');
}

// Текст, введённый гостем (адрес, комментарий), и названия блюд выводим
// через экранирование, чтобы символы < > & не ломали страницу.
function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, (c) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

function formatMoney(value) {
    return Number(value).toLocaleString('ru-RU') + ' ₽';
}

function formatOrderDateTime(isoString) {
    if (!isoString) return '—';
    return new Date(isoString).toLocaleString('ru-RU', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function renderOrderBody(o) {
    const isDelivery = o.deliveryType === 'delivery';

    const itemsRows = (o.items || []).map((i) => `
        <tr>
            <td>${escapeHtml(i.name)}</td>
            <td class="num">${i.quantity}</td>
            <td class="num">${formatMoney(i.price)}</td>
            <td class="num">${formatMoney(i.price * i.quantity)}</td>
        </tr>
    `).join('');

    const deliveryRow = isDelivery
        ? `<tr><td colspan="3">Доставка</td><td class="num">${formatMoney(o.deliveryCost)}</td></tr>` : '';
    const bonusRow = o.bonusUsed > 0
        ? `<tr><td colspan="3">Оплачено бонусами</td><td class="num">−${formatMoney(o.bonusUsed)}</td></tr>` : '';

    return `
        <div class="order-history-details">
            <p><strong>Дата и время:</strong> ${formatOrderDateTime(o.createdAt)}</p>
            <p><strong>Получение:</strong> ${isDelivery ? 'доставка' : 'самовывоз'}</p>
            ${isDelivery && o.address ? `<p><strong>Адрес:</strong> ${escapeHtml(o.address)}</p>` : ''}
            ${o.comment ? `<p><strong>Комментарий:</strong> ${escapeHtml(o.comment)}</p>` : ''}
            <table class="order-history-items">
                <thead>
                    <tr><th>Блюдо</th><th class="num">Кол-во</th><th class="num">Цена</th><th class="num">Сумма</th></tr>
                </thead>
                <tbody>${itemsRows}</tbody>
                <tfoot>
                    <tr><td colspan="3">Сумма блюд</td><td class="num">${formatMoney(o.subtotal)}</td></tr>
                    ${deliveryRow}
                    ${bonusRow}
                    <tr class="order-history-total"><td colspan="3">Итого</td><td class="num">${formatMoney(o.total)}</td></tr>
                </tfoot>
            </table>
        </div>
    `;
}

function renderOrdersHistory(orders) {
    const container = document.getElementById('orders-history');

    if (orders.length === 0) {
        container.innerHTML = '<p class="empty-message">Вы ещё не делали заказов. <a href="menu.html">Перейти к меню</a></p>';
        return;
    }

    // <details> раскрывается по клику сам, без JS
    container.innerHTML = orders.map((o) => `
        <details class="order-history-card">
            <summary class="order-history-item">
                <div>
                    <span class="order-date">${formatDate(o.createdAt)}</span>
                    <span class="order-id">Заказ №${o.id}</span>
                </div>
                <span class="order-total">${formatMoney(o.total)}</span>
                <span class="status-badge status-${o.status}">${o.status}</span>
            </summary>
            ${renderOrderBody(o)}
        </details>
    `).join('');
}