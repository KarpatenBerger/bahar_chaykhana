using bahar_chaykhana.Data;
using bahar_chaykhana.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace bahar_chaykhana.Endpoints;

public static class MenuEndpoints
{
    public static void MapMenuEndpoints(this WebApplication app)
    {
        // GET /api/menu — список всех доступных блюд
        app.MapGet("/api/menu", async (DbConnectionFactory db) =>
        {
            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, category, weight_g, price, image_url, description, is_available
                FROM dishes
                WHERE is_available = TRUE
                ORDER BY category, name";

            var dishes = new List<Dish>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dishes.Add(new Dish
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    WeightG = reader.GetInt32(3),
                    Price = reader.GetDecimal(4),
                    ImageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsAvailable = reader.GetBoolean(7)
                });
            }

            return Results.Ok(dishes);
        });
    }
}