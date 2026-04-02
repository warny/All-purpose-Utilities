using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    private static readonly Regex TokenRegex = new("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    private readonly ITextViewSnapshot textView;
    private readonly VisualStudioSyntaxColorisationRegistry registry;
    private readonly VisualStudioSyntaxColorisationExtension extension;

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
            if (matches.Count == 0)
            {
                continue;
            }

            updatedRanges.Add(range);
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
        IReadOnlyList<ISyntaxColorisation> allProfiles;
        try
        {
            allProfiles = registry.LoadProfiles(AppDomain.CurrentDomain.GetAssemblies(), EnumerateDescriptorFiles(filePath));
        }
        catch
        {
            return Array.Empty<ISyntaxColorisation>();
        }

        return extension.GetSecondaryProfilesForFileExtension(allProfiles, fileExtension, Array.Empty<string>());
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

        while (current != null)
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
        }

        return descriptors;
    }
}
