#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Attach this to the player actor. Required for `LaysAutoTerrain` to work.")]
	public class BuildableAutoTerrainRendererInfo : ITraitInfo
	{
		[Desc("Palette to render the layer sprites in.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public object Create(ActorInitializer init) { return new BuildableAutoTerrainRenderer(init.Self, this); }
	}

	public class BuildableAutoTerrainRenderer : IRenderOverlay, IWorldLoaded, ITickRender, INotifyActorDisposing
	{
		readonly BuildableAutoTerrainRendererInfo info;
		readonly Dictionary<CPos, Sprite> dirty = new Dictionary<CPos, Sprite>();
		readonly World world;
		readonly BuildableAutoTerrainLayer layer;

		TerrainSpriteLayer render;
		Theater theater;

		public BuildableAutoTerrainRenderer(Actor self, BuildableAutoTerrainRendererInfo info)
		{
			this.info = info;

			world = self.World;
			layer = self.Trait<BuildableAutoTerrainLayer>();
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			theater = wr.Theater;
			System.Console.WriteLine("WOrldLoaded");
			render = new TerrainSpriteLayer(w, wr, theater.Sheet, BlendMode.Alpha, wr.Palette(info.Palette), wr.World.Type != WorldType.Editor);
		}

		[Flags]
		public enum ClearSides : byte
		{
			None = 0x0,
			Left = 0x1,
			Top = 0x2,
			Right = 0x4,
			Bottom = 0x8,

			All = 0xFF
		}

		public static readonly Dictionary<ClearSides, ushort> SpriteMap = new Dictionary<ClearSides, ushort>()
		{
			{ ClearSides.None, 671 },
			{ ClearSides.Right, 597 },
			{ ClearSides.Bottom, 598 },
			{ ClearSides.Right | ClearSides.Bottom, 599 },
			{ ClearSides.Left, 600 },
			{ ClearSides.Left | ClearSides.Right, 601 },
			{ ClearSides.Left | ClearSides.Bottom, 602 },
			{ ClearSides.Left | ClearSides.Right | ClearSides.Bottom, 603 },
			{ ClearSides.Top, 604 },
			{ ClearSides.Top | ClearSides.Right, 605 },
			{ ClearSides.Top | ClearSides.Bottom, 606 },
			{ ClearSides.Top | ClearSides.Right | ClearSides.Bottom, 607 },
			{ ClearSides.Top | ClearSides.Left, 608 },
			{ ClearSides.Top | ClearSides.Right | ClearSides.Left, 609 },
			{ ClearSides.Top | ClearSides.Left | ClearSides.Bottom, 609 },
			{ ClearSides.All, 611 },
		};

		ClearSides FindClearSides(CPos p)
		{
			var ret = ClearSides.None;
			if (!layer.Terrain[p + new CVec(0, -1)])
				ret |= ClearSides.Right;

			if (!layer.Terrain[p + new CVec(-1, 0)])
				ret |= ClearSides.Top;

			if (!layer.Terrain[p + new CVec(1, 0)])
				ret |= ClearSides.Bottom;

			if (!layer.Terrain[p + new CVec(0, 1)])
				ret |= ClearSides.Left;

			return ret;
		}

		public void Add(CPos cell)
		{
			UpdateRenderedSprite(cell);
			foreach (var direction in CVec.Directions)
			{
				var neighbor = direction + cell;
				UpdateRenderedSprite(neighbor);
			}
		}

		void UpdateRenderedSprite(CPos cell)
		{
			if (!layer.Terrain[cell])
				return;

			var viewer = world.RenderPlayer ?? world.LocalPlayer;
			if (!viewer.Shroud.IsVisible(cell))
				return;

			var clear = FindClearSides(cell);
			ushort tile;
			SpriteMap.TryGetValue(clear, out tile);

			var template = world.Map.Rules.TileSet.Templates[tile];
			var index = Game.CosmeticRandom.Next(template.TilesCount);

			var s = theater.TileSprite(new TerrainTile(template.Id, (byte)index));
			var offset = new float3(0, 0, -6);
			dirty[cell] = new Sprite(s.Sheet, s.Bounds, 1, s.Offset + offset, s.Channel, s.BlendMode);
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			var remove = new List<CPos>();
			foreach (var kv in dirty)
			{
				if (!self.World.FogObscures(kv.Key))
				{
					render.Update(kv.Key, kv.Value);
					remove.Add(kv.Key);
				}
			}

			foreach (var r in remove)
				dirty.Remove(r);
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			render.Draw(wr.Viewport);
		}

		bool disposed;
		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed || render == null)
				return;

			render.Dispose();
			disposed = true;
		}
	}
}
