-- seed_dishes.sql
-- Полное наполнение таблицы dishes реальными данными меню чайханы «Бахар» (90 позиций),
-- заменяет собой прежний плейсхолдер-набор из 60 придуманных блюд.

-- ==== ОБНУЛЕНИЕ ТАБЛИЦЫ (выполняется первым) ====
-- Полностью очищает dishes и сбрасывает счётчик id (SERIAL) перед вставкой новых данных.
-- CASCADE удалит и все order_items, ссылающиеся на старые блюда — это осознанный выбор
-- для тестовой базы; на проде с реальными заказами так делать нельзя.
-- Отдельной таблицы категорий в схеме нет (category — это просто VARCHAR(50) внутри самой
-- dishes, см. schema.sql / Models/Dish.cs), поэтому обнулять отдельно нечего — сброс dishes
-- уже обнуляет и все значения категорий.
TRUNCATE TABLE dishes RESTART IDENTITY CASCADE;
-- ================================================
--
-- ВАЖНО: в реальном меню оказалось 8 категорий, а не 6, как в прежнем черновике.
-- Новые коды категорий, добавленные этим файлом: 'starters' (Закуски) и 'pastries' (Выпечка).
-- Как и для 'bread'/'banquet' ранее — на menu.html для них ещё нет отдельных кнопок-фильтров,
-- их нужно будет добавить в разметку.
-- 'bread' и 'banquet' в реальном меню отсутствуют: лаваш и лепёшка идут внутри 'pastries'
-- (в источнике данных они перечислены в категории «Выпечка», а не отдельно), банкетных
-- пакетов в текущем прайсе нет.
--
-- image_url — полный путь от корня сайта (/images/dishes/<файл>), а не голое имя файла:
-- именно так menu.js подставляет его в <img src="${dish.imageUrl}">. Имена файлов взяты
-- из wwwroot/images/dishes в самом репозитории (не из ворд-таблицы) — там были расхождения
-- в регистре и минимум одна опечатка (khinkali_trlyatina → правильно khinkali_telyatina).

-- Супы
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Окрошка с копчёной курицей на айране', 'soups', 320, 340, '/images/dishes/chicken_okroshka_na_airane.jpg', 'копчёная курица, огурец, картофель, редис, яйцо, зелень, специи, заправка айран'),
('Куриный суп', 'soups', 300, 420, '/images/dishes/chicken_soup.jpg', 'бульон куриный, лапша домашняя, курица, яйцо, зелень'),
('Дюшбара', 'soups', 300, 470, '/images/dishes/dushbara.jpg', 'суп с традиционными пельмешками из фарша ягнёнка, лук, соль, перец, специи'),
('Грибной крем-суп', 'soups', 300, 380, '/images/dishes/gribnoy_krem_soup.jpg', 'грибы, лук, сливки, специи'),
('Харчо', 'soups', 350, 490, '/images/dishes/kharcho.jpg', 'телятина, рис, помидор, лук, чеснок, специи'),
('Кюфта-бозбаш', 'soups', 350, 510, '/images/dishes/kufta_bozbash.jpg', 'фарш ягнёнка, картофель, нут, мята сушёная, специи'),
('Пити', 'soups', 400, 650, '/images/dishes/piti.jpg', 'баранина, картофель, нут, специи'),
('Шурпа', 'soups', 400, 590, '/images/dishes/shurpa.jpg', 'телятина, картофель, морковь, помидор, болгарский перец, лук, нут, специи');

