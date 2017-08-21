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

using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Does a suicide attack upon deployment.")]
	class SuicidesInfo : ITraitInfo
	{
		[VoiceReference] public readonly string Voice = "Action";

		public object Create(ActorInitializer init) { return new Suicides(init.Self, this); }
	}

	class Suicides : IResolveOrder, IOrderVoice, IIssueDeployOrder
	{
		readonly SuicidesInfo info;

		public Suicides(Actor self, SuicidesInfo info)
		{
			this.info = info;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				yield return new DeployOrderTargeter("Detonate", 5);
			}
		}

		Order IIssueDeployOrder.IssueDeployOrder(Actor self)
		{
			return new Order("Detonate", self, false);
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return info.Voice;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Detonate")
				self.Kill(self);
		}
	}
}
