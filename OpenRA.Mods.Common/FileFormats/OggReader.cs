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
using NVorbis;

namespace OpenRA.Mods.Common.FileFormats
{
	public static class OggReader
	{
		public static bool LoadSound(Stream s, out Func<Stream> result, out short channels, out int sampleBits, out int sampleRate, out int length)
		{
			result = null;
			channels = -1;
			sampleBits = -1;
			sampleRate = -1;
			length = -1;

			var start = s.Position;
			var signature = s.ReadASCII(4);
			s.Position = start;
			if (signature != "OggS")
				return false;

			var vorbis = new VorbisReader(s, closeOnDispose: false);

			channels = (short)vorbis.Channels;
			sampleRate = vorbis.SampleRate;
			sampleBits = vorbis.NominalBitrate;
			length = (int)vorbis.TotalTime.TotalSeconds;

			var buffer = new float[vorbis.TotalSamples];
			var count = vorbis.ReadSamples(buffer, 0, buffer.Length);
			var byteArray = new byte[buffer.Length * sizeof(float)];
			Buffer.BlockCopy(buffer, 0, byteArray, 0, byteArray.Length);

			result = () => { return new MemoryStream(byteArray); };

			return true;
		}
	}
}
