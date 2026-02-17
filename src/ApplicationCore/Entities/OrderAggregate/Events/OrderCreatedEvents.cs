using MediatR;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Events;

public class OrderCreatedEvent : INotification
{
    public OrderCreatedEvent(Order order)
    {
        Order = order;
    }

    public Order Order { get; }
}