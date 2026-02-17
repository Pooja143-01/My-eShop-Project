using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace eShop.Function;

public class OrderItemsReserver
{
    private readonly ILogger _logger;
    private const int MaxRetries = 3;

    public OrderItemsReserver(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<OrderItemsReserver>();
    }

    [Function("OrderItemsReserver")]
    public async Task Run(
        [ServiceBusTrigger("order-requests", Connection = "ServiceBusConnectionString")] 
        string myQueueItem)
    {
        _logger.LogInformation("Processing order request from Service Bus.");

        var storageConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
        
        if (string.IsNullOrEmpty(storageConnectionString))
        {
            _logger.LogCritical("BlobStorageConnectionString is missing in configuration.");
            throw new Exception("Configuration Missing"); 
        }

        // Implementation of Retry Policy (3 attempts)
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(storageConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("orders-to-reserve");
                
                // Ensure container exists
                await containerClient.CreateIfNotExistsAsync();

                var fileName = $"order-{Guid.NewGuid():N}.json";
                var blobClient = containerClient.GetBlobClient(fileName);

                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(myQueueItem));
                await blobClient.UploadAsync(ms, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                });

                _logger.LogInformation($"Successfully uploaded {fileName} on attempt {attempt}");
                
                // Success - exit the function
                return; 
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Attempt {attempt} failed: {ex.Message}");

                if (attempt == MaxRetries)
                {
                    _logger.LogError("Final attempt failed. Message will be moved to Dead Letter Queue for Logic App processing.");
                    // Throwing the exception tells Service Bus the processing failed.
                    // After the Service Bus "Max Delivery Count" is reached, it goes to the DLQ.
                    throw; 
                }

                // Wait briefly before retrying (Exponential backoff could be used here)
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
        }
    }
}