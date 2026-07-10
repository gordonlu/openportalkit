namespace OpenPortalKit.Modules.Content.BlockTemplates;

public interface IBlockDefinitionCatalog
{
    IReadOnlyList<BlockDefinition> List();

    BlockDefinition? FindByCode(string code);
}