-- Шашлык
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Цыплёнок на решётке', 'shashlik', 500, 970, '/images/dishes/cyplenok_na_reshotke.jpg', 'цыплёнок, лук, лаваш, соус сацебели'),
('Ягненок на кости', 'shashlik', 250, 890, '/images/dishes/jagnenok_on_bone.jpg', 'ягнёнок на косточке, лук, лаваш, соус сацебели'),
('Хан-кебаб', 'shashlik', 320, 1000, '/images/dishes/khan_kebab.jpg', 'рулеты из телятины с курдюком ягненка, лук, лаваш, соус сацебели'),
('Шашлык из креветки', 'shashlik', 200, 980, '/images/dishes/krevetki_shashlyk.jpg', 'тигровые креветки, лимон, лаваш'),
('Куриное филе', 'shashlik', 250, 550, '/images/dishes/kurinoe_file.jpg', 'куриное филе, лук, лаваш, соус сацебели'),
('Куриные бёдра', 'shashlik', 250, 650, '/images/dishes/kurinye_bedra.jpg', 'куриные бёдра, лук, лаваш, соус сацебели'),
('Куриные крылья', 'shashlik', 250, 500, '/images/dishes/kurinye_krylya.jpg', 'куриные крылья, лук, лаваш, соус сацебели'),
('Люля из ягнёнка на лаваше', 'shashlik', 420, 890, '/images/dishes/lula_iz_jagnenka_na_lavashe.jpg', 'люля-кебаб ягненок, лепешка, лук, помидоры, зелень, соус'),
('Люля из телятины на лаваше', 'shashlik', 420, 890, '/images/dishes/lula_iz_telyatiny_na_lavashe.jpg', 'люля-кебаб телятина, лепешка, лук, помидоры, зелень, соус'),
('Люля из курицы на лаваше', 'shashlik', 420, 650, '/images/dishes/lula_iz_kuricy_na_lavashe.jpg', 'люля-кебаб курица, лепешка, лук, помидоры, зелень, соус'),
('Люля-кебаб из курицы с грибами', 'shashlik', 250, 620, '/images/dishes/lula_iz_kuricy_s_gribami.jpg', 'курица, курдюк, шампиньоны, лук, лаваш, соус сацебели'),
('Люля-кебаб из курицы', 'shashlik', 250, 590, '/images/dishes/lulakebab_chicken.jpg', 'курица, курдюк, лук, лаваш, соус сацебели'),
('Люля-кебаб из ягнёнка', 'shashlik', 250, 790, '/images/dishes/lulakebab_jagnenok.jpg', 'ягнёнок, курдюк, лук, лаваш, соус сацебели'),
('Люля-кебаб из телятины', 'shashlik', 250, 790, '/images/dishes/lulakebab_telyatina.jpg', 'телятина, говяжий жир, лаваш, лук, соус "Сацебели"'),
('Овощи на мангале', 'shashlik', 300, 420, '/images/dishes/ovoshi_na_mangale.jpg', 'баклажан, кабачок, сладкий перец, шампиньоны, помидор'),
('Ассорти из шашлыков', 'shashlik', 1300, 5400, '/images/dishes/shashlychnoe_assorti.jpg', 'мякоть ягнёнка, ягнёнок на кости, люля-кебаб из ягнёнка, люля-кебаб из курицы, антрекот ягнёнка, куриное филе, куриные бёдра, куриные крылья, картофель с курдюком, лаваш, лук, соус "Сацебели"'),
('Шашлык из антрекота ягнёнка', 'shashlik', 250, 980, '/images/dishes/shashlyk_iz_antrekota_jagnenka.jpg', 'антрекот ягненка, лук, лаваш, соус сацебели'),
('Шашлык из говядины', 'shashlik', 250, 710, '/images/dishes/shashlyk_iz_govyadiny.jpg', 'Говядина, лук, лаваш, соус сацебели'),
('Шашлык из языка с картофелем', 'shashlik', 420, 980, '/images/dishes/shashlyk_iz_jazyka_s_kartofelem.jpg', 'говяжий язык, картофель, соус сметанно-горчичный'),
('Шашлык из мякоти ягнёнка', 'shashlik', 250, 950, '/images/dishes/shashlyk_iz_myakoti_jagnenko.jpg', 'мякоть ягненка, лук, лаваш, соус сацебели'),
('Шашлык из телячьей вырезки', 'shashlik', 250, 1150, '/images/dishes/shashlyk_iz_telyachey_vyrezki.jpg', 'вырезка телятины, лук, лаваш, соус сацебели');

-- Горячее
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Бадымджан', 'hot', 300, 550, '/images/dishes/badymdzhan.jpg', 'баклажан, фарш говяжий, лук красный, сладкий перец, микс зелени, помидоры, специи'),
('Долма', 'hot', 220, 690, '/images/dishes/dolma.jpg', 'виноградные листья, фарш из ягнёнка, рис, мята, лук, специи, соус «Гатык»'),
('Кузу Хамира', 'hot', 400, 850, '/images/dishes/kuzu_khamira.jpg', 'ножка ягнёнка, картофель, сладкий перец, помидор, лук, зелень, специи, запечённые под тестом'),
('Плов классический', 'hot', 250, 500, '/images/dishes/plov.jpg', 'баранина, лук, морковь, чеснок, нут, изюм, рис, специи'),
('Плов азербайджанский', 'hot', 350, 620, '/images/dishes/plov_azerbajdzanskiy.jpg', 'лук, баранина, курага, изюм, чернослив, рис длиннозёрный, алыча, специи'),
('Телятина с овощами', 'hot', 250, 750, '/images/dishes/telatina_with_vegetables.jpg', 'телятина, морковь, сладкий перец, лук, черри, специи, зелень');

