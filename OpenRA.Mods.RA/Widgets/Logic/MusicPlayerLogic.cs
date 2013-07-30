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
using System.IO;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.GameRules;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
	public class MusicPlayerLogic
	{
		bool installed;
		MusicInfo currentSong = null;
		Widget panel;
		MusicInfo[] music;
		MusicInfo[] random;
		ScrollPanelWidget musicList;

		ScrollItemWidget itemTemplate;

		[ObjectCreator.UseCtor]
		public MusicPlayerLogic(Widget widget, Action onExit)
		{
			panel = widget.Get("MUSIC_PANEL");

			musicList = panel.Get<ScrollPanelWidget>("MUSIC_LIST");
			itemTemplate = musicList.Get<ScrollItemWidget>("MUSIC_TEMPLATE");

			BuildMusicTable(musicList);

			currentSong = Sound.CurrentMusic ?? GetNextSong();

			if (currentSong != null)
				musicList.ScrollToItem(currentSong.Filename);

			installed = Rules.InstalledMusic.Any();
			Func<bool> noMusic = () => !installed;

			var playButton = panel.Get<ButtonWidget>("BUTTON_PLAY");
			playButton.OnClick = Play;
			playButton.IsDisabled = noMusic;
			playButton.IsVisible = () => !Sound.MusicPlaying;

			var pauseButton = panel.Get<ButtonWidget>("BUTTON_PAUSE");
			pauseButton.OnClick = Sound.PauseMusic;
			pauseButton.IsDisabled = noMusic;
			pauseButton.IsVisible = () => Sound.MusicPlaying;

			var stopButton = panel.Get<ButtonWidget>("BUTTON_STOP");
			stopButton.OnClick = Sound.StopMusic;
			stopButton.IsDisabled = noMusic;

			var nextButton = panel.Get<ButtonWidget>("BUTTON_NEXT");
			nextButton.OnClick = () => { currentSong = GetNextSong(); Play(); };
			nextButton.IsDisabled = noMusic;

			var prevButton = panel.Get<ButtonWidget>("BUTTON_PREV");
			prevButton.OnClick = () => { currentSong = GetPrevSong(); Play(); };
			prevButton.IsDisabled = noMusic;

			var shuffleCheckbox = panel.Get<CheckboxWidget>("SHUFFLE");
			shuffleCheckbox.IsChecked = () => Game.Settings.Sound.Shuffle;
			shuffleCheckbox.OnClick = () => Game.Settings.Sound.Shuffle ^= true;

			var repeatCheckbox = panel.Get<CheckboxWidget>("REPEAT");
			repeatCheckbox.IsChecked = () => Game.Settings.Sound.Repeat;
			repeatCheckbox.OnClick = () => Game.Settings.Sound.Repeat ^= true;

			panel.Get<LabelWidget>("TIME_LABEL").GetText = () => (currentSong == null) ? "" :
				"{0:D2}:{1:D2} / {2:D2}:{3:D2}".F((int)Sound.MusicSeekPosition / 60, (int)Sound.MusicSeekPosition % 60,
				    currentSong.Length / 60, currentSong.Length % 60);

			var musicSlider = panel.Get<SliderWidget>("MUSIC_SLIDER");
			musicSlider.OnChange += x => Sound.MusicVolume = x;
			musicSlider.Value = Sound.MusicVolume;

			panel.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => { Game.Settings.Save(); Ui.CloseWindow(); onExit(); };
		}

		void BuildMusicTable(Widget list)
		{
			music = Rules.InstalledMusic.Select(a => a.Value).ToArray();
			random = music.Shuffle(Game.CosmeticRandom).ToArray();

			list.RemoveChildren();
			foreach (var s in music)
			{
				var song = s;
				if (currentSong == null)
					currentSong = song;

				var item = ScrollItemWidget.Setup(song.Filename, itemTemplate, () => currentSong == song, () => { currentSong = song; Play(); });
				item.Get<LabelWidget>("TITLE").GetText = () => song.Title;
				item.Get<LabelWidget>("LENGTH").GetText = () => SongLengthLabel(song);
				list.AddChild(item);
			}
		}

		void Play()
		{
			if (currentSong == null)
				return;

			musicList.ScrollToItem(currentSong.Filename);

			Sound.PlayMusicThen(currentSong, () =>
			                    {
				if (!Game.Settings.Sound.Repeat)
					currentSong = GetNextSong();
				Play();
			});
		}

		string SongLengthLabel(MusicInfo song)
		{
			return "{0:D1}:{1:D2}".F(song.Length / 60, song.Length % 60);
		}

		MusicInfo GetNextSong()
		{
			if (!music.Any())
				return null;

			var songs = Game.Settings.Sound.Shuffle ? random : music;
			return songs.SkipWhile(m => m != currentSong)
				.Skip(1).FirstOrDefault() ?? songs.FirstOrDefault();
		}

		MusicInfo GetPrevSong()
		{
			if (!music.Any())
				return null;

			var songs = Game.Settings.Sound.Shuffle ? random : music;
			return songs.Reverse().SkipWhile(m => m != currentSong)
				.Skip(1).FirstOrDefault() ?? songs.Reverse().FirstOrDefault();
		}
	}
}
