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

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Actor must deploy before firing.")]
	public class AttackDeploysInfo : AttackOmniInfo
	{
		public override object Create(ActorInitializer init) { return new AttackDeploys(init.Self, this); }
	}

	public class AttackDeploys : AttackOmni
	{
		readonly AttackDeploysInfo info;
		Transforms transforms;

		public AttackDeploys(Actor self, AttackDeploysInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			transforms = self.TraitOrDefault<Transforms>();}

		bool deploy;
		protected override void Tick(Actor self)
		{
			if (transforms == null)
				return;

			if (deploy)
				return;
			
			self.World.IssueOrder(new Order("DeployTransform", self, false));
			self.World.IssueOrder(new Order("GrantConditionOnDeploy", self, false));

			deploy = true;

			base.Tick(self);
		}

		protected override bool CanAttack(Actor self, Target target)
		{
			return (transforms != null && transforms.IsTraitEnabled() && !transforms.CanDeploy());
		}
	}
}
