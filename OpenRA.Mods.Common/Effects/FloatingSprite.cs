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
using OpenRA.Effects;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common.Effects
{
	class FloatingSprite : IEffect, ISync, ISpatiallyPartitionable
	{
		readonly WDist[] speed;
		readonly WDist[] gravity;
		readonly Animation anim;

		readonly bool visibleThroughFog;
		readonly int turnRate;
		readonly int randomRate;
		readonly string palette;

		[Sync]
		WPos pos;
		WVec offset;
		int lifetime;
		int ticks;
		int facing;

		public FloatingSprite(Actor emitter, string image, string[] sequences, string palette, bool isPlayerPalette,
			int[] duration, WDist[] speed, WDist[] gravity, int turnRate, int randomRate, WPos pos, int facing = -1,
			bool visibleThroughFog = false)
		{
			var world = emitter.World;
			this.pos = pos;
			this.turnRate = turnRate;
			this.randomRate = randomRate;
			this.speed = speed;
			this.gravity = gravity;
			this.visibleThroughFog = visibleThroughFog;

			this.facing = facing > -1
				? facing
				: world.SharedRandom.Next(256);

			anim = new Animation(world, image, () => WAngle.FromFacing(facing));
			anim.PlayRepeating(sequences.Random(world.SharedRandom));
			world.ScreenMap.Add(this, pos, anim.Image);
			lifetime = Util.RandomDelay(world, duration);

			this.palette = isPlayerPalette ? palette + emitter.Owner.InternalName : palette;
		}

		public void Tick(World world)
		{
			if (--lifetime < 0)
			{
				world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });
				return;
			}

			if (--ticks < 0)
			{
				var forward = Util.RandomDistance(world, speed).Length;
				var height = Util.RandomDistance(world, gravity).Length;

				offset = new WVec(forward, 0, height);

				if (turnRate > 0)
					facing = (facing + world.SharedRandom.Next(-turnRate, turnRate)) & 0xFF;

				offset = offset.Rotate(WRot.FromFacing(facing));

				ticks = randomRate;
			}

			anim.Tick();

			pos += offset;

			world.ScreenMap.Update(this, pos, anim.Image);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (wr.World.FogObscures(pos) && !visibleThroughFog)
				return SpriteRenderable.None;

			return anim.Render(pos, wr.Palette(palette));
		}
	}
}
