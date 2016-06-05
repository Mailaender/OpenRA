#region Copyright & License Information
/*
 * Copyright 2007-2016 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	[Desc("Attach this to the world actor.", "Order of the layers defines the Z sorting.")]
	public class TerrainAnimationLayerInfo : ITraitInfo
	{
		[Desc("Palette to render the layer sprites in.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public object Create(ActorInitializer init) { return new TerrainAnimationLayer(init.Self, this); }
	}

	public class TerrainAnimationLayer : IRender, ITick
	{
		readonly TerrainAnimationLayerInfo info;
		readonly Animation overlay;

		public TerrainAnimationLayer(Actor self, TerrainAnimationLayerInfo info)
		{
			this.info = info;

			overlay = new Animation(self.World, "wa01x");
			overlay.PlayRepeating("idle");
		}

		public void Tick(Actor self)
		{
			overlay.Tick();
		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			var map = self.World.Map;
			foreach (var cell in map.AllCells.Where(cell => map.Tiles[cell].Type == 435))
				return overlay.Render(map.CenterOfCell(cell), wr.Palette(info.Palette));

			return SpriteRenderable.None;
		}
	}
}
