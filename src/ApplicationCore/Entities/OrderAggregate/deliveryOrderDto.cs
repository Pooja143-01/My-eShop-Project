using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public class DeliveryOrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; }
    public List<DeliveryOrderItemDto> Items { get; set; } = new();
}

public class DeliveryOrderItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}