using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Utils.Parser.Runtime;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Produces classification tags from descriptor-driven syntax colorization profiles.
/// </summary>
public sealed class OutOfProcSyntaxColorizationTagger : TextViewTagger<ClassificationTag>
{
    private static readonly Regex TokenRegex = new("@[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*|[:|]", RegexOptions.Compiled);
    private const int MaxParentDirectoryHops = 12;
    private const int MaxProjectDirectories = 256;
    private const int MaxAssemblyFiles = 4096;

    /// <summary>How long a loaded profile set is reused before being refreshed from disk.</summary>
    private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromMinutes(2);

    private readonly ITextViewSnapshot textView;
    private readonly VisualStudioSyntaxColorisationRegistry registry;
    private readonly VisualStudioSyntaxColorisationExtension extension;
    private IReadOnlyList<ISyntaxColorisation>? cachedProfiles;
    private DateTime cachedProfilesTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutOfProcSyntaxColorizationTagger"/> class.
    /// </summary>
    /// <param name="textView">The text view associated with this tagger.</param>
    public OutOfProcSyntaxColorizationTagger(ITextViewSnapshot textView)
    {
        this.textView = textView;
        registry = new VisualStudioSyntaxColorisationRegistry();
        extension = new VisualStudioSyntaxColorisationExtension();
    }

    /// <summary>
    /// Generates classification tags for requested ranges.
    /// </summary>
    /// <param name="requestedRanges">Ranges for which tags are requested.</param>
    /// <param name="recalculateAll">Whether previous tags should be invalidated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completed once tags are published.</returns>
    protected override Task RequestTagsAsync(NormalizedTextRangeCollection requestedRanges, bool recalculateAll, CancellationToken cancellationToken)
    {
        string? filePath = textView.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.CompletedTask;
        }

