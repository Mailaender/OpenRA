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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	[Desc("Allows to play animations on resources.", "Attach this to the world actor.")]
	public class WithResourceAnimationInfo : TraitInfo, Requires<IResourceLayerInfo>
	{
		[FieldLoader.Require]
		[Desc("Resource types to animate.")]
		public readonly HashSet<string> Types = null;

		[Desc("The percentage of resource cells to play the animation on.", "Use two values to randomize between them.")]
		public readonly int[] Ratio = { 1, 5 };

		[Desc("Tick interval between two animation spawning.", "Use two values to randomize between them.")]
		public readonly int[] Interval = { 200, 500 };

		[FieldLoader.Require]
		[Desc("Animation image.")]
		public readonly string Image = null;

		[SequenceReference(nameof(Image))]
		[Desc("Randomly select one of these sequences to render.")]
		public readonly string[] Sequences = new string[] { "idle" };

		[PaletteReference]
		[Desc("Animation palette.")]
		public readonly string Palette = null;

		public override object Create(ActorInitializer init) { return new WithResourceAnimation(init.Self, this); }
	}

	class WithResourceAnimation : IWorldLoaded, ITick
	{
		readonly WithResourceAnimationInfo info;
		readonly World world;

		WorldRenderer worldRenderer;
		IResourceRenderer resourceRenderer;

		int ticks;

		public WithResourceAnimation(Actor self, WithResourceAnimationInfo info)
		{
			world = self.World;
			this.info = info;

			ticks = Common.Util.RandomDelay(self.World, info.Interval);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			worldRenderer = wr;

			var resourceRenderers = w.WorldActor.TraitsImplementing<IResourceRenderer>().ToArray();
			resourceRenderer = resourceRenderers.First(r => r.ResourceTypes.Intersect(info.Types).Any());
		}

		void ITick.Tick(Actor self)
		{
			if (--ticks > 0)
				return;

			var cells = new HashSet<CPos>();
			foreach (var uv in worldRenderer.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				var cell = uv.ToCPos(world.Map);
				var type = resourceRenderer.GetRenderedResourceType(cell);
				if (type != null && info.Types.Contains(type))
					cells.Add(cell);
			}

			var cellsToAnimate = cells.Shuffle(world.SharedRandom);
			var ratio = Common.Util.RandomDelay(self.World, info.Ratio);

			var amount = cellsToAnimate.Count() * ratio / 100;
			var positions = cellsToAnimate.Take(amount).Select(x => world.Map.CenterOfCell(x));

			foreach (var position in positions)
				world.AddFrameEndTask(w => w.Add(new SpriteEffect(position, w, info.Image, info.Sequences.Random(w.SharedRandom), info.Palette)));

			ticks = Common.Util.RandomDelay(self.World, info.Interval);
		}
	}
}
