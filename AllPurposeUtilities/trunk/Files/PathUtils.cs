using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Files
{
	public static class PathUtils
	{
		public static void CreateDirectories( this DirectoryInfo baseDirectory, string newPath = null )
		{
			string path = newPath!= null ? Path.Combine(baseDirectory.FullName, newPath) : baseDirectory.FullName;
			CreateDirectories(path);
		}

		public static void CreateDirectories( string fullPath )
		{
			if (!Directory.Exists(fullPath)) {
				CreateDirectories(Path.GetDirectoryName(fullPath));
				Directory.CreateDirectory(fullPath);
			}
		}

		public static IEnumerable<string> EnumerateDirectories( string path )
		{
			var parent = Path.GetDirectoryName(path);
			var current = Path.GetFileName(path);
			if (Path.GetPathRoot(path) == path) {
				yield return path;
			} else if (parent == "") {
				yield return Directory.GetCurrentDirectory();
			} else {
				foreach (var directory in EnumerateDirectories(parent)) {
					IEnumerable<string> directories;
					try {
						directories = Directory.EnumerateDirectories(directory, current);
					} catch {
						directories = new string[] { };
					}
					foreach (var subdirectory in directories) {
						yield return subdirectory;
					}
				}
			}
		}

		public static IEnumerable<string> EnumerateFiles( string path )
		{
			var parent = Path.GetDirectoryName(path);
			var current = Path.GetFileName(path);
			if (Path.GetPathRoot(path) == parent) {
				foreach (var file in Directory.EnumerateFiles(parent, current)) {
					yield return file;
				}
			} else if (parent == "") {
				foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), current)) {
					yield return file;
				}
			} else {
				foreach (var directory in EnumerateDirectories(parent)) {
					foreach (var subdirectory in Directory.EnumerateFiles(directory, current)) {
						yield return subdirectory;
					}
				}
			}
		}

	}
}
