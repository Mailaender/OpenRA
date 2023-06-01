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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Traits;
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
					var chromeReference = field.GetCustomAttributes<ChromeReferenceAttribute>(true).FirstOrDefault();
					if (chromeReference == null)
						continue;

					var instance = CreateWidgetInstance(modData, widgetType);
					var baseKey = (string)field.GetValue(instance);
					if (string.IsNullOrWhiteSpace(baseKey))
					{
						System.Console.WriteLine($"{field.Name} has an empty base key.");
						continue;
					}

					chromeWidgetReferences.Add(baseKey);

					var prefix = GetPrefix(field, fields, modData, widgetType);
					if (!string.IsNullOrEmpty(prefix))
					{
						System.Console.WriteLine($"PREFIX {prefix}");
						chromeWidgetReferences.Add(prefix + baseKey);
					}


					var suffixes = GetSuffixes(field, fields, modData, widgetType);
					foreach (var suffix in suffixes)
						chromeWidgetReferences.Add(baseKey + suffix);

					foreach (var filename in modData.Manifest.ChromeLayout)
						ExtractChromeLayout(MiniYaml.FromStream(modData.DefaultFileSystem.Open(filename), filename), field.Name, factions, prefix, suffixes);
				}
			}
		}

		static string GetPrefix(FieldInfo fieldInfo, IEnumerable<FieldInfo> fields, ModData modData, Type widgetType)
		{
			foreach (var field in fields)
			{
				var chromeReferencePrefix = field.GetCustomAttributes<ChromeReferencePrefixAttribute>(true).FirstOrDefault();
				if (chromeReferencePrefix == null)
					continue;

				System.Console.WriteLine($"{chromeReferencePrefix.ChromeReference} vs {fieldInfo.Name}");

				if (chromeReferencePrefix.ChromeReference == fieldInfo.Name)
				{
					var prefixedInstance = CreateWidgetInstance(modData, widgetType);
					var prefix = (string)field.GetValue(prefixedInstance);
					return prefix;
				}
			}

			return string.Empty;
		}

		static IEnumerable<string> GetSuffixes(FieldInfo fieldInfo, IEnumerable<FieldInfo> fields, ModData modData, Type widgetType)
		{
			var suffixes = new List<string>();

			foreach (var field in fields)
			{
				var chromeReferenceSuffix = field.GetCustomAttributes<ChromeReferenceSuffixAttribute>(true).FirstOrDefault();
				if (chromeReferenceSuffix == null)
					continue;

				if (chromeReferenceSuffix.ChromeReference == fieldInfo.Name)
				{
					var suffixedInstance = CreateWidgetInstance(modData, widgetType);
					var suffix = (string)field.GetValue(suffixedInstance);
					suffixes.Add(suffix);
				}
			}

			return suffixes;
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

		void ExtractChromeLayout(List<MiniYamlNode> nodes, string key, IEnumerable<string> factions, string prefix, IEnumerable<string> suffixes)
		{
			foreach (var node in nodes)
			{
				if (node.Value == null)
					continue;

				if (node.Key == key)
				{
					if (IsFactionSuffixed(nodes))
					{
						foreach (var faction in factions)
						{
							if (faction == "Random")
								continue;

							var chromeReference = node.Value.Value;
							chromeReference = node.Value.Value;
							chromeReference = chromeReference + "-" + faction;

							if (!string.IsNullOrEmpty(prefix))
								chromeReference = prefix + chromeReference;

							if (suffixes.Any())
								foreach (var suffix in suffixes)
									chromeWidgetReferences.Add(chromeReference + suffix);
							else
								chromeWidgetReferences.Add(chromeReference);
						}
					}
					else
					{
						var chromeReference = node.Value.Value;

						if (!string.IsNullOrEmpty(prefix))
							chromeReference = prefix + chromeReference;

						if (suffixes.Any())
								foreach (var suffix in suffixes)
									chromeWidgetReferences.Add(chromeReference + suffix);
						else
							chromeWidgetReferences.Add(chromeReference);
					}
				}

				if (node.Value.Nodes != null)
					ExtractChromeLayout(node.Value.Nodes, key, factions, prefix, suffixes);

				//Console.WriteLine($"\t\t{node.Value.Value}");
			}
		}

		static bool IsFactionSuffixed(List<MiniYamlNode> nodes)
		{
			var factionSuffixed = false;

			foreach (var node in nodes)
			{
				if (node.Key == "Logic" && node.Value.Value == "AddFactionSuffixLogic")
					factionSuffixed = true;
			}

			return factionSuffixed;
		}
	}
}
