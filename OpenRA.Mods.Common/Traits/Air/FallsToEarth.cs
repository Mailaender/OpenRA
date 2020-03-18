#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.GameRules;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Causes aircraft husks that are spawned in the air to crash to the ground.")]
	public class FallsToEarthInfo : ITraitInfo, IRulesetLoaded, Requires<AircraftInfo>
	{
		[WeaponReference]
		public readonly string Explosion = "UnitExplode";

		[Desc("Will the actor rotate when falling to the ground.")]
		public readonly bool Spins = false;

		[Desc("Spin velocity at the start of the descent.")]
		public readonly int InitialSpin = 10;

		[Desc("How fast the spinning will increase upon descent.")]
		public readonly int SpinAcceleration = 1;

		public readonly bool Moves = false;
		public readonly WDist Velocity = new WDist(43);

		public WeaponInfo ExplosionWeapon { get; private set; }

		public object Create(ActorInitializer init) { return new FallsToEarth(init, this); }
		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (string.IsNullOrEmpty(Explosion))
				return;

			WeaponInfo weapon;
			var weaponToLower = Explosion.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out weapon))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));

			ExplosionWeapon = weapon;
		}
	}

	public class FallsToEarth : IEffectiveOwner, INotifyCreated
	{
		readonly FallsToEarthInfo info;
		readonly Player effectiveOwner;

		public FallsToEarth(ActorInitializer init, FallsToEarthInfo info)
		{
			this.info = info;
			effectiveOwner = init.Contains<EffectiveOwnerInit>() ? init.Get<EffectiveOwnerInit, Player>() : init.Self.Owner;
		}

		// We return init.Self.Owner if there's no effective owner
		bool IEffectiveOwner.Disguised { get { return true; } }
		Player IEffectiveOwner.Owner { get { return effectiveOwner; } }

		void INotifyCreated.Created(Actor self)
		{
			self.QueueActivity(false, new FallToEarth(self, info));
		}
	}
}
