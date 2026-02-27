namespace OMG.Management.Domain.Abstractions;

public interface IManagementUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

