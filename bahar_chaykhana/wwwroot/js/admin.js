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
    setupMenuManagement();
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

// ИСПРАВЛЕНО: разделили "рабочие" статусы (для кнопки "Далее →") и
// полный список для <select> — иначе отменённый заказ не имел подходящей
// опции в выпадающем списке и next-button вёл себя непредсказуемо.
const ORDER_STATUSES = ['новый', 'принят', 'готовится', 'готов', 'выдан'];
const ORDER_STATUS_OPTIONS = [...ORDER_STATUSES, 'отменён'];

async function loadOrdersIfPresent() {
    const tbody = document.getElementById('orders-table-body');
    if (!tbody) return;

    let orders;
    try {
        orders = await api.get('/admin/orders');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="6" class="empty-state">Не удалось загрузить заказы: ${err.message}</td></tr>`;
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
        tbody.innerHTML = '<tr><td colspan="6" class="empty-state">Заказов нет</td></tr>';
        return;
    }

    tbody.innerHTML = filtered.map((o) => {
        return `
        <tr data-id="${o.id}">
            <td>${o.id}</td>
            <td>${formatDateTime(o.createdAt)}</td>
            <td>${o.customerName}</td>
            <td>${o.deliveryType === 'delivery' ? 'Доставка' : 'Самовывоз'}</td>
            <td>${o.total} ₽</td>
            <td>${renderStatusSelect(o.id, o.status, ORDER_STATUS_OPTIONS, 'order')}</td>
        </tr>
    `;
    }).join('');

    attachOrderRowHandlers();
}

function attachOrderRowHandlers() {
    // ИСПРАВЛЕНО: раньше в одной строке было сразу два способа сменить статус —
    // select (применялся мгновенно по onChange) и кнопка "Далее →" (продвигала
    // на шаг вперёд от ТЕКУЩЕГО значения select). Если сначала выбрать статус
    // в списке, а затем ещё нажать "Далее" — заказ реально продвигался ещё на
    // один шаг дальше задуманного (например, "готов" → "выдан"), хотя внешне
    // выглядело как одно действие. Кнопку убрали, select — единственный способ.
    document.querySelectorAll('select[data-type="order"]').forEach((select) => {
        select.addEventListener('change', () => changeOrderStatus(select.dataset.id, select.value));
    });
}

async function changeOrderStatus(orderId, newStatus) {
    try {
        await api.patch(`/admin/orders/${orderId}`, { status: newStatus });
        loadOrdersIfPresent();
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

    tbody.innerHTML = filtered.map((r) => {
        // ИСПРАВЛЕНО: раньше любой статус, кроме 'ожидает', показывал кнопку
        // "Отменить" — в т.ч. на уже отклонённых и самоотменённых (отменено) бронях,
        // хотя менять их статус админ-эндпоинт теперь запрещает.
        let actions = '';
        if (r.status === 'ожидает') {
            actions = `
                <button class="btn btn-small confirm-btn" data-id="${r.id}">Подтвердить</button>
                <button class="btn btn-small reject-btn" data-id="${r.id}">Отклонить</button>
            `;
        } else if (r.status === 'подтверждено') {
            actions = `<button class="btn btn-small cancel-btn" data-id="${r.id}">Отменить</button>`;
        }

        return `
        <tr data-id="${r.id}">
            <td>${r.id}</td>
            <td>${formatDate(r.date)}</td>
            <td>${r.time}</td>
            <td>${r.customerName}</td>
            <td>${r.phone}</td>
            <td>${r.guests}</td>
            <td>${r.status}</td>
            <td>${actions}</td>
        </tr>
    `;
    }).join('');

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
    // ИСПРАВЛЕНО: select для отменённого заказа отключаем — статус менять нельзя
    const disabled = currentStatus === 'отменён' ? 'disabled' : '';
    return `<select data-id="${id}" data-type="${type}" ${disabled}>${options}</select>`;
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

// ---------- Вкладка "Меню": категории и блюда ----------
// Присутствует только на dashboard.html — на остальных страницах контейнеров
// с этими id нет, все функции ниже просто тихо выходят (см. if (!el) return).

let cachedCategories = []; // используется и таблицей категорий, и формой блюда

function setupMenuManagement() {
    if (!document.getElementById('tab-menu')) return; // не на dashboard.html

    loadCategories();
    loadDishes();

    document.getElementById('add-category-btn').addEventListener('click', addCategory);
    document.getElementById('dish-category-filter').addEventListener('change', loadDishes);
    document.getElementById('add-dish-btn').addEventListener('click', () => openDishForm(null));
    document.getElementById('dish-form-cancel').addEventListener('click', closeDishForm);
    document.getElementById('dish-form-close').addEventListener('click', closeDishForm);
    document.getElementById('dish-form').addEventListener('submit', submitDishForm);
}

// ---------- Категории ----------

async function loadCategories() {
    const tbody = document.getElementById('categories-table-body');
    if (!tbody) return;

    try {
        cachedCategories = await api.get('/admin/categories');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="4" class="empty-state">Не удалось загрузить категории: ${err.message}</td></tr>`;
        return;
    }

    renderCategoriesTable();
    populateCategorySelects();
}

