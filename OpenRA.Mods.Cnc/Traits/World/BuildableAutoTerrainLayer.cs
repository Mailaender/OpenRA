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

using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Attach this to the world actor. Required for `LaysAutoTerrain` to work.")]
	public class BuildableAutoTerrainLayerInfo : ITraitInfo
	{
		[Desc("The terrain type to place.")]
		[FieldLoader.Require] public readonly string TerrainType = null;

		public object Create(ActorInitializer init) { return new BuildableAutoTerrainLayer(init.Self, this); }
	}

	public class BuildableAutoTerrainLayer
	{
		public readonly CellLayer<bool> Terrain;

		readonly BuildableAutoTerrainLayerInfo info;
		readonly Map map;

		public BuildableAutoTerrainLayer(Actor self, BuildableAutoTerrainLayerInfo info)
		{
			this.info = info;
			map = self.World.Map;
			Terrain = new CellLayer<bool>(map);
		}

		public void Add(CPos cell)
		{
			Terrain[cell] = true;
			map.CustomTerrain[cell] = map.Rules.TileSet.GetTerrainIndex(info.TerrainType);
		}
	}
}
