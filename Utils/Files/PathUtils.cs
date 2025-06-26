using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Utils.Files
{
	public static class PathUtils
	{
		/// <summary>
		/// Splits a full path into an absolute root (drive or UNC) and an ordered list of sub-path segments.
		/// For example, "C:\Root\Sub*\Another*\File??.txt" might split into:
		///   Root: "C:\"
		///   Segments: ["Root", "Sub*", "Another*", "File??.txt"]
		/// </summary>
		private static (string root, IReadOnlyList<string> segments) SplitPathIntoSegments(string path)
		{
			// 1) Convert to absolute path
			string fullPath = Path.GetFullPath(path);

			// 2) Identify the root (e.g. "C:\") or ("\\Server\Share\")
			string root = Path.GetPathRoot(fullPath) ?? string.Empty;

			// 3) Get the substring after the root. For example, if fullPath = "C:\Root\Sub*\File.txt",
			//    root might be "C:\", and remainder = "Root\Sub*\File.txt"
			string remainder = fullPath[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			// 4) Split on directory separators to get each segment
                        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                        var segments = remainder.Split(separators, StringSplitOptions.RemoveEmptyEntries);

			return (root, segments);
		}

		/// <summary>
		/// Expands a set of directories by matching a single path segment (which may be a wildcard).
		/// In other words, for each directory in <paramref name="currentDirectories"/>, we call
		/// Directory.EnumerateDirectories(dir, segmentPattern). 
		/// </summary>
		/// <param name="currentDirectories">The directories from the previous step.</param>
		/// <param name="segmentPattern">A single path segment, possibly with wildcards (e.g. "Sub*").</param>
		/// <returns>All matching subdirectories under each of the <paramref name="currentDirectories"/>.</returns>
		private static IEnumerable<string> ExpandDirectories(IEnumerable<string> currentDirectories, string segmentPattern)
		{
			foreach (var dir in currentDirectories)
			{
				if (!Directory.Exists(dir))
					continue; // skip invalid or inaccessible directories

				IEnumerable<string> matches;
				try
				{
					matches = Directory.EnumerateDirectories(dir, segmentPattern);
				}
				catch
				{
					// skip inaccessible directories
					continue;
				}

				foreach (var subdirectory in matches)
				{
					yield return subdirectory;
				}
			}
		}

		/// <summary>
		/// Expands the entire path in two phases:
		///  1) For all but the last segment: enumerate directories only, using <see cref="ExpandDirectories"/>.
		///  2) For the last segment: call the user-supplied <paramref name="finalEnumerator"/> 
		///     (e.g. <see cref="Directory.EnumerateFiles"/> or <see cref="Directory.EnumerateFileSystemEntries"/>),
		///     against each of the directories collected so far.
		///  
		/// That way, intermediate segments (which may have wildcards) are managed as directories,
		/// but the final segment is enumerated according to your needs (files, directories, or both).
		/// </summary>
		/// <param name="path">A path that may have multiple wildcard segments, e.g. "C:\Root\Sub*\Another*\File??.txt"</param>
		/// <param name="finalEnumerator">
		/// A function like <see cref="Directory.EnumerateFiles"/>, <see cref="Directory.EnumerateDirectories"/>, 
		/// or <see cref="Directory.EnumerateFileSystemEntries"/> that is used only on the final segment.
		/// </param>
		/// <returns>All matching paths returned by the final enumeration step.</returns>
		private static IEnumerable<string> EnumeratePath(
			string path,
			Func<string, string, IEnumerable<string>> finalEnumerator)
		{
			// 1) Split the absolute path into "root" and individual segments
			var (root, segments) = SplitPathIntoSegments(path);

			// Edge case: If the path is exactly a root like "C:\" with no other segments
			if (segments.Count == 0)
			{
				// Typically just yield the root itself if it is a valid directory
				// (Though "Directory.Exists" on root often returns true for a valid drive)
				if (Directory.Exists(root))
					yield return root;
				yield break;
			}

			// 2) We handle all but the final segment as "directory expansions"
			//    Start with a single list containing the root, e.g. "C:\"
			IEnumerable<string> current = [root];

			// Expand each segment except the last one
			for (int i = 0; i < segments.Count - 1; i++)
			{
				string segment = segments[i];
				current = ExpandDirectories(current, segment).ToList();
				// ToList() to materialize before next iteration
			}

			// 3) The final segment is enumerated via the user-supplied delegate
			string finalSegment = segments[^1];
			foreach (var dir in current)
			{
				if (!Directory.Exists(dir))
					continue;

				IEnumerable<string> matches;
				try
				{
					// e.g. Directory.EnumerateFiles(dir, finalSegment)
					//      or Directory.EnumerateDirectories(dir, finalSegment)
					//      or Directory.EnumerateFileSystemEntries(dir, finalSegment)
					matches = finalEnumerator(dir, finalSegment);
				}
				catch
				{
					continue;
				}

				foreach (var pathResult in matches)
				{
					yield return pathResult;
				}
			}
		}

		// ---------------------------------------------------------------------
		//                 Public methods for final enumeration
		// ---------------------------------------------------------------------

		/// <summary>
		/// Enumerates only directories for the final segment, while managing wildcards 
		/// in intermediate segments as directories. The final call is 
		/// <see cref="Directory.EnumerateDirectories"/> on each matched parent.
		/// </summary>
		/// <param name="path">
		/// A path that may contain multiple wildcard segments (e.g. "C:\Root\Sub*\Another*\EndDir??").
		/// </param>
		/// <returns>
		/// Fully qualified directory paths matching the final wildcard, 
		/// after expanding any intermediate wildcard directories.
		/// </returns>
		public static IEnumerable<string> EnumerateDirectories(string path)
		{
			return EnumeratePath(path, Directory.EnumerateDirectories);
		}

		/// <summary>
		/// Enumerates only files for the final segment, while managing wildcards 
		/// in intermediate segments as directories. The final call is 
		/// <see cref="Directory.EnumerateFiles"/> on each matched parent.
		/// </summary>
		/// <param name="path">
		/// A path that may contain multiple wildcard segments (e.g. "C:\Root\Sub*\Another*\File??.txt").
		/// </param>
		/// <returns>
		/// Fully qualified file paths matching the final wildcard, 
		/// after expanding any intermediate wildcard directories.
		/// </returns>
		public static IEnumerable<string> EnumerateFiles(string path)
		{
			return EnumeratePath(path, Directory.EnumerateFiles);
		}

		/// <summary>
		/// Enumerates both files and directories for the final segment, while managing 
		/// wildcards in intermediate segments as directories. The final call is 
		/// <see cref="Directory.EnumerateFileSystemEntries"/> on each matched parent.
		/// </summary>
		/// <param name="path">
		/// A path that may contain multiple wildcard segments (e.g. "C:\Root\Sub*\Another*\*.*").
		/// </param>
		/// <returns>
		/// Fully qualified paths (files or directories) matching the final wildcard, 
		/// after expanding any intermediate wildcard directories.
		/// </returns>
		public static IEnumerable<string> EnumerateFileSystemEntries(string path)
		{
			return EnumeratePath(path, Directory.EnumerateFileSystemEntries);
		}

		// ---------------------------------------------------------------------
		//          Optional DirectoryInfo Extension Methods
		// ---------------------------------------------------------------------

		/// <summary>
		/// Enumerates only directories (as <see cref="DirectoryInfo"/>) beneath 
		/// the specified <paramref name="baseDir"/>, allowing wildcards in 
		/// intermediate segments and in the final segment for directories.
		/// </summary>
		public static IEnumerable<DirectoryInfo> EnumerateDirectories(this DirectoryInfo baseDir, string path)
		{
			string combined = Path.Combine(baseDir.FullName, path);
			return EnumerateDirectories(combined).Select(p => new DirectoryInfo(p));
		}

		/// <summary>
		/// Enumerates only files (as <see cref="FileInfo"/>) beneath 
		/// the specified <paramref name="baseDir"/>, allowing wildcards in 
		/// intermediate segments and in the final segment for files.
		/// </summary>
		public static IEnumerable<FileInfo> EnumerateFiles(this DirectoryInfo baseDir, string path)
		{
			string combined = Path.Combine(baseDir.FullName, path);
			return EnumerateFiles(combined).Select(p => new FileInfo(p));
		}

		/// <summary>
		/// Enumerates both files and directories (as <see cref="FileSystemInfo"/>) 
		/// beneath the specified <paramref name="baseDir"/>, allowing wildcards in 
		/// intermediate segments and in the final segment for either directories or files.
		/// </summary>
		public static IEnumerable<FileSystemInfo> EnumerateFileSystemEntries(this DirectoryInfo baseDir, string path)
		{
			string combined = Path.Combine(baseDir.FullName, path);

			foreach (var itemPath in EnumerateFileSystemEntries(combined))
			{
				FileAttributes attr;
				try
				{
					attr = File.GetAttributes(itemPath);
				}
				catch
				{
					continue;
				}

				if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
					yield return new DirectoryInfo(itemPath);
				else
					yield return new FileInfo(itemPath);
			}
		}
	}
}
