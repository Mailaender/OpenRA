#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Drawing;
using System.Collections.Generic;
using OpenRA.Effects;
using OpenRA.Mods.RA.Activities;
using OpenRA.Mods.RA.Orders;
using OpenRA.Traits;
using OpenRA.Graphics;

namespace OpenRA.Mods.RA
{
	class ChronoshiftDeployInfo : ITraitInfo
	{
		public readonly int ChargeTime = 30; // seconds
		public readonly int JumpDistance = 10;
		public object Create(ActorInitializer init) { return new ChronoshiftDeploy(init.self, this); }
	}

	class ChronoshiftDeploy : IIssueOrder, IResolveOrder, ITick, IPips, IOrderVoice, ISync
	{
		[Sync] int chargeTick = 0;
		public readonly ChronoshiftDeployInfo Info;
		readonly Actor self;

		public ChronoshiftDeploy(Actor self, ChronoshiftDeployInfo info)
		{
			this.self = self;
			this.Info = info;
		}

		public void Tick(Actor self)
		{
			if (chargeTick > 0)
				chargeTick--;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get { yield return new DeployOrderTargeter("ChronoshiftJump", 5, () => chargeTick <= 0); }
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "ChronoshiftJump" && chargeTick <= 0)
				self.World.OrderGenerator = new ChronoTankOrderGenerator(self);

			return new Order("ChronoshiftJump", self, false); // Hack until we can return null
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "ChronoshiftJump")
			{
				if (CanJumpTo(order.TargetLocation))
				{
					self.CancelActivity();
					self.QueueActivity(new Teleport(null, order.TargetLocation, true));
					chargeTick = 25 * Info.ChargeTime;
				}
			}
		}

		public string VoicePhraseForOrder(Actor self, Order order)
		{
			return (order.OrderString == "ChronoshiftDeploy" && chargeTick <= 0) ? "Move" : null;
		}

		// Display 2 pips indicating the current charge status
		public IEnumerable<PipType> GetPips(Actor self)
		{
			const int numPips = 2;
			for (int i = 0; i < numPips; i++)
			{
				if ((1 - chargeTick * 1.0f / (25 * Info.ChargeTime)) * numPips < i + 1)
				{
					yield return PipType.Transparent;
					continue;
				}

				yield return PipType.Blue;
			}
		}

		public bool CanJumpTo(CPos xy)
		{
			var movement = self.TraitOrDefault<IMove>();

			if (chargeTick <= 0 // Can jump
				&& self.World.LocalPlayer.Shroud.IsExplored(xy) // Not in shroud
				&& movement.CanEnterCell(xy) // Can enter cell
				&& (self.Location - xy).Length <= Info.JumpDistance) // Within jump range
				return true;
			else
				return false;
		}
	}

	class ChronoTankOrderGenerator : IOrderGenerator
	{
		readonly Actor self;

		public ChronoTankOrderGenerator(Actor self) { this.self = self; }

		public IEnumerable<Order> Order(World world, CPos xy, MouseInput mi)
		{
			if (mi.Button == MouseButton.Left)
			{
				world.CancelInputMode();
				yield break;
			}

			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);

			var cinfo = self.Trait<ChronoshiftDeploy>();
			if (cinfo.CanJumpTo(xy))
			{
				self.World.CancelInputMode();
				yield return new Order("ChronoshiftJump", self, queued) { TargetLocation = xy };
			}
		}

		public string GetCursor(World world, CPos xy, MouseInput mi)
		{
			var cinfo = self.Trait<ChronoshiftDeploy>();
			if (cinfo.CanJumpTo(xy))
				return "chrono-target";
			else
				return "move-blocked";
		}

		public void Tick(World world) { }
		public void RenderAfterWorld(WorldRenderer wr, World world) { }
		public void RenderBeforeWorld(WorldRenderer wr, World world)
		{
			wr.DrawRangeCircle(
				Color.FromArgb(128, Color.DeepSkyBlue),
				self.CenterLocation.ToFloat2(), (int)self.Trait<ChronoshiftDeploy>().Info.JumpDistance);
		}
	}
}
