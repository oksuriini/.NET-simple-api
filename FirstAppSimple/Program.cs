using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpLogging;


// adding logging for request
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpLogging(opts => opts.LoggingFields = HttpLoggingFields.RequestProperties);
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpLogging", LogLevel.Information);

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseHttpLogging();
}

app.UseDeveloperExceptionPage();
app.UseStaticFiles();
app.UseRouting();


var snacks = new ConcurrentDictionary<string, Snack>();


app.MapGet("/", () => "API has received your request");

app.MapGet("/snacks", () => snacks);

app.MapGet("/snack/{id}", (string id) =>
    snacks.TryGetValue(id, out var snack)
        ? TypedResults.Ok(snack)
        : Results.NotFound()
        );

app.MapPost("/snack/{id}", (string id, Snack snack) => snacks.TryAdd(id, snack) 
    ? TypedResults.Created($"/snack/{id}", snack) 
    : Results.BadRequest(new {id = "Snack with this ID already exists"}));

app.MapPut("/snack/{id}", (string id, Snack snack) =>
{
    snacks[id] = snack;
    return Results.NoContent();
});

app.MapDelete("/snack/{id}", (string id) =>
{
    snacks.TryRemove(id, out Snack snack);
    if (snack == null)
    {
        return Results.NotFound();
    }

    return TypedResults.Ok(snack);
});


app.Run();

public record Snack(string name, int rating, string[] taste);
