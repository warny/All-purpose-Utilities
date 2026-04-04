using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

using Utils.Parser.VisualStudio.Worker;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Provides classification taggers for text views in the out-of-process VisualStudio.Extensibility model.
/// </summary>
[VisualStudioContribution]
public sealed class OutOfProcSyntaxColorizationTaggerProvider : ExtensionPart, ITextViewTaggerProvider<ClassificationTag>, ITextViewExtension
{
    // One worker process shared across all tagger instances to amortize startup cost.
    // TryCreate returns null when the worker executable has not been deployed yet.
    private static readonly Lazy<PluginWorkerProcess?> sharedWorker =
        new(PluginWorkerProcess.TryCreate, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the activation scope for this text view extension.
    /// </summary>
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo =
        [
            DocumentFilter.FromDocumentType(DocumentType.KnownValues.Text),
        ],
    };

    /// <summary>
    /// Creates a new tagger for the requested text view.
    /// </summary>
    /// <param name="textView">The target text view.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created tagger instance.</returns>
    public Task<TextViewTagger<ClassificationTag>> CreateTaggerAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        return Task.FromResult<TextViewTagger<ClassificationTag>>(
            new OutOfProcSyntaxColorizationTagger(textView, sharedWorker.Value));
    }
}
