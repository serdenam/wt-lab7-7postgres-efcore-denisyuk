using Microsoft.EntityFrameworkCore;
using wt_lab7_7postgres_efcore_denisyuk.Data;
using wt_lab7_7postgres_efcore_denisyuk.Models;
using NSwag.AspNetCore;
using Npgsql; // Обязательно добавлено для NpgsqlConnectionStringBuilder

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. Подключение к базе данных (PostgreSQL)
// ==========================================
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Диагностика: выводим в логи, что видит приложение
Console.WriteLine("========================================");
Console.WriteLine($"DEBUG: DATABASE_URL is null or empty? {string.IsNullOrWhiteSpace(databaseUrl)}");
if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    Console.WriteLine($"DEBUG: DATABASE_URL starts with: {databaseUrl.Substring(0, Math.Min(20, databaseUrl.Length))}...");
}
Console.WriteLine($"DEBUG: DefaultConnection is null or empty? {string.IsNullOrWhiteSpace(connectionString)}");
Console.WriteLine("========================================");

if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    try
    {
        // Надежный парсинг URL от Render
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var dbBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Disable // Важно для внутренней сети Render
        };

        connectionString = dbBuilder.ConnectionString;
        Console.WriteLine("DEBUG: Successfully parsed DATABASE_URL into connection string.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG ERROR: Failed to parse DATABASE_URL. Exception: {ex.Message}");
    }
}

// Если строки подключения нет вообще, выбрасываем понятную ошибку
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("КРИТИЧЕСКАЯ ОШИБКА: Строка подключения пуста! Render не передал DATABASE_URL, и локальная строка тоже отсутствует.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// ==========================================
// 2. Настройка OpenAPI и Swagger (NSwag)
// ==========================================
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "WT-7 Library API (PostgreSQL)";
    config.Version = "v1";
});

var app = builder.Build();

// ==========================================
// 3. Автоматическое применение миграций при запуске
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ==========================================
// 4. Включаем Swagger UI
// ==========================================
app.MapOpenApi();
app.UseOpenApi();
app.UseSwaggerUi();

// ==========================================
// 5. Эндпоинты (CRUD через Entity Framework Core)
// ==========================================

// GET: Список всех книг
app.MapGet("/api/books", async (AppDbContext db) =>
{
    var books = await db.Books.ToListAsync();
    return Results.Ok(books);
});

// GET: Поиск по названию или автору (LINQ)
app.MapGet("/api/books/search", async (string? search, AppDbContext db) =>
{
    var query = db.Books.AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(b => b.Title.ToLower().Contains(search.ToLower())
                              || b.Author.ToLower().Contains(search.ToLower()));
    }
    var books = await query.ToListAsync();
    return Results.Ok(books);
});

// GET: Получение книги по ID
app.MapGet("/api/books/{id}", async (int id, AppDbContext db) =>
{
    var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
    if (book == null) return Results.NotFound();
    return Results.Ok(book);
});

// POST: Создание новой книги
app.MapPost("/api/books", async (Book book, AppDbContext db) =>
{
    db.Books.Add(book);
    await db.SaveChangesAsync();
    return Results.Created($"/api/books/{book.Id}", book);
});

// PUT: Изменение существующей книги
app.MapPut("/api/books/{id}", async (int id, Book updatedBook, AppDbContext db) =>
{
    var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
    if (book == null) return Results.NotFound();

    book.Title = updatedBook.Title;
    book.Author = updatedBook.Author;
    book.Year = updatedBook.Year;
    book.IsAvailable = updatedBook.IsAvailable;

    await db.SaveChangesAsync();
    return Results.Ok(book);
});

// DELETE: Удаление книги
app.MapDelete("/api/books/{id}", async (int id, AppDbContext db) =>
{
    var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
    if (book == null) return Results.NotFound();

    db.Books.Remove(book);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ==========================================
// 6. Дополнительные LINQ-запросы (по заданию)
// ==========================================

// Фильтрация: книги, изданные после 1950 года
app.MapGet("/api/books/filtered", async (AppDbContext db) =>
{
    var books = await db.Books.Where(b => b.Year > 1950).ToListAsync();
    return Results.Ok(books);
});

// Сортировка: по году издания
app.MapGet("/api/books/sorted", async (AppDbContext db) =>
{
    var books = await db.Books.OrderBy(b => b.Year).ToListAsync();
    return Results.Ok(books);
});

// Выборка с условием: только доступные книги, сортировка по названию
app.MapGet("/api/books/available", async (AppDbContext db) =>
{
    var books = await db.Books.Where(b => b.IsAvailable).OrderBy(b => b.Title).ToListAsync();
    return Results.Ok(books);
});

// Собственный запрос: поиск по автору
app.MapGet("/api/books/by-author/{author}", async (string author, AppDbContext db) =>
{
    var books = await db.Books.Where(b => b.Author.ToLower().Contains(author.ToLower())).ToListAsync();
    return Results.Ok(books);
});

// ==========================================
// 7. Запуск приложения на порту Render
// ==========================================
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");