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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class EncyclopediaLogic : ChromeLogic
	{
		readonly World world;
		readonly ModData modData;

		readonly ScrollPanelWidget descriptionPanel;
		readonly LabelWidget descriptionLabel;
		readonly SpriteFont descriptionFont;

		readonly ScrollPanelWidget actorList;
		readonly ScrollItemWidget headerTemplate;
		readonly ScrollItemWidget template;
		readonly ActorPreviewWidget previewWidget;

		ActorInfo selectedActor;

		[ObjectCreator.UseCtor]
		public EncyclopediaLogic(Widget widget, World world, ModData modData, Action onExit)
		{
			this.world = world;
			this.modData = modData;

			actorList = widget.Get<ScrollPanelWidget>("ACTOR_LIST");

			headerTemplate = widget.Get<ScrollItemWidget>("HEADER");
			template = widget.Get<ScrollItemWidget>("TEMPLATE");

			widget.Get("ACTOR_INFO").IsVisible = () => selectedActor != null;

			previewWidget = widget.Get<ActorPreviewWidget>("ACTOR_PREVIEW");
			previewWidget.IsVisible = () => selectedActor != null;

			descriptionPanel = widget.Get<ScrollPanelWidget>("ACTOR_DESCRIPTION_PANEL");

			descriptionLabel = descriptionPanel.Get<LabelWidget>("ACTOR_DESCRIPTION");
			descriptionFont = Game.Renderer.Fonts[descriptionLabel.Font];

			var units = new List<ActorInfo>();
			var buildings = new List<ActorInfo>();
			actorList.RemoveChildren();

			foreach (var actor in modData.DefaultRules.Actors.Values)
			{
				if (actor.TraitInfoOrDefault<BuildableInfo>() == null)
					continue;

				if (!actor.TraitInfos<IRenderActorPreviewSpritesInfo>().Any())
					continue;

				var statistics = actor.TraitInfoOrDefault<UpdatesPlayerStatisticsInfo>();
				if (statistics != null && !string.IsNullOrEmpty(statistics.OverrideActor))
					continue;

				if (actor.TraitInfoOrDefault<EncyclopediaInfo>() == null)
					continue;

				if (actor.TraitInfoOrDefault<BuildingInfo>() != null)
					buildings.Add(actor);
				else
					units.Add(actor);
			}

			CreateActorGroup("Buildings", buildings);
			CreateActorGroup("Units", units);

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Game.Disconnect();
				Ui.CloseWindow();
				onExit();
			};
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
			}

			base.Dispose(disposing);
		}

		void CreateActorGroup(string title, List<ActorInfo> actors)
		{
			var header = ScrollItemWidget.Setup(headerTemplate, () => true, () => { });
			header.Get<LabelWidget>("LABEL").GetText = () => title;
			actorList.AddChild(header);

			foreach (var actor in actors)
			{
				var item = ScrollItemWidget.Setup(template,
					() => selectedActor != null && selectedActor.Name == actor.Name,
					() => SelectActor(actor));

				var label = item.Get<LabelWithTooltipWidget>("TITLE");
				var name = actor.TraitInfoOrDefault<TooltipInfo>()?.Name;
				if (!string.IsNullOrEmpty(name))
					WidgetUtils.TruncateLabelToTooltip(label, name);

				actorList.AddChild(item);
			}
		}

		void SelectActor(ActorInfo actor)
		{
			selectedActor = actor;

			var typeDictionary = new TypeDictionary();
			typeDictionary.Add(new OwnerInit(world.WorldActor.Owner));
			typeDictionary.Add(new FactionInit(world.WorldActor.Owner.PlayerReference.Faction));

			foreach (var actorPreviewInit in actor.TraitInfos<IActorPreviewInitInfo>())
				foreach (var inits in actorPreviewInit.ActorPreviewInits(actor, ActorPreviewType.ColorPicker))
					typeDictionary.Add(inits);

			previewWidget.SetPreview(actor, typeDictionary);

			var text = "";

			var buildable = actor.TraitInfo<BuildableInfo>();
			var prerequisites = buildable.Prerequisites.Select(a => ActorName(modData.DefaultRules, a))
				.Where(s => !s.StartsWith("~", StringComparison.Ordinal) && !s.StartsWith("!", StringComparison.Ordinal));
			if (prerequisites.Any())
				text += "Requires {0}\n\n".F(prerequisites.JoinWith(", "));

			var info = actor.TraitInfoOrDefault<EncyclopediaInfo>();
			if (info != null && !string.IsNullOrEmpty(info.Description))
				text += WidgetUtils.WrapText(info.Description.Replace("\\n", "\n") + "\n\n", descriptionLabel.Bounds.Width, descriptionFont);

			var height = descriptionFont.Measure(text).Y;
			descriptionLabel.Text = text;
			descriptionLabel.Bounds.Height = height;
			descriptionPanel.Layout.AdjustChildren();

			descriptionPanel.ScrollToTop();
		}

		static string ActorName(Ruleset rules, string name)
		{
			if (rules.Actors.TryGetValue(name.ToLowerInvariant(), out var actor))
			{
				var actorTooltip = actor.TraitInfos<TooltipInfo>().FirstOrDefault(info => info.EnabledByDefault);
				if (actorTooltip != null)
					return actorTooltip.Name;
			}

			return name;
		}
	}
}
