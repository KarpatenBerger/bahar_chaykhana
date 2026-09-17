using bahar_chaykhana.Data;
using Npgsql;
using System.Text.RegularExpressions;

namespace bahar_chaykhana.Endpoints;

public static class AdminMenuEndpoints
{
    public static void MapAdminMenuEndpoints(this WebApplication app)
    {
        // ---------- Категории ----------

        // GET /api/admin/categories — то же самое, что публичный /api/categories,
        // но с id и sort_order — нужны для формы редактирования в панели.
        app.MapGet("/api/admin/categories", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, slug, label, sort_order FROM categories ORDER BY sort_order";

            var categories = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new
                {
                    id = reader.GetInt32(0),
                    slug = reader.GetString(1),
                    label = reader.GetString(2),
                    sortOrder = reader.GetInt32(3)
                });
            }

            return Results.Ok(categories);
        });

        app.MapPost("/api/admin/categories", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            var payload = await context.Request.ReadFromJsonAsync<CategoryRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Slug) || string.IsNullOrWhiteSpace(payload.Label))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });

            // Слаг — техническое имя категории (как в коде: soups, drinks и т.д.),
            // именно его хранит dishes.category, поэтому ограничиваем алфавит.
            var slug = payload.Slug.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(slug, "^[a-z0-9_-]+$"))
                return Results.BadRequest(new { message = "Слаг категории может содержать только латинские буквы, цифры, - и _" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO categories (slug, label, sort_order)
                    VALUES (@slug, @label, @sortOrder)
                    RETURNING id";
                cmd.Parameters.AddWithValue("@slug", slug);
                cmd.Parameters.AddWithValue("@label", payload.Label.Trim());
                cmd.Parameters.AddWithValue("@sortOrder", payload.SortOrder);
                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return Results.Ok(new { id, slug, label = payload.Label.Trim(), sortOrder = payload.SortOrder });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return Results.BadRequest(new { message = "Категория с таким слагом уже существует" });
            }
        });

        // ПРИМЕЧАНИЕ: слаг после создания изменить нельзя — на него по значению
        // ссылаются существующие блюда (dishes.category), переименование задним
        // числом потребовало бы обновлять их все разом. Можно менять только
        // отображаемое название и порядок сортировки.
        app.MapPatch("/api/admin/categories/{slug}", async (HttpContext context, string slug, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            var payload = await context.Request.ReadFromJsonAsync<CategoryUpdateRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Label))
                return Results.BadRequest(new { message = "Не указано название категории" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE categories SET label = @label, sort_order = @sortOrder WHERE slug = @slug";
            cmd.Parameters.AddWithValue("@label", payload.Label.Trim());
            cmd.Parameters.AddWithValue("@sortOrder", payload.SortOrder);
            cmd.Parameters.AddWithValue("@slug", slug);
            var affected = await cmd.ExecuteNonQueryAsync();

            return affected == 0 ? Results.NotFound(new { message = "Категория не найдена" }) : Results.Ok();
        });

        app.MapDelete("/api/admin/categories/{slug}", async (HttpContext context, string slug, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM categories WHERE slug = @slug";
                cmd.Parameters.AddWithValue("@slug", slug);
                var affected = await cmd.ExecuteNonQueryAsync();

                return affected == 0 ? Results.NotFound(new { message = "Категория не найдена" }) : Results.Ok();
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                // dishes_category_fkey: в категории ещё остались блюда (ON DELETE RESTRICT)
                return Results.BadRequest(new { message = "Нельзя удалить категорию, пока в ней есть блюда. Сначала удалите или перенесите блюда в другую категорию." });
            }
        });

        // ---------- Блюда ----------

        // GET /api/admin/dishes — в отличие от публичного /api/menu, отдаёт ВСЕ
        // блюда, включая is_available = false: админу нужно их видеть, чтобы
        // отредактировать или снова опубликовать.
        app.MapGet("/api/admin/dishes", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, category, weight_g, price, image_url, description, is_available
                FROM dishes
                ORDER BY category, name";

            var dishes = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dishes.Add(new
                {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1),
                    category = reader.GetString(2),
                    weightG = reader.GetInt32(3),
                    price = reader.GetDecimal(4),
                    imageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                    description = reader.IsDBNull(6) ? null : reader.GetString(6),
                    isAvailable = reader.GetBoolean(7)
                });
            }

            return Results.Ok(dishes);
        });

        app.MapPost("/api/admin/dishes", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            var payload = await context.Request.ReadFromJsonAsync<DishRequest>();
            var error = ValidateDish(payload);
            if (error != null) return Results.BadRequest(new { message = error });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO dishes (name, category, weight_g, price, image_url, description, is_available)
                    VALUES (@name, @category, @weightG, @price, @imageUrl, @description, @isAvailable)
                    RETURNING id";
                cmd.Parameters.AddWithValue("@name", payload!.Name.Trim());
                cmd.Parameters.AddWithValue("@category", payload.Category);
                cmd.Parameters.AddWithValue("@weightG", payload.WeightG);
                cmd.Parameters.AddWithValue("@price", payload.Price);
                cmd.Parameters.AddWithValue("@imageUrl", (object?)payload.ImageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@description", (object?)payload.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@isAvailable", payload.IsAvailable);
                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return Results.Ok(new { id });
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                return Results.BadRequest(new { message = "Такой категории не существует" });
            }
        });

        app.MapPatch("/api/admin/dishes/{id:int}", async (HttpContext context, int id, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            var payload = await context.Request.ReadFromJsonAsync<DishRequest>();
            var error = ValidateDish(payload);
            if (error != null) return Results.BadRequest(new { message = error });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE dishes
                    SET name = @name, category = @category, weight_g = @weightG, price = @price,
                        image_url = @imageUrl, description = @description, is_available = @isAvailable
                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@name", payload!.Name.Trim());
                cmd.Parameters.AddWithValue("@category", payload.Category);
                cmd.Parameters.AddWithValue("@weightG", payload.WeightG);
                cmd.Parameters.AddWithValue("@price", payload.Price);
                cmd.Parameters.AddWithValue("@imageUrl", (object?)payload.ImageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@description", (object?)payload.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@isAvailable", payload.IsAvailable);
                cmd.Parameters.AddWithValue("@id", id);
                var affected = await cmd.ExecuteNonQueryAsync();

                return affected == 0 ? Results.NotFound(new { message = "Блюдо не найдено" }) : Results.Ok();
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                return Results.BadRequest(new { message = "Такой категории не существует" });
            }
        });

        app.MapDelete("/api/admin/dishes/{id:int}", async (HttpContext context, int id, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM dishes WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                var affected = await cmd.ExecuteNonQueryAsync();

                return affected == 0 ? Results.NotFound(new { message = "Блюдо не найдено" }) : Results.Ok();
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                // order_items.dish_id: блюдо уже встречалось в чьём-то заказе —
                // удалить нельзя, не сломав историю заказов гостя. Предлагаем
                // снять с публикации вместо жёсткого удаления.
                return Results.BadRequest(new { message = "Нельзя удалить: блюдо есть в истории заказов. Снимите его с публикации переключателем «доступно» — оно пропадёт из меню, но история заказов не пострадает." });
            }
        });
    }

    private static string? ValidateDish(DishRequest? payload)
    {
        if (payload == null) return "Некорректные данные";
        if (string.IsNullOrWhiteSpace(payload.Name)) return "Не указано название блюда";
        if (string.IsNullOrWhiteSpace(payload.Category)) return "Не указана категория";
        if (payload.WeightG <= 0) return "Вес/объём должен быть больше нуля";
        if (payload.Price <= 0) return "Цена должна быть больше нуля";
        return null;
    }
}

public record CategoryRequest(string Slug, string Label, int SortOrder);
public record CategoryUpdateRequest(string Label, int SortOrder);
public record DishRequest(string Name, string Category, int WeightG, decimal Price, string? ImageUrl, string? Description, bool IsAvailable);