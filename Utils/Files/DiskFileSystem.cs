using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.Files
{
	public class DiskFileSystem : IFileSystemProvider
	{

	}

	public class DiskDirectoryInfo : IDirectoryInfo
	{
		private readonly DirectoryInfo directoryInfo;

		public FileAttributes Attributes => directoryInfo.Attributes;
		public IDirectoryInfo Parent => new DiskDirectoryInfo(directoryInfo.Parent);
		public string Name => directoryInfo.Name;
		public string FullName => directoryInfo.FullName;
		public DateTime CreationTime => directoryInfo.CreationTime;
		public DateTime LastAccessTime => directoryInfo.LastAccessTime;
		public DateTime LastWriteTime => directoryInfo.LastWriteTime;

		public DiskDirectoryInfo(string path!!)
		{
			this.directoryInfo = new DirectoryInfo(path);
		}

		public DiskDirectoryInfo(DirectoryInfo directoryInfo!!)
		{
			this.directoryInfo = directoryInfo;
		}

		public void Create() => directoryInfo.Create();
		public DirectoryInfo CreateSubdirectory(string path) => directoryInfo.CreateSubdirectory(path);
		public void Delete(bool recursive) => directoryInfo.Delete(recursive);
		public void Delete() => directoryInfo.Delete();
		public IEnumerable<DirectoryInfo> EnumerateDirectories() => directoryInfo.EnumerateDirectories();
		public IEnumerable<DirectoryInfo> EnumerateDirectories(string searchPattern) => directoryInfo.EnumerateDirectories(searchPattern);
		public IEnumerable<DirectoryInfo> EnumerateDirectories(string searchPattern, EnumerationOptions enumerationOptions) => directoryInfo.EnumerateDirectories();
		public IEnumerable<DirectoryInfo> EnumerateDirectories(string searchPattern, SearchOption searchOption) => directoryInfo.EnumerateDirectories(searchPattern, searchOption);
		public IEnumerable<FileInfo> EnumerateFiles() => directoryInfo.EnumerateFiles();
		public IEnumerable<FileInfo> EnumerateFiles(string searchPattern) => directoryInfo.EnumerateFiles(searchPattern);
		public IEnumerable<FileInfo> EnumerateFiles(string searchPattern, EnumerationOptions enumerationOptions) => directoryInfo.EnumerateFiles(searchPattern, enumerationOptions);
		public IEnumerable<FileInfo> EnumerateFiles(string searchPattern, SearchOption searchOption) => directoryInfo.EnumerateFiles(searchPattern, searchOption);
		public IEnumerable<FileSystemInfo> EnumerateFileSystemInfos() => directoryInfo.EnumerateFileSystemInfos();
		public IEnumerable<FileSystemInfo> EnumerateFileSystemInfos(string searchPattern) => directoryInfo.EnumerateFiles(searchPattern);
		public IEnumerable<FileSystemInfo> EnumerateFileSystemInfos(string searchPattern, EnumerationOptions enumerationOptions) => directoryInfo.EnumerateFiles(searchPattern, enumerationOptions);
		public IEnumerable<FileSystemInfo> EnumerateFileSystemInfos(string searchPattern, SearchOption searchOption) => directoryInfo.EnumerateFiles(searchPattern, searchOption);

		public static explicit operator DirectoryInfo(DiskDirectoryInfo diskDirectoryInfo)=> diskDirectoryInfo.directoryInfo;
		public static implicit operator DiskDirectoryInfo(DirectoryInfo directoryInfo) => new(directoryInfo);
	}

	public class DiskFileInfo : IFileInfo
	{
		private readonly FileInfo fileInfo;

		public bool Exists => fileInfo.Exists;
		public bool IsReadOnly => fileInfo.IsReadOnly;
		public long Length => fileInfo.Length;
		public string Name  => fileInfo.Name;
		public FileAttributes Attributes => fileInfo.Attributes;
		public IDirectoryInfo Parent => new DiskDirectoryInfo(fileInfo.Directory);
		public string FullName => fileInfo.FullName;
		public DateTime CreationTime => fileInfo.CreationTime;
		public DateTime LastAccessTime => fileInfo.LastAccessTime;
		public DateTime LastWriteTime => fileInfo.LastWriteTime;

		public DiskFileInfo(string path!!)
		{
			this.fileInfo = new FileInfo(path);
		}

		public DiskFileInfo(FileInfo fileInfo!!)
		{
			this.fileInfo = fileInfo;
		}

		public StreamWriter AppendText()=> fileInfo.AppendText();
		public IFileInfo CopyTo(string destFileName)=> new DiskFileInfo(fileInfo.CopyTo(destFileName));
		public IFileInfo CopyTo(string destFileName, bool overwrite)=> new DiskFileInfo(fileInfo.CopyTo(destFileName, overwrite));
		public Stream Create()=> fileInfo.Create();
		public StreamWriter CreateText() => fileInfo.CreateText();
		public void Delete() => fileInfo.Delete();
		public void MoveTo(string destFileName) => fileInfo.MoveTo(destFileName);
		public void MoveTo(string destFileName, bool overwrite) => fileInfo.MoveTo(destFileName, overwrite);
		public Stream Open(FileMode mode)=> fileInfo.Open(mode);
		public Stream Open(FileMode mode, FileAccess access) => fileInfo.Open(mode, access);
		public Stream Open(FileMode mode, FileAccess access, FileShare share)=> fileInfo.Open(mode, access, share);
		public Stream OpenRead() => fileInfo.OpenRead();
		public StreamReader OpenText() => fileInfo.OpenText();
		public Stream OpenWrite()=> fileInfo.OpenWrite();
		public IFileInfo Replace(string destinationFileName, string destinationBackupFileName)=>new DiskFileInfo(fileInfo.Replace(destinationFileName, destinationBackupFileName));
		public IFileInfo Replace(string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors) => new DiskFileInfo(fileInfo.Replace(destinationFileName, destinationBackupFileName, ignoreMetadataErrors));

		public static explicit operator FileInfo(DiskFileInfo diskFileInfo) => diskFileInfo.fileInfo;
		public static implicit operator DiskFileInfo(FileInfo fileInfo) => new(fileInfo);

	}
}
