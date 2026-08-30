using bahar_chaykhana.Data;
using bahar_chaykhana.Endpoints;
using bahar_chaykhana.Services;

var builder = WebApplication.CreateBuilder(args);

// Регистрируем подключение к БД
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

// Регистрируем сервис отправки email (понадобится для уведомлений)
builder.Services.AddSingleton<EmailService>();

var app = builder.Build();

// Раздача статических файлов (HTML/CSS/JS)
app.UseDefaultFiles();
app.UseStaticFiles();

// Подключаем все эндпоинты API
app.MapMenuEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapOrderEndpoints();
app.MapReservationEndpoints();
app.MapAdminEndpoints();
app.MapCancelEndpoints();

// Проверочный endpoint
app.MapGet("/api/health", () => "OK");

app.Run();