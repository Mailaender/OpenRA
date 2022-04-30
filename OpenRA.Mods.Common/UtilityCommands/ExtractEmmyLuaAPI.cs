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
using System.Reflection;
using OpenRA.Scripting;

namespace OpenRA.Mods.Common.UtilityCommands
{
	// See https://emmylua.github.io/annotation.html for reference
	class ExtractEmmyLuaAPI : IUtilityCommand
	{
		string IUtilityCommand.Name => "--emmy-lua-api";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return true;
		}

		[Desc("Generate EmmyLua API annotations for use in IDEs.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var version = Game.ModData.Manifest.Metadata.Version;
			Console.WriteLine($"-- This is an automatically generated Lua API definition generated for {version} of OpenRA.");
			Console.WriteLine("-- https://wiki.openra.net/Utility was used with the --emmy-lua-api parameter.");
			Console.WriteLine("-- See https://docs.openra.net/en/latest/release/lua/ for human readable documentation.");
			Console.WriteLine();

			Console.WriteLine("--- This function is triggered once, after the map is loaded.");
			Console.WriteLine("function WorldLoaded() end");
			Console.WriteLine();
			Console.WriteLine("--- This function will hit every game tick which by default is every 40 ms.");
			Console.WriteLine("function Tick() end");
			Console.WriteLine();

			var tables = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptGlobal>().OrderBy(t => t.Name);
			foreach (var t in tables)
			{
				var name = t.GetCustomAttributes<ScriptGlobalAttribute>(true).First().Name;
				Console.WriteLine("---Global variable provided by the game scripting engine.");

				foreach (var obsolete in t.GetCustomAttributes(false).OfType<ObsoleteAttribute>())
				{
					Console.WriteLine("---@deprecated");
					Console.WriteLine($"--- {obsolete.Message}");
				}

				Console.WriteLine(name + " = { }");
				Console.WriteLine();

				var members = ScriptMemberWrapper.WrappableMembers(t);
				foreach (var member in members.OrderBy(m => m.Name))
				{
					var body = "";

					var propertyInfo = member as PropertyInfo;
					if (propertyInfo != null)
					{
						var attributes = propertyInfo.GetCustomAttributes(false);
						foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
							Console.WriteLine($"---@deprecated {obsolete.Message}");
					}

					var methodInfo = member as MethodInfo;
					if (methodInfo != null)
					{
						var parameters = methodInfo.GetParameters();
						foreach (var parameter in parameters)
							Console.WriteLine($"---@param {parameter.EmmyLuaString()}");

						body = parameters.Select(p => p.Name).JoinWith(", ");

						var attributes = methodInfo.GetCustomAttributes(false);
						foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
							Console.WriteLine($"---@deprecated {obsolete.Message}");

						var returnType = methodInfo.ReturnType.EmmyLuaString();
						Console.WriteLine($"---@return {returnType}");
					}

					if (member.HasAttribute<DescAttribute>())
					{
						var lines = member.GetCustomAttributes<DescAttribute>(true).First().Lines;
						foreach (var line in lines)
							Console.WriteLine($"--- {line}");
					}

					Console.WriteLine($"function {name}.{member.Name}({body}) end");
					Console.WriteLine();
				}
			}



			var actorProperties = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptActorProperties>();
			WriteScriptProperties(typeof(Actor), actorProperties);

			var playerProperties = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptPlayerProperties>();
			WriteScriptProperties(typeof(Player), playerProperties);
		}

		public void WriteScriptProperties(Type type, IEnumerable<Type> implementingTypes)
		{
			var className = $"{type.Name}Instance";
			var tableName = $"{type.Name.ToLowerInvariant()}Instance";
			Console.WriteLine($"---@class {className}");
			Console.WriteLine("local " + tableName + " = { }");
			Console.WriteLine();

			var properties = implementingTypes.SelectMany(t =>
			{
				var required = ScriptMemberWrapper.RequiredTraitNames(t);
				return ScriptMemberWrapper.WrappableMembers(t).Select(memberInfo => (memberInfo, required));
			});

			foreach (var property in properties)
			{
				var body = "";
				var isActivity = false;

				var methodInfo = property.memberInfo as MethodInfo;
				if (methodInfo != null)
				{
					var parameters = methodInfo.GetParameters();
					foreach (var parameter in parameters)
						Console.WriteLine($"---@param {parameter.EmmyLuaString()}");

					body = parameters.Select(p => p.Name).JoinWith(", ");

					var attributes = methodInfo.GetCustomAttributes(false);
					foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
						Console.WriteLine($"---@deprecated {obsolete.Message}");

					var returnType = methodInfo.ReturnType.EmmyLuaString();
					Console.WriteLine($"---@return {returnType}");

					isActivity = methodInfo.HasAttribute<ScriptActorPropertyActivityAttribute>();
				}

				var propertyInfo = property.memberInfo as PropertyInfo;
				if (propertyInfo != null)
				{
					Console.WriteLine($"---@class {className}");
					Console.Write($"---@field {propertyInfo.EmmyLuaString()} ");
				}

				if (property.memberInfo.HasAttribute<DescAttribute>())
				{
					var lines = property.memberInfo.GetCustomAttributes<DescAttribute>(true).First().Lines;

					if (propertyInfo != null)
						Console.WriteLine(lines.JoinWith(" "));
					else
						foreach (var line in lines)
							Console.WriteLine($"--- {line}");
				}

				if (isActivity)
					Console.WriteLine("--- *Queued Activity*");

				if (property.required.Any())
						Console.WriteLine($"--- **Requires {(property.required.Length == 1 ? "Trait" : "Traits")}:** {property.required.Select(GetDocumentationUrl).JoinWith(", ")}");

				if (methodInfo != null)
					Console.WriteLine($"function {tableName}.{methodInfo.Name}({body}) end");

				Console.WriteLine();
			}
		}

		static string GetDocumentationUrl(string trait)
		{
			return $"[{trait}](https://docs.openra.net/en/latest/release/traits/#{trait.ToLowerInvariant()})";
		}
	}

	public static class EmmyLuaExts
	{
		static readonly Dictionary<string, string> LuaTypeNameReplacements = new Dictionary<string, string>()
		{
			{ "Void", "void" },
			{ "Int32", "integer" },
			{ "String", "string" },
			{ "String[]", "string[]" },
			{ "Boolean", "boolean" },
			{ "Object", "any" },
			{ "LuaTable", "table" },
			{ "LuaValue", "any" },
			{ "LuaValue[]", "table" },
			{ "LuaFunction", "function" },
			{ "Actor", "ActorInstance" },
			{ "Actor[]", "ActorInstance[]" },
			{ "Player", "PlayerInstance" },
			{ "Player[]", "PlayerInstance[]" },
		};

		public static string EmmyLuaString(this Type type)
		{
			if (!LuaTypeNameReplacements.TryGetValue(type.Name, out var replacement))
				replacement = type.Name;

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				replacement = $"{type.GetGenericArguments().Select(p => p.Name).First()}?";

			return replacement;
		}

		public static string EmmyLuaString(this ParameterInfo parameterInfo)
		{
			var optional = parameterInfo.IsOptional ? "?" : "";
			return $"{parameterInfo.Name}{optional} {parameterInfo.ParameterType.EmmyLuaString()}";
		}

		public static string EmmyLuaString(this PropertyInfo propertyInfo)
		{
			return $"{propertyInfo.Name} {propertyInfo.PropertyType.EmmyLuaString()}";
		}
	}
}
