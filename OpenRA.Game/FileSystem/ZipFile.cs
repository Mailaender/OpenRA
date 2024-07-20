#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenRA.FileSystem
{
	public class ZipFileLoader : IPackageLoader
	{
		const uint ZipSignature = 0x04034b50;

		public class ReadOnlyZipFile : IReadOnlyPackage
		{
			public string Name { get; protected set; }
			protected ZipArchive pkg;

			// Dummy constructor for use with ReadWriteZipFile
			protected ReadOnlyZipFile() { }

			public ReadOnlyZipFile(Stream s, string filename)
			{
				Name = filename;
				pkg = new ZipArchive(s, ZipArchiveMode.Read);
			}

			public Stream GetStream(string filename)
			{
				var entry = pkg.GetEntry(filename);
				if (entry == null)
					return null;

				var ms = new MemoryStream();
				using (var z = entry.Open())
					z.CopyTo(ms);

				ms.Seek(0, SeekOrigin.Begin);
				return ms;
			}

			public IEnumerable<string> Contents
			{
				get
				{
					foreach (var entry in pkg.Entries)
					{
						if (!string.IsNullOrEmpty(entry.Name))
							yield return entry.FullName;
					}
				}
			}

			public bool Contains(string filename)
			{
				return pkg.GetEntry(filename) != null;
			}

			public void Dispose()
			{
				pkg?.Dispose();
				GC.SuppressFinalize(this);
			}

			public IReadOnlyPackage OpenPackage(string filename, FileSystem context)
			{
				// Directories are stored with a trailing "/" in the index
				var entry = pkg.GetEntry(filename) ?? pkg.GetEntry(filename + "/");
				if (entry == null)
					return null;

				if (entry.FullName.EndsWith("/", StringComparison.InvariantCulture))
					return new ZipFolder(this, filename);

				var s = GetStream(filename);
				if (s == null)
					return null;

				if (context.TryParsePackage(s, filename, out var package))
					return package;

				s.Dispose();
				return null;
			}
		}

		public sealed class ReadWriteZipFile : ReadOnlyZipFile, IReadWritePackage
		{
			readonly MemoryStream pkgStream = new();

			public ReadWriteZipFile(string filename, bool create = false)
			{
				if (!create)
					using (var copy = new MemoryStream(File.ReadAllBytes(filename)))
						copy.CopyTo(pkgStream);

				pkgStream.Position = 0;
				pkg = new ZipArchive(pkgStream, ZipArchiveMode.Update);
				Name = filename;
			}

			void Commit()
			{
				File.WriteAllBytes(Name, pkgStream.ToArray());
			}

			public void Update(string filename, byte[] contents)
			{
				var entry = pkg.GetEntry(filename);
				entry?.Delete();

				entry = pkg.CreateEntry(filename);
				using (var entryStream = entry.Open())
				using (var contentStream = new MemoryStream(contents))
					contentStream.CopyTo(entryStream);

				Commit();
			}

			public void Delete(string filename)
			{
				var entry = pkg.GetEntry(filename);
				entry?.Delete();
				Commit();
			}
		}

		sealed class ZipFolder : IReadOnlyPackage
		{
			public string Name { get; }
			public ReadOnlyZipFile Parent { get; }

			public ZipFolder(ReadOnlyZipFile parent, string path)
			{
				if (path.EndsWith('/'))
					path = path[..^1];

				Name = path;
				Parent = parent;
			}

			public Stream GetStream(string filename)
			{
				// Zip files use '/' as a path separator
				return Parent.GetStream(Name + '/' + filename);
			}

			public IEnumerable<string> Contents
			{
				get
				{
					foreach (var entry in Parent.Contents)
					{
						if (entry.StartsWith(Name, StringComparison.Ordinal) && entry != Name)
						{
							var filename = entry[(Name.Length + 1)..];
							var dirLevels = filename.Split('/').Count(c => !string.IsNullOrEmpty(c));
							if (dirLevels == 1)
								yield return filename;
						}
					}
				}
			}

			public bool Contains(string filename)
			{
				return Parent.Contains(Name + '/' + filename);
			}

			public IReadOnlyPackage OpenPackage(string filename, FileSystem context)
			{
				return Parent.OpenPackage(Name + '/' + filename, context);
			}

			public void Dispose() { /* nothing to do */ }
		}

		public bool TryParsePackage(Stream s, string filename, FileSystem context, out IReadOnlyPackage package)
		{
			var readSignature = s.ReadUInt32();
			s.Position -= 4;

			if (readSignature != ZipSignature)
			{
				package = null;
				return false;
			}

			package = new ReadOnlyZipFile(s, filename);
			return true;
		}

		public static bool TryParseReadWritePackage(string filename, out IReadWritePackage package)
		{
			using (var s = File.OpenRead(filename))
			{
				if (s.ReadUInt32() != ZipSignature)
				{
					package = null;
					return false;
				}
			}

			package = new ReadWriteZipFile(filename);
			return true;
		}

		public static IReadWritePackage Create(string filename)
		{
			return new ReadWriteZipFile(filename, true);
		}
	}
}
