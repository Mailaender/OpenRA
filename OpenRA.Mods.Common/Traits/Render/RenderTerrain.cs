#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	[Desc("Render terrain individually.")]
	public class RenderTerrainInfo : ITraitInfo, Requires<BuildingInfo>
	{
		[Desc("TODO")]
		public readonly ushort Template;

		[PaletteReference] public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public virtual object Create(ActorInitializer init) { return new RenderTerrain(init.Self, this); }

		/*public IEnumerable<IActorPreview> RenderPreview(ActorPreviewInitializer init)
		{

		}*/
	}

	public class RenderTerrain : IRender, IAutoSelectionSize
	{
		readonly RenderTerrainInfo info;
		readonly TerrainTemplateInfo template;
		readonly MapGrid grid;
		readonly BuildingInfo buildingInfo;

		public RenderTerrain(Actor self, RenderTerrainInfo info)
		{
			this.info = info;
			template = self.World.Map.Rules.TileSet.Templates.First(t => t.Value.Id == info.Template).Value;
			grid = Game.ModData.Manifest.Get<MapGrid>();
			buildingInfo = self.Info.TraitInfo<BuildingInfo>();
		}

		public IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr)
		{
			var i = 0;
			var tileset = self.World.Map.Rules.TileSet;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)(i++));
					var tileInfo = tileset.GetTileInfo(tile);

					// Empty tile
					if (tileInfo == null)
						continue;

					var offset = buildingInfo.CenterOffset(self.World).Y + 1024;
					var palette = template.Palette ?? info.Palette;

					yield return new SpriteRenderable(wr.Theater.TileSprite(tile),
					                                  wr.World.Map.CenterOfCell(self.Location), WVec.Zero, -offset, wr.Palette(palette), 1f, true);
				}
			}
		}


		int2 IAutoSelectionSize.SelectionSize(Actor self)
		{
			return new int2(template.Size.X * grid.TileSize.Width, template.Size.Y * grid.TileSize.Height);
		}
	}
}
