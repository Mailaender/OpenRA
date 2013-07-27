#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
	public class MusicPlayerLogic
	{
		Widget bg;
		ScrollPanelWidget ml;

		public void Play(string song)
		{
			Game.Settings.Sound.CurrentSong = song;
			if (Game.Settings.Sound.CurrentSong == null)
				return;
			if (!Rules.Music.ContainsKey(Game.Settings.Sound.CurrentSong))
			{
				Game.Settings.Sound.CurrentSong = null;
				return;
			}

			ml.ScrollToItem(Game.Settings.Sound.CurrentSong);

			Sound.PlayMusicThen(
				Rules.Music[Game.Settings.Sound.CurrentSong],
				() => Play(Game.Settings.Sound.Repeat ? Game.Settings.Sound.CurrentSong : GetNextSong()));
		}

		[ObjectCreator.UseCtor]
		public MusicPlayerLogic(Action onExit)
		{
			bg = Ui.Root.Get("MUSIC_MENU");
			ml = bg.Get<ScrollPanelWidget>("MUSIC_LIST");

			if (Game.Settings.Sound.CurrentSong == null || !Rules.Music.ContainsKey(Game.Settings.Sound.CurrentSong))
				Game.Settings.Sound.CurrentSong = GetNextSong();

			bg.Get("BUTTON_PAUSE").IsVisible = () => Sound.MusicPlaying;
			bg.Get("BUTTON_PLAY").IsVisible = () => !Sound.MusicPlaying;

			bg.Get<ButtonWidget>("BUTTON_CLOSE").OnClick =
				() => { Game.Settings.Save(); Ui.CloseWindow(); onExit(); };

			bg.Get("BUTTON_INSTALL").IsVisible = () => false;

			bg.Get<ButtonWidget>("BUTTON_PLAY").OnClick = () => Play(Game.Settings.Sound.CurrentSong);
			bg.Get<ButtonWidget>("BUTTON_PAUSE").OnClick = Sound.PauseMusic;
			bg.Get<ButtonWidget>("BUTTON_STOP").OnClick = Sound.StopMusic;
			bg.Get<ButtonWidget>("BUTTON_NEXT").OnClick = () => Play(GetNextSong());
			bg.Get<ButtonWidget>("BUTTON_PREV").OnClick = () => Play(GetPrevSong());

			var shuffleCheckbox = bg.Get<CheckboxWidget>("SHUFFLE");
			shuffleCheckbox.IsChecked = () => Game.Settings.Sound.Shuffle;
			shuffleCheckbox.OnClick = () => Game.Settings.Sound.Shuffle ^= true;

			var repeatCheckbox = bg.Get<CheckboxWidget>("REPEAT");
			repeatCheckbox.IsChecked = () => Game.Settings.Sound.Repeat;
			repeatCheckbox.OnClick = () => Game.Settings.Sound.Repeat ^= true;

			bg.Get<LabelWidget>("TIME").GetText = () =>
			{
				if (Game.Settings.Sound.CurrentSong == null)
					return "";
				return "{0} / {1}".F(
					WidgetUtils.FormatTimeSeconds((int)Sound.MusicSeekPosition),
					WidgetUtils.FormatTimeSeconds(Rules.Music[Game.Settings.Sound.CurrentSong].Length));
			};

			var itemTemplate = ml.Get<ScrollItemWidget>("MUSIC_TEMPLATE");

			if (!Rules.InstalledMusic.Any())
			{
				itemTemplate.IsVisible = () => true;
				itemTemplate.Get<LabelWidget>("TITLE").GetText = () => "No Music Installed";
				itemTemplate.Get<LabelWidget>("TITLE").Align = TextAlign.Center;
			}

			foreach (var kv in Rules.InstalledMusic)
			{
				var song = kv.Key;
				var item = ScrollItemWidget.Setup(
					song,
					itemTemplate,
					() => Game.Settings.Sound.CurrentSong == song,
					() => Play(song));
				item.Get<LabelWidget>("TITLE").GetText = () => Rules.Music[song].Title;
				item.Get<LabelWidget>("LENGTH").GetText =
					() => WidgetUtils.FormatTimeSeconds(Rules.Music[song].Length);
				ml.AddChild(item);
			}

			ml.ScrollToItem(Game.Settings.Sound.CurrentSong);
		}

		string ChooseSong(IEnumerable<string> songs)
		{
			if (!songs.Any())
				return null;

			if (Game.Settings.Sound.Shuffle)
				return songs.Random(Game.CosmeticRandom);

			return songs.SkipWhile(m => m != Game.Settings.Sound.CurrentSong)
				.Skip(1).FirstOrDefault() ?? songs.FirstOrDefault();
		}

		string GetNextSong() { return ChooseSong(Rules.InstalledMusic.Select(a => a.Key)); }
		string GetPrevSong() { return ChooseSong(Rules.InstalledMusic.Select(a => a.Key).Reverse()); }
	}
}
