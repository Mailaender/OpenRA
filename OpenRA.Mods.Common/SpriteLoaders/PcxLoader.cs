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

using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.SpriteLoaders
{
	public class PcxLoader : ISpriteLoader
	{
		class PcxFrame : ISpriteFrame
		{
			public SpriteFrameType Type { get { return SpriteFrameType.Indexed; } }
			public Size Size { get; set; }
			public Size FrameSize { get; set; }
			public float2 Offset { get; set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			frames = null;

			var manufacturer = s.ReadByte();
			if (manufacturer != 10)
				return false;

			var paintbrushVersion = s.ReadByte();
			if (paintbrushVersion != 5)
				return false;

			var encoding = s.ReadByte();
			if (encoding != 1)
				return false;

			var bitsPerPixel = s.ReadByte();
			if (bitsPerPixel != 8)
				return false;

			var xMin = s.ReadUInt16();
			var yMin = s.ReadUInt16();
			var xMax = s.ReadUInt16();
			var yMax = s.ReadUInt16();

			var horizontalDpi = s.ReadUInt16();
			var verticalDpi = s.ReadUInt16();

			var palette = new Color[256];
			for (var i = 0; i < 48; i++)
			{
				var r = s.ReadByte(); var g = s.ReadByte(); var b = s.ReadByte();
				palette[i] = Color.FromArgb(r, g, b);
			}

			metadata = new TypeDictionary
			{
				new EmbeddedSpritePalette(palette.Select(x => (uint)x.ToArgb()).ToArray())
			};

			var reserved = s.ReadByte();
			var bitPlanes = s.ReadByte();
			var bytesPerLine = s.ReadByte();
			var paletteInfo = s.ReadUInt16();
			var horizontalScreenSize = s.ReadUInt16();
			var verticalScreenSize = s.ReadUInt16();
			s.Seek(54, SeekOrigin.Current); // filler

			var frameSize = new Size(xMax - xMin + 1, yMax - yMin + 1);
			var scanLines = bitPlanes * bytesPerLine;
			var linePaddingSize = new Size(bytesPerLine * bitPlanes, (8 / bitsPerPixel) - (xMax - xMin + 1));

			var data = new byte[bytesPerLine * scanLines];

			// Accumulate indices across bit planes
			var indexBuffer = new uint[frameSize.Width];

			for (var y = 0; y < frameSize.Height; y++)
			{
				// Decode the RLE byte stream
				var byteReader = new PcxRleByteReader(s);

				// Read indices of a given length out of the byte stream
				var indexReader = new PcxIndexReader(byteReader, (uint)bitsPerPixel);

				// Planes are stored consecutively for each scan line
				for (var plane = 0; plane < bitPlanes; plane++)
				{
					for (var x = 0; x < bytesPerLine; x++)
					{
						var index = indexReader.ReadIndex();

						// Account for padding bytes
						if (x < frameSize.Width)
							indexBuffer[x] = indexBuffer[x] | (index << (plane * bitsPerPixel));
					}
				}

				/*for (var x = 0; x < frameSize.Width; x++)
				{
					var index = indexBuffer[x];
					data[x] = (byte)palette[index].ToArgb();
				}*/
			}

			frames = new ISpriteFrame[1];
			for (var i = 0; i < frames.Length; i++)
			{
				System.Console.WriteLine(frameSize.Width + "x" + frameSize.Height);
				frames[i] = new PcxFrame()
				{
					Size = linePaddingSize,
					FrameSize = frameSize,
					Offset = float2.Zero,
					Data = data,
				};
			}

			return true;
		}

		private const int PcxRleMask = 0xC0;

		private abstract class PcxByteReader
		{
			public abstract byte ReadByte();
		}

		private abstract class PcxByteWriter
		{
			public abstract void WriteByte(byte value);
			public abstract void Flush();
		}

		private class PcxRawByteReader : PcxByteReader
		{
			private Stream m_stream;
			public PcxRawByteReader(Stream stream)
			{
				m_stream = stream;
			}
			public override byte ReadByte()
			{
				return (byte)m_stream.ReadByte();
			}
		}

		private class PcxRleByteWriter : PcxByteWriter
		{
			private Stream m_stream;
			private byte m_lastValue;
			private uint m_count = 0;

			public PcxRleByteWriter(Stream output)
			{
				m_stream = output;
			}

			public override void WriteByte(byte value)
			{
				if (m_count == 0 || m_count == 63 || value != m_lastValue)
				{
					Flush();

					m_lastValue = value;
					m_count = 1;
				}
				else
				{
					m_count++;
				}
			}

			public override void Flush()
			{
				if (m_count == 0)
					return;

				if ((m_count > 1) || ((m_count == 1) && ((m_lastValue & PcxRleMask) == PcxRleMask)))
				{
					m_stream.WriteByte((byte)(PcxRleMask | m_count));
					m_stream.WriteByte(m_lastValue);
					m_count = 0;
				}
				else
				{
					m_stream.WriteByte(m_lastValue);
					m_count = 0;
				}
			}


		}

		private class PcxRleByteReader : PcxByteReader
		{
			private Stream m_stream;
			private uint m_count = 0;
			private byte m_rleValue;

			public PcxRleByteReader(Stream input)
			{
				m_stream = input;
			}

			public override byte ReadByte()
			{
				if (m_count > 0)
				{
					m_count--;
					return m_rleValue;
				}

				byte code = (byte)m_stream.ReadByte();

				if ((code & PcxRleMask) == PcxRleMask)
				{
					m_count = (uint)(code & (PcxRleMask ^ 0xff));
					m_rleValue = (byte)m_stream.ReadByte();

					m_count--;
					return m_rleValue;
				}

				return code;
			}
		}

		private class PcxIndexReader
		{
			private PcxByteReader m_reader;
			private uint m_bitsPerPixel;
			private uint m_bitMask;

			private uint m_bitsRemaining = 0;
			private uint m_byteRead;

			public PcxIndexReader(PcxByteReader reader, uint bitsPerPixel)
			{
				if (!(bitsPerPixel == 1 || bitsPerPixel == 2 || bitsPerPixel == 4 || bitsPerPixel == 8))
					throw new InvalidDataException("bitsPerPixel must be 1, 2, 4 or 8");

				m_reader = reader;
				m_bitsPerPixel = bitsPerPixel;
				m_bitMask = (uint)((1 << (int)m_bitsPerPixel) - 1);
			}

			public uint ReadIndex()
			{
				// NOTE: This does not work for non-power-of-two bits per pixel (e.g. 6)
				// since it does not concatenate shift adjacent bytes together

				if (m_bitsRemaining == 0)
				{
					m_byteRead = m_reader.ReadByte();
					m_bitsRemaining = 8;
				}

				// NOTE: Reads from the most significant bits
				uint index = (m_byteRead >> (int)(8 - m_bitsPerPixel)) & m_bitMask;
				m_byteRead = m_byteRead << (int)m_bitsPerPixel;
				m_bitsRemaining -= m_bitsPerPixel;

				return index;
			}
		}
	}
}
