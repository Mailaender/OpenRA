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
using System.Collections.Generic;
using System.IO;
using NVorbis;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.FileFormats
{
	public static class OggReader
	{
		private const int SampleSizeBytes = 4;

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

			using (var vorbis = new VorbisReader(s, closeOnDispose: false))
			{
				channels = (short)vorbis.Channels;
				sampleRate = vorbis.SampleRate;
				sampleBits = vorbis.NominalBitrate / 8;
				length = (int)vorbis.TotalTime.TotalSeconds;
			}

			result = () => { return new OggStream(s); };

			return true;
		}

		sealed class OggStream : ReadOnlyAdapterStream
		{
			public OggStream(Stream stream)
				: base(stream) { }

			protected override bool BufferData(Stream baseStream, Queue<byte> data)
			{
				var samplesRead = 0;
				var samples = new List<float>();
				using (var vorbis = new VorbisReader(baseStream, closeOnDispose: false))
				{
					var bufferSize = vorbis.Channels * vorbis.SampleRate / 5;
					System.Console.WriteLine("bufferSize " + bufferSize);

					var buffer = new float[bufferSize];

					while ((samplesRead = vorbis.ReadSamples(buffer, 0, buffer.Length)) > 0)
					{
						System.Console.WriteLine(samplesRead);

						if (samplesRead != buffer.Length)
							Array.Resize(ref buffer, samplesRead);

						samples.AddRange(buffer);
						var samplesArray = samples.ToArray();

						buffer = new float[bufferSize];
						var sampleDatas = new byte[samplesArray.Length * SampleSizeBytes];
						Buffer.BlockCopy(samplesArray, 0, sampleDatas, 0, sampleDatas.Length);

						foreach (var sampleData in sampleDatas)
							data.Enqueue(sampleData);
					}
				}

				return true;
			}
		}
	}
}
