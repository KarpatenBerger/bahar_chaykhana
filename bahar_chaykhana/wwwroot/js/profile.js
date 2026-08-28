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

function renderOrdersHistory(orders) {
    const container = document.getElementById('orders-history');

    if (orders.length === 0) {
        container.innerHTML = '<p class="empty-message">Вы ещё не делали заказов. <a href="menu.html">Перейти к меню</a></p>';
        return;
    }

    container.innerHTML = orders.map((o) => `
        <div class="order-history-item">
            <div>
                <span class="order-date">${formatDate(o.createdAt)}</span>
                <span class="order-id">Заказ №${o.id}</span>
            </div>
            <span class="order-total">${o.total} ₽</span>
            <span class="status-badge status-${o.status}">${o.status}</span>
        </div>
    `).join('');
}
