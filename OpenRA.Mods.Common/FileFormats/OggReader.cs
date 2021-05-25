#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
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
using NVorbis;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.FileFormats
{
	public static class OggReader
	{
		public static bool LoadSound(Stream s, out Func<Stream> result, out short channels, out int sampleBits, out int sampleRate)
		{
			result = null;
			channels = -1;
			sampleBits = -1;
			sampleRate = -1;

			var start = s.Position;
			var signature = s.ReadASCII(4);
			s.Position = start;
			if (signature != "OggS")
				return false;

			var vorbis = new VorbisReader(s);
			channels = (short)vorbis.Channels;
			sampleRate = vorbis.SampleRate;
			sampleBits = vorbis.NominalBitrate;

			result = () =>
			{
				s.Seek(0, SeekOrigin.Begin);
				var audioStream = SegmentStream.CreateWithoutOwningStream(s, 0, (int)s.Length);
				return new OggStream(s);
			};

			return true;
		}

		public static float SoundLength(Stream s)
		{
			var vorbis = new VorbisReader(s);
			return (float)vorbis.TotalTime.TotalSeconds;
		}

		sealed class OggStream : ReadOnlyAdapterStream
		{
			const int DefaultBufferSize = 2;
			const int DefaultBufferCount = 1;

			readonly float[] readSampleBuffer;

			public OggStream(Stream stream)
				: base(stream)
			{
				readSampleBuffer = new float[DefaultBufferSize];
			}

			protected override bool BufferData(Stream baseStream, Queue<byte> data)
			{
				var reader = new VorbisReader(baseStream);
				var readSamples = reader.ReadSamples(readSampleBuffer, 0, DefaultBufferCount);
				foreach (var buffer in readSampleBuffer)
					data.Enqueue((byte)buffer);
				return readSamples == 0;
			}
		}
	}
}
