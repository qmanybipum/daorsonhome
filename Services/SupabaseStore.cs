using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed class SupabaseStore(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "rest/v1/products?select=id,name,description,price,image_url&is_active=eq.true&order=created_at.desc");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ProductResponse>>(JsonOptions, cancellationToken)
            ?? throw new JsonException("Supabase returned an empty product response.");
    }

    public async Task<Guid> CreateOrderAsync(CreateOrderRequest order, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "rest/v1/rpc/create_order");
        request.Content = JsonContent.Create(new
        {
            p_customer_name = order.CustomerName!.Trim(),
            p_customer_email = order.CustomerEmail!.Trim(),
            p_customer_phone = order.CustomerPhone!.Trim(),
            p_shipping_address = order.ShippingAddress!.Trim(),
            p_items = order.Items!.Select(item => new
            {
                productId = item.ProductId,
                quantity = item.Quantity
            })
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(JsonOptions, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var supabaseUrl = configuration["SUPABASE_URL"] ?? configuration["Supabase:Url"];
        var serviceRoleKey = configuration["SUPABASE_SERVICE_ROLE_KEY"] ?? configuration["Supabase:ServiceRoleKey"];

        if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !baseUri.IsLoopback) ||
            string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new SupabaseConfigurationException(
                "Set SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY in the server environment.");
        }

        var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        return request;
    }
}

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl);

public sealed class SupabaseConfigurationException(string message) : Exception(message);
