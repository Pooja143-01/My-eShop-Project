using MediatR;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Entities
{
    /// <summary>
    /// Base entity that supports domain events using MediatR.
    /// </summary>
    public abstract class BaseEntity
    {
        public virtual int Id { get; protected set; }

        // Domain events list
        private List<INotification>? _domainEvents;
        public IReadOnlyCollection<INotification>? DomainEvents => _domainEvents?.AsReadOnly();

        public void AddDomainEvent(INotification eventItem)
        {
            _domainEvents ??= new List<INotification>();
            _domainEvents.Add(eventItem);
        }

        public void ClearDomainEvents()
        {
            _domainEvents?.Clear();
        }
    }
}