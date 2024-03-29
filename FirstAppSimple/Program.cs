using System.Collections.Concurrent;
using System.Net.Mime;
using System.Reflection;
using Microsoft.AspNetCore.HttpLogging;



// adding logging for request
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpLogging(opts => opts.LoggingFields = HttpLoggingFields.RequestProperties);
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpLogging", LogLevel.Information);
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks();
builder.Services.AddRazorPages();

WebApplication app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHttpLogging();
}
app.UseStatusCodePages();
app.UseStaticFiles();
app.UseRouting();


var snacks = new ConcurrentDictionary<string, Snack>();

app.MapGet("/", () => "API has received your request");

app.MapGet("/test", () => "Helloust there");
app.MapHealthChecks("/healthz");
app.MapRazorPages();

RouteGroupBuilder snackApi = app.MapGroup("/snack");

snackApi.MapGet("/", () => snacks);

RouteGroupBuilder snackApiWithValid = snackApi.MapGroup("/")
    .AddEndpointFilterFactory(ValidationHelper.ValidateIdFactory);

snackApiWithValid.MapGet("/{id}", (string id) =>

    snacks.TryGetValue(id, out var snack)
        ? TypedResults.Ok(snack)
        : Results.Problem(statusCode: 404)
).AddEndpointFilter<IdValidationFilter>();

snackApiWithValid.MapPost("/{id}", (Snack snack, string id) => snacks.TryAdd(id, snack)
    ? TypedResults.Created($"/snack/{id}", snack)
    : Results.ValidationProblem(new Dictionary<string, string[]>
    {
        { "id", new[] { "A fruit with this id already exists" } }
    }));

snackApi.MapPut("/{id}", (string id, Snack snack) =>
{
    snacks[id] = snack;
    return Results.NoContent();
});

snackApi.MapDelete("/{id}", (string id) =>
{
    snacks.TryRemove(id, out Snack snack);
    if (snack == null)
    {
        return Results.NotFound();
    }

    return TypedResults.Ok(snack);
});

app.MapGet("/secret", (HttpResponse httpRes) =>
{
    httpRes.StatusCode = 420;
    httpRes.ContentType = MediaTypeNames.Text.Plain;
    return httpRes.WriteAsync("FirstAppSimple/wwwroot/FunnyDoggie.jpg");
});

app.MapGet("/throwerror",  () => Results.NotFound());


app.Run();

class ValidationHelper
{
    internal static async ValueTask<object?> ValidateId(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var id = context.GetArgument<string>(0);
        if (string.IsNullOrEmpty(id) || !id.StartsWith('s'))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    { "id", new[] { "Snack ID must not be empty, and has to start with the letter 's'" } }
                });
        }

        return await next(context);
    }

    internal static EndpointFilterDelegate ValidateIdFactory(
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next)
    {
        ParameterInfo[] parameters = context.MethodInfo.GetParameters();
        int? idPosition = null;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == "id" && parameters[i].ParameterType == typeof(string))
            {
                idPosition = i;
                break;
            }
        }

        if (!idPosition.HasValue)
        {
            return next;
        }

        return async (invocationContext) =>
        {
            var id = invocationContext.GetArgument<string>(idPosition.Value);
            if (string.IsNullOrEmpty(id) || !id.StartsWith('s'))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                        { { "id", new[] { "Snack ID must not be empty, and has to start with the letter 's'" } } });
            }

            return await next(invocationContext);
        };
    }
}

class IdValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var id = context.GetArgument<string>(0);
        if (string.IsNullOrEmpty(id) || !id.StartsWith('s'))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    { "id", new[] { "Snack ID must not be empty, and has to start with the letter 's'" } }
                });
        }
        return await next(context);
    }
}

public record Snack(string name, int rating, string[] taste);
