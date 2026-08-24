using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CafeMaestro.Tests;

/// <summary>
/// Reads the shared visual-system resource dictionaries straight from source so the
/// design contract can be asserted without spinning up a MAUI application host.
/// </summary>
internal static class XamlResourceReader
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static readonly Regex ResourceReferencePattern =
        new(@"\{(?:Static|Dynamic)Resource\s+([A-Za-z_][A-Za-z0-9_]*)\s*\}", RegexOptions.Compiled);

    private static readonly Regex KeyDeclarationPattern =
        new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static string StylesDirectory { get; } =
        Path.Combine(RepositoryRoot, "CafeMaestro", "Resources", "Styles");

    /// <summary>Reads the top-level keyed entries of a resource dictionary as key/value text.</summary>
    internal static Dictionary<string, string> ReadDictionary(string fileName)
    {
        XDocument document = XDocument.Load(Path.Combine(StylesDirectory, fileName));

        return document.Root!
            .Elements()
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element.Value.Trim(),
                StringComparer.Ordinal);
    }

    /// <summary>Every XAML file that ships inside the app project.</summary>
    internal static IReadOnlyList<string> AppXamlFiles() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot, "CafeMaestro"), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    /// <summary>Every XAML and C# source file that ships inside the app project.</summary>
    internal static IReadOnlyList<(string Path, string RelativePath)> AppSourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "CafeMaestro"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (path, Path.GetRelativePath(RepositoryRoot, path)))
            .ToList();

    /// <summary>Every <c>x:Key</c> declared anywhere in the app's XAML.</summary>
    internal static HashSet<string> DeclaredResourceKeys()
    {
        HashSet<string> keys = new(StringComparer.Ordinal);

        foreach (string file in AppXamlFiles())
        {
            foreach (Match match in KeyDeclarationPattern.Matches(File.ReadAllText(file)))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    /// <summary>
    /// Resource keys reachable from the merge graph rooted at <c>App.xaml</c>.
    /// Unlike <see cref="DeclaredResourceKeys"/>, this does not let an unrelated or
    /// unmerged dictionary make a missing application resource look valid.
    /// </summary>
    internal static HashSet<string> AppMergedResourceKeys()
    {
        string appPath = Path.Combine(RepositoryRoot, "CafeMaestro", "App.xaml");
        HashSet<string> keys = new(StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);

        AddReachableKeys(appPath, keys, visited);
        return keys;
    }

    /// <summary>Every <c>StaticResource</c>/<c>DynamicResource</c> reference, keyed by the file that makes it.</summary>
    internal static IEnumerable<(string File, string Key)> ResourceReferences()
    {
        foreach (string file in AppXamlFiles())
        {
            string relativePath = Path.GetRelativePath(RepositoryRoot, file);

            foreach (Match match in ResourceReferencePattern.Matches(File.ReadAllText(file)))
            {
                yield return (relativePath, match.Groups[1].Value);
            }
        }
    }

    private static void AddReachableKeys(string path, HashSet<string> keys, HashSet<string> visited)
    {
        string fullPath = Path.GetFullPath(path);
        if (!visited.Add(fullPath))
        {
            return;
        }

        XDocument document = XDocument.Load(fullPath);

        foreach (XElement element in document.Root!.DescendantsAndSelf())
        {
            XAttribute? key = element.Attribute(XamlNamespace + "Key");
            if (key is not null)
            {
                keys.Add(key.Value);
            }

            XAttribute? source = element.Attribute("Source");
            if (source is null || string.IsNullOrWhiteSpace(source.Value))
            {
                continue;
            }

            string sourcePath = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(fullPath)!, source.Value.Replace('/', Path.DirectorySeparatorChar)));
            AddReachableKeys(sourcePath, keys, visited);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CafeMaestro.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the CafeMaestro repository root.");
    }
}
