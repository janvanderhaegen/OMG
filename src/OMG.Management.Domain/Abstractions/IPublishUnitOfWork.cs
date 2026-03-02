namespace OMG.Management.Domain.Abstractions;

public interface IPublishUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

