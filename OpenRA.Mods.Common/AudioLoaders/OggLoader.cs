#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using OpenRA.Mods.Common.FileFormats;

namespace OpenRA.Mods.Common.AudioLoaders
{
	public class OggLoader : ISoundLoader
	{
		bool IsOgg(Stream s)
		{
			var start = s.Position;
			var signature = s.ReadASCII(4);
			s.Position = start;
			return signature == "OggS";
		}

		bool ISoundLoader.TryParseSound(Stream stream, out ISoundFormat sound)
		{
			try
			{
				if (IsOgg(stream))
				{
					sound = new OggFormat(stream);
					return true;
				}
			}
			catch (Exception e)
			{
				// Unsupported file
				System.Console.WriteLine(e.StackTrace);
			}

			sound = null;
			return false;
		}
	}

	public sealed class OggFormat : ISoundFormat
	{
		public int Channels { get { return channels; } }
		public int SampleBits { get { return sampleBits; } }
		public int SampleRate { get { return sampleRate; } }
		public float LengthInSeconds { get { return length; } }
		public Stream GetPCMInputStream() { return oggStreamFactory(); }
		public void Dispose() { sourceStream.Dispose(); }

		readonly Stream sourceStream;
		readonly Func<Stream> oggStreamFactory;
		readonly short channels;
		readonly int sampleBits;
		readonly int sampleRate;
		readonly float length;

		public OggFormat(Stream stream)
		{
			sourceStream = stream;

			if (!OggReader.LoadSound(stream, out oggStreamFactory, out channels, out sampleBits, out sampleRate, out length))
				throw new InvalidDataException();
		}
	}
}