-- Закуски
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Брокколи запечённая с сыром', 'starters', 170, 420, '/images/dishes/brokoli_zpaechonye_s_syrom.jpg', 'брокколи, сливки, сыр моцарелла'),
('Ассорти сыров', 'starters', 270, 920, '/images/dishes/cheese_assorti.jpg', 'кавказские сыры, грецкий орех, мёд'),
('Рыбное ассорти', 'starters', 200, 1200, '/images/dishes/fish_assorti.jpg', 'малосольные форель, нерка и палтус, лимон, оливки'),
('Кавказские соленья', 'starters', 420, 550, '/images/dishes/kavkazskie_solenya.jpg', 'грибы, огурцы, черри, сладкий перец, капуста белая, капуста красная, ранетки, айва, брусника'),
('Мясное ассорти', 'starters', 280, 1300, '/images/dishes/meat_assorti.jpg', 'ростбиф, рулет куриный, язык говяжий, бастурма, хрен, огурец солёный'),
('Рулетики из баклажана', 'starters', 180, 550, '/images/dishes/ruletiki_iz_baklazhana.jpg', 'баклажан, творожный сыр, сыр сиртаки, кинза, вяленые помидоры'),
('Шампиньоны с сыром', 'starters', 170, 490, '/images/dishes/shampinony_s_syrom.jpg', 'грибы, сливки, сыр моцарелла'),
('Овощное ассорти', 'starters', 350, 550, '/images/dishes/vegetables_assorti.jpg', 'огурец, сладкий перец, помидор, редис, лук красный, зелень, перец чили');

-- Салаты
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Цезарь с курицей', 'salads', 230, 450, '/images/dishes/caesar_chicken.jpg', 'листья салата, куриное филе, черри, сухари, сыр пармезан, соус «Цезарь»'),
('Цезарь с креветкой', 'salads', 230, 500, '/images/dishes/caesar_krevetky.jpg', 'листья салата, тигровая креветка, черри, сухари, сыр пармезан, соус «Цезарь»'),
('Кавказский салат', 'salads', 220, 775, '/images/dishes/caucasian_salad.jpg', 'телячья вырезка, болгарский перец, баклажан, микс зелени, соус устричный, кунжут'),
('Хрустящий баклажан', 'salads', 250, 500, '/images/dishes/khrustyashiy_baklazhan.jpg', 'баклажан, соус «Азия», сыр фета, черри, зелень'),
('Салат с ростбифом', 'salads', 200, 690, '/images/dishes/rostbeef_salad.jpg', 'ростбиф, микс салата, огурец, редис, черри, заправка азиатская'),
('Салат с языком', 'salads', 280, 620, '/images/dishes/salat_s_jazykom.jpg', 'говяжий язык, картофель, лук, микс салата, маринованный огурец, черри, зелень, заправка: соус из хрена/сметанно-горчичный соус'),
('Салат со свёклой', 'salads', 200, 450, '/images/dishes/svekla_salad.jpg', 'микс салата, свёкла, огурец, овечий сыр, медово-горчичная заправка'),
('Салат с тыквой', 'salads', 210, 480, '/images/dishes/tykva_salad.jpg', 'тыква печёная, салат айсберг, вяленые томаты, творожный сыр, тыквенные семечки, орехи кешью');

-- Выпечка
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Чебурек', 'pastries', 200, 300, '/images/dishes/cheburek.jpg', 'тесто, фарш ягнёнка, специи'),
('Гюрза с курицей', 'pastries', 250, 490, '/images/dishes/gurza_chicken.jpg', 'тесто, лук, специи, фарш курицы'),
('Гюрза с ягнёнком', 'pastries', 250, 570, '/images/dishes/gurza_jagnenok.jpg', 'тесто, лук, специи, фарш ягнёнка'),
('Гюрза с телятиной', 'pastries', 250, 590, '/images/dishes/gurza_telatina.jpg', 'тесто, лук, специи, фарш телятины'),
('Хачапури по-мегрельски', 'pastries', 300, 560, '/images/dishes/khachapuri_megrelian.jpg', 'дрожжевое тесто, сыр'),
('Хачапури с картофелем и сыром', 'pastries', 300, 560, '/images/dishes/khachapuri_with_potato_and_cheese.jpg', 'дрожжевое тесто, картофель, сыр'),
('Хачапури по-аджарски', 'pastries', 300, 580, '/images/dishes/khachapuri_po_adzharski.jpg', 'дрожжевое тесто, сыр, яйцо'),
('Хинкали с ягненком', 'pastries', 100, 160, '/images/dishes/khinkali_jagnenok.jpg', '1 шт, фарш ягнёнка с зеленью и чесноком, перец чили, специи, лук'),
('Хинкали с телятиной', 'pastries', 100, 110, '/images/dishes/khinkali_telyatina.jpg', '1 шт, фарш телятины с зеленью и чесноком, перец чили, специи, лук'),
('Хинкали с морепродуктами', 'pastries', 100, 190, '/images/dishes/khinkali_moreprodukty.jpg', '1 шт, кальмар, гребешок, креветка, сливки, зелень, специи'),
('Кутабы с сыром', 'pastries', 150, 300, '/images/dishes/kutaby_s_syrom.jpg', 'тесто, сыр сулугуни'),
('Кутабы с сыром и зеленью', 'pastries', 200, 400, '/images/dishes/kutaby_syr_zelen.jpg', 'тесто, сыр моцарелла, кинза, укроп, петрушка, зеленый лук, соус'),
('Кутабы с телятиной', 'pastries', 200, 450, '/images/dishes/kutaby_telyatina.jpg', 'тесто, фарш телятины'),
('Лаваш', 'pastries', 80, 100, '/images/dishes/lavash.jpg', 'тонкий азербайджанский лаваш'),
('Лепёшка', 'pastries', 180, 100, '/images/dishes/lepeshka.jpg', 'домашняя лепешка'),
('Манты с ягнёнком', 'pastries', 300, 480, '/images/dishes/manty_jagnenok.jpg', 'фарш ягнёнка, лук, специи'),
('Манты с телятиной', 'pastries', 300, 490, '/images/dishes/manty_telyatina.jpg', 'фарш телятины, лук, специи');

