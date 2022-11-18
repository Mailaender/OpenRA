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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenRA.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Lint
{
	class CheckChrome : ILintPass
	{
		const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

		readonly HashSet<string> chromeWidgetReferences = new();
		readonly HashSet<string> chromeReferences = new();

		public void Run(Action<string> emitError, Action<string> emitWarning, ModData modData)
		{
			ChromeMetrics.Initialize(modData);
			AddChromeReferences(modData);

			foreach (var filename in modData.Manifest.Chrome)
				CheckChromeReferences(MiniYaml.FromStream(modData.DefaultFileSystem.Open(filename), filename), filename, emitWarning);

			CheckChromeWidgetReferences(emitError);
		}

		void AddChromeReferences(ModData modData)
		{
			var factions = modData.DefaultRules.Actors[SystemActors.World].TraitInfos<FactionInfo>().Select(f => f.InternalName).ToArray();

			foreach (var widgetType in modData.ObjectCreator.GetTypesImplementing<Widget>())
			{
				System.Console.WriteLine(widgetType.Name);
				var fields = widgetType.GetFields().Concat(widgetType.BaseType.GetFields());
				foreach (var field in fields)
				{
					var chromeReferencePrefix = field.GetCustomAttributes<ChromeReferencePrefixAttribute>(true).FirstOrDefault();
					if (chromeReferencePrefix != null)
					{
						var prefixedInstance = CreateWidgetInstance(modData, widgetType);
						var prefix = (string)field.GetValue(prefixedInstance);
						var chromeField = fields.First(f => f.Name == chromeReferencePrefix.ChromeReference);
						var baseName = (string)chromeField.GetValue(prefixedInstance);
						chromeWidgetReferences.Add(prefix + baseName);
						continue;
					}

					var chromeReferenceSuffix = field.GetCustomAttributes<ChromeReferenceSuffixAttribute>(true).FirstOrDefault();
					if (chromeReferenceSuffix != null)
					{
						var suffixedInstance = CreateWidgetInstance(modData, widgetType);
						var suffix = (string)field.GetValue(suffixedInstance);
						var chromeField = fields.First(f => f.Name == chromeReferenceSuffix.ChromeReference);
						var baseName = (string)chromeField.GetValue(suffixedInstance);
						chromeWidgetReferences.Add(baseName + suffix);
						System.Console.WriteLine("\t"+baseName + suffix);
						continue;
					}

					var chromeReference = field.GetCustomAttributes<ChromeReferenceAttribute>(true).FirstOrDefault();
					if (chromeReference == null)
						continue;

					if (field.Name == nameof(ImageWidget.ImageCollection))
					foreach (var filename in modData.Manifest.ChromeLayout)
						ExtractChromeLayout(MiniYaml.FromStream(modData.DefaultFileSystem.Open(filename), filename), field.Name);


					var instance = CreateWidgetInstance(modData, widgetType);
					var key = (string)field.GetValue(instance);


					var logic = fields.First(f => f.Name == "Logic");
					var logics = (string[])logic.GetValue(instance);
					if (logics.Any(l => l == "AddFactionSuffixLogic"))
					{
						foreach (var faction in factions)
							if (!ChromeMetrics.TryGet<string>("FactionSuffix-" + faction, out var factionSuffix))
								key = key + "-" + factionSuffix;
					}

					System.Console.WriteLine("\t"+key);
					chromeWidgetReferences.Add(key);

				}
			}
		}

		static Widget CreateWidgetInstance(ModData modData, Type widgetType)
		{
			var widgetArguments = new WidgetArgs { { "modData", modData }, { "world", null } };
			if (widgetType.Name == nameof(ImageWidget))
				return modData.ObjectCreator.CreateObject<Widget>(widgetType.Name);
			else
				return modData.ObjectCreator.CreateObject<Widget>(widgetType.Name, widgetArguments);
		}

		void CheckChromeReferences(List<MiniYamlNode> nodes, string filename, Action<string> emitWarning)
		{
			foreach (var node in nodes)
			{
				var chromeReference = node.Key;
				if (chromeReference.StartsWith("^"))
					continue;

				chromeReferences.Add(chromeReference);

				if (!chromeWidgetReferences.Contains(chromeReference))
					emitWarning($"{filename} refers to unused chrome reference `{chromeReference}` that does not exist.");
			}
		}

		void CheckChromeWidgetReferences(Action<string> emitError)
		{
			foreach (var chromeWidgetReference in chromeWidgetReferences)
			{
				if (!chromeReferences.Contains(chromeWidgetReference))
					emitError($"{chromeWidgetReference} is undefined in Chrome definitions.");
			}
		}

		void ExtractChromeLayout(List<MiniYamlNode> nodes, string key)
		{
			//System.Console.WriteLine($"Checking {key}");
			foreach (var node in nodes)
			{
				if (node.Value == null)
					continue;

				if (node.Key == key)
				{
					chromeWidgetReferences.Add(node.Value.Value);
					Console.WriteLine($"\t\t{node.Value.Value}");
				}

				if (node.Value.Nodes != null)
					ExtractChromeLayout(node.Value.Nodes, key);
			}
		}
	}
}