function renderCategoriesTable() {
    const tbody = document.getElementById('categories-table-body');
    if (cachedCategories.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" class="empty-state">Категорий нет</td></tr>';
        return;
    }

    tbody.innerHTML = cachedCategories.map((c) => `
        <tr data-slug="${c.slug}">
            <td><code>${c.slug}</code></td>
            <td>${c.label}</td>
            <td>${c.sortOrder}</td>
            <td>
                <button class="btn btn-small btn-outline delete-category-btn" data-slug="${c.slug}">Удалить</button>
            </td>
        </tr>
    `).join('');

    document.querySelectorAll('.delete-category-btn').forEach((btn) => {
        btn.addEventListener('click', () => deleteCategory(btn.dataset.slug));
    });
}

// Заполняет и фильтр над таблицей блюд, и select внутри модалки добавления/
// редактирования блюда — им обеим нужен один и тот же список категорий.
function populateCategorySelects() {
    const filter = document.getElementById('dish-category-filter');
    const formSelect = document.getElementById('dish-form-category');

    const optionsHtml = cachedCategories.map((c) => `<option value="${c.slug}">${c.label}</option>`).join('');

    if (filter) {
        const current = filter.value;
        filter.innerHTML = '<option value="all">Все категории</option>' + optionsHtml;
        filter.value = current || 'all';
    }
    if (formSelect) formSelect.innerHTML = optionsHtml;
}

async function addCategory() {
    const slugInput = document.getElementById('new-category-slug');
    const labelInput = document.getElementById('new-category-label');
    const orderInput = document.getElementById('new-category-order');

    const payload = {
        slug: slugInput.value.trim(),
        label: labelInput.value.trim(),
        sortOrder: Number(orderInput.value) || 0
    };

    if (!payload.slug || !payload.label) {
        alert('Заполните слаг и название категории');
        return;
    }

    try {
        await api.post('/admin/categories', payload);
        slugInput.value = '';
        labelInput.value = '';
        orderInput.value = '';
        await loadCategories();
    } catch (err) {
        alert(err.message || 'Не удалось добавить категорию');
    }
}

async function deleteCategory(slug) {
    if (!confirm(`Удалить категорию «${slug}»?`)) return;

    try {
        await api.delete(`/admin/categories/${slug}`);
        await loadCategories();
    } catch (err) {
        // Сервер сам объясняет, если в категории ещё остались блюда
        alert(err.message || 'Не удалось удалить категорию');
    }
}

// ---------- Блюда ----------

let cachedDishes = []; // нужен, чтобы найти блюдо по id при открытии формы редактирования

