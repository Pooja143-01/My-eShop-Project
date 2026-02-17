using System; // Fixes the 'Guid' error
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.UnitTests.Builders;
using Moq;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.OrderRepositoryTests;

public class GetByIdWithItemsAsync
{
    private readonly CatalogContext _catalogContext;
    private readonly EfRepository<Order> _orderRepository;
    private OrderBuilder OrderBuilder { get; } = new OrderBuilder();

    public GetByIdWithItemsAsync()
{
    var dbOptions = new DbContextOptionsBuilder<CatalogContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
        .Options;
    _catalogContext = new CatalogContext(dbOptions);

    // Using Full Namespace to avoid CS0246
    var mockDispatcher = new Moq.Mock<Microsoft.eShopWeb.ApplicationCore.Interfaces.IDomainEventDispatcher>();
    
    _orderRepository = new EfRepository<Order>(_catalogContext, mockDispatcher.Object);
}

    [Fact]
    public async Task GetOrderAndItemsByOrderIdWhenMultipleOrdersPresent()
    {
        var itemOneUnitPrice = 5.50m;
        var itemOneUnits = 2;
        var itemTwoUnitPrice = 7.50m;
        var itemTwoUnits = 5;

        var firstOrder = OrderBuilder.WithDefaultValues();
        _catalogContext.Orders.Add(firstOrder);

        var secondOrderItems = new List<OrderItem>
        {
            new(OrderBuilder.TestCatalogItemOrdered, itemOneUnitPrice, itemOneUnits),
            new(OrderBuilder.TestCatalogItemOrdered, itemTwoUnitPrice, itemTwoUnits)
        };
        var secondOrder = OrderBuilder.WithItems(secondOrderItems);
        _catalogContext.Orders.Add(secondOrder);

        _catalogContext.SaveChanges();
        int secondOrderId = secondOrder.Id;

        var spec = new OrderWithItemsByIdSpec(secondOrderId);
        var orderFromRepo = await _orderRepository.FirstOrDefaultAsync(spec, default);

        Assert.Equal(secondOrderId, orderFromRepo.Id);
        Assert.Equal(secondOrder.OrderItems.Count, orderFromRepo.OrderItems.Count);
    }
}