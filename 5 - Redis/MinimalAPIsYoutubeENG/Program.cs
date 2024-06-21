using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using MinimalAPIsYoutubeENG.Contexts;
using MinimalAPIsYoutubeENG.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//builder.Services.AddOutputCache();

builder.Services.AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer("name=defaultConnection"));

var app = builder.Build();

// Configure the HTTP request pipeline (middlewares)

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(state =>
    {
        var httpContext = (HttpContext)state;
        httpContext.Response.Headers.Remove("Age");
        return Task.CompletedTask;
    }, context);

    await next.Invoke(context);
});

app.UseOutputCache();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 10).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

var message = builder.Configuration.GetValue<string>("message");
app.MapGet("/message", () => message);

app.MapGet("/people", async (ApplicationDbContext context) =>
{
    var people = await context.People.ToListAsync();
    return TypedResults.Ok(people);
}).CacheOutput(c => c.Expire(TimeSpan.FromSeconds(60)).Tag("people-get"));

// /people/7
app.MapGet("/people/{id:int}", async Task<Results<Ok<Person>, NotFound>> (int id,
    ApplicationDbContext context) =>
{
    var person = await context.People.FirstOrDefaultAsync(p => p.Id == id);

    if (person is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(person);

}).WithName("GetPerson");

app.MapPost("/people", async (Person person, 
    ApplicationDbContext context, IOutputCacheStore outputCacheStore) =>
{
    context.Add(person);
    await context.SaveChangesAsync();
    await outputCacheStore.EvictByTagAsync("people-get", default);
    return TypedResults.CreatedAtRoute(person, "GetPerson", new { id = person.Id });
});

app.MapPut("/people/{id:int}", async Task<Results<BadRequest<string>, NotFound, NoContent>>
    (int id, Person person, ApplicationDbContext context, IOutputCacheStore outputCacheStore) =>
{
    if (id != person.Id)
    {
        return TypedResults.BadRequest("The ids do not match");
    }

    var exists = await context.People.AnyAsync(p => p.Id == id);

    if (!exists)
    {
        return TypedResults.NotFound();
    }

    context.Update(person);
    await context.SaveChangesAsync();
    await outputCacheStore.EvictByTagAsync("people-get", default);

    return TypedResults.NoContent();
});

app.MapDelete("/people/{id:int}", async Task<Results<NotFound, NoContent>> (int id,
    ApplicationDbContext context, IOutputCacheStore outputCacheStore) =>
{
    var deletedRecords = await context.People.Where(p => p.Id == id).ExecuteDeleteAsync();

    if (deletedRecords == 0)
    {
        return TypedResults.NotFound();
    }

    await outputCacheStore.EvictByTagAsync("people-get", default);

    return TypedResults.NoContent();
});

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
