using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Web.DomainEventHandlers
{
    public class OrderCreatedHandler : INotificationHandler<OrderCreatedEvent>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<OrderCreatedHandler> _logger;

        public OrderCreatedHandler(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<OrderCreatedHandler> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
        {
            var functionUrl = _config["DeliveryFunctionUrl"];
            if (string.IsNullOrWhiteSpace(functionUrl))
            {
                _logger.LogWarning("DeliveryFunctionUrl not configured.");
                return;
            }

            var order = notification.Order;

            // Build DTO matching the Function's expected payload
            var dto = new DeliveryOrderDto
            {
                OrderId = order.Id.ToString(),
                ShippingAddress = $"{order.ShipToAddress.Street}, {order.ShipToAddress.City}, {order.ShipToAddress.State} {order.ShipToAddress.ZipCode}, {order.ShipToAddress.Country}",
                FinalPrice = order.Total(),
                Items = order.OrderItems.Select(i => new DeliveryOrderItemDto
                {
                    ProductName = i.ItemOrdered.ProductName,
                    Quantity = i.Units
                }).ToList()
            };

            // Serialize in camelCase so it becomes: orderId, shippingAddress, finalPrice, items[ { productName, quantity } ]
            var json = JsonSerializer.Serialize(dto,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _logger.LogInformation("OrderCreatedHandler -> URL: {Url}", functionUrl);
            _logger.LogInformation("OrderCreatedHandler -> Payload: {Json}", json);

            var client = _httpClientFactory.CreateClient();
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(functionUrl, content, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("ProcessDeliveryOrder failed. Status={Status} Body={Body}", resp.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("ProcessDeliveryOrder succeeded for OrderId={OrderId}", order.Id);
            }
        }
    }
}