using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Mutable collection of parser diagnostics.
/// </summary>
public sealed class DiagnosticBag : IReadOnlyCollection<ParserDiagnostic>
{
    private readonly List<ParserDiagnostic> _items = new();

    /// <summary>
    /// Adds a diagnostic instance to the bag.
    /// </summary>
    /// <param name="diagnostic">Diagnostic to add.</param>
    public void Add(ParserDiagnostic diagnostic)
    {
        if (diagnostic is null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        _items.Add(diagnostic);
    }

    /// <summary>
    /// Creates and adds a diagnostic from a descriptor.
    /// </summary>
    /// <param name="descriptor">Diagnostic descriptor.</param>
    /// <param name="arguments">Message format arguments.</param>
    /// <returns>The added diagnostic.</returns>
    public ParserDiagnostic Add(
        ParserDiagnosticDescriptor descriptor,
        params object?[] arguments)
    {
        return AddWithContext(
            descriptor,
            spanStart: null,
            spanLength: null,
            ruleName: null,
            exception: null,
            arguments: arguments);
    }

    /// <summary>
    /// Creates and adds a diagnostic from a descriptor with contextual information.
    /// </summary>
    /// <param name="descriptor">Diagnostic descriptor.</param>
    /// <param name="spanStart">Optional source span start.</param>
    /// <param name="spanLength">Optional source span length.</param>
    /// <param name="ruleName">Optional rule context.</param>
    /// <param name="exception">Optional related exception.</param>
    /// <param name="arguments">Message format arguments.</param>
    /// <returns>The added diagnostic.</returns>
    public ParserDiagnostic AddWithContext(
        ParserDiagnosticDescriptor descriptor,
        int? spanStart,
        int? spanLength,
        string? ruleName,
        Exception? exception,
        params object?[] arguments)
    {
        if (descriptor is null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        var diagnostic = new ParserDiagnostic(
            descriptor,
            descriptor.FormatMessage(arguments),
            spanStart,
            spanLength,
            ruleName,
            exception);

        _items.Add(diagnostic);
        return diagnostic;
    }

    /// <summary>
    /// Adds all diagnostics from another sequence.
    /// </summary>
    /// <param name="diagnostics">Diagnostics to append.</param>
    public void AddRange(IEnumerable<ParserDiagnostic> diagnostics)
    {
        if (diagnostics is null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        _items.AddRange(diagnostics);
    }

    /// <summary>
    /// Gets all diagnostics whose severity is at least as important as <paramref name="severity"/>.
    /// </summary>
    /// <param name="severity">Minimum severity threshold.</param>
    /// <returns>Filtered diagnostics.</returns>
    public IReadOnlyList<ParserDiagnostic> GetAtLeast(DiagnosticSeverity severity)
    {
        return _items.Where(item => item.Severity <= severity).ToList();
    }

    /// <summary>
    /// Gets a value indicating whether at least one error diagnostic is present.
    /// </summary>
    public bool HasErrors => _items.Any(item => item.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Gets the total diagnostic count.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Returns a read-only snapshot of the diagnostics.
    /// </summary>
    /// <returns>Snapshot list.</returns>
    public IReadOnlyList<ParserDiagnostic> ToList() => _items.ToList();

    /// <inheritdoc />
    public IEnumerator<ParserDiagnostic> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
