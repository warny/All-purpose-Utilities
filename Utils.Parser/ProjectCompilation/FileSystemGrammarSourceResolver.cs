using System;
using System.IO;
using System.Linq;

namespace Utils.Parser.ProjectCompilation;

/// <summary>
/// Resolves grammar sources from files rooted at a directory.
/// </summary>
public sealed class FileSystemGrammarSourceResolver : IGrammarSourceResolver
{
    private readonly string _rootDirectory;

    /// <summary>
    /// Initialises a resolver bound to a root directory.
    /// </summary>
    /// <param name="rootDirectory">Base directory used to locate <c>.g4</c> files.</param>
    public FileSystemGrammarSourceResolver(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <inheritdoc />
    public bool TryResolve(string grammarName, out GrammarSource source)
    {
        var candidateFileName = EnsureGrammarExtension(grammarName);
        var directPath = GetCandidatePath(candidateFileName);
        if (TryLoadSource(directPath, out source))
        {
            return true;
        }

        var shortName = Path.GetFileName(candidateFileName);
        var searchResult = Directory
            .EnumerateFiles(_rootDirectory, shortName, SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (searchResult != null && TryLoadSource(searchResult, out source))
        {
            return true;
        }

        source = null!;
        return false;
    }

    /// <summary>
    /// Ensures that the candidate grammar file name has the <c>.g4</c> extension.
    /// </summary>
    /// <param name="grammarName">Input grammar name.</param>
    /// <returns>File name with extension.</returns>
    private static string EnsureGrammarExtension(string grammarName)
    {
        return Path.HasExtension(grammarName)
            ? grammarName
            : $"{grammarName}.g4";
    }

    /// <summary>
    /// Computes the full candidate path from a file name.
    /// </summary>
    /// <param name="candidateFileName">Relative or absolute file name.</param>
    /// <returns>Absolute file path.</returns>
    private string GetCandidatePath(string candidateFileName)
    {
        if (Path.IsPathRooted(candidateFileName))
        {
            return Path.GetFullPath(candidateFileName);
        }

        return Path.GetFullPath(Path.Combine(_rootDirectory, candidateFileName));
    }

    /// <summary>
    /// Tries to load a grammar source from disk.
    /// </summary>
    /// <param name="path">Candidate absolute file path.</param>
    /// <param name="source">Loaded source when the file exists.</param>
    /// <returns><c>true</c> when the file is loaded.</returns>
    private static bool TryLoadSource(string path, out GrammarSource source)
    {
        if (!File.Exists(path))
        {
            source = null!;
            return false;
        }

        var text = ReadAllText(path);
        source = new GrammarSource(Path.GetFileNameWithoutExtension(path), path, text);
        return true;
    }

    /// <summary>
    /// Reads all text from a grammar file.
    /// </summary>
    /// <param name="path">File path to read.</param>
    /// <returns>File content.</returns>
    private static string ReadAllText(string path) => File.ReadAllText(path);
}
