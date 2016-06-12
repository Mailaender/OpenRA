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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	[Desc("Attach this to the world layer for regrowable ice terrain.")]
	class IceLayerInfo : ITraitInfo
	{
		[Desc("Tileset IDs where the trait is activated.")]
		public readonly string[] Tilesets = { "SNOW" };

		public readonly string ImpassableTerrainType = "Water";

		public readonly string MaxStrengthTerrainType = "Ice";

		public readonly string HalfStrengthTerrainType = "Cracked";

		public int MaxStrength = 1024;

		[Desc("Measured in game ticks")]
		public int GrowthRate = 10;

		public object Create(ActorInitializer init) { return new IceLayer(init.Self, this); }
	}

	class IceLayer : ITick, IWorldLoaded
	{
		readonly IceLayerInfo info;

		public readonly CellLayer<int> Strength;

		Dictionary<ushort, int> strengthPerTile;
		List<CPos> iceCells = new List<CPos>();

		int growthTicks;
		bool initialIceLoaded;

		public IceLayer(Actor self, IceLayerInfo info)
		{
			this.info = info;

			if (!info.Tilesets.Contains(self.World.Map.Tileset))
				return;

			strengthPerTile = new Dictionary<ushort, int>
			{
				// Ice 01
				{ 439, 1 },

				{ 440, 2 }, { 441, 2 }, { 442, 2 }, { 443, 2 }, { 444, 2 }, { 445, 2 }, { 446, 2 }, { 447, 2 },
				{ 448, 2 }, { 449, 2 }, { 450, 2 }, { 451, 2 }, { 452, 2 }, { 453, 2 }, { 454, 2 }, { 455, 2 },

				{ 456, 4 }, { 457, 4 }, { 458, 4 }, { 459, 4 }, { 460, 4 }, { 461, 4 }, { 462, 4 }, { 463, 4 },
				{ 464, 4 }, { 465, 4 }, { 466, 4 }, { 467, 4 }, { 468, 4 }, { 469, 4 }, { 470, 4 }, { 471, 4 },
				{ 472, 4 }, { 473, 4 }, { 474, 4 }, { 475, 4 }, { 476, 4 }, { 477, 4 }, { 478, 4 }, { 479, 4 },
				{ 480, 4 }, { 481, 4 }, { 482, 4 }, { 483, 4 }, { 484, 4 }, { 485, 4 }, { 486, 4 },

				{ 487, 8 }, { 488, 8 }, { 489, 8 }, { 490, 8 }, { 491, 8 }, { 492, 8 }, { 493, 8 }, { 494, 8 },
				{ 495, 8 }, { 496, 8 }, { 497, 8 }, { 498, 8 }, { 499, 8 }, { 500, 8 }, { 501, 8 },

				{ 502, 16 },

				// Ice 02
				{ 503, 1 },

				{ 504, 2 }, { 505, 2 }, { 506, 2 }, { 507, 2 }, { 508, 2 }, { 509, 2 }, { 510, 2 }, { 511, 2 },
				{ 512, 2 }, { 513, 2 }, { 514, 2 }, { 515, 2 }, { 516, 2 }, { 517, 2 }, { 518, 2 }, { 519, 2 },

				{ 520, 4 }, { 521, 4 }, { 522, 4 }, { 523, 4 }, { 524, 4 }, { 525, 4 }, { 526, 4 }, { 527, 4 },
				{ 528, 4 }, { 529, 4 }, { 530, 4 }, { 531, 4 }, { 532, 4 }, { 533, 4 }, { 534, 4 }, { 535, 4 },
				{ 536, 4 }, { 537, 4 }, { 538, 4 }, { 539, 4 }, { 540, 4 }, { 541, 4 }, { 542, 4 }, { 543, 4 },
				{ 544, 4 }, { 545, 4 }, { 546, 4 }, { 547, 4 }, { 548, 4 }, { 549, 4 }, { 550, 4 },

				{ 551, 8 }, { 552, 8 }, { 553, 8 }, { 554, 8 }, { 555, 8 }, { 556, 8 }, { 557, 8 }, { 558, 8 },
				{ 559, 8 }, { 560, 8 }, { 561, 8 }, { 562, 8 }, { 563, 8 }, { 564, 8 }, { 565, 8 },

				{ 566, 16 },

				// Ice 03
				{ 567, 1 },

				{ 568, 2 }, { 569, 2 }, { 570, 2 }, { 571, 2 }, { 572, 2 }, { 573, 2 }, { 574, 2 }, { 575, 2 },
				{ 576, 2 }, { 577, 2 }, { 578, 2 }, { 579, 2 }, { 580, 2 }, { 581, 2 }, { 582, 2 }, { 583, 2 },

				{ 584, 4 }, { 585, 4 }, { 586, 4 }, { 587, 4 }, { 588, 4 }, { 589, 4 }, { 590, 4 }, { 591, 4 },
				{ 592, 4 }, { 593, 4 }, { 594, 4 }, { 595, 4 }, { 596, 4 }, { 597, 4 }, { 598, 4 }, { 599, 4 },
				{ 600, 4 }, { 601, 4 }, { 602, 4 }, { 603, 4 }, { 604, 4 }, { 605, 4 }, { 606, 4 }, { 607, 4 },
				{ 608, 4 }, { 609, 4 }, { 610, 4 }, { 611, 4 }, { 612, 4 }, { 613, 4 }, { 614, 4 },

				{ 615, 8 }, { 616, 8 }, { 617, 8 }, { 618, 8 }, { 619, 8 }, { 620, 8 }, { 621, 8 }, { 622, 8 },
				{ 623, 8 }, { 624, 8 }, { 625, 8 }, { 626, 8 }, { 627, 8 }, { 628, 8 }, { 629, 8 },

				{ 630, 16 }
			};

			growthTicks = info.GrowthRate;

			Strength = new CellLayer<int>(self.World.Map);
		}

		void UpdateCell(World world, CPos cell)
		{
			var strength = Strength[cell];
			if (strength >= info.MaxStrength)
				world.Map.CustomTerrain[cell] = world.Map.Rules.TileSet.GetTerrainIndex(info.MaxStrengthTerrainType);
			else if (strength >= info.MaxStrength / 2)
				world.Map.CustomTerrain[cell] = world.Map.Rules.TileSet.GetTerrainIndex(info.HalfStrengthTerrainType);
			else if (strength <= info.MaxStrength / 16)
				world.Map.CustomTerrain[cell] = world.Map.Rules.TileSet.GetTerrainIndex(info.ImpassableTerrainType);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			if (!info.Tilesets.Contains(w.Map.Tileset))
				return;

			foreach (var cell in w.Map.AllCells)
			{
				var tile = w.Map.Tiles[cell];
				var template = w.Map.Rules.TileSet.Templates[tile.Type];
				if (strengthPerTile.ContainsKey(template.Id))
				{
					iceCells.Add(cell);
					var factor = strengthPerTile[template.Id];
					var strength = Game.CosmeticRandom.Next(info.MaxStrength / factor / 2, info.MaxStrength / factor);
					Strength[cell] = strength;
					UpdateCell(w, cell);
				}
			}

			initialIceLoaded = true;
		}

		void ITick.Tick(Actor self)
		{
			if (!info.Tilesets.Contains(self.World.Map.Tileset))
				return;

			if (!initialIceLoaded)
				return;

			if (--growthTicks <= 0)
			{
				foreach (var cell in iceCells)
				{
					var strength = Strength[cell];
					if (strength >= info.MaxStrength)
						continue;

					Strength[cell] = strength + 1;
					UpdateCell(self.World, cell);
				}

				growthTicks = info.GrowthRate;
			}
		}
	}
}
