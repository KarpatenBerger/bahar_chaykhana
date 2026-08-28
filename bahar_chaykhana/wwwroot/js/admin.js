// admin.js — панель сотрудника: вход, список заказов и броней, смена статусов.

const ADMIN_SESSION_KEY = 'bahar_admin_session';

document.addEventListener('DOMContentLoaded', () => {
    setupAdminLogin();
    setupAdminLogout();
    guardAdminPage();
    setupTabs();
    loadOrdersIfPresent();
    loadReservationsIfPresent();
    setupStatusFilters();
});

// ---------- Вход ----------

function setupAdminLogin() {
    const form = document.getElementById('admin-login-form');
    if (!form) return;

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const errorBox = document.getElementById('admin-error');
        errorBox.style.display = 'none';

        const payload = {
            login: document.getElementById('login').value,
            password: document.getElementById('password').value
        };

        try {
            const employee = await api.post('/admin/login', payload);
            localStorage.setItem(ADMIN_SESSION_KEY, JSON.stringify({ name: employee.name }));
            window.location.href = 'dashboard.html';
        } catch (err) {
            errorBox.textContent = err.message || 'Неверный логин или пароль';
            errorBox.style.display = 'block';
        }
    });
}

// Не пускаем на страницы админки без входа (кроме самой страницы входа)
function guardAdminPage() {
    const isLoginPage = !!document.getElementById('admin-login-form');
    if (isLoginPage) return;

    const isAdminPage = document.body.classList.contains('admin-body');
    if (!isAdminPage) return;

    const session = localStorage.getItem(ADMIN_SESSION_KEY);
    if (!session) {
        window.location.href = 'login.html';
        return;
    }

    const nameEl = document.getElementById('employee-name');
    if (nameEl) {
        try {
            nameEl.textContent = JSON.parse(session).name || 'Сотрудник';
        } catch {
            nameEl.textContent = 'Сотрудник';
        }
    }
}

function setupAdminLogout() {
    const btn = document.getElementById('admin-logout-btn');
    if (!btn) return;

    btn.addEventListener('click', async () => {
        try {
            await api.post('/admin/logout', {});
        } catch {
            // сессия на сервере могла уже истечь — это не мешает выйти локально
        }
        localStorage.removeItem(ADMIN_SESSION_KEY);
        window.location.href = 'login.html';
    });
}

// ---------- Вкладки на dashboard.html ----------

