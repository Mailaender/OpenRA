#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
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

			var max = info.MaxStrength;
			strengthPerTile = new Dictionary<ushort, int>
			{
				// Ice 01
				{ 439, max },

				{ 440, max / 2 }, { 441, max / 2 }, { 442, max / 2 }, { 443, max / 2 }, { 444, max / 2 }, { 445, max / 2 }, { 446, max / 2 }, { 447, max / 2 },
				{ 448, max / 2 }, { 449, max / 2 }, { 450, max / 2 }, { 451, max / 2 }, { 452, max / 2 }, { 453, max / 2 }, { 454, max / 2 }, { 455, max / 2 },

				{ 456, max / 4 }, { 457, max / 4 }, { 458, max / 4 }, { 459, max / 4 }, { 460, max / 4 }, { 461, max / 4 }, { 462, max / 4 }, { 463, max / 4 },
				{ 464, max / 4 }, { 465, max / 4 }, { 466, max / 4 }, { 467, max / 4 }, { 468, max / 4 }, { 469, max / 4 }, { 470, max / 4 }, { 471, max / 4 },
				{ 472, max / 4 }, { 473, max / 4 }, { 474, max / 4 }, { 475, max / 4 }, { 476, max / 4 }, { 477, max / 4 }, { 478, max / 4 }, { 479, max / 4 },
				{ 480, max / 4 }, { 481, max / 4 }, { 482, max / 4 }, { 483, max / 4 }, { 484, max / 4 }, { 485, max / 4 }, { 486, max / 4 },

				{ 487, max / 8 }, { 488, max / 8 }, { 489, max / 8 }, { 490, max / 8 }, { 491, max / 8 }, { 492, max / 8 }, { 493, max / 8 }, { 494, max / 8 },
				{ 495, max / 8 }, { 496, max / 8 }, { 497, max / 8 }, { 498, max / 8 }, { 499, max / 8 }, { 500, max / 8 }, { 501, max / 8 },

				{ 502, 0 },

				// Ice 02
				{ 503, max },

				{ 504, max / 2 }, { 505, max / 2 }, { 506, max / 2 }, { 507, max / 2 }, { 508, max / 2 }, { 509, max / 2 }, { 510, max / 2 }, { 511, max / 2 },
				{ 512, max / 2 }, { 513, max / 2 }, { 514, max / 2 }, { 515, max / 2 }, { 516, max / 2 }, { 517, max / 2 }, { 518, max / 2 }, { 519, max / 2 },

				{ 520, max / 4 }, { 521, max / 4 }, { 522, max / 4 }, { 523, max / 4 }, { 524, max / 4 }, { 525, max / 4 }, { 526, max / 4 }, { 527, max / 4 },
				{ 528, max / 4 }, { 529, max / 4 }, { 530, max / 4 }, { 531, max / 4 }, { 532, max / 4 }, { 533, max / 4 }, { 534, max / 4 }, { 535, max / 4 },
				{ 536, max / 4 }, { 537, max / 4 }, { 538, max / 4 }, { 539, max / 4 }, { 540, max / 4 }, { 541, max / 4 }, { 542, max / 4 }, { 543, max / 4 },
				{ 544, max / 4 }, { 545, max / 4 }, { 546, max / 4 }, { 547, max / 4 }, { 548, max / 4 }, { 549, max / 4 }, { 550, max / 4 },

				{ 551, max / 8 }, { 552, max / 8 }, { 553, max / 8 }, { 554, max / 8 }, { 555, max / 8 }, { 556, max / 8 }, { 557, max / 8 }, { 558, max / 8 },
				{ 559, max / 8 }, { 560, max / 8 }, { 561, max / 8 }, { 562, max / 8 }, { 563, max / 8 }, { 564, max / 8 }, { 565, max / 8 },

				{ 566, 0 },

				// Ice 03
				{ 567, max },

				{ 568, max / 2 }, { 569, max / 2 }, { 570, max / 2 }, { 571, max / 2 }, { 572, max / 2 }, { 573, max / 2 }, { 574, max / 2 }, { 575, max / 2 },
				{ 576, max / 2 }, { 577, max / 2 }, { 578, max / 2 }, { 579, max / 2 }, { 580, max / 2 }, { 581, max / 2 }, { 582, max / 2 }, { 583, max / 2 },

				{ 584, max / 4 }, { 585, max / 4 }, { 586, max / 4 }, { 587, max / 4 }, { 588, max / 4 }, { 589, max / 4 }, { 590, max / 4 }, { 591, max / 4 },
				{ 592, max / 4 }, { 593, max / 4 }, { 594, max / 4 }, { 595, max / 4 }, { 596, max / 4 }, { 597, max / 4 }, { 598, max / 4 }, { 599, max / 4 },
				{ 600, max / 4 }, { 601, max / 4 }, { 602, max / 4 }, { 603, max / 4 }, { 604, max / 4 }, { 605, max / 4 }, { 606, max / 4 }, { 607, max / 4 },
				{ 608, max / 4 }, { 609, max / 4 }, { 610, max / 4 }, { 611, max / 4 }, { 612, max / 4 }, { 613, max / 4 }, { 614, max / 4 },

				{ 615, max / 8 }, { 616, max / 8 }, { 617, max / 8 }, { 618, max / 8 }, { 619, max / 8 }, { 620, max / 8 }, { 621, max / 8 }, { 622, max / 8 },
				{ 623, max / 8 }, { 624, max / 8 }, { 625, max / 8 }, { 626, max / 8 }, { 627, max / 8 }, { 628, max / 8 }, { 629, max / 8 },

				{ 630, 0 }
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
			else if (strength <= 0)
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
					var strength = strengthPerTile[template.Id];
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
