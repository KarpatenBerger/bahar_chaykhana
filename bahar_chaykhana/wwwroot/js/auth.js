// auth.js — вход и регистрация гостя в личном кабинете.
// Сама проверка пароля происходит на сервере; здесь только отправка формы
// и сохранение данных для отображения интерфейса (см. getGuestSession в api.js).

document.addEventListener('DOMContentLoaded', () => {
    setupLoginForm();
    setupRegisterForm();
    setupLogoutButton();
});

function setupLoginForm() {
    const form = document.getElementById('login-form');
    if (!form) return;

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const submitBtn = form.querySelector('button[type="submit"]');
        submitBtn.disabled = true;

        const payload = {
            email: document.getElementById('email').value,
            password: document.getElementById('password').value
        };

        try {
            const user = await api.post('/auth/login', payload);

            // ДОБАВЛЕНО: разветвление по роли — сотрудник уходит в админ-панель,
            // гость — в личный кабинет, как раньше.
            if (user.role === 'employee') {
                // Тот же localStorage-ключ и формат, что использует admin.js
                // при входе через admin/login.html — поэтому guardAdminPage()
                // в admin.js пропустит сотрудника без дополнительных правок.
                localStorage.setItem('bahar_admin_session', JSON.stringify({ name: user.name }));
                window.location.href = 'admin/dashboard.html';
                return;
            }

            setGuestSession({
                name: user.name,
                email: user.email,
                bonusBalance: user.bonusBalance
            });
            window.location.href = 'profile.html';
        } catch (err) {
            alert('Не удалось войти: ' + err.message);
            submitBtn.disabled = false;
        }
    });
}

function setupRegisterForm() {
    const form = document.getElementById('register-form');
    if (!form) return;

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const password = document.getElementById('password').value;
        const passwordConfirm = document.getElementById('password-confirm').value;

        if (password !== passwordConfirm) {
            alert('Пароли не совпадают');
            return;
        }

        const submitBtn = form.querySelector('button[type="submit"]');
        submitBtn.disabled = true;

        const payload = {
            name: document.getElementById('name').value,
            phone: document.getElementById('phone').value,
            email: document.getElementById('email').value,
            password
        };

        try {
            const user = await api.post('/auth/register', payload);
            setGuestSession({
                name: user.name,
                email: user.email,
                bonusBalance: 0
            });
            window.location.href = 'profile.html';
        } catch (err) {
            alert('Не удалось зарегистрироваться: ' + err.message);
            submitBtn.disabled = false;
        }
    });
}

function setupLogoutButton() {
    const btn = document.getElementById('logout-btn');
    if (!btn) return;

    btn.addEventListener('click', async () => {
        try {
            await api.post('/auth/logout', {});
        } catch {
            // сессия на сервере могла уже истечь — это не мешает выйти локально
        }
        clearGuestSession();
        // ДОБАВЛЕНО: на случай если это был сотрудник, вошедший через общую форму —
        // чистим и его localStorage-сессию тоже, чтобы не осталось «залипшего» входа
        localStorage.removeItem('bahar_admin_session');
        window.location.href = 'index.html';
    });
}