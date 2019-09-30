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
		/// <summary>
		/// Créé le repertoire en paramètre en créant tous les sous-répertoires si besoin
		/// </summary>
		/// <param name="baseDirectory">Répertoire de base</param>
		/// <param name="newPath">chemin à créer</param>
		public static void CreateDirectories( this DirectoryInfo baseDirectory, string newPath = null )
		{
			string path = newPath!= null ? Path.Combine(baseDirectory.FullName, newPath) : baseDirectory.FullName;
			CreateDirectories(path);
		}

		/// <summary>
		/// Créé le repertoire en paramètre en créant tous les sous-répertoires si besoin
		/// </summary>
		/// <param name="fullPath">Chemin à créer</param>
		public static void CreateDirectories( string fullPath )
		{
			if (!Directory.Exists(fullPath)) {
				CreateDirectories(Path.GetDirectoryName(fullPath));
				Directory.CreateDirectory(fullPath);
			}
		}

		/// <summary>
		/// Enumère les répertoires
		/// </summary>
		/// <param name="path">Chemin à énumérer</param>
		/// <returns></returns>
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

		/// <summary>
		/// Enumère les fichiers
		/// </summary>
		/// <param name="path">Chemin à énumérer</param>
		/// <returns></returns>
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
