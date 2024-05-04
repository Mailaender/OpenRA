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

using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	class PlayerLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public PlayerLogic(Widget widget, ModData modData, World world, WorldRenderer worldRenderer)
		{
			var playerPanel = widget.Get<ScrollPanelWidget>("PLAYER_LIST");

			var playerTemplate = playerPanel.Get("PLAYER_TEMPLATE");
			playerPanel.RemoveChildren();

			var editorActorLayer = world.WorldActor.Trait<EditorActorLayer>();
			foreach (var player in editorActorLayer.Players.Players.Values)
			{
				var item = playerTemplate.Clone();
				var colorManager = modData.DefaultRules.Actors[SystemActors.World].TraitInfo<IColorPickerManagerInfo>();
				var colorDropdown = item.Get<DropDownButtonWidget>("PLAYERCOLOR");
				var hardcodedPalettes = modData.DefaultRules.Actors[SystemActors.World].TraitInfoOrDefault<IndexedPlayerPaletteInfo>();
				colorDropdown.IsDisabled = () => hardcodedPalettes != null && hardcodedPalettes.PlayerIndex.ContainsKey(player.Name);
				colorDropdown.OnMouseDown = _ => colorManager.ShowColorDropDown(colorDropdown, player.Color, player.Faction, worldRenderer, color =>
				{
					player.Color = color;
					worldRenderer.UpdatePalettesForPlayer(player.Name, player.Color, true);

					foreach (var preview in editorActorLayer.Previews.Where(p => p.Owner == player).ToList())
						preview.UpdateRadarColor();
				});
				colorDropdown.Get<ColorBlockWidget>("COLORBLOCK").GetColor = () => player.Color;

				var factions = modData.DefaultRules.Actors[SystemActors.World].TraitInfos<FactionInfo>();

				var dropdown = item.Get<DropDownButtonWidget>("FACTION");
				/*dropdown.OnMouseDown = _ => (factionId, itemTemplate) () =>
				{
					var item = ScrollItemWidget.Setup(itemTemplate,
					() => client.Faction == factionId,
					() => player.Faction = factionId);
						var faction = factions[factionId];
						item.Get<LabelWidget>("LABEL").GetText = () => faction.Name;
						var flag = item.Get<ImageWidget>("FLAG");
						flag.GetImageCollection = () => "flags";
						flag.GetImageName = () => factionId;
				 }*/

				var flag = dropdown.Get<ImageWidget>("FACTIONFLAG");
				flag.GetImageCollection = () => "flags";
				flag.GetImageName = () => player.Faction;
				var faction = factions.FirstOrDefault(f => f.InternalName == player.Faction);
				dropdown.Get<LabelWidget>("FACTIONNAME").GetText = () => faction.Name;

				var name = item.Get<TextFieldWidget>("NAME");
				name.Text = player.Name;
				name.IsDisabled = () => player.NonCombatant || player.OwnsWorld || player.Name.StartsWith("Multi");
				var escPressed = false;
				name.OnLoseFocus = () =>
				{
					if (escPressed)
					{
						escPressed = false;
						return;
					}
					name.Text = name.Text.Trim();
					if (name.Text.Length == 0)
						name.Text = player.Name;
					else if (name.Text != player.Name)
						player.Name = name.Text;
				};
				name.OnEnterKey = _ =>
				{
					name.YieldKeyboardFocus();
					return true;
				};
				name.OnEscKey = _ =>
				{
					name.Text = player.Name;
					escPressed = true;
					name.YieldKeyboardFocus();
					return true;
				};

				playerPanel.AddChild(item);
			}
		}
	}
}
