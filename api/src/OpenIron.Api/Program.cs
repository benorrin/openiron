using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Tests swap in SQLite, which has no migrations; create its schema from the model instead.
    if (db.Database.IsNpgsql()) await db.Database.MigrateAsync();
    else await db.Database.EnsureCreatedAsync();
    await SeedData.EnsureAsync(db);
}

app.MapControllers();

app.Run();

public partial class Program;
