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

using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class FloatingSpriteEmitterInfo : ConditionalTraitInfo, IRulesetLoaded
	{
		[FieldLoader.Require]
		[Desc("The duration of an individual particle. Two values mean actual lifetime will vary between them.")]
		public readonly int[] Duration;

		[Desc("Offset for the particle emitter.")]
		public readonly WVec[] Offset = { WVec.Zero };

		[Desc("Randomized particle forward movement.")]
		public readonly WDist[] Speed = { WDist.Zero };

		[Desc("Randomized particle gravity.")]
		public readonly WDist[] Gravity = { WDist.Zero };

		[Desc("Randomize particle facing.")]
		public readonly bool RandomFacing = true;

		[Desc("Randomize particle turnrate.")]
		public readonly int TurnRate = 0;

		[Desc("Rate to reset particle movement properties.")]
		public readonly int RandomRate = 4;

		[Desc("How many particles should spawn. Two values for a random range.")]
		public readonly int[] SpawnFrequency = { 1 };

		[Desc("Which image to use.")]
		public readonly string Image = "smoke";

		[Desc("Which sequence to use.")]
		[SequenceReference(nameof(Image))]
		public readonly string[] Sequences = { "particles" };

		[Desc("Which palette to use.")]
		[PaletteReference("IsPlayerPalette")]
		public readonly string Palette = "effect";

		public readonly bool IsPlayerPalette = false;

		public override object Create(ActorInitializer init) { return new FloatingSpriteEmitter(init.Self, this); }
	}

	public class FloatingSpriteEmitter : ConditionalTrait<FloatingSpriteEmitterInfo>, ITick
	{
		readonly WVec offset;

		IFacing facing;
		int ticks;

		public FloatingSpriteEmitter(Actor self, FloatingSpriteEmitterInfo info)
			: base(info)
		{
			offset = Util.RandomVector(self.World, Info.Offset);
		}

		protected override void Created(Actor self)
		{
			facing = self.TraitOrDefault<IFacing>();

			base.Created(self);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
				return;

			if (--ticks < 0)
			{
				ticks = Util.RandomDelay(self.World, Info.SpawnFrequency);

				var spawnFacing = (!Info.RandomFacing && facing != null) ? facing.Facing.Facing : -1;

				self.World.AddFrameEndTask(w => w.Add(new FloatingSprite(self, Info.Image, Info.Sequences, Info.Palette, Info.IsPlayerPalette,
					Info.Duration, Info.Speed, Info.Gravity, Info.TurnRate, Info.RandomRate, self.CenterPosition + offset, spawnFacing)));
			}
		}
	}
}
