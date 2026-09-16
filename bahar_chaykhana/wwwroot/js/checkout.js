// checkout.js — оформление заказа: показывает состав корзины,
// переключает поле адреса при выборе доставки, считает скидку бонусами,
// отправляет заказ на сервер.

document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('checkout-form');
    if (!form) return; // не на странице оформления заказа

    const cart = getCart(); // функция из cart.js

    // Если корзина пуста — незачем оформлять заказ, отправляем в меню
    if (cart.length === 0) {
        window.location.href = 'menu.html';
        return;
    }

    renderOrderSummary();
    prefillFromAccount();

    // Если гость авторизован — подставляем его имя, телефон и email из
    // личного кабинета, чтобы не заставлять вводить то, что там уже есть.
    // Поля остаются редактируемыми: например, гость может оформлять заказ
    // на другой номер телефона или для другого получателя.
    async function prefillFromAccount() {
        const session = getGuestSession(); // функция из api.js
        if (!session) return; // гость не авторизован — оставляем поля пустыми

        try {
            const profile = await api.get('/account'); // сервер сверяет cookie-сессию
            document.getElementById('name').value = profile.name;
            document.getElementById('phone').value = profile.phone;
            document.getElementById('email').value = profile.email;

            const hint = document.getElementById('prefill-hint');
            if (hint) hint.style.display = 'block';
        } catch {
            // сессия на сервере истекла — просто оставляем поля пустыми,
            // гость введёт данные вручную, как незарегистрированный
        }
    }

    // Переключение доставка/самовывоз показывает поле адреса и стоимость доставки
    const deliveryRadios = form.querySelectorAll('input[name="delivery-type"]');
    const addressGroup = document.getElementById('address-group');
    const addressInput = document.getElementById('address');
    const deliveryRow = document.getElementById('delivery-row');
    const DELIVERY_COST = 300;

    deliveryRadios.forEach((radio) => {
        radio.addEventListener('change', () => {
            const isDelivery = radio.value === 'delivery' && radio.checked;
            if (radio.checked) {
                addressGroup.style.display = isDelivery ? 'block' : 'none';
                addressInput.required = isDelivery;
                deliveryRow.style.display = isDelivery ? 'flex' : 'none';
                renderOrderSummary();
            }
        });
    });

    // Пересчёт итога при изменении суммы списываемых бонусов.
    // Слайдер и числовое поле синхронизированы между собой — можно тянуть
    // ползунок или сразу вписать точное число баллов.
    const bonusSlider = document.getElementById('bonus-slider');
    const bonusInput = document.getElementById('bonus-input');

    if (bonusSlider && bonusInput) {
        bonusSlider.addEventListener('input', () => {
            bonusInput.value = bonusSlider.value;
            renderOrderSummary();
        });
        bonusInput.addEventListener('input', () => {
            // Заодно подчищаем то, что можно ввести вручную в number-поле:
            // отрицательные числа и значения выше текущего максимума.
            const max = Number(bonusInput.max) || 0;
            let value = Math.round(Number(bonusInput.value)) || 0;
            value = Math.min(Math.max(value, 0), max);
            bonusInput.value = value;
            bonusSlider.value = value;
            renderOrderSummary();
        });
    }

    function getSelectedDeliveryType() {
        const checked = form.querySelector('input[name="delivery-type"]:checked');
        return checked ? checked.value : 'pickup';
    }

    function renderOrderSummary() {
        const itemsContainer = document.getElementById('order-items');
        const subtotal = getCartTotal(cart); // функция из cart.js

        itemsContainer.innerHTML = cart.map((item) => `
            <div class="order-item">
                <span>${item.name} × ${item.quantity}</span>
                <span>${item.price * item.quantity} ₽</span>
            </div>
        `).join('');

        const isDelivery = getSelectedDeliveryType() === 'delivery';
        const deliveryCost = isDelivery ? DELIVERY_COST : 0;

        const session = getGuestSession(); // функция из api.js
        let bonusDiscount = 0;

        const bonusSliderEl = document.getElementById('bonus-slider');
        const bonusInputEl = document.getElementById('bonus-input');
        const bonusBalanceEl = document.getElementById('bonus-balance');

        if (session) {
            const balance = session.bonusBalance || 0;
            if (bonusBalanceEl) bonusBalanceEl.textContent = balance;

            // Правило из ТЗ: бонусами можно оплатить не больше 90% суммы заказа,
            // и не больше, чем реально есть на балансе.
            const maxBonus = Math.min(balance, Math.floor((subtotal + deliveryCost) * 0.9));

            if (bonusSliderEl && bonusInputEl) {
                const isUsable = maxBonus > 0;
                bonusSliderEl.disabled = !isUsable;
                bonusInputEl.disabled = !isUsable;
                bonusSliderEl.max = maxBonus;
                bonusInputEl.max = maxBonus;

                // Если максимум уменьшился (например, поменяли способ доставки
                // и сумма заказа снизилась) — подрезаем уже выбранное значение,
                // а не сбрасываем его в ноль.
                const current = Math.min(Number(bonusSliderEl.value) || 0, maxBonus);
                bonusSliderEl.value = current;
                bonusInputEl.value = current;

                bonusDiscount = current;
            }
        }

        const bonusDiscountEl = document.getElementById('bonus-discount');
        if (bonusDiscountEl) bonusDiscountEl.textContent = `-${bonusDiscount} ₽`;

        const total = subtotal + deliveryCost - bonusDiscount;

        document.getElementById('subtotal').textContent = `${subtotal} ₽`;
        document.getElementById('final-total').textContent = `${total} ₽`;
    }

    // Отправка заказа
    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const submitBtn = form.querySelector('button[type="submit"]');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Отправляем...';

        const deliveryType = getSelectedDeliveryType();
        const bonusSliderEl = document.getElementById('bonus-slider');

        const orderPayload = {
            items: cart.map((item) => ({ dishId: item.id, quantity: item.quantity })),
            deliveryType,
            address: deliveryType === 'delivery' ? addressInput.value : null,
            phone: document.getElementById('phone').value,
            email: document.getElementById('email').value || null, // необязательное поле
            customerName: document.getElementById('name').value,
            comment: document.getElementById('comment').value,
            bonusToUse: bonusSliderEl ? Number(bonusSliderEl.value) || 0 : 0
        };

        try {
            const order = await api.post('/orders', orderPayload);

            // Заказ успешно создан — очищаем корзину и показываем подтверждение
            localStorage.removeItem('bahar_cart');

            alert(`Заказ №${order.id} принят! ${orderPayload.email
                ? 'Уведомления о статусе придут вам на почту.'
                : 'Уточнить статус можно по телефону.'}`);

            window.location.href = 'menu.html';
        } catch (err) {
            alert('Не удалось оформить заказ: ' + err.message);
            submitBtn.disabled = false;
            submitBtn.textContent = 'Подтвердить заказ';
        }
    });
});