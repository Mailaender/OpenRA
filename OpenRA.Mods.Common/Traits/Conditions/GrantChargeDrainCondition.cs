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
using OpenRA.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Grants a condition for a certain time when deployed. Can be reversed anytime " +
		"by undeploying. Leftover charges are taken into account when recharging.")]
	public class GrantChargeDrainConditionInfo : PausableConditionalTraitInfo
	{
		[GrantedConditionReference]
		[FieldLoader.Require]
		[Desc("The condition granted after deploying.")]
		public readonly string DeployedCondition = null;

		[FieldLoader.Require]
		[Desc("Time it takes to fully charge up again.")]
		public readonly int ChargeTicks = 0;

		[FieldLoader.Require]
		[Desc("The deployed state's length in ticks.")]
		public readonly int DrainTicks = 0;

		[Desc("Cursor to display when able to (un)deploy the actor.")]
		public readonly string DeployCursor = "deploy";

		[Desc("Cursor to display when unable to (un)deploy the actor.")]
		public readonly string DeployBlockedCursor = "deploy-blocked";

		public readonly bool StartsFullyCharged = false;

		[VoiceReference]
		public readonly string Voice = "Action";

		public readonly bool ShowSelectionBar = true;
		public readonly Color ChargingColor = Color.DarkRed;
		public readonly Color DischargingColor = Color.DarkMagenta;

		public override object Create(ActorInitializer init) { return new GrantChargeDrainCondition(init.Self, this); }
	}

	public enum TimedDeployState { Charging, Ready, Active, Deploying, Undeploying }

	public class GrantChargeDrainCondition : PausableConditionalTrait<GrantChargeDrainConditionInfo>,
		IResolveOrder, IIssueOrder, ISelectionBar, IOrderVoice, ISync, ITick, IIssueDeployOrder
	{
		readonly Actor self;

		int deployedToken = Actor.InvalidConditionToken;

		[Sync]
		int ticks;

		[Sync]
		TimedDeployState deployState;

		public GrantChargeDrainCondition(Actor self, GrantChargeDrainConditionInfo info)
			: base(info)
		{
			this.self = self;
		}

		protected override void Created(Actor self)
		{
			if (Info.StartsFullyCharged)
			{
				ticks = Info.DrainTicks;
				deployState = TimedDeployState.Ready;
			}
			else
			{
				ticks = Info.ChargeTicks;
				deployState = TimedDeployState.Charging;
			}

			base.Created(self);
		}

		Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
		{
			return new Order("GrantChargeDrainCondition", self, queued);
		}

		bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return !IsTraitPaused && !IsTraitDisabled; }

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get
			{
				if (!IsTraitDisabled)
					yield return new DeployOrderTargeter("GrantChargeDrainCondition", 5,
						() => IsCursorBlocked() ? Info.DeployBlockedCursor : Info.DeployCursor);
			}
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			if (order.OrderID == "GrantChargeDrainCondition")
				return new Order(order.OrderID, self, queued);

			return null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != "GrantChargeDrainCondition")
				return;

			if (!order.Queued)
				self.CancelActivity();

			if (deployState != TimedDeployState.Active)
				self.QueueActivity(new CallFunc(Deploy));
			else
				self.QueueActivity(new CallFunc(RevokeDeploy));
		}

		bool IsCursorBlocked()
		{
			return IsTraitPaused;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == "GrantChargeDrainCondition" ? Info.Voice : null;
		}

		void Deploy()
		{
			deployState = TimedDeployState.Deploying;

			OnDeployCompleted();
		}

		void OnDeployCompleted()
		{
			if (deployedToken == Actor.InvalidConditionToken)
				deployedToken = self.GrantCondition(Info.DeployedCondition);

			var drainPercentage = 100 - (ticks * 100 / Info.ChargeTicks);
			ticks = Info.DrainTicks * drainPercentage / 100;

			deployState = TimedDeployState.Active;
		}

		void RevokeDeploy()
		{
			deployState = TimedDeployState.Undeploying;

			OnUndeployCompleted();
		}

		void OnUndeployCompleted()
		{
			if (deployedToken != Actor.InvalidConditionToken)
				deployedToken = self.RevokeCondition(deployedToken);

			deployState = TimedDeployState.Charging;

			var chargePercentage = 100 - (ticks * 100 / Info.DrainTicks);
			ticks = Info.ChargeTicks * chargePercentage / 100;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitPaused || IsTraitDisabled)
				return;

			if (deployState == TimedDeployState.Ready || deployState == TimedDeployState.Deploying || deployState == TimedDeployState.Undeploying)
				return;

			if (--ticks < 1)
			{
				if (deployState == TimedDeployState.Charging)
				{
					ticks = Info.DrainTicks;
					deployState = TimedDeployState.Ready;
				}
				else
					RevokeDeploy();
			}
		}

		float ISelectionBar.GetValue()
		{
			if (IsTraitDisabled || !Info.ShowSelectionBar || deployState == TimedDeployState.Undeploying)
				return 0f;

			if (deployState == TimedDeployState.Deploying || deployState == TimedDeployState.Ready)
				return 1f;

			return deployState == TimedDeployState.Charging
				? (float)(Info.ChargeTicks - ticks) / Info.ChargeTicks
				: (float)ticks / Info.DrainTicks;
		}

		bool ISelectionBar.DisplayWhenEmpty { get { return !IsTraitDisabled && Info.ShowSelectionBar; } }

		Color ISelectionBar.GetColor() { return deployState == TimedDeployState.Charging ? Info.ChargingColor : Info.DischargingColor; }
	}
}
