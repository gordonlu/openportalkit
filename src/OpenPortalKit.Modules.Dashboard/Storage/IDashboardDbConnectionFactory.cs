using System.Data.Common;

namespace OpenPortalKit.Modules.Dashboard.Storage;

public interface IDashboardDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
