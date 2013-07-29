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

namespace OpenRA.Mods.Cnc.Widgets.Logic
{
	public class CncInstallMusicLogic
	{
		bool installed;
		Widget panel;

		[ObjectCreator.UseCtor]
		public CncInstallMusicLogic(Widget widget, Action onExit)
		{
			panel = widget.Get("MUSIC_INSTALL_PANEL");

			installed = Rules.InstalledMusic.Any();
			Func<bool> noMusic = () => !installed;

			Action afterInstall = () =>
			{
				try
				{
					var path = new string[] { Platform.SupportDir, "Content", "cnc" }.Aggregate(Path.Combine);
					FileSystem.Mount(Path.Combine(path, "scores.mix"));
					FileSystem.Mount(Path.Combine(path, "transit.mix"));
					Rules.Music.Do(m => m.Value.Reload());
				}
				catch (Exception e)
				{
					Log.Write("debug", "Mounting the new mixfile and rebuild of scores list failed:\n{0}", e);
				}

				installed = Rules.InstalledMusic.Any();
				// TODO: MusicPlayerLogic.BuildMusicTable(musicList);
			};

			var installButton = panel.Get<ButtonWidget>("INSTALL_BUTTON");
			installButton.OnClick = () =>
				Ui.OpenWindow("INSTALL_MUSIC_PANEL", new WidgetArgs() {
					{ "afterInstall", afterInstall },
					{ "filesToCopy", new [] { "SCORES.MIX" } },
					{ "filesToExtract", new [] { "transit.mix" } },
				});
			installButton.IsVisible = () => Rules.InstalledMusic.ToArray().Length < 3; // Hack around music being split between transit.mix and scores.mix

			panel.Get("NO_MUSIC_LABEL").IsVisible = noMusic;
		}
	}
}
