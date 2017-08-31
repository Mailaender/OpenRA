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

using System.Collections.Generic;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Lays isometric terrain whose sprite is picked by it's neighbours.")]
	public class LaysAutoTerrainInfo : ITraitInfo, Requires<BuildingInfo>
	{
		[FieldLoader.Require]
		[Desc("The terrain types that this template will be placed on.")]
		public readonly HashSet<string> TerrainTypes = new HashSet<string>();

		[Desc("Offset relative to the actor TopLeft. Not used if the template is PickAny.",
			"Tiles being offset out of the actor's footprint will not be placed.")]
		public readonly CVec Offset = CVec.Zero;

		public object Create(ActorInitializer init) { return new LaysAutoTerrain(init.Self, this); }
	}

	public class LaysAutoTerrain : INotifyAddedToWorld
	{
		readonly LaysAutoTerrainInfo info;
		readonly BuildableAutoTerrainLayer layer;
		readonly BuildingInfluence bi;
		readonly BuildingInfo buildingInfo;
		readonly Map map;

		public LaysAutoTerrain(Actor self, LaysAutoTerrainInfo info)
		{
			map = self.World.Map;
			this.info = info;
			layer = self.World.WorldActor.Trait<BuildableAutoTerrainLayer>();
			bi = self.World.WorldActor.Trait<BuildingInfluence>();
			buildingInfo = self.Info.TraitInfo<BuildingInfo>();
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			foreach (var c in buildingInfo.Tiles(self.Location))
			{
				// Only place on allowed terrain types
				if (!map.Contains(c) || !info.TerrainTypes.Contains(map.GetTerrainInfo(c).Type))
					continue;

				// Don't place under other buildings or custom terrain
				if (bi.GetBuildingAt(c) != self || map.CustomTerrain[c] != byte.MaxValue)
					continue;

				layer.Add(c);
			}
		}
	}
}
