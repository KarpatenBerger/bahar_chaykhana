// booking.js — отправка формы бронирования столика.

document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('booking-form');
    if (!form) return; // не на странице бронирования

    const messageBox = document.getElementById('booking-message');

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const submitBtn = form.querySelector('button[type="submit"]');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Отправляем...';

        const payload = {
            date: document.getElementById('date').value,
            time: document.getElementById('time').value,
            guests: Number(document.getElementById('guests').value),
            customerName: document.getElementById('name').value,
            phone: document.getElementById('phone').value,
            email: document.getElementById('email').value || null, // необязательное поле
            comment: document.getElementById('comment').value
        };

        try {
            await api.post('/reservations', payload);

            messageBox.textContent = payload.email
                ? 'Заявка отправлена! Подтверждение и ссылку для отмены пришлём вам на почту в течение 15 минут.'
                : 'Заявка отправлена! Мы подтвердим бронь по телефону в течение 15 минут.';
            messageBox.className = 'booking-message success';
            messageBox.style.display = 'block';

            form.reset();
        } catch (err) {
            messageBox.textContent = 'Не удалось отправить заявку: ' + err.message;
            messageBox.className = 'booking-message error';
            messageBox.style.display = 'block';
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = 'Забронировать столик';
        }
    });
});
