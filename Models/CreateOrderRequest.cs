namespace WebApplication1.Models;

public sealed record CreateOrderRequest(
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? ShippingAddress,
    IReadOnlyList<OrderItemRequest>? Items);

public sealed record OrderItemRequest(Guid ProductId, int Quantity);

public sealed record CreateOrderResponse(Guid OrderId);
