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
		public int SampleBits => reader.NominalBitrate;
		public int Channels => reader.Channels;
		public int SampleRate => reader.SampleRate;
		public float LengthInSeconds => (float)reader.TotalTime.TotalSeconds;
		public Stream GetPCMInputStream() { return new OggStream(stream); }
		public void Dispose() { reader.Dispose(); }

		readonly VorbisReader reader;
		readonly Stream stream;

		public OggFormat(Stream stream)
		{
			if (stream.ReadASCII(4) != "OggS")
				throw new InvalidDataException("Ogg header not recognized");

			stream.Position = 0;
			this.stream = stream;
			reader = new VorbisReader(stream);
		}

		public class OggStream : Stream
		{
			readonly VorbisReader reader;
			readonly Stream clone;

			public OggStream(Stream stream)
			{
				clone = SegmentStream.CreateWithoutOwningStream(stream, 0, (int)stream.Length);
				reader = new VorbisReader(clone);
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
				var cb = new float[count];


				// let Read(float[], int, int) do the actual reading; adjust count back to bytes
				var cnt = Read(cb, 0, count) * sizeof(float);

				// move the data back to the request buffer
				Buffer.BlockCopy(cb, 0, buffer, offset, cnt);

				// done!
				return cnt;
			}

			public int Read(float[] buffer, int offset, int count)
			{
				var cnt = reader.ReadSamples(buffer, offset, count);
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
