using System.Data.Common;

namespace OpenPortalKit.Kernel.Persistence;

public interface IOpenPortalKitDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
