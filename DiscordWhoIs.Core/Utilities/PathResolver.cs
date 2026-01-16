namespace DiscordWhoIs.Core.Utilities;

public static class PathResolver
{
    public static bool IsDevelopment()
    {
        string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolvePath(string targetDir, string fileName)
    {
        if (IsDevelopment())
        {
            return ResolveDevPath(targetDir, fileName);
        }

        return ResolveProdPath(targetDir, fileName);
    }

    private static string ResolveDevPath(string targetDir, string fileName)
    {
        string solutionRoot = GetSolutionRoot();

        // treat TargetDirectory as a folder name in dev mode
        string relative = targetDir.TrimStart('/', '\\');

        string finalDir = Path.Combine(solutionRoot, relative);

        Directory.CreateDirectory(finalDir);

        return Path.Combine(finalDir, fileName);
    }

    private static string ResolveProdPath(string targetDir, string fileName)
    {
        string dir = string.IsNullOrWhiteSpace(targetDir)
            ? AppContext.BaseDirectory
            : targetDir;

        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    public static string GetSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException("Cannot find solution root (no .sln file found).");
        }

        return dir.FullName;
    }
}
