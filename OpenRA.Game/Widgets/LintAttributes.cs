#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;

namespace OpenRA.Widgets
{
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class ChromeLogicArgsHotkeys : Attribute
	{
		public string[] LogicArgKeys;
		public ChromeLogicArgsHotkeys(params string[] logicArgKeys)
		{
			LogicArgKeys = logicArgKeys;
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public sealed class CustomLintableHotkeyNames : Attribute { }

	[AttributeUsage(AttributeTargets.Field)]
	public sealed class ChromeReferenceAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Field)]
	public sealed class ChromeReferencePrefixAttribute : Attribute
	{
		// The field name in the same widget info that connects with it.
		public readonly string ChromeReference;

		public ChromeReferencePrefixAttribute(string chromeReference)
		{
			ChromeReference = chromeReference;
		}
	}

	[AttributeUsage(AttributeTargets.Field)]
	public sealed class ChromeReferenceSuffixAttribute : Attribute
	{
		// The field name in the same widget info that connects with it.
		public readonly string ChromeReference;

		public ChromeReferenceSuffixAttribute(string chromeReference)
		{
			ChromeReference = chromeReference;
		}
	}
}