-- Напитки
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Айран', 'drinks', 500, 160, '/images/dishes/airan.png', NULL),
('Американо', 'drinks', 150, 100, '/images/dishes/americano.jpg', NULL),
('Азерчай с чабрецом', 'drinks', 400, 150, '/images/dishes/azerchay_s_chabrecom.jpg', NULL),
('Капучино', 'drinks', 180, 180, '/images/dishes/cappuccino.jpg', NULL),
('Кола Рич 0,33', 'drinks', 330, 140, '/images/dishes/cola_rich.jpg', 'кола "Рич" 330мл в стекле'),
('Сок Добрый 1л в ассортименте', 'drinks', 1000, 350, '/images/dishes/dobry_juice.jpg', 'Ананас, яблоко, апельсин, вишня'),
('Чай «Восточная сказка»', 'drinks', 400, 300, '/images/dishes/east_fairy_tea.jpg', NULL),
('Эспрессо', 'drinks', 30, 130, '/images/dishes/espresso.jpg', NULL),
('Чай «Фруктовый»', 'drinks', 400, 300, '/images/dishes/fruit_tea.jpg', NULL),
('Чай зелёный/чёрный', 'drinks', 200, 75, '/images/dishes/green_black_tea.png', NULL),
('Медовый красный чай', 'drinks', 400, 250, '/images/dishes/honey_red_tea.jpg', NULL),
('Морс 1л', 'drinks', 1000, 470, '/images/dishes/mors_1l.jpg', 'Брусничный морс'),
('Морс 0,5л', 'drinks', 500, 250, '/images/dishes/mors_05l.jpg', 'Брусничный морс'),
('Сок «Рич» в ассортименте', 'drinks', 200, 190, '/images/dishes/rich_juice.jpg', 'Вишня, томат, апельсин, персик, яблоко'),
('Чай «Сокровище императора»', 'drinks', 400, 300, '/images/dishes/sokrovishe_imperatora_tea.jpg', NULL),
('Зелёный чай с жасмином', 'drinks', 400, 280, '/images/dishes/zeleny_chay_s_zhasminom.jpg', NULL);

-- Десерты
INSERT INTO dishes (name, category, weight_g, price, image_url, description) VALUES
('Яблочный штрудель', 'desserts', 200, 320, '/images/dishes/apple_shtrudel.jpg', 'слоеное тесто, яблоки, корица, мед, мороженое'),
('Медовик', 'desserts', 160, 390, '/images/dishes/medovik.jpg', 'бездрожжевое тесто, заварной крем, мед, яйца, сливочное масло'),
('Орешки со сгущёнкой', 'desserts', 120, 300, '/images/dishes/oreshki_so_sgushenkoy.jpg', 'тесто песочное, вареная сгущенка'),
('Пахлава азербайджанская', 'desserts', 150, 400, '/images/dishes/pahlava_azerbajdzhanskaya.jpg', 'грецкий орех, фундук, сахар, мед, сок лимона'),
('Тирамису', 'desserts', 200, 490, '/images/dishes/tiramisu.jpg', 'печенье савоярди, яйца, крем маскарпоне, кофе'),
('Трубочки со сгущёнкой', 'desserts', 140, 350, '/images/dishes/trubochki_so_sgushenkoy.jpg', 'тесто, сливки, вареная сгущенка, грецкий орех (2шт)');