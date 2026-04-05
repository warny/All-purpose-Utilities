using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Utils.Parser.Runtime;
using Utils.Parser.VisualStudio.Worker;

using Microsoft.VisualStudio.Extensibility.Editor;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Produces classification tags from descriptor-driven syntax colorization profiles.
/// Built-in profiles (own assembly + descriptor files) are resolved in-process.
/// User plugin assemblies are classified out-of-process via <see cref="PluginWorkerProcess"/>.
/// </summary>
public sealed class OutOfProcSyntaxColorizationTagger : TextViewTagger<ClassificationTag>
{
    private static readonly Regex TokenRegex = new("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    private const int MaxParentDirectoryHops = 12;

    // Trusted assemblies whose ISyntaxColorisation implementations run in-process.
    // Only the extension's own DLL and its direct library dependency are included.
    private static readonly Assembly[] TrustedAssemblies =
    [
        typeof(OutOfProcSyntaxColorizationTagger).Assembly,  // Utils.Parser.VisualStudio.dll
        typeof(ISyntaxColorisation).Assembly,                 // Utils.Parser.dll (G4SyntaxColorisation, etc.)
    ];

    private readonly ITextViewSnapshot textView;
    private readonly VisualStudioSyntaxColorisationRegistry registry;
    private readonly VisualStudioSyntaxColorisationExtension extension;
    private readonly PluginWorkerProcess? pluginWorker;

    // Cache for in-process descriptor profiles. Invalidated when the set of .syntaxcolor
    // files on disk changes (different paths or different last-write timestamps).
    private IReadOnlyList<ISyntaxColorisation>? profileCache;
    private string[] cachedDescriptorPaths = Array.Empty<string>();
    private DateTime[] cachedDescriptorTimestamps = Array.Empty<DateTime>();

    /// <summary>
    /// Initializes a new instance of the <see cref="OutOfProcSyntaxColorizationTagger"/> class.
    /// </summary>
    /// <param name="textView">The text view associated with this tagger.</param>
    /// <param name="pluginWorker">
    /// Shared worker process for out-of-process plugin classification.
    /// May be <see langword="null"/> when the worker executable is not deployed.
    /// </param>
    internal OutOfProcSyntaxColorizationTagger(ITextViewSnapshot textView, PluginWorkerProcess? pluginWorker)
    {
        this.textView = textView;
        this.pluginWorker = pluginWorker;
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
    protected override async Task RequestTagsAsync(
        NormalizedTextRangeCollection requestedRanges,
        bool recalculateAll,
        CancellationToken cancellationToken)
    {
        string? filePath = textView.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        string fileExtension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            return;
        }

        // In-process: built-in profiles from trusted assemblies + descriptor files.
        // No user code executes in this process.
        IReadOnlyList<ISyntaxColorisation> inProcProfiles = LoadInProcessProfiles(filePath, fileExtension);

        // Collect all unique tokens across all requested ranges in a single pass
        // so we can send them to the worker in one batch request.
        var rangeData = new List<(TextRange Range, MatchCollection Matches)>(requestedRanges.Count);
        var uniqueTokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (TextRange range in requestedRanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = range.CopyToString();
            MatchCollection matches = TokenRegex.Matches(text);
            rangeData.Add((range, matches));
            foreach (Match m in matches)
            {
                uniqueTokens.Add(m.Value);
            }
        }

        // Out-of-process: classify tokens using user plugin assemblies running in the worker.
        // A crash or hang in a plugin only affects the worker process, not this extension.
        IReadOnlyDictionary<string, string?> pluginClassifications =
            await GetPluginClassificationsAsync(uniqueTokens, fileExtension, cancellationToken);

        // Build tags by merging worker results (higher priority) with in-process profiles.
        var tags = new List<TaggedTrackingTextRange<ClassificationTag>>();
        var updatedRanges = new List<TextRange>(rangeData.Count);

        foreach ((TextRange range, MatchCollection matches) in rangeData)
        {
            updatedRanges.Add(range);

            foreach (Match match in matches)
            {
                string token = match.Value;
                string? classificationName = ResolveClassification(token, pluginClassifications, inProcProfiles);
                if (string.IsNullOrWhiteSpace(classificationName))
                {
                    continue;
                }

                ClassificationType classificationType = MapClassification(classificationName);
                var tokenRange = new TextRange(range.Document, range.Start.Offset + match.Index, match.Length);
                tags.Add(new TaggedTrackingTextRange<ClassificationTag>(
                    tokenRange, TextRangeTrackingMode.ExtendNone, new ClassificationTag(classificationType)));
            }
        }

        if (updatedRanges.Count == 0)
        {
            return;
        }

        await UpdateTagsAsync(updatedRanges, tags, cancellationToken);
    }

    /// <summary>
    /// Sends all unique tokens to the worker process for batch out-of-process classification.
    /// Returns an empty dictionary when the worker is unavailable or no plugin assemblies are present.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string?>> GetPluginClassificationsAsync(
        IEnumerable<string> tokens,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        if (pluginWorker is null)
        {
            return new Dictionary<string, string?>();
        }

        string[] assemblyPaths = PluginAssemblyVerifier.Filter(PluginDirectoryLocator.GetPluginAssemblyPaths());
        if (assemblyPaths.Length == 0)
        {
            return new Dictionary<string, string?>();
        }

        string[] tokenArray = tokens.ToArray();
        if (tokenArray.Length == 0)
        {
            return new Dictionary<string, string?>();
        }

        return await pluginWorker.ClassifyAsync(assemblyPaths, fileExtension, tokenArray, cancellationToken);
    }

