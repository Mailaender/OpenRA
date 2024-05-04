#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.Common.Scripting;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	class EditorInfoLogic : ChromeLogic
	{
		IngameInfoPanel activePanel;

		[ObjectCreator.UseCtor]
		public EditorInfoLogic(Widget widget, World world, IngameInfoPanel initialPanel, Action<bool> hideMenu)
		{
			activePanel = initialPanel;

			var titleLabel = widget.Get<LabelWidget>("TITLE");
			titleLabel.Text = world.Map.Title;

			var playersTabButton = widget.Get<ButtonWidget>("BUTTON1");
			playersTabButton.Text = "Players";
			playersTabButton.OnClick = () => activePanel = IngameInfoPanel.Players;
			playersTabButton.IsHighlighted = () => activePanel == IngameInfoPanel.Players;

			var playerPanel = widget.Get<ContainerWidget>("PLAYER_PANEL");
			playerPanel.IsVisible = () => activePanel == IngameInfoPanel.Players;

			Game.LoadWidget(world, "PLAYER_PANEL", playerPanel, new WidgetArgs());

			activePanel = IngameInfoPanel.Players;
		}
	}
}
