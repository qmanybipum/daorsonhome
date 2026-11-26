using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<SupabaseStore>();

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    if (!int.TryParse(renderPort, out var port) || port is < 1 or > 65535)
    {
        throw new InvalidOperationException("Render PORT must be a valid TCP port.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    var accessUsername = builder.Configuration["SITE_ACCESS_USERNAME"] ?? "daorson";
    var accessPassword = builder.Configuration["SITE_ACCESS_PASSWORD"];

    if (string.IsNullOrWhiteSpace(accessPassword))
    {
        app.Logger.LogError("SITE_ACCESS_PASSWORD is missing; public requests will be blocked.");
    }

    app.Use(async (context, next) =>
    {
        if (string.IsNullOrWhiteSpace(accessPassword))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Private site access is not configured.");
            return;
        }

        if (!HasValidCredentials(context.Request.Headers.Authorization, accessUsername, accessPassword))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"Daebak Supplies\", charset=\"UTF-8\"";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync("Authentication is required.");
            return;
        }

        await next();
    });
}

app.UseAuthorization();
app.MapStaticAssets();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/products", async (
    SupabaseStore store,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await store.GetProductsAsync(cancellationToken));
    }
    catch (SupabaseConfigurationException exception)
    {
        logger.LogError(exception, "Supabase is not configured.");
        return Results.Problem("The product service is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception) when (exception is HttpRequestException or JsonException)
    {
        logger.LogError(exception, "Could not retrieve products from Supabase.");
        return Results.Problem("The product service is temporarily unavailable.", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/orders", async (
    CreateOrderRequest order,
    SupabaseStore store,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(order.CustomerName) || order.CustomerName.Length > 120)
        errors[nameof(order.CustomerName)] = ["Name is required and must be 120 characters or fewer."];
    if (string.IsNullOrWhiteSpace(order.CustomerEmail) || order.CustomerEmail.Length > 254 ||
        !MailAddress.TryCreate(order.CustomerEmail, out _))
        errors[nameof(order.CustomerEmail)] = ["A valid email address is required."];
    if (string.IsNullOrWhiteSpace(order.CustomerPhone) || order.CustomerPhone.Length > 40)
        errors[nameof(order.CustomerPhone)] = ["Phone is required and must be 40 characters or fewer."];
    if (string.IsNullOrWhiteSpace(order.ShippingAddress) || order.ShippingAddress.Length > 1000)
        errors[nameof(order.ShippingAddress)] = ["Address is required and must be 1000 characters or fewer."];
    if (order.Items is null || order.Items.Count is < 1 or > 30 ||
        order.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity is < 1 or > 10))
        errors[nameof(order.Items)] = ["Provide 1 to 30 items with valid product IDs and quantities from 1 to 10."];

    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    try
    {
        var orderId = await store.CreateOrderAsync(order, cancellationToken);
        return Results.Ok(new CreateOrderResponse(orderId));
    }
    catch (SupabaseConfigurationException exception)
    {
        logger.LogError(exception, "Supabase is not configured.");
        return Results.Problem("The order service is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception) when (exception is HttpRequestException or JsonException)
    {
        logger.LogError(exception, "Could not create an order in Supabase.");
        return Results.Problem("The order service is temporarily unavailable.", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static bool HasValidCredentials(string? authorizationHeader, string expectedUsername, string expectedPassword)
{
    if (string.IsNullOrEmpty(authorizationHeader) || authorizationHeader.Length > 4096 ||
        !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    byte[] credentialBytes;
    try
    {
        credentialBytes = Convert.FromBase64String(authorizationHeader[6..]);
    }
    catch (FormatException)
    {
        return false;
    }

    string credentials;
    try
    {
        credentials = new UTF8Encoding(false, true).GetString(credentialBytes);
    }
    catch (DecoderFallbackException)
    {
        return false;
    }

    var separator = credentials.IndexOf(':');
    if (separator < 0)
    {
        return false;
    }

    var suppliedUsername = Encoding.UTF8.GetBytes(credentials[..separator]);
    var suppliedPassword = Encoding.UTF8.GetBytes(credentials[(separator + 1)..]);
    var usernameMatches = CryptographicOperations.FixedTimeEquals(
        suppliedUsername,
        Encoding.UTF8.GetBytes(expectedUsername));
    var passwordMatches = CryptographicOperations.FixedTimeEquals(
        suppliedPassword,
        Encoding.UTF8.GetBytes(expectedPassword));

    return usernameMatches && passwordMatches;
}
