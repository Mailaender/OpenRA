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

using MoonSharp.Interpreter;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Scripting
{
	[ScriptPropertyGroup("Support Powers")]
	public class ChronsphereProperties : ScriptActorProperties, Requires<ChronoshiftPowerInfo>
	{
		public ChronsphereProperties(ScriptContext context, Actor self)
			: base(context, self) { }

		[Desc("Chronoshift a group of actors. A duration of 0 will teleport the actors permanently.")]
		public void Chronoshift(Table unitLocation, int duration = 0, bool killCargo = false)
		{
			foreach (var kv in unitLocation.Pairs)
			{
				Actor actor = kv.Key.UserData != null ? (Actor)kv.Key.UserData.Object : null;
				CPos? cell = kv.Value.UserData != null ? (CPos?)kv.Value.UserData.Object : null;

				var cs = actor.TraitOrDefault<Chronoshiftable>();
				if (cs != null && cs.CanChronoshiftTo(actor, cell.Value))
					cs.Teleport(actor, cell.Value, duration, killCargo, Self);
			}
		}
	}
}