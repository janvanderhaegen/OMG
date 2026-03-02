using OMG.Management.Domain.Abstractions;

namespace OMG.Management.Infrastructure;

public sealed class PublishingUnitOfWork(ManagementDbContext dbContext) : IPublishUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

