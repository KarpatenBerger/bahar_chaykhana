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

    // Пересчёт итога при переключении бонусов
    const bonusCheckbox = document.getElementById('use-bonus');
    if (bonusCheckbox) {
        bonusCheckbox.addEventListener('change', renderOrderSummary);
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

        const bonusCheckboxEl = document.getElementById('use-bonus');
        const bonusBalanceEl = document.getElementById('bonus-balance');

        if (session) {
            const balance = session.bonusBalance || 0;
            if (bonusBalanceEl) bonusBalanceEl.textContent = balance;
            if (bonusCheckboxEl) {
                bonusCheckboxEl.disabled = balance === 0;
                if (bonusCheckboxEl.checked) {
                    bonusDiscount = Math.min(balance, Math.round((subtotal + deliveryCost) * 0.9));
                }
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
        const bonusCheckboxEl = document.getElementById('use-bonus');

        const orderPayload = {
            items: cart.map((item) => ({ dishId: item.id, quantity: item.quantity })),
            deliveryType,
            address: deliveryType === 'delivery' ? addressInput.value : null,
            phone: document.getElementById('phone').value,
            email: document.getElementById('email').value || null, // необязательное поле
            customerName: document.getElementById('name').value,
            comment: document.getElementById('comment').value,
            useBonus: bonusCheckboxEl ? bonusCheckboxEl.checked : false
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
