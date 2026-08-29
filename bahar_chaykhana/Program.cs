using bahar_chaykhana.Data;
using bahar_chaykhana.Endpoints;
using bahar_chaykhana.Services;

var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();


app.MapMenuEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapOrderEndpoints();
app.MapReservationEndpoints();
app.MapAdminEndpoints();
app.MapCancelEndpoints();

app.MapGet("/api/health", () => "OK");


app.Run();