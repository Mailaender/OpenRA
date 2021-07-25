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
using ICSharpCode.SharpZipLib.Lzw;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.SpriteLoaders
{
	public class GifLoader : ISpriteLoader
	{
		[Flags]
		enum BlockTypes
		{
			ImageDescriptor = 0x2C,
			ExtensionBlock = 0x21,
			GraphicExtensionBlock = 0xF9,
			ApplicationExtensionBlock = 0xFF,
			CommentExtensionBlock = 0xFE,
			PlainTextExtension = 0x01,
			Trailer = 0x3B
		}

		[Flags]
		enum ImageFlag
		{
			Interlaced = 0x40,
			ColorTable = 0x80,
			TableSizeMask = 0x07,
			BitDepthMask = 0x70,
		}

		[Flags]
		enum ControlFlags
		{
			HasTransparency = 0x01,
			DisposalMask = 0x0C
		}

		static readonly int[] PowerOfTwo = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };

		public class GifFrame : ISpriteFrame
		{
			public SpriteFrameType Type => SpriteFrameType.Indexed8;
			public Color[] LocalColorTable { get; private set; }
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding => false;
			public uint TransparentIndex;

			public GifFrame(Stream s, Size size, Color[] globalColorTable)
			{
				var imageLeft = s.ReadUInt16();
				var imageTop = s.ReadUInt16();
				var imageWidth = s.ReadUInt16();
				var imageHeight = s.ReadUInt16();

				var flags = (ImageFlag)s.ReadByte();
				if (flags.HasFlag(ImageFlag.ColorTable))
					LocalColorTable = ReadColorTable(s, flags);
				else
					LocalColorTable = globalColorTable;

				var count = imageWidth * imageHeight;
				var data = new byte[count];
				var offset = 0;

				s.ReadByte(); // lzwMinimumCodeSize
				s.ReadByte(); // blockSize
				using (var lzwStream = new LzwInputStream(s))
				{
					while (true)
					{
						var length = s.ReadUInt8();
						if (length == 0)
							break;

						lzwStream.Read(data, offset, length);
						offset += length;
					}
				}

				if (flags.HasFlag(ImageFlag.Interlaced))
				{
					var interlacedData = data;

					var offsets = new int[] { 0, 4, 2, 1 };
					var steps = new int[] { 8, 8, 4, 2 };

					var j = 0;
					for (var pass = 0; pass < 4; pass++)
					{
						for (var i = offsets[pass]; i < interlacedData.Length; i += steps[pass])
						{
							data[i] = interlacedData[j];
							j++;
						}
					}
				}

				Size = size;
				FrameSize = new Size(imageWidth, imageHeight);
				Offset = new float2(imageLeft, imageTop);
				Data = data;
			}
		}

		static bool IsGif(Stream s)
		{
			var start = s.Position;

			var a = s.ReadASCII(6);

			s.Position = start;
			return a.StartsWith("GIF");
		}

		GifFrame[] ParseFrames(Stream s)
		{
			s.Position += 3; // GIF
			var version = s.ReadASCII(3);
			if (version != "87a" && version != "89a")
				throw new InvalidDataException($"{version} is not a a valid version.");

			var width = s.ReadUInt16();
			var height = s.ReadUInt16();
			var size = new Size(width, height);

			var flags = (ImageFlag)s.ReadByte();
			s.ReadByte(); // backgroundColorIndex
			s.ReadByte(); // pixelAspectRatio

			Color[] globalColorTable = null;
			if (flags.HasFlag(ImageFlag.ColorTable))
				globalColorTable = ReadColorTable(s, flags);

			var frames = new List<GifFrame>();

			while (s.CanRead)
			{
				var block = s.ReadUInt8();

				switch ((BlockTypes)block)
				{
					case BlockTypes.ExtensionBlock:
						var type = (BlockTypes)s.ReadUInt8();
						switch (type)
						{
							case BlockTypes.GraphicExtensionBlock:
								var blockSize = s.ReadByte();
								if (blockSize != 0x04)
									throw new InvalidDataException($"Invalid extension block size {blockSize}");
								var controlFlags = (ControlFlags)s.ReadByte();
								s.ReadUInt16(); // delay
								var transparentColor = s.ReadByte();
								s.ReadByte(); // terminator (0x00)

								if (controlFlags.HasFlag(ControlFlags.HasTransparency))
									TransparentIndex = transparentColor;
								else
									TransparentIndex = NoTransparency;

								// dispose of current image

								switch( (Disposal)( controlFlags & ControlFlags.DisposalMask ) )
								{
									default:
									case Disposal.None:
									case Disposal.DoNotDispose:
										// remember current image in case we need to "return to previous"
										PreviousImage = Output;
										break;

									case Disposal.RestoreBackground:
										// empty image - don't track
										Output = new Color32[ Width * Height ];
										break;

									case Disposal.ReturnToPrevious:

										// return to previous image

										Output = new Color32[ Width * Height ];

										if( PreviousImage != null )
										{
											Array.Copy( PreviousImage, Output, Output.Length );
										}

										break;
								}
							default:
								SkipBlocks(s);
								break;
						}

						break;
					case BlockTypes.ImageDescriptor:
						frames.Add(new GifFrame(s, size, globalColorTable));
						break;
					case BlockTypes.Trailer:
						return frames.ToArray();
					default:
						throw new InvalidDataException($"Unknown block {block} at {s.Position}");
				}
			}

			return frames.ToArray();
		}

		static Color[] ReadColorTable(Stream s, ImageFlag flags)
		{
			var tableSize = PowerOfTwo[(int)(flags & ImageFlag.TableSizeMask) + 1];
			var palette = new Color[tableSize];
			for (var i = 0; i < palette.Length; i++)
			{
				var r = s.ReadByte(); var g = s.ReadByte(); var b = s.ReadByte();
				palette[i] = Color.FromArgb(r, g, b);
			}

			return palette;
		}

		void ReadExtensionBlock(Stream s)
		{

		}

		static void SkipBlocks(Stream s)
		{
			var blockSize = s.ReadUInt8();
			while (blockSize != 0x00)
			{
				s.Position += blockSize;
				blockSize = s.ReadUInt8();
			}
		}

		public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			var start = s.Position;

			metadata = null;

			if (!IsGif(s))
			{
				frames = null;
				return false;
			}

			frames = ParseFrames(s);
			System.Console.WriteLine("frames: " + frames.Length);
			// TODO
			//if (palettes.Any())
			//	metadata = new TypeDictionary { new EmbeddedSpritePalette(framePalettes: palettes) };

			s.Position = start;

			return true;
		}
	}
}
