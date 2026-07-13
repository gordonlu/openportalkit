namespace OpenPortalKit.Cli;

public static class RepositoryLocator
{
    public static string Find(string? startPath = null)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath ?? Directory.GetCurrentDirectory()));

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OpenPortalKit.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate OpenPortalKit.sln. Run the command inside an OpenPortalKit repository or pass --root.");
    }
}
