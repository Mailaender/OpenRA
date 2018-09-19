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

using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Attach this to the world actor. Required for `LaysAutoTerrain` to work.")]
	public class BuildableAutoTerrainLayerInfo : ITraitInfo
	{
		[Desc("The terrain type to place.")]
		[FieldLoader.Require] public readonly string TerrainType = null;

		[Desc("Palette to render the layer sprites in.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public object Create(ActorInitializer init) { return new BuildableAutoTerrainLayer(init.Self, this); }
	}

	public class BuildableAutoTerrainLayer : IWorldLoaded, INotifyActorDisposing
	{
		public readonly CellLayer<bool> Terrain;

		readonly BuildableAutoTerrainLayerInfo info;
		readonly Map map;

		public TerrainSpriteLayer Render;
		public Theater Theater;

		public BuildableAutoTerrainLayer(Actor self, BuildableAutoTerrainLayerInfo info)
		{
			this.info = info;
			map = self.World.Map;
			Terrain = new CellLayer<bool>(map);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			Theater = wr.Theater;
			System.Console.WriteLine("WOrldLoaded");
			Render = new TerrainSpriteLayer(w, wr, Theater.Sheet, BlendMode.Alpha, wr.Palette(info.Palette), wr.World.Type != WorldType.Editor);
		}

		public void Add(CPos cell)
		{
			Terrain[cell] = true;
			map.CustomTerrain[cell] = map.Rules.TileSet.GetTerrainIndex(info.TerrainType);
		}

		bool disposed;
		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			Render.Dispose();
			disposed = true;
		}
	}
}
