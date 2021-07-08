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
using System.IO;
using NVorbis;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.AudioLoaders
{
	public class OggLoader : ISoundLoader
	{
		bool ISoundLoader.TryParseSound(Stream stream, out ISoundFormat sound)
		{
			try
			{
				sound = new OggFormat(stream);
				return true;
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
		public int SampleBits => 16;	// hardcode to 16-bit samples
		public int Channels => reader.Channels;
		public int SampleRate => reader.SampleRate;
		public float LengthInSeconds => (float)reader.TotalTime.TotalSeconds;
		public Stream GetPCMInputStream() { return new OggStream(new OggFormat(this)); }
		public void Dispose() { reader.Dispose(); }

		readonly VorbisReader reader;
		readonly Stream stream;

		public OggFormat(Stream stream)
		{
			this.stream = stream;
			reader = new VorbisReader(stream);
		}

		OggFormat(OggFormat cloneFrom)
		{
			stream = SegmentStream.CreateWithoutOwningStream(cloneFrom.stream, 0, (int)cloneFrom.stream.Length);
			reader = new VorbisReader(stream)
			{
				// tell NVorbis to clip samples so we don't have to range-check in Read(byte[], int, int)
				ClipSamples = true
			};
		}

		public class OggStream : Stream
		{
			readonly OggFormat format;

			// This buffer can be static because it can only be used by 1 instance per thread
			[ThreadStatic]
			static float[] conversionBuffer = null;

			public OggStream(OggFormat format)
			{
				this.format = format;
			}

			public override bool CanRead => !format.reader.IsEndOfStream;
			public override bool CanSeek => false;
			public override bool CanWrite => false;

			public override long Length => format.reader.TotalSamples;

			public override long Position
			{
				get => format.reader.SamplePosition;
				set => throw new NotImplementedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				// adjust count so it is in 16-bit samples instead of bytes
				count /= 2;

				// make sure we don't have an odd count
				count -= count % format.reader.Channels;

				// get the buffer, creating a new one if none exists or the existing one is too small
				var cb = conversionBuffer ?? (conversionBuffer = new float[count]);
				if (cb.Length < count)
				{
					cb = (conversionBuffer = new float[count]);
				}

				// let Read(float[], int, int) do the actual reading; adjust count back to bytes
				var cnt = Read(cb, 0, count);

				// move the data back to the request buffer and convert to 16-bit signed samples
				for (var i = 0; i < cnt; i++)
				{
					var val = (short)(cb[i] * 32767);
					buffer[offset++] = (byte)(val & 255);
					buffer[offset++] = (byte)(val >> 8);
				}

				// done!
				return cnt * 2;
			}

			public int Read(float[] buffer, int offset, int count)
			{
				var cnt = format.reader.ReadSamples(buffer, offset, count);
				if (cnt == 0)
				{
					if (format.reader.IsEndOfStream)
					{
						if (format.reader.StreamIndex < format.reader.Streams.Count - 1)
						{
							if (format.reader.SwitchStreams(format.reader.StreamIndex + 1))
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
					format.reader.Dispose();
				base.Dispose(disposing);
			}
		}
	}
}