async function loadDishes() {
    const tbody = document.getElementById('dishes-table-body');
    if (!tbody) return;

    try {
        cachedDishes = await api.get('/admin/dishes');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="7" class="empty-state">Не удалось загрузить блюда: ${err.message}</td></tr>`;
        return;
    }

    renderDishesTable();
}

function renderDishesTable() {
    const tbody = document.getElementById('dishes-table-body');
    const filterEl = document.getElementById('dish-category-filter');
    const filter = filterEl ? filterEl.value : 'all';

    const list = filter === 'all' ? cachedDishes : cachedDishes.filter((d) => d.category === filter);

    if (list.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="empty-state">Блюд нет</td></tr>';
        return;
    }

    const labelBySlug = Object.fromEntries(cachedCategories.map((c) => [c.slug, c.label]));

    tbody.innerHTML = list.map((d) => `
        <tr data-id="${d.id}">
            <td>${d.imageUrl ? `<img class="dish-thumb" src="${d.imageUrl}" alt="${d.name}">` : '—'}</td>
            <td>${d.name}</td>
            <td>${labelBySlug[d.category] || d.category}</td>
            <td>${d.weightG}</td>
            <td>${d.price} ₽</td>
            <td>
                <span class="availability-badge ${d.isAvailable ? 'is-available' : 'is-hidden'}">
                    ${d.isAvailable ? 'Да' : 'Скрыто'}
                </span>
            </td>
            <td>
                <div class="row-actions">
                    <button class="btn btn-small edit-dish-btn" data-id="${d.id}">Изменить</button>
                    <button class="btn btn-small btn-outline delete-dish-btn" data-id="${d.id}">Удалить</button>
                </div>
            </td>
        </tr>
    `).join('');

    document.querySelectorAll('.edit-dish-btn').forEach((btn) => {
        btn.addEventListener('click', () => {
            const dish = cachedDishes.find((d) => String(d.id) === btn.dataset.id);
            if (dish) openDishForm(dish);
        });
    });

    document.querySelectorAll('.delete-dish-btn').forEach((btn) => {
        btn.addEventListener('click', () => deleteDish(btn.dataset.id));
    });
}

// dish === null → форма добавления нового блюда, иначе — редактирование существующего
function openDishForm(dish) {
    const overlay = document.getElementById('dish-form-overlay');
    const errorBox = document.getElementById('dish-form-error');
    errorBox.style.display = 'none';

    document.getElementById('dish-form-title').textContent = dish ? 'Редактировать блюдо' : 'Новое блюдо';
    document.getElementById('dish-form-id').value = dish ? dish.id : '';
    document.getElementById('dish-form-name').value = dish ? dish.name : '';
    document.getElementById('dish-form-category').value = dish ? dish.category : (cachedCategories[0]?.slug || '');
    document.getElementById('dish-form-weight').value = dish ? dish.weightG : '';
    document.getElementById('dish-form-price').value = dish ? dish.price : '';
    document.getElementById('dish-form-image').value = dish ? (dish.imageUrl || '') : '';
    document.getElementById('dish-form-description').value = dish ? (dish.description || '') : '';
    document.getElementById('dish-form-available').checked = dish ? dish.isAvailable : true;

    overlay.style.display = 'flex';
}

function closeDishForm() {
    document.getElementById('dish-form-overlay').style.display = 'none';
}

async function submitDishForm(e) {
    e.preventDefault();
    const errorBox = document.getElementById('dish-form-error');
    errorBox.style.display = 'none';

    const id = document.getElementById('dish-form-id').value;
    const payload = {
        name: document.getElementById('dish-form-name').value.trim(),
        category: document.getElementById('dish-form-category').value,
        weightG: Number(document.getElementById('dish-form-weight').value),
        price: Number(document.getElementById('dish-form-price').value),
        imageUrl: document.getElementById('dish-form-image').value.trim() || null,
        description: document.getElementById('dish-form-description').value.trim() || null,
        isAvailable: document.getElementById('dish-form-available').checked
    };

    try {
        if (id) {
            await api.patch(`/admin/dishes/${id}`, payload);
        } else {
            await api.post('/admin/dishes', payload);
        }
        closeDishForm();
        await loadDishes();
    } catch (err) {
        errorBox.textContent = err.message || 'Не удалось сохранить блюдо';
        errorBox.style.display = 'block';
    }
}

async function deleteDish(id) {
    if (!confirm('Удалить блюдо? Если оно уже встречалось в чьём-то заказе, сервер откажет и предложит просто скрыть его из меню.')) return;

    try {
        await api.delete(`/admin/dishes/${id}`);
        await loadDishes();
    } catch (err) {
        alert(err.message || 'Не удалось удалить блюдо');
    }
}