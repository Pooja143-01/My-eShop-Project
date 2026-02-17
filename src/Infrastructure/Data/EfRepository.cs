using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Services;

namespace Microsoft.eShopWeb.Infrastructure.Data
{
    /// <summary>
    /// EF Repository extended with domain event dispatching.
    /// Compatible with Ardalis.Specification.RepositoryBase<T>.
    /// </summary>
    public class EfRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T>
        where T : class, IAggregateRoot
    {
        private readonly CatalogContext _dbContext;
        private readonly IDomainEventDispatcher _dispatcher;

        public EfRepository(CatalogContext dbContext, IDomainEventDispatcher dispatcher)
            : base(dbContext)
        {
            _dbContext = dbContext;
            _dispatcher = dispatcher;
        }

        public override async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await base.AddAsync(entity, cancellationToken);

            await DispatchDomainEvents();

            return entity;
        }

        // FIXED: Must return Task<int>
        public override async Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var result = await base.UpdateAsync(entity, cancellationToken);

            await DispatchDomainEvents();

            return result;
        }

        // FIXED: Must return Task<int>
        public override async Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            var result = await base.DeleteAsync(entity, cancellationToken);

            await DispatchDomainEvents();

            return result;
        }

        private async Task DispatchDomainEvents()
        {
            var entitiesWithEvents = _dbContext.ChangeTracker
                .Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents != null && e.Entity.DomainEvents.Any())
                .Select(e => e.Entity)
                .ToList();

            if (entitiesWithEvents.Any())
            {
                await _dispatcher.DispatchAndClearEvents(entitiesWithEvents);
            }
        }
    }
}