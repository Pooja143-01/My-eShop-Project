using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    
    // Updated for Azure Service Bus Integration
    private readonly string _serviceBusConnectionString;
    private readonly string _queueName;

    public OrderService(
        IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IConfiguration configuration)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;

        // Retrieve Service Bus settings from configuration
        _serviceBusConnectionString = configuration["ServiceBusConnectionString"];
        _queueName = configuration["OrderQueueName"] ?? "order-requests";
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        Guard.Against.Null(basket, nameof(basket));
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification =
            new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());

        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(
                catalogItem.Id,
                catalogItem.Name,
                _uriComposer.ComposePicUri(catalogItem.PictureUri));

            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        // 1. Save order to DB
        await _orderRepository.AddAsync(order);

        // 2. --- Send Reservation Message to Service Bus ---
        await SendOrderToQueue(order);
    }

    private async Task SendOrderToQueue(Order order)
    {
        if (string.IsNullOrEmpty(_serviceBusConnectionString))
        {
            System.Console.WriteLine("Service Bus connection string is missing. Skipping reservation message.");
            return;
        }

        var reservationData = new
        {
            OrderId = order.Id,
            Items = order.OrderItems.Select(i => new
            {
                ItemId = i.ItemOrdered.CatalogItemId,
                Quantity = i.Units
            }).ToList()
        };

        string jsonContent = JsonConvert.SerializeObject(reservationData);

        try
        {
            // Initialize the Service Bus client and sender
            await using var client = new ServiceBusClient(_serviceBusConnectionString);
            ServiceBusSender sender = client.CreateSender(_queueName);

            // Create and send the message
            ServiceBusMessage message = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonContent));
            await sender.SendMessageAsync(message);
            
            System.Console.WriteLine($"Successfully sent order {order.Id} to Service Bus queue.");
        }
        catch (System.Exception ex)
        {
            // Fallback: If Service Bus fails, the message won't be sent, but the order is still in the DB
            System.Console.WriteLine($"Failed to send order to Service Bus: {ex.Message}");
        }
    }
}