    /// <summary>
    /// Resolves classification for a single token.
    /// Plugin (worker) results take priority over in-process profiles.
    /// </summary>
    private static string? ResolveClassification(
        string token,
        IReadOnlyDictionary<string, string?> pluginClassifications,
        IEnumerable<ISyntaxColorisation> inProcProfiles)
    {
        if (pluginClassifications.TryGetValue(token, out string? pluginResult) &&
            !string.IsNullOrWhiteSpace(pluginResult))
        {
            return pluginResult;
        }

        return ResolveFromProfiles(token, inProcProfiles);
    }

    /// <summary>
    /// Resolves classification from in-process profiles, trying direct, uppercase,
    /// and title-case token variants.
    /// </summary>
    private static string? ResolveFromProfiles(string token, IEnumerable<ISyntaxColorisation> profiles)
    {
        foreach (ISyntaxColorisation profile in profiles)
        {
            string? direct = profile.GetClassification(token);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            string? upper = profile.GetClassification(token.ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(upper))
            {
                return upper;
            }

            string? title = profile.GetClassification(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(token));
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads profiles from the extension's trusted assemblies and from descriptor files only.
    /// User-provided assemblies are NOT loaded here; they run in the worker process instead.
    /// Results are cached by (descriptor paths, last-write timestamps) and reused across
    /// tag requests as long as no descriptor file is added, removed, or modified on disk.
    /// </summary>
    private IReadOnlyList<ISyntaxColorisation> LoadInProcessProfiles(string filePath, string fileExtension)
    {
        string[] descriptorPaths = EnumerateDescriptorFiles(filePath)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        DateTime[] timestamps = GetDescriptorTimestamps(descriptorPaths);

        if (profileCache is not null &&
            descriptorPaths.SequenceEqual(cachedDescriptorPaths, StringComparer.OrdinalIgnoreCase) &&
            timestamps.SequenceEqual(cachedDescriptorTimestamps))
        {
            return profileCache;
        }

        IReadOnlyList<ISyntaxColorisation> allProfiles;
        try
        {
            allProfiles = registry.LoadProfiles(TrustedAssemblies, descriptorPaths);
        }
        catch
        {
            return [];
        }

        IReadOnlyList<ISyntaxColorisation> filtered =
            extension.GetSecondaryProfilesForFileExtension(allProfiles, fileExtension, []);

        profileCache = filtered;
        cachedDescriptorPaths = descriptorPaths;
        cachedDescriptorTimestamps = timestamps;

        return filtered;
    }

    /// <summary>
    /// Returns the UTC last-write timestamps of each descriptor file in
    /// <paramref name="paths"/>, in the same order.
    /// Files that cannot be read are given <see cref="DateTime.MinValue"/> so a
    /// subsequent read attempt always causes a cache miss.
    /// </summary>
    /// <param name="paths">Sorted descriptor file paths.</param>
    /// <returns>Array of UTC last-write timestamps, one per path.</returns>
    private static DateTime[] GetDescriptorTimestamps(string[] paths)
    {
        var timestamps = new DateTime[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            try
            {
                timestamps[i] = File.GetLastWriteTimeUtc(paths[i]);
            }
            catch
            {
                timestamps[i] = DateTime.MinValue;
            }
        }

        return timestamps;
    }

    /// <summary>
    /// Maps project classification names to known Visual Studio classification values.
    /// </summary>
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
    /// Enumerates descriptor files from the current directory to the filesystem root.
    /// </summary>
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

}