function setupTabs() {
    const tabButtons = document.querySelectorAll('.tab-btn');
    if (tabButtons.length === 0) return;

    tabButtons.forEach((btn) => {
        btn.addEventListener('click', () => {
            tabButtons.forEach((b) => b.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach((c) => c.classList.remove('active'));

            btn.classList.add('active');
            document.getElementById('tab-' + btn.dataset.tab).classList.add('active');
        });
    });
}

// ---------- Заказы ----------

const ORDER_STATUSES = ['новый', 'принят', 'готовится', 'готов', 'выдан'];

async function loadOrdersIfPresent() {
    const tbody = document.getElementById('orders-table-body');
    if (!tbody) return;

    let orders;
    try {
        orders = await api.get('/admin/orders');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="7" class="empty-state">Не удалось загрузить заказы: ${err.message}</td></tr>`;
        return;
    }

    renderOrdersTable(orders);
}

function renderOrdersTable(orders) {
    const tbody = document.getElementById('orders-table-body');
    const filter = document.getElementById('order-status-filter');
    const activeStatus = filter ? filter.value : 'all';

    const filtered = activeStatus === 'all'
        ? orders
        : orders.filter((o) => o.status === activeStatus);

    if (filtered.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="empty-state">Заказов нет</td></tr>';
        return;
    }

    tbody.innerHTML = filtered.map((o) => `
        <tr data-id="${o.id}">
            <td>${o.id}</td>
            <td>${formatDateTime(o.createdAt)}</td>
            <td>${o.customerName}</td>
            <td>${o.deliveryType === 'delivery' ? 'Доставка' : 'Самовывоз'}</td>
            <td>${o.total} ₽</td>
            <td>${renderStatusSelect(o.id, o.status, ORDER_STATUSES, 'order')}</td>
            <td><button class="btn btn-small next-status-btn" data-id="${o.id}" data-type="order">Далее →</button></td>
        </tr>
    `).join('');

    attachOrderRowHandlers();
}

function attachOrderRowHandlers() {
    document.querySelectorAll('.next-status-btn[data-type="order"]').forEach((btn) => {
        btn.addEventListener('click', async () => {
            const id = btn.dataset.id;
            const row = btn.closest('tr');
            const currentStatus = row.querySelector('select').value;
            const currentIndex = ORDER_STATUSES.indexOf(currentStatus);

            if (currentIndex === -1 || currentIndex === ORDER_STATUSES.length - 1) return;
            const nextStatus = ORDER_STATUSES[currentIndex + 1];

            await changeOrderStatus(id, nextStatus);
        });
    });

    document.querySelectorAll('select[data-type="order"]').forEach((select) => {
        select.addEventListener('change', () => changeOrderStatus(select.dataset.id, select.value));
    });
}

async function changeOrderStatus(orderId, newStatus) {
    try {
        await api.patch(`/admin/orders/${orderId}`, { status: newStatus });
        loadOrdersIfPresent(); // перезагружаем список, чтобы увидеть актуальные данные
    } catch (err) {
        alert('Не удалось изменить статус заказа: ' + err.message);
    }
}

// ---------- Бронирования ----------

async function loadReservationsIfPresent() {
    const tbody = document.getElementById('reservations-table-body');
    if (!tbody) return;

    let reservations;
    try {
        reservations = await api.get('/admin/reservations');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="8" class="empty-state">Не удалось загрузить брони: ${err.message}</td></tr>`;
        return;
    }

    renderReservationsTable(reservations);
}

function renderReservationsTable(reservations) {
    const tbody = document.getElementById('reservations-table-body');
    const filter = document.getElementById('reservation-status-filter');
    const activeStatus = filter ? filter.value : 'all';

    const filtered = activeStatus === 'all'
        ? reservations
        : reservations.filter((r) => r.status === activeStatus);

    if (filtered.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" class="empty-state">Бронирований нет</td></tr>';
        return;
    }

    tbody.innerHTML = filtered.map((r) => `
        <tr data-id="${r.id}">
            <td>${r.id}</td>
            <td>${formatDate(r.date)}</td>
            <td>${r.time}</td>
            <td>${r.customerName}</td>
            <td>${r.phone}</td>
            <td>${r.guests}</td>
            <td>${r.status}</td>
            <td>
                ${r.status === 'ожидает' ? `
                    <button class="btn btn-small confirm-btn" data-id="${r.id}">Подтвердить</button>
                    <button class="btn btn-small reject-btn" data-id="${r.id}">Отклонить</button>
                ` : `<button class="btn btn-small cancel-btn" data-id="${r.id}">Отменить</button>`}
            </td>
        </tr>
    `).join('');

    attachReservationRowHandlers();
}

function attachReservationRowHandlers() {
    document.querySelectorAll('.confirm-btn').forEach((btn) => {
        btn.addEventListener('click', () => changeReservationStatus(btn.dataset.id, 'подтверждено'));
    });
    document.querySelectorAll('.reject-btn').forEach((btn) => {
        btn.addEventListener('click', () => changeReservationStatus(btn.dataset.id, 'отклонено'));
    });
    document.querySelectorAll('.cancel-btn').forEach((btn) => {
        btn.addEventListener('click', () => changeReservationStatus(btn.dataset.id, 'отклонено'));
    });
}

async function changeReservationStatus(reservationId, newStatus) {
    try {
        await api.patch(`/admin/reservations/${reservationId}`, { status: newStatus });
        loadReservationsIfPresent();
    } catch (err) {
        alert('Не удалось изменить статус брони: ' + err.message);
    }
}

// ---------- Общие мелочи ----------

function renderStatusSelect(id, currentStatus, statuses, type) {
    const options = statuses.map((s) =>
        `<option value="${s}" ${s === currentStatus ? 'selected' : ''}>${s}</option>`
    ).join('');
    return `<select data-id="${id}" data-type="${type}">${options}</select>`;
}

function setupStatusFilters() {
    const orderFilter = document.getElementById('order-status-filter');
    if (orderFilter) orderFilter.addEventListener('change', loadOrdersIfPresent);

    const reservationFilter = document.getElementById('reservation-status-filter');
    if (reservationFilter) reservationFilter.addEventListener('change', loadReservationsIfPresent);
}

function formatDate(isoString) {
    if (!isoString) return '—';
    return new Date(isoString).toLocaleDateString('ru-RU');
}

function formatDateTime(isoString) {
    if (!isoString) return '—';
    return new Date(isoString).toLocaleString('ru-RU');
}
