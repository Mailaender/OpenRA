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

using System;
using OpenRA.Scripting;

namespace OpenRA.Mods.Common.Scripting.Global
{
	[ScriptGlobal("Facing")]
	[Obsolete("The Facing table is deprecated. Use Angle instead.")]
	public class FacingGlobal : ScriptGlobal
	{
		public FacingGlobal(ScriptContext context)
			: base(context) { }

		void Deprecated()
		{
			TextNotificationsManager.Debug("The Facing table is deprecated. Use Angle instead.");
		}

		[Obsolete("Use Angle.North instead.")]
		public int North { get { Deprecated(); return 0; } }

		[Obsolete("Use Angle.NorthWest instead.")]
		public int NorthWest { get { Deprecated(); return 32; } }

		[Obsolete("Use Angle.West instead.")]
		public int West { get { Deprecated(); return 64; } }

		[Obsolete("Use Angle.SouthWest instead.")]
		public int SouthWest { get { Deprecated(); return 96; } }

		[Obsolete("Use Angle.South instead.")]
		public int South { get { Deprecated(); return 128; } }

		[Obsolete("Use Angle.SouthEast instead.")]
		public int SouthEast { get { Deprecated(); return 160; } }

		[Obsolete("Use Angle.East instead.")]
		public int East { get { Deprecated(); return 192; } }

		[Obsolete("Use Angle.NorthEast instead.")]
		public int NorthEast { get { Deprecated(); return 224; } }
	}
}
