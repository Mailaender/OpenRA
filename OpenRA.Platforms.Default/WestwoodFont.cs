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
using System.Linq;
using System.Text;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	public sealed class WestwoodFont : IFont
	{
		private const int BitsPerPixel = 4;

		FontGlyph[] glyphs;

		public WestwoodFont(byte[] data)
		{
			LoadV3V4Font(data);
		}

		int GetMinimumStride(int width, int bitsLength)
        {
            return ((bitsLength * width) + 7) / 8;
        }

		byte[] ConvertTo8Bit(byte[] fileData, int width, int height, int start, int bitsLength, bool bigEndian)
        {
            var stride = GetMinimumStride(width, bitsLength);
            return ConvertTo8Bit(fileData, width, height, start, bitsLength, bigEndian, ref stride);
        }

		byte[] ConvertTo8Bit(byte[] fileData, int width, int height, int start, int bitsLength, bool bigEndian, ref int stride)
        {
            if (bitsLength != 1 && bitsLength != 2 && bitsLength != 4 && bitsLength != 8)
                throw new ArgumentOutOfRangeException("Cannot handle image data with " + bitsLength + " bits per pixel.");

            var data8bit = new byte[width * height];
            var parts = 8 / bitsLength;
            var newStride = width;
            var bitmask = (1 << bitsLength) - 1;
            var size = stride * height;
            if (start + size > fileData.Length)
                throw new IndexOutOfRangeException("Data exceeds array bounds!");

            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    var indexXbit = start + y * stride + x / parts;
                    var index8bit = y * newStride + x;
                    var shift = x % parts * bitsLength;
                    if (bigEndian)
                        shift = 8 - shift - bitsLength;

                    data8bit[index8bit] = (byte)((fileData[indexXbit] >> shift) & bitmask);
                }
            }

            stride = newStride;
            return data8bit;
        }

		void LoadV3V4Font(byte[] data)
		{
			var fileLength = data.Length;
			if (fileLength < 0x14)
                throw new InvalidDataException("File data too short to contain a header.");

			using (var s = new MemoryStream(data))
			{
				var fileLSizeHeader = s.ReadUInt16();
				if (fileLSizeHeader != fileLength)
               		throw new InvalidDataException("File size value in header does not match file data length.");

				var dataFormat = s.ReadByte();
				var unknown3 = s.ReadByte();
				var unknown4 = s.ReadUInt16();
				var fontDataOffsetsListOffset = s.ReadUInt16();
				var widthsListOffset = s.ReadUInt16();
				var fontDataOffset = s.ReadUInt16();
				var heightsListOffset = s.ReadUInt16();

				var unknown5 = s.ReadUInt16();
				var alwaysZero = s.ReadByte();
				if (alwaysZero > 0)
					throw new InvalidDataException("Unexpected value {0}".F(alwaysZero));

				var length = 0;

				var isVersion4 = dataFormat == 0x02;
				var isVersion3 = dataFormat == 0x00;
				if (isVersion4)
				{
					// "last symbol" byte 0x11 is not filled in on TS fonts, so instead, calculate it from the header offsets. Sort by offset and take the lowest two.
					var headerVals = new int[] { fontDataOffsetsListOffset, widthsListOffset, fontDataOffset, heightsListOffset }.OrderBy(n => n).Take(2).ToArray();

					// The difference between these two, divided by the item length in that particular list, is the amount of symbols.
					var divval = 1;
					if (headerVals[0] == fontDataOffsetsListOffset || headerVals[0] == heightsListOffset)
						divval = 2;

					length = (headerVals[1] - headerVals[0]) / divval;
				}
				else if (isVersion3)
                	length = data[0x11] + 1; // "last symbol" byte, so actual amount is this value + 1.
				else
					throw new InvalidDataException("Unknown font type identifier: '{0}'.".F(dataFormat));

				var fontHeight = s.ReadByte();
				var fontWidth = s.ReadByte();

				if (fontDataOffsetsListOffset + length * 2 > fileLength)
                	throw new InvalidDataException("File data too short for offsets list!");
				if (widthsListOffset + length > fileLength)
                	throw new InvalidDataException("File data too short for symbol widths list starting from offset!");
				if (heightsListOffset + length * 2 > fileLength)
                	throw new InvalidDataException("File data too short for symbol heights list!");

				var fontDataOffsetsList = new int[length];
				s.Seek(fontDataOffsetsListOffset, SeekOrigin.Begin);
				for (var i = 0; i < length; ++i)
                	fontDataOffsetsList[i] = s.ReadUInt16() + (isVersion4 ? fontDataOffset : 0);

				var widthsList = new List<int>();
				s.Seek(widthsListOffset, SeekOrigin.Begin);
				for (var i = 0; i < length; ++i)
				{
					var width = s.ReadByte();
					widthsList.Add(width);
            	}

				var yOffsetsList = new List<int>();
				var heightsList = new List<int>();
				s.Seek(heightsListOffset, SeekOrigin.Begin);
				for (var i = 0; i < length; ++i)
				{
					yOffsetsList.Add(s.ReadByte());
					var height = s.ReadByte();
					heightsList.Add(height);
				}

				var imageData = new List<FontGlyph>();
				var bitsLength = BitsPerPixel;
				for (var i = 0; i < length; ++i)
				{
					var start = fontDataOffsetsList[i];
					var width = widthsList[i];
					var height = heightsList[i];
					var data8Bit = new byte[0];
					try
					{
						data8Bit = ConvertTo8Bit(data, width, height, start, bitsLength, false);
					}
					catch (IndexOutOfRangeException)
					{
						throw new IndexOutOfRangeException("Data for font entry #{0} exceeds file bounds!".F(i));
					}

					var glyph = new FontGlyph
					{
						Offset = new int2(0, 0),
						Size = new Size(width, height),
						Advance = width,
						Data = data8Bit
					};

					imageData.Add(glyph);
				}

				glyphs = imageData.ToArray();
			}
		}

		static readonly FontGlyph EmptyGlyph = new FontGlyph
		{
			Offset = int2.Zero,
			Size = new Size(0, 0),
			Advance = 0,
			Data = null
		};

		public FontGlyph CreateGlyph(char c, int size, float deviceScale)
		{
			var dos472 = Encoding.GetEncoding(437);
			var charIndex = dos472.GetBytes(c.ToString())[0];
			if (charIndex > glyphs.Length)
			{
				System.Console.WriteLine("to large glyph " + charIndex);
				return EmptyGlyph;
			}
			else
				return glyphs[charIndex];
		}

		public void Dispose() { }
	}
}
