using OMG.Management.Domain.Abstractions;

namespace OMG.Management.Infrastructure;

public sealed class ManagementUnitOfWork(ManagementDbContext dbContext) : IManagementUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

