using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.UnitTests.Builders;
using Moq;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.OrderRepositoryTests;

public class GetById
{
    private readonly CatalogContext _catalogContext;
    private readonly EfRepository<Order> _orderRepository;
    private OrderBuilder OrderBuilder { get; } = new OrderBuilder();

    public GetById()
    {
        var dbOptions = new DbContextOptionsBuilder<CatalogContext>()
            .UseInMemoryDatabase(databaseName: "TestCatalog_GetById")
            .Options;
        _catalogContext = new CatalogContext(dbOptions);

        var mockDispatcher = new Mock<IDomainEventDispatcher>();
        _orderRepository = new EfRepository<Order>(_catalogContext, mockDispatcher.Object);
    }

    [Fact]
    public async Task GetsExistingOrder()
    {
        var existingOrder = OrderBuilder.WithDefaultValues();
        _catalogContext.Orders.Add(existingOrder);
        _catalogContext.SaveChanges();
        int orderId = existingOrder.Id;

        var orderFromRepo = await _orderRepository.GetByIdAsync(orderId, default);
        Assert.Equal(OrderBuilder.TestBuyerId, orderFromRepo.BuyerId);

        var firstItem = orderFromRepo.OrderItems.FirstOrDefault();
        Assert.Equal(OrderBuilder.TestUnits, firstItem.Units);
    }
}