        string extensionName = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extensionName))
        {
            return Task.CompletedTask;
        }

        IReadOnlyList<ISyntaxColorisation> profiles = LoadProfiles(filePath, extensionName);
        if (profiles.Count == 0)
        {
            return Task.CompletedTask;
        }

        var tags = new List<TaggedTrackingTextRange<ClassificationTag>>();
        var updatedRanges = new List<TextRange>();

        foreach (TextRange range in requestedRanges)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string text = range.CopyToString();
            MatchCollection matches = TokenRegex.Matches(text);
            updatedRanges.Add(range);
            if (matches.Count == 0)
            {
                continue;
            }
            foreach (Match match in matches)
            {
                string token = match.Value;
                string? classificationName = ResolveClassification(token, profiles);
                if (string.IsNullOrWhiteSpace(classificationName))
                {
                    continue;
                }

                ClassificationType classificationType = MapClassification(classificationName);
                var tokenRange = new TextRange(range.Document, range.Start.Offset + match.Index, match.Length);
                tags.Add(new TaggedTrackingTextRange<ClassificationTag>(tokenRange, TextRangeTrackingMode.ExtendNone, new ClassificationTag(classificationType)));
            }
        }

        if (updatedRanges.Count == 0)
        {
            return Task.CompletedTask;
        }

        return UpdateTagsAsync(updatedRanges, tags, cancellationToken);
    }

    /// <summary>
    /// Maps project classification names to known Visual Studio classification values.
    /// </summary>
    /// <param name="classificationName">Classification name provided by a profile.</param>
    /// <returns>A Visual Studio classification type.</returns>
    private static ClassificationType MapClassification(string classificationName)
    {
        if (classificationName.Equals(Utils.Parser.Runtime.VisualStudioClassificationNames.Keyword, StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationType.KnownValues.Keyword;
        }

        if (classificationName.Equals(Utils.Parser.Runtime.VisualStudioClassificationNames.Number, StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationType.KnownValues.Number;
        }

        if (classificationName.Equals(Utils.Parser.Runtime.VisualStudioClassificationNames.String, StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationType.KnownValues.String;
        }

        if (classificationName.Equals(Utils.Parser.Runtime.VisualStudioClassificationNames.Operator, StringComparison.OrdinalIgnoreCase))
        {
            return ClassificationType.KnownValues.Operator;
        }

        return ClassificationType.KnownValues.Text;
    }

    /// <summary>
    /// Resolves the best classification for a token.
    /// </summary>
    /// <param name="token">Token text.</param>
    /// <param name="profiles">Profiles that apply to the current file.</param>
    /// <returns>Resolved classification name, or <see langword="null"/>.</returns>
    private static string? ResolveClassification(string token, IEnumerable<ISyntaxColorisation> profiles)
    {
        foreach (ISyntaxColorisation profile in profiles)
        {
            string? direct = profile.GetClassification(token);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            string upper = token.ToUpperInvariant();
            string? upperValue = profile.GetClassification(upper);
            if (!string.IsNullOrWhiteSpace(upperValue))
            {
                return upperValue;
            }

            string title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(token);
            string? titleValue = profile.GetClassification(title);
            if (!string.IsNullOrWhiteSpace(titleValue))
            {
                return titleValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads descriptor and runtime profiles for the current file.
    /// </summary>
    /// <param name="filePath">Current file path.</param>
    /// <param name="fileExtension">Current file extension.</param>
    /// <returns>Profiles applicable to this file extension.</returns>
    private IReadOnlyList<ISyntaxColorisation> LoadProfiles(string filePath, string fileExtension)
    {
        if (cachedProfiles != null && DateTime.UtcNow - cachedProfilesTimestamp < ProfileCacheTtl)
        {
            return cachedProfiles;
        }

        IReadOnlyList<ISyntaxColorisation> allProfiles;
        try
        {
            allProfiles = registry.LoadProfiles(
                AppDomain.CurrentDomain.GetAssemblies(),
                EnumerateDescriptorFiles(filePath),
                EnumerateProjectAssemblyFiles(filePath));
        }
        catch
        {
            cachedProfiles = Array.Empty<ISyntaxColorisation>();
            cachedProfilesTimestamp = DateTime.UtcNow;
            return cachedProfiles;
        }

        cachedProfiles = extension.GetSecondaryProfilesForFileExtension(allProfiles, fileExtension, Array.Empty<string>());
        cachedProfilesTimestamp = DateTime.UtcNow;
        return cachedProfiles;
    }

    /// <summary>
    /// Enumerates descriptor files from the current directory to the filesystem root.
    /// </summary>
    /// <param name="filePath">Edited file path.</param>
    /// <returns>Descriptor file paths.</returns>
    private static IEnumerable<string> EnumerateDescriptorFiles(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Array.Empty<string>();
        }

        var descriptors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DirectoryInfo? current = new(directory);
        int hops = 0;

        while (current != null && hops < MaxParentDirectoryHops)
        {
            FileInfo[] files;
            try
            {
                files = current.GetFiles("*.syntaxcolor", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                files = Array.Empty<FileInfo>();
            }

            foreach (FileInfo file in files)
            {
                descriptors.Add(file.FullName);
            }

            current = current.Parent;
            hops++;
        }

        return descriptors;
    }

    /// <summary>
    /// Enumerates assembly files produced by projects participating in the current solution context.
    /// </summary>
    /// <param name="filePath">Edited file path.</param>
    /// <returns>Assembly file paths under project output directories.</returns>
    private static IEnumerable<string> EnumerateProjectAssemblyFiles(string filePath)
    {
        string? projectDirectory = FindOwningProjectDirectory(filePath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return Array.Empty<string>();
        }

        string searchRoot = FindSolutionDirectory(projectDirectory) ?? projectDirectory;
        IReadOnlyList<string> projectDirectories = EnumerateProjectDirectories(searchRoot);

        if (projectDirectories.Count == 0)
        {
            return Array.Empty<string>();
        }

        var assemblyFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in projectDirectories)
        {
            AddAssembliesFromOutputFolder(assemblyFiles, Path.Combine(directory, "bin"));
            AddAssembliesFromOutputFolder(assemblyFiles, Path.Combine(directory, "obj"));
            if (assemblyFiles.Count >= MaxAssemblyFiles)
            {
                break;
            }
        }

        return assemblyFiles.Take(MaxAssemblyFiles).ToArray();
    }

    /// <summary>
    /// Enumerates project directories containing a <c>.csproj</c> file.
    /// </summary>
    /// <param name="searchRoot">Root directory to inspect.</param>
    /// <returns>Project directories.</returns>
    private static IReadOnlyList<string> EnumerateProjectDirectories(string searchRoot)
    {
        try
        {
            return Directory
                .EnumerateFiles(searchRoot, "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => !path!.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path!.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxProjectDirectories)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Adds assembly files from an output folder when it exists.
    /// </summary>
    /// <param name="assemblyFiles">Destination assembly set.</param>
    /// <param name="outputDirectory">Output folder path.</param>
    private static void AddAssembliesFromOutputFolder(HashSet<string> assemblyFiles, string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        try
        {
            foreach (string assemblyFile in Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.AllDirectories))
            {
                assemblyFiles.Add(assemblyFile);
            }
        }
        catch
        {
            // Ignore inaccessible project output folders.
        }
    }

    /// <summary>
    /// Finds the nearest directory containing a project file for the edited file.
    /// </summary>
    /// <param name="filePath">Edited file path.</param>
    /// <returns>Project directory path, or <see langword="null"/> when none is found.</returns>
    private static string? FindOwningProjectDirectory(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        DirectoryInfo? current = new(directory);
        while (current != null)
        {
            try
            {
                if (current.EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly).Any())
                {
                    return current.FullName;
                }
            }
            catch
            {
                return null;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest solution directory containing a <c>.sln</c> file.
    /// </summary>
    /// <param name="projectDirectory">Current project directory.</param>
    /// <returns>Solution directory path, or <see langword="null"/> when none is found.</returns>
    private static string? FindSolutionDirectory(string projectDirectory)
    {
        DirectoryInfo? current = new(projectDirectory);
        while (current != null)
        {
            try
            {
                if (current.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).Any())
                {
                    return current.FullName;
                }
            }
            catch
            {
                return null;
            }

            current = current.Parent;
        }

        return null;
    }
}
