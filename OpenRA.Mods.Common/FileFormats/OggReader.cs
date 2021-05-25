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
		public static bool LoadSound(Stream s, out Func<Stream> result, out short channels, out int sampleBits, out int sampleRate, out float length)
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

			var vorbis = new VorbisReader(s);
			channels = (short)vorbis.Channels;
			sampleRate = vorbis.SampleRate;
			sampleBits = vorbis.NominalBitrate;
			length = (float)vorbis.TotalTime.TotalSeconds;

			result = () =>
			{
				return new OggStream(vorbis);
			};

			return true;
		}

		public class OggStream : Stream
		{
			readonly VorbisReader reader;
			float[] conversionBuffer;

			public OggStream(VorbisReader reader)
			{
				this.reader = reader;
			}

			public override bool CanRead => !reader.IsEndOfStream;
			public override bool CanSeek => false;
			public override bool CanWrite => false;

			public override long Length => reader.TotalSamples;

			public override long Position
			{
				get => reader.SamplePosition;
				set => throw new NotImplementedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				// adjust count so it is in floats instead of bytes
				count /= sizeof(float);

				// make sure we don't have an odd count
				count -= count % reader.Channels;

				// get the buffer, creating a new one if none exists or the existing one is too small
				var cb = conversionBuffer ?? (conversionBuffer = new float[count]);
				if (cb.Length < count)
				{
					cb = (conversionBuffer = new float[count]);
				}

				// let Read(float[], int, int) do the actual reading; adjust count back to bytes
				int cnt = Read(cb, 0, count) * sizeof(float);

				// move the data back to the request buffer
				Buffer.BlockCopy(cb, 0, buffer, offset, cnt);


				System.Console.WriteLine(cnt);

				// done!
				return cnt;
			}

			public int Read(float[] buffer, int offset, int count)
			{
				var cnt = reader.ReadSamples(buffer, offset, count);
				if (cnt == 0)
				{
					if (reader.IsEndOfStream)
					{
						if (reader.StreamIndex < reader.Streams.Count - 1)
						{
							if (reader.SwitchStreams(reader.StreamIndex + 1))
							{
								return 0;
							}
							else
							{
								return Read(buffer, offset, count);
							}
						}
					}
				}

				return cnt;
			}

			public override void Flush() { throw new NotImplementedException(); }
			public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
			public override void SetLength(long value) { throw new NotImplementedException(); }
			public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					reader.Dispose();
				base.Dispose(disposing);
			}
		}
	}
}
