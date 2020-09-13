#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	[Desc("Plays an animation on the ground position when the actor lands.")]
	public class WithAircraftDustEffectInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		public readonly string Image = null;

		[SequenceReference(nameof(Image))]
		public readonly string[] Sequences = { "idle" };

		[PaletteReference]
		public readonly string Palette = "effect";

		[Desc("Should the sprite effect be visible through fog.")]
		public readonly bool VisibleThroughFog = false;

		[Desc("Time in ticks to play the animation after descending.")]
		public readonly int LandingDelay = 0;

		public override object Create(ActorInitializer init) { return new WithAircraftDustEffect(this); }
	}

	public class WithAircraftDustEffect : ConditionalTrait<WithAircraftDustEffectInfo>, INotifyLanding
	{
		readonly WithAircraftDustEffectInfo info;

		public WithAircraftDustEffect(WithAircraftDustEffectInfo info)
			: base(info)
		{
			this.info = info;
		}

		void AddEffect(Actor self, int delay)
		{
			var position = self.CenterPosition - new WVec(WDist.Zero, WDist.Zero, self.World.Map.DistanceAboveTerrain(self.CenterPosition));
			self.World.AddFrameEndTask(w => w.Add(new SpriteEffect(position, self.World, Info.Image,
				info.Sequences.Random(Game.CosmeticRandom), info.Palette, info.VisibleThroughFog, delay)));
		}

		void INotifyLanding.Landing(Actor self)
		{
			AddEffect(self, info.LandingDelay);
		}
	}
}
