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
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.FileFormats
{
	[Flags]
	enum SoundFlags
	{
		_8Bit = 0x0,
		Stereo = 0x1,
		_16Bit = 0x2,
	}

	enum SoundFormat
	{
		WestwoodCompressed = 1,
		ImaAdpcm = 99,
	}

	public static class AudReader
	{
		public static float SoundLength(Stream s)
		{
			var sampleRate = s.ReadUInt16();
			/*var dataSize = */ s.ReadInt32();
			var outputSize = s.ReadInt32();
			var flags = (SoundFlags)s.ReadByte();

			var samples = outputSize;
			if ((flags & SoundFlags.Stereo) != 0)
				samples /= 2;

			if ((flags & SoundFlags._16Bit) != 0)
				samples /= 2;

			return (float)samples / sampleRate;
		}

		public static bool LoadSound(Stream s, out Func<Stream> result, out int sampleRate, out int sampleBits, out int channels)
		{
			result = null;
			var startPosition = s.Position;
			try
			{
				sampleRate = s.ReadUInt16();
				var dataSize = s.ReadInt32();
				var outputSize = s.ReadInt32();

				sampleBits = 0;
				channels = 0;

				var readFlag = s.ReadByte();
				if (!Enum.IsDefined(typeof(SoundFlags), readFlag))
					return false;

				sampleBits = readFlag == (int)SoundFlags._8Bit ? 8 : 16;
				channels = readFlag == (int)SoundFlags.Stereo ? 2 : 1;

				var readFormat = s.ReadByte();
				System.Console.WriteLine(readFormat);
				if (!Enum.IsDefined(typeof(SoundFormat), readFormat))
					return false;

				var offsetPosition = s.Position;

				result = () =>
				{
					var audioStream = SegmentStream.CreateWithoutOwningStream(s, offsetPosition, (int)(s.Length - offsetPosition));
					if (readFormat == (int)SoundFormat.ImaAdpcm)
						return new AudStream(audioStream, outputSize, dataSize);
					else
						return new WestwoodAudStream(audioStream, outputSize, dataSize);
				};
			}
			finally
			{
				s.Position = startPosition;
			}

			return true;
		}

		sealed class AudStream : ReadOnlyAdapterStream
		{
			readonly int outputSize;
			int dataSize;

			int currentSample;
			int baseOffset;
			int index;

			public AudStream(Stream stream, int outputSize, int dataSize)
				: base(stream)
			{
				this.outputSize = outputSize;
				this.dataSize = dataSize;
			}

			public override long Length
			{
				get { return outputSize; }
			}

			protected override bool BufferData(Stream baseStream, Queue<byte> data)
			{
				if (dataSize <= 0)
					return true;

				var chunk = ImaAdpcmChunk.Read(baseStream);
				for (var n = 0; n < chunk.CompressedSize; n++)
				{
					var b = baseStream.ReadUInt8();

					var t = ImaAdpcmReader.DecodeImaAdpcmSample(b, ref index, ref currentSample);
					data.Enqueue((byte)t);
					data.Enqueue((byte)(t >> 8));
					baseOffset += 2;

					if (baseOffset < outputSize)
					{
						/* possible that only half of the final byte is used! */
						t = ImaAdpcmReader.DecodeImaAdpcmSample((byte)(b >> 4), ref index, ref currentSample);
						data.Enqueue((byte)t);
						data.Enqueue((byte)(t >> 8));
						baseOffset += 2;
					}
				}

				dataSize -= 8 + chunk.CompressedSize;

				return dataSize <= 0;
			}
		}

		sealed class WestwoodAudStream : ReadOnlyAdapterStream
		{
			readonly int outputSize;
			readonly int dataSize;

			static readonly short[] WSTable2bit = { -2, -1, 0, 1 };
			static readonly short[] WSTable4bit =
			{
				-9, -8, -6, -5, -4, -3, -2, -1,
				0,  1,  2,  3,  4,  5,  6,  8
			};

			public WestwoodAudStream(Stream stream, int outputSize, int dataSize)
				: base(stream)
			{
				this.outputSize = outputSize;
				this.dataSize = dataSize;

				System.Console.WriteLine(outputSize + " " + dataSize);
			}

			public override long Length
			{
				get { return outputSize; }
			}

			protected override bool BufferData(Stream baseStream, Queue<byte> data)
			{
				if (dataSize <= 0)
					return true;

				if (dataSize == outputSize)
				{
					data.Append(baseStream.ReadAllBytes());
					return true;
				}

				var sample = 0x80;

				while (baseStream.Position + 16 < baseStream.Length)
				{
					var input = baseStream.ReadUInt8();
					input <<= 2;
					var command = (byte)(input >> 8);
					var count = (byte)((input & 0xff) >> 2);

					System.Console.WriteLine("dataSize: " + dataSize + " baseStream.Position " + baseStream.Position);
					System.Console.WriteLine("count " + count);
					switch (command)
					{
						case 2: // no compression
						if ((count & 0x20) != 0)
						{
							count <<= 3;
							sample += count >> 3;
							data.Enqueue((byte)sample);
						}
						else
						{
							for (var i = 0; i < count; i++)
								data.Enqueue(baseStream.ReadUInt8());

							baseStream.Position -= 1;
							sample = baseStream.ReadUInt8();
							sample &= 0xffff;
						}

						break;

						case 1: // ADPCM 8-bit -> 4-bit
						for (count++; count > 0; count--)
						{
							command = baseStream.ReadUInt8();

							sample += WSTable4bit[command & 0x0F]; // lower nibble

							sample = sample.Clamp(0, 255);
							data.Enqueue((byte)sample);

							sample += WSTable4bit[command >> 4]; // higher nibble
							sample = sample.Clamp(0, 255);
							data.Enqueue((byte)sample);
						}

						break;

						case 0: // ADPCM 8-bit -> 2-bit
						for (count++; count > 0; count--)
						{
							command = baseStream.ReadUInt8();

							sample += WSTable2bit[command & 0x03]; // lower 2 bits
							sample = sample.Clamp(0, 255);
							data.Enqueue((byte)sample);

							sample += WSTable2bit[(command >> 2) & 0x03]; // lower middle 2 bits
							sample = sample.Clamp(0, 255);
							data.Enqueue((byte)sample);

							sample += WSTable2bit[(command >> 4) & 0x03]; // higher middle 2 bits
							sample.Clamp(0, 255);
							data.Enqueue((byte)sample);

							sample += WSTable2bit[(command >> 6) & 0x03]; // higher 2 bits
							sample = sample.Clamp(0, 255);
							data.Enqueue((byte)sample);
						}

						break;

						default:
						for (count++; count > 0; count--)
							data.Enqueue((byte)sample);
						break;
					}
				}

				//data = data.Reverse();

				return true;
			}
		}
	}
}
