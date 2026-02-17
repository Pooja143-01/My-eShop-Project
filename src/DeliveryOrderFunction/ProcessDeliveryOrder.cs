using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.CosmosDB;
using Microsoft.Extensions.Logging;

namespace eshop.Function;

public class ProcessDeliveryOrder
{
    private readonly ILogger<ProcessDeliveryOrder> _logger;

    public ProcessDeliveryOrder(ILogger<ProcessDeliveryOrder> logger)
    {
        _logger = logger;
    }

    [Function("ProcessDeliveryOrder")]
    public async Task<MyOutputType> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing new delivery order request.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        try
        {
            var orderData = JsonSerializer.Deserialize<DeliveryOrderRequest>(
                requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // ==== Basic validation ====
            if (orderData is null)
            {
                return await BadRequest(req, "Invalid JSON payload.");
            }

            if (string.IsNullOrWhiteSpace(orderData.ShippingAddress))
            {
                return await BadRequest(req, "ShippingAddress is required.");
            }

            if (orderData.Items is null || orderData.Items.Count == 0)
            {
                return await BadRequest(req, "At least one item is required.");
            }

            if (orderData.FinalPrice <= 0)
            {
                return await BadRequest(req, "FinalPrice must be greater than zero.");
            }

            // ==== Build Cosmos document ====
            var newOrder = new DeliveryOrderDocument
            {
                Id = Guid.NewGuid().ToString(),
                OrderId = Guid.NewGuid().ToString(), // if you already have orderId from web app, populate it instead
                ShippingAddress = orderData.ShippingAddress,
                Items = orderData.Items,
                FinalPrice = orderData.FinalPrice,
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Prepared Cosmos DB document. id={Id}, orderId={OrderId}",
                                   newOrder.Id, newOrder.OrderId);

            // ==== Prepare HTTP response ====
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new
            {
                message = "Order saved to Cosmos DB.",
                orderId = newOrder.OrderId,
                documentId = newOrder.Id
            });

            // Multiple outputs: HTTP + Cosmos DB document
            return new MyOutputType
            {
                HttpResponse = ok,
                NewOrderDocument = newOrder
            };
        }
        catch (JsonException jex)
        {
            _logger.LogError(jex, "JSON parse error.");
            return await BadRequest(req, "Malformed JSON.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing order.");
            var fatal = req.CreateResponse(HttpStatusCode.InternalServerError);
            await fatal.WriteAsJsonAsync(new { message = "Internal server error." });
            return new MyOutputType { HttpResponse = fatal };
        }
    }

    private static async Task<MyOutputType> BadRequest(HttpRequestData req, string message)
    {
        var res = req.CreateResponse(HttpStatusCode.BadRequest);
        await res.WriteAsJsonAsync(new { message });
        return new MyOutputType { HttpResponse = res };
    }
}

// Wrapper class to handle multiple outputs
public class MyOutputType
{
    // Cosmos DB Output:
    // - DatabaseName: DeliveryOrdersDb
    // - ContainerName: Orders
    // - CreateIfNotExists: true
    // - PartitionKeyPath: /orderId  (created if container doesn't exist)
    // - PartitionKey value for this item: {orderId}
    [CosmosDBOutput(
        databaseName: "DeliveryOrdersDb",
        containerName: "Orders",
        Connection = "CosmosDBConnection",
        CreateIfNotExists = true,
        PartitionKey = "{orderId}")]
    public DeliveryOrderDocument? NewOrderDocument { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}

// ====== Data models ======

public class DeliveryOrderDocument
{
    // 'id' is the unique document id in Cosmos DB.
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Partition key. Keep it stable (e.g., the order id from your SQL order).
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("shippingAddress")]
    public string ShippingAddress { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<DeliveryOrderItem> Items { get; set; } = new();

    [JsonPropertyName("finalPrice")]
    public decimal FinalPrice { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class DeliveryOrderItem
{
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public class DeliveryOrderRequest
{
    public string ShippingAddress { get; set; } = string.Empty;
    public List<DeliveryOrderItem> Items { get; set; } = new();
    public decimal FinalPrice { get; set; }
}