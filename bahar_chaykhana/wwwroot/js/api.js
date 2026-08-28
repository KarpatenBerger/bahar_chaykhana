// api.js — общий помощник для обращения к бэкенду (ASP.NET Core Minimal API)
// Подключается почти на всех страницах перед остальными скриптами.

const API_BASE = '/api';

/**
 * Универсальная обёртка над fetch().
 * Сама добавляет заголовки JSON, разбирает ответ и бросает понятную ошибку,
 * если сервер ответил кодом ошибки (4xx/5xx).
 */
async function apiRequest(endpoint, { method = 'GET', body = null } = {}) {
    const options = {
        method,
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include' // отправляем cookie-сессию (нужно для входа гостя/сотрудника)
    };

    if (body !== null) {
        options.body = JSON.stringify(body);
    }

    let response;
    try {
        response = await fetch(API_BASE + endpoint, options);
    } catch (networkError) {
        // сервер недоступен, нет интернета и т.п.
        throw new Error('Не удалось связаться с сервером. Проверьте подключение и попробуйте ещё раз.');
    }

    // Пустой ответ (например, 204 No Content) — не пытаемся парсить JSON
    const text = await response.text();
    const data = text ? JSON.parse(text) : null;

    if (!response.ok) {
        const message = (data && data.message) ? data.message : `Ошибка сервера (${response.status})`;
        throw new Error(message);
    }

    return data;
}

const api = {
    get: (endpoint) => apiRequest(endpoint, { method: 'GET' }),
    post: (endpoint, body) => apiRequest(endpoint, { method: 'POST', body }),
    patch: (endpoint, body) => apiRequest(endpoint, { method: 'PATCH', body }),
    delete: (endpoint) => apiRequest(endpoint, { method: 'DELETE' })
};

// ---------- Корзина: счётчик в шапке ----------
// Эта часть нужна почти на каждой странице, поэтому вынесена сюда,
// а не в cart.js (он подключён не везде, например, отсутствует на index.html).

function getCartFromStorage() {
    const raw = localStorage.getItem('bahar_cart');
    if (!raw) return [];
    try {
        return JSON.parse(raw);
    } catch {
        // повреждённые данные в localStorage — не роняем страницу
        return [];
    }
}

function updateCartBadge() {
    const cart = getCartFromStorage();
    const count = cart.reduce((sum, item) => sum + item.quantity, 0);
    const badge = document.getElementById('cart-count');
    if (badge) {
        badge.textContent = count;
    }
}

// Обновляем счётчик сразу при загрузке любой страницы, где есть api.js
document.addEventListener('DOMContentLoaded', updateCartBadge);

// ---------- Сессия гостя (личный кабинет) ----------
// Хранится в localStorage в виде простого объекта после успешного входа.
// Настоящая проверка прав всё равно происходит на сервере по cookie-сессии —
// то, что лежит тут, используется только для отображения интерфейса
// (имя, баланс бонусов), чтобы не дёргать сервер на каждой странице.

const SESSION_KEY = 'bahar_session';

function getGuestSession() {
    const raw = localStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    try {
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

function setGuestSession(sessionData) {
    localStorage.setItem(SESSION_KEY, JSON.stringify(sessionData));
}

function clearGuestSession() {
    localStorage.removeItem(SESSION_KEY);
}
