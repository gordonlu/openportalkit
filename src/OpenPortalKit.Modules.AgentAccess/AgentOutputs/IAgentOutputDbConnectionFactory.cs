using System.Data.Common;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public interface IAgentOutputDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
