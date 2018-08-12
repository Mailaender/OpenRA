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

using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Scripting
{
	public enum Trigger
	{
		OnIdle, OnDamaged, OnKilled, OnProduction, OnOtherProduction, OnPlayerWon, OnPlayerLost,
		OnObjectiveAdded, OnObjectiveCompleted, OnObjectiveFailed, OnCapture, OnInfiltrated,
		OnAddedToWorld, OnRemovedFromWorld, OnDiscovered, OnPlayerDiscovered,
		OnPassengerEntered, OnPassengerExited, OnSelling, OnSold
	}

	[Desc("Allows map scripts to attach triggers to this actor via the Triggers global.")]
	public class ScriptTriggersInfo : ITraitInfo
	{
		public object Create(ActorInitializer init) { return new ScriptTriggers(init.World, init.Self); }
	}

	public sealed class ScriptTriggers : INotifyIdle, INotifyDamage, INotifyKilled, INotifyProduction, INotifyOtherProduction,
		INotifyObjectivesUpdated, INotifyCapture, INotifyInfiltrated, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyDiscovered, INotifyActorDisposing,
		INotifyPassengerEntered, INotifyPassengerExited, INotifySold
	{
		readonly World world;
		readonly Actor self;

		public event Action<Actor> OnKilledInternal = _ => { };
		public event Action<Actor> OnCapturedInternal = _ => { };
		public event Action<Actor> OnRemovedInternal = _ => { };
		public event Action<Actor, Actor> OnProducedInternal = (a, b) => { };
		public event Action<Actor, Actor> OnOtherProducedInternal = (a, b) => { };

		readonly List<Triggerable>[] triggerables = Exts.MakeArray(Enum.GetValues(typeof(Trigger)).Length, _ => new List<Triggerable>());

		struct Triggerable
		{
			public readonly Closure Function;
			public readonly ScriptContext Context;
			public readonly Actor Self;
			public Triggerable(Closure function, ScriptContext context, Actor self)
			{
				Function = function;
				Context = context;
				Self = self;
			}
		}

		public ScriptTriggers(World world, Actor self)
		{
			this.world = world;
			this.self = self;
		}

		List<Triggerable> Triggerables(Trigger trigger)
		{
			return triggerables[(int)trigger];
		}

		public void RegisterCallback(Trigger trigger, Closure func, ScriptContext context)
		{
			Triggerables(trigger).Add(new Triggerable(func, context, self));
		}

		public bool HasAnyCallbacksFor(Trigger trigger)
		{
			return Triggerables(trigger).Count > 0;
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnIdle))
			{
				try
				{
					f.Function.Call(f.Self);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnDamaged))
			{
				try
				{
					f.Function.Call(f.Self, e.Attacker);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (world.Disposing)
				return;

			// Run Lua callbacks
			foreach (var f in Triggerables(Trigger.OnKilled))
			{
				try
				{
					f.Function.Call(f.Self, e.Attacker);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}

			// Run any internally bound callbacks
			OnKilledInternal(self);
		}

		public void UnitProduced(Actor self, Actor other, CPos exit)
		{
			if (world.Disposing)
				return;

			// Run Lua callbacks
			foreach (var f in Triggerables(Trigger.OnProduction))
			{
				try
				{
					f.Function.Call(f.Self, other);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}

			// Run any internally bound callbacks
			OnProducedInternal(self, other);
		}

		public void OnPlayerWon(Player player)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnPlayerWon))
			{
				try
				{
					f.Function.Call(player);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void OnPlayerLost(Player player)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnPlayerLost))
			{
				try
				{
					f.Function.Call(player);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void OnObjectiveAdded(Player player, int id)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnObjectiveAdded))
			{
				try
				{
					f.Function.Call(player, id);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void OnObjectiveCompleted(Player player, int id)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnObjectiveCompleted))
			{
				try
				{
					f.Function.Call(player, id);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void OnObjectiveFailed(Player player, int id)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnObjectiveFailed))
			{
				try
				{
					f.Function.Call(player, id);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void OnCapture(Actor self, Actor captor, Player oldOwner, Player newOwner)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnCapture))
			{
				try
				{
					f.Function.Call(f.Self, captor, oldOwner, newOwner);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}

			// Run any internally bound callbacks
			OnCapturedInternal(self);
		}

		void INotifyInfiltrated.Infiltrated(Actor self, Actor infiltrator, BitSet<TargetableType> types)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnInfiltrated))
			{
				try
				{
					f.Function.Call(f.Self, infiltrator);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnAddedToWorld))
			{
				try
				{
					f.Function.Call(f.Self);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (world.Disposing)
				return;

			// Run Lua callbacks
			foreach (var f in Triggerables(Trigger.OnRemovedFromWorld))
			{
				try
				{
					f.Function.Call(f.Self);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}

			// Run any internally bound callbacks
			OnRemovedInternal(self);
		}

		void INotifySold.Selling(Actor self)
		{
			if (world.Disposing)
				return;

			// Run Lua callbacks
			foreach (var f in Triggerables(Trigger.OnSelling))
			{
				try
				{
					f.Function.Call(f.Self);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifySold.Sold(Actor self)
		{
			if (world.Disposing)
				return;

			// Run Lua callbacks
			foreach (var f in Triggerables(Trigger.OnSold))
			{
				try
				{
					f.Function.Call(f.Self);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void UnitProducedByOther(Actor self, Actor producee, Actor produced, string productionType)
		{
			if (world.Disposing)
				return;

			// Run Lua callbacks
			foreach (var f in Triggerables(Trigger.OnOtherProduction))
			{
				try
				{
					f.Function.Call(producee, produced);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}

			// Run any internally bound callbacks
			OnOtherProducedInternal(producee, produced);
		}

		public void OnDiscovered(Actor self, Player discoverer, bool playNotification)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnDiscovered))
			{
				try
				{
					f.Function.Call(f.Self, discoverer);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}

			foreach (var f in Triggerables(Trigger.OnPlayerDiscovered))
			{
				try
				{
					f.Function.Call(self.Owner, discoverer, f.Self);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifyPassengerEntered.OnPassengerEntered(Actor self, Actor passenger)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnPassengerEntered))
			{
				try
				{
					f.Function.Call(self, passenger);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		void INotifyPassengerExited.OnPassengerExited(Actor self, Actor passenger)
		{
			if (world.Disposing)
				return;

			foreach (var f in Triggerables(Trigger.OnPassengerExited))
			{
				try
				{
					f.Function.Call(self, passenger);
				}
				catch (Exception ex)
				{
					f.Context.FatalError(ex.Message);
					return;
				}
			}
		}

		public void Clear(Trigger trigger)
		{
			world.AddFrameEndTask(w =>
			{
				var triggerables = Triggerables(trigger);
				triggerables.Clear();
			});
		}

		public void ClearAll()
		{
			foreach (Trigger t in Enum.GetValues(typeof(Trigger)))
				Clear(t);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			ClearAll();
		}
	}
}
