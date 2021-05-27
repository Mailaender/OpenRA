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
using Concentus.Structs;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.AudioLoaders
{
	public class OpusLoader : ISoundLoader
	{
		bool ISoundLoader.TryParseSound(Stream stream, out ISoundFormat sound)
		{
			try
			{
				sound = new OpusFormat(stream);
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

	public sealed class OpusFormat : ISoundFormat
	{
		public int SampleBits => 16;
		public int Channels => decoder.NumChannels;
		public int SampleRate => decoder.SampleRate;
		public float LengthInSeconds => 2.8f;
		public Stream GetPCMInputStream()
		{
			var audioStream = SegmentStream.CreateWithoutOwningStream(stream, 0, (int)stream.Length);
			return new OpusStream(audioStream);
		}

		public void Dispose() { stream.Dispose(); }

		readonly Stream stream;
		readonly OpusDecoder decoder;

		public OpusFormat(Stream stream)
		{
			this.stream = stream;
			decoder = new OpusDecoder(16000, 1);
		}

		public class OpusStream : ReadOnlyAdapterStream
		{
			public OpusStream(Stream stream)
				: base(stream) { }

			protected override bool BufferData(Stream baseStream, Queue<byte> data)
			{
				var decoder = OpusDecoder.Create(16000, 1);
				var pcm = new short[baseStream.Length];
				decoder.Decode(baseStream.ReadAllBytes(), 0, (int)baseStream.Length, pcm, 0, 0);

				for (var i = 0; i < pcm.Length; i++)
				{
					var bytes = BitConverter.GetBytes(pcm[i]);
					foreach (var b in bytes)
						data.Enqueue(b);
				}

				return true;
			}
		}
	}
}
