using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.Files
{
	public interface IFileSystemProvider
	{
		
	}

	public interface IFileSystemProviderFactory
	{
		IFileSystemProvider Create(string source);
	}

	public interface IFileSystemInfo
	{
		System.IO.FileAttributes Attributes { get; }
		IDirectoryInfo Parent { get; }
		string Name { get; }
		string FullName { get; }
		DateTime CreationTime { get; }
		DateTime LastAccessTime { get; }
		DateTime LastWriteTime { get; }

		void Delete();
	}

	public interface IDirectoryInfo	: IFileSystemInfo
	{
		/// <summary>
		/// Creates a directory
		/// </summary>
		/// <exception cref="System.IO.IOException">The directory cannot be created.</exception>
		void Create();

		//
		// Résumé :
		//     Creates a subdirectory or subdirectories on the specified path. The specified
		//     path can be relative to this instance of the System.IO.DirectoryInfo class.
		//
		// Paramètres :
		//   path:
		//     The specified path. This cannot be a different disk volume or Universal Naming
		//     Convention (UNC) name.
		//
		// Retourne :
		//     The last directory specified in path.
		//
		// Exceptions :
		//   T:System.ArgumentException:
		//     path does not specify a valid file path or contains invalid DirectoryInfo characters.
		//
		//   T:System.ArgumentNullException:
		//     path is null.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.IOException:
		//     The subdirectory cannot be created. -or- A file or directory already has the
		//     name specified by path.
		//
		//   T:System.IO.PathTooLongException:
		//     The specified path, file name, or both exceed the system-defined maximum length.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have code access permission to create the directory. -or-
		//     The caller does not have code access permission to read the directory described
		//     by the returned System.IO.DirectoryInfo object. This can occur when the path
		//     parameter describes an existing directory.
		//
		//   T:System.NotSupportedException:
		//     path contains a colon character (:) that is not part of a drive label ("C:\").
		DirectoryInfo CreateSubdirectory(string path);

		//
		// Résumé :
		//     Deletes this instance of a System.IO.DirectoryInfo, specifying whether to delete
		//     subdirectories and files.
		//
		// Paramètres :
		//   recursive:
		//     true to delete this directory, its subdirectories, and all files; otherwise,
		//     false.
		//
		// Exceptions :
		//   T:System.UnauthorizedAccessException:
		//     The directory contains a read-only file.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The directory described by this System.IO.DirectoryInfo object does not exist
		//     or could not be found.
		//
		//   T:System.IO.IOException:
		//     The directory is read-only. -or- The directory contains one or more files or
		//     subdirectories and recursive is false. -or- The directory is the application's
		//     current working directory. -or- There is an open handle on the directory or on
		//     one of its files, and the operating system is Windows XP or earlier. This open
		//     handle can result from enumerating directories and files. For more information,
		//     see How to: Enumerate Directories and Files.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		void Delete(bool recursive);

		//
		// Résumé :
		//     Returns an enumerable collection of directory information in the current directory.
		//
		// Retourne :
		//     An enumerable collection of directories in the current directory.
		//
		// Exceptions :
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<DirectoryInfo> EnumerateDirectories();

		//
		// Résumé :
		//     Returns an enumerable collection of directory information that matches a specified
		//     search pattern.
		//
		// Paramètres :
		//   searchPattern:
		//     The search string to match against the names of directories. This parameter can
		//     contain a combination of valid literal path and wildcard (* and ?) characters,
		//     but it doesn't support regular expressions.
		//
		// Retourne :
		//     An enumerable collection of directories that matches searchPattern.
		//
		// Exceptions :
		//   T:System.ArgumentNullException:
		//     searchPattern is null.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<DirectoryInfo> EnumerateDirectories(string searchPattern);

		//
		// Paramètres :
		//   searchPattern:
		//
		//   enumerationOptions:
		IEnumerable<DirectoryInfo> EnumerateDirectories(string searchPattern, EnumerationOptions enumerationOptions);

		//
		// Résumé :
		//     Returns an enumerable collection of directory information that matches a specified
		//     search pattern and search subdirectory option.
		//
		// Paramètres :
		//   searchPattern:
		//     The search string to match against the names of directories. This parameter can
		//     contain a combination of valid literal path and wildcard (* and ?) characters,
		//     but it doesn't support regular expressions.
		//
		//   searchOption:
		//     One of the enumeration values that specifies whether the search operation should
		//     include only the current directory or all subdirectories. The default value is
		//     System.IO.SearchOption.TopDirectoryOnly.
		//
		// Retourne :
		//     An enumerable collection of directories that matches searchPattern and searchOption.
		//
		// Exceptions :
		//   T:System.ArgumentNullException:
		//     searchPattern is null.
		//
		//   T:System.ArgumentOutOfRangeException:
		//     searchOption is not a valid System.IO.SearchOption value.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<DirectoryInfo> EnumerateDirectories(string searchPattern, SearchOption searchOption);

		//
		// Résumé :
		//     Returns an enumerable collection of file information in the current directory.
		//
		// Retourne :
		//     An enumerable collection of the files in the current directory.
		//
		// Exceptions :
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<FileInfo> EnumerateFiles();

		//
		// Résumé :
		//     Returns an enumerable collection of file information that matches a search pattern.
		//
		// Paramètres :
		//   searchPattern:
		//     The search string to match against the names of files. This parameter can contain
		//     a combination of valid literal path and wildcard (* and ?) characters, but it
		//     doesn't support regular expressions.
		//
		// Retourne :
		//     An enumerable collection of files that matches searchPattern.
		//
		// Exceptions :
		//   T:System.ArgumentNullException:
		//     searchPattern is null.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid, (for
		//     example, it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<FileInfo> EnumerateFiles(string searchPattern);

		//
		// Paramètres :
		//   searchPattern:
		//
		//   enumerationOptions:
		IEnumerable<FileInfo> EnumerateFiles(string searchPattern, EnumerationOptions enumerationOptions);

		//
		// Résumé :
		//     Returns an enumerable collection of file information that matches a specified
		//     search pattern and search subdirectory option.
		//
		// Paramètres :
		//   searchPattern:
		//     The search string to match against the names of files. This parameter can contain
		//     a combination of valid literal path and wildcard (* and ?) characters, but it
		//     doesn't support regular expressions.
		//
		//   searchOption:
		//     One of the enumeration values that specifies whether the search operation should
		//     include only the current directory or all subdirectories. The default value is
		//     System.IO.SearchOption.TopDirectoryOnly.
		//
		// Retourne :
		//     An enumerable collection of files that matches searchPattern and searchOption.
		//
		// Exceptions :
		//   T:System.ArgumentNullException:
		//     searchPattern is null.
		//
		//   T:System.ArgumentOutOfRangeException:
		//     searchOption is not a valid System.IO.SearchOption value.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<FileInfo> EnumerateFiles(string searchPattern, SearchOption searchOption);

		//
		// Résumé :
		//     Returns an enumerable collection of file system information in the current directory.
		//
		// Retourne :
		//     An enumerable collection of file system information in the current directory.
		//
		// Exceptions :
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<FileSystemInfo> EnumerateFileSystemInfos();

		//
		// Résumé :
		//     Returns an enumerable collection of file system information that matches a specified
		//     search pattern.
		//
		// Paramètres :
		//   searchPattern:
		//     The search string to match against the names of directories. This parameter can
		//     contain a combination of valid literal path and wildcard (* and ?) characters,
		//     but it doesn't support regular expressions.
		//
		// Retourne :
		//     An enumerable collection of file system information objects that matches searchPattern.
		//
		// Exceptions :
		//   T:System.ArgumentNullException:
		//     searchPattern is null.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<FileSystemInfo> EnumerateFileSystemInfos(string searchPattern);

		//
		// Paramètres :
		//   searchPattern:
		//
		//   enumerationOptions:
		IEnumerable<FileSystemInfo> EnumerateFileSystemInfos(string searchPattern, EnumerationOptions enumerationOptions);

		//
		// Résumé :
		//     Returns an enumerable collection of file system information that matches a specified
		//     search pattern and search subdirectory option.
		//
		// Paramètres :
		//   searchPattern:
		//     The search string to match against the names of directories. This parameter can
		//     contain a combination of valid literal path and wildcard (* and ?) characters,
		//     but it doesn't support regular expressions.
		//
		//   searchOption:
		//     One of the enumeration values that specifies whether the search operation should
		//     include only the current directory or all subdirectories. The default value is
		//     System.IO.SearchOption.TopDirectoryOnly.
		//
		// Retourne :
		//     An enumerable collection of file system information objects that matches searchPattern
		//     and searchOption.
		//
		// Exceptions :
		//   T:System.ArgumentNullException:
		//     searchPattern is null.
		//
		//   T:System.ArgumentOutOfRangeException:
		//     searchOption is not a valid System.IO.SearchOption value.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path encapsulated in the System.IO.DirectoryInfo object is invalid (for example,
		//     it is on an unmapped drive).
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		IEnumerable<FileSystemInfo> EnumerateFileSystemInfos(string searchPattern, SearchOption searchOption);
	}

	public interface IFileInfo : IFileSystemInfo
	{
		//
		// Résumé :
		//     Gets or sets a value that determines if the current file is read only.
		//
		// Retourne :
		//     true if the current file is read only; otherwise, false.
		//
		// Exceptions :
		//   T:System.IO.FileNotFoundException:
		//     The file described by the current System.IO.FileInfo object could not be found.
		//
		//   T:System.IO.IOException:
		//     An I/O error occurred while opening the file.
		//
		//   T:System.UnauthorizedAccessException:
		//     This operation is not supported on the current platform. -or- The caller does
		//     not have the required permission.
		//
		//   T:System.ArgumentException:
		//     The user does not have write permission, but attempted to set this property to
		//     false.
		bool IsReadOnly { get; }

		//
		// Résumé :
		//     Gets the size, in bytes, of the current file.
		//
		// Retourne :
		//     The size of the current file in bytes.
		//
		// Exceptions :
		//   T:System.IO.IOException:
		//     System.IO.FileSystemInfo.Refresh cannot update the state of the file or directory.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file does not exist. -or- The Length property is called for a directory.
		long Length { get; }

		//
		// Résumé :
		//     Creates a System.IO.StreamWriter that appends text to the file represented by
		//     this instance of the System.IO.FileInfo.
		//
		// Retourne :
		//     A new StreamWriter.
		StreamWriter AppendText();

		//
		// Résumé :
		//     Copies an existing file to a new file, disallowing the overwriting of an existing
		//     file.
		//
		// Paramètres :
		//   destFileName:
		//     The name of the new file to copy to.
		//
		// Retourne :
		//     A new file with a fully qualified path.
		//
		// Exceptions :
		//   T:System.ArgumentException:
		//     destFileName is empty, contains only white spaces, or contains invalid characters.
		//
		//   T:System.IO.IOException:
		//     An error occurs, or the destination file already exists.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.ArgumentNullException:
		//     destFileName is null.
		//
		//   T:System.UnauthorizedAccessException:
		//     A directory path is passed in, or the file is being moved to a different drive.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The directory specified in destFileName does not exist.
		//
		//   T:System.IO.PathTooLongException:
		//     The specified path, file name, or both exceed the system-defined maximum length.
		//
		//   T:System.NotSupportedException:
		//     destFileName contains a colon (:) within the string but does not specify the
		//     volume.
		IFileInfo CopyTo(string destFileName);

		//
		// Résumé :
		//     Copies an existing file to a new file, allowing the overwriting of an existing
		//     file.
		//
		// Paramètres :
		//   destFileName:
		//     The name of the new file to copy to.
		//
		//   overwrite:
		//     true to allow an existing file to be overwritten; otherwise, false.
		//
		// Retourne :
		//     A new file, or an overwrite of an existing file if overwrite is true. If the
		//     file exists and overwrite is false, an System.IO.IOException is thrown.
		//
		// Exceptions :
		//   T:System.ArgumentException:
		//     destFileName is empty, contains only white spaces, or contains invalid characters.
		//
		//   T:System.IO.IOException:
		//     An error occurs, or the destination file already exists and overwrite is false.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.ArgumentNullException:
		//     destFileName is null.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The directory specified in destFileName does not exist.
		//
		//   T:System.UnauthorizedAccessException:
		//     A directory path is passed in, or the file is being moved to a different drive.
		//
		//   T:System.IO.PathTooLongException:
		//     The specified path, file name, or both exceed the system-defined maximum length.
		//
		//   T:System.NotSupportedException:
		//     destFileName contains a colon (:) in the middle of the string.
		IFileInfo CopyTo(string destFileName, bool overwrite);

		//
		// Résumé :
		//     Creates a file.
		//
		// Retourne :
		//     A new file.
		Stream Create();

		//
		// Résumé :
		//     Creates a System.IO.StreamWriter that writes a new text file.
		//
		// Retourne :
		//     A new StreamWriter.
		//
		// Exceptions :
		//   T:System.UnauthorizedAccessException:
		//     The file name is a directory.
		//
		//   T:System.IO.IOException:
		//     The disk is read-only.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		StreamWriter CreateText();

		//
		// Résumé :
		//     Moves a specified file to a new location, providing the option to specify a new
		//     file name.
		//
		// Paramètres :
		//   destFileName:
		//     The path to move the file to, which can specify a different file name.
		//
		// Exceptions :
		//   T:System.IO.IOException:
		//     An I/O error occurs, such as the destination file already exists or the destination
		//     device is not ready.
		//
		//   T:System.ArgumentNullException:
		//     destFileName is null.
		//
		//   T:System.ArgumentException:
		//     destFileName is empty, contains only white spaces, or contains invalid characters.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.UnauthorizedAccessException:
		//     destFileName is read-only or is a directory.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file is not found.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.PathTooLongException:
		//     The specified path, file name, or both exceed the system-defined maximum length.
		//
		//   T:System.NotSupportedException:
		//     destFileName contains a colon (:) in the middle of the string.
		void MoveTo(string destFileName);

		//
		// Résumé :
		//     Moves a specified file to a new location, providing the options to specify a
		//     new file name and to overwrite the destination file if it already exists.
		//
		// Paramètres :
		//   destFileName:
		//     The path to move the file to, which can specify a different file name.
		//
		//   overwrite:
		//     true to overwrite the destination file if it already exists; false otherwise.
		//
		// Exceptions :
		//   T:System.IO.IOException:
		//     An I/O error occurred, such as the destination device is not ready.
		//
		//   T:System.ArgumentNullException:
		//     destFileName is null.
		//
		//   T:System.ArgumentException:
		//     destFileName is empty, contains only white spaces, or contains invalid characters.
		//
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.UnauthorizedAccessException:
		//     destFileName is read-only or is a directory.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file is not found.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.PathTooLongException:
		//     The specified path, file name, or both exceed the system-defined maximum length.
		//
		//   T:System.NotSupportedException:
		//     destFileName contains a colon (:) in the middle of the string.
		void MoveTo(string destFileName, bool overwrite);

		//
		// Résumé :
		//     Opens a file in the specified mode.
		//
		// Paramètres :
		//   mode:
		//     A System.IO.FileMode constant specifying the mode (for example, Open or Append)
		//     in which to open the file.
		//
		// Retourne :
		//     A file opened in the specified mode, with read/write access and unshared.
		//
		// Exceptions :
		//   T:System.IO.FileNotFoundException:
		//     The file is not found.
		//
		//   T:System.UnauthorizedAccessException:
		//     The file is read-only or is a directory.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.IOException:
		//     The file is already open.
		Stream Open(FileMode mode);

		//
		// Résumé :
		//     Opens a file in the specified mode with read, write, or read/write access.
		//
		// Paramètres :
		//   mode:
		//     A System.IO.FileMode constant specifying the mode (for example, Open or Append)
		//     in which to open the file.
		//
		//   access:
		//     A System.IO.FileAccess constant specifying whether to open the file with Read,
		//     Write, or ReadWrite file access.
		//
		// Retourne :
		//     A System.IO.FileStream object opened in the specified mode and access, and unshared.
		//
		// Exceptions :
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file is not found.
		//
		//   T:System.UnauthorizedAccessException:
		//     path is read-only or is a directory.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.IOException:
		//     The file is already open.
		//
		//   T:System.ArgumentException:
		//     path is empty or contains only white spaces.
		//
		//   T:System.ArgumentNullException:
		//     One or more arguments is null.
		Stream Open(FileMode mode, FileAccess access);

		//
		// Résumé :
		//     Opens a file in the specified mode with read, write, or read/write access and
		//     the specified sharing option.
		//
		// Paramètres :
		//   mode:
		//     A System.IO.FileMode constant specifying the mode (for example, Open or Append)
		//     in which to open the file.
		//
		//   access:
		//     A System.IO.FileAccess constant specifying whether to open the file with Read,
		//     Write, or ReadWrite file access.
		//
		//   share:
		//     A System.IO.FileShare constant specifying the type of access other FileStream
		//     objects have to this file.
		//
		// Retourne :
		//     A System.IO.FileStream object opened with the specified mode, access, and sharing
		//     options.
		//
		// Exceptions :
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file is not found.
		//
		//   T:System.UnauthorizedAccessException:
		//     path is read-only or is a directory.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.IOException:
		//     The file is already open.
		//
		//   T:System.ArgumentException:
		//     path is empty or contains only white spaces.
		//
		//   T:System.ArgumentNullException:
		//     One or more arguments is null.
		Stream Open(FileMode mode, FileAccess access, FileShare share);

		//
		// Résumé :
		//     Creates a read-only System.IO.FileStream.
		//
		// Retourne :
		//     A new read-only System.IO.FileStream object.
		//
		// Exceptions :
		//   T:System.UnauthorizedAccessException:
		//     path is read-only or is a directory.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		//
		//   T:System.IO.IOException:
		//     The file is already open.
		Stream OpenRead();

		//
		// Résumé :
		//     Creates a System.IO.StreamReader with UTF8 encoding that reads from an existing
		//     text file.
		//
		// Retourne :
		//     A new StreamReader with UTF8 encoding.
		//
		// Exceptions :
		//   T:System.Security.SecurityException:
		//     The caller does not have the required permission.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file is not found.
		//
		//   T:System.UnauthorizedAccessException:
		//     path is read-only or is a directory.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The specified path is invalid, such as being on an unmapped drive.
		StreamReader OpenText();

		//
		// Résumé :
		//     Creates a write-only System.IO.FileStream.
		//
		// Retourne :
		//     A write-only unshared System.IO.FileStream object for a new or existing file.
		//
		// Exceptions :
		//   T:System.UnauthorizedAccessException:
		//     The path specified when creating an instance of the System.IO.FileInfo object
		//     is read-only or is a directory.
		//
		//   T:System.IO.DirectoryNotFoundException:
		//     The path specified when creating an instance of the System.IO.FileInfo object
		//     is invalid, such as being on an unmapped drive.
		Stream OpenWrite();

		//
		// Résumé :
		//     Replaces the contents of a specified file with the file described by the current
		//     System.IO.FileInfo object, deleting the original file, and creating a backup
		//     of the replaced file.
		//
		// Paramètres :
		//   destinationFileName:
		//     The name of a file to replace with the current file.
		//
		//   destinationBackupFileName:
		//     The name of a file with which to create a backup of the file described by the
		//     destFileName parameter.
		//
		// Retourne :
		//     A System.IO.FileInfo object that encapsulates information about the file described
		//     by the destFileName parameter.
		//
		// Exceptions :
		//   T:System.ArgumentException:
		//     The path described by the destFileName parameter was not of a legal form. -or-
		//     The path described by the destBackupFileName parameter was not of a legal form.
		//
		//   T:System.ArgumentNullException:
		//     The destFileName parameter is null.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file described by the current System.IO.FileInfo object could not be found.
		//     -or- The file described by the destinationFileName parameter could not be found.
		//
		//   T:System.PlatformNotSupportedException:
		//     The current operating system is not Microsoft Windows NT or later.
		IFileInfo Replace(string destinationFileName, string destinationBackupFileName);

		//
		// Résumé :
		//     Replaces the contents of a specified file with the file described by the current
		//     System.IO.FileInfo object, deleting the original file, and creating a backup
		//     of the replaced file. Also specifies whether to ignore merge errors.
		//
		// Paramètres :
		//   destinationFileName:
		//     The name of a file to replace with the current file.
		//
		//   destinationBackupFileName:
		//     The name of a file with which to create a backup of the file described by the
		//     destFileName parameter.
		//
		//   ignoreMetadataErrors:
		//     true to ignore merge errors (such as attributes and ACLs) from the replaced file
		//     to the replacement file; otherwise false.
		//
		// Retourne :
		//     A System.IO.FileInfo object that encapsulates information about the file described
		//     by the destFileName parameter.
		//
		// Exceptions :
		//   T:System.ArgumentException:
		//     The path described by the destFileName parameter was not of a legal form. -or-
		//     The path described by the destBackupFileName parameter was not of a legal form.
		//
		//   T:System.ArgumentNullException:
		//     The destFileName parameter is null.
		//
		//   T:System.IO.FileNotFoundException:
		//     The file described by the current System.IO.FileInfo object could not be found.
		//     -or- The file described by the destinationFileName parameter could not be found.
		//
		//   T:System.PlatformNotSupportedException:
		//     The current operating system is not Microsoft Windows NT or later.
		IFileInfo Replace(string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors);

	}
}
