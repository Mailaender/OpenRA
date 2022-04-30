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
			Console.WriteLine();
			Console.WriteLine("--");
			Console.WriteLine("-- SECTION Manually added:");
			Console.WriteLine("--");
			Console.WriteLine();
			Console.WriteLine();

			WriteManual();

			Console.WriteLine();
			Console.WriteLine("--");
			Console.WriteLine("-- SECTION Script API global methods:");
			Console.WriteLine("--");
			Console.WriteLine();
			Console.WriteLine();

			var globalTables = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptGlobal>().OrderBy(t => t.Name);
			WriteGlobals(globalTables);

			Console.WriteLine();
			Console.WriteLine("--");
			Console.WriteLine("-- SECTION Script API object properties:");
			Console.WriteLine("--");
			Console.WriteLine();
			Console.WriteLine();

			var actorProperties = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptActorProperties>();
			WriteScriptProperties(typeof(Actor), actorProperties);

			var playerProperties = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptPlayerProperties>();
			WriteScriptProperties(typeof(Player), playerProperties);
		}

		void WriteManual()
		{
			Console.WriteLine("--- This function is triggered once, after the map is loaded.");
			Console.WriteLine("function WorldLoaded() end");
			Console.WriteLine();
			Console.WriteLine("--- This function will hit every game tick which by default is every 40 ms.");
			Console.WriteLine("function Tick() end");
			Console.WriteLine();

			Console.WriteLine("---@alias Color HSLColor");
			Console.WriteLine();

			// TODO: Maybe get all ActorInits and generate this from them? But are they all supported?
			Console.WriteLine("---A subset of all ActorInit implementations.");
			Console.WriteLine("---@class initTable");
			Console.WriteLine("---@field Location CPos");
			Console.WriteLine("---@field Owner PlayerInstance");
			Console.WriteLine("---@field Facing WAngle");
			Console.WriteLine("---@field CreationActivityDelay integer");
			Console.WriteLine("---@field CenterPosition WPos");
			Console.WriteLine("---@field Faction string");
			Console.WriteLine("---@field EffectiveOwner PlayerInstance");
			Console.WriteLine("---@field Stance UnitStance");
			Console.WriteLine("---@field FreeActor boolean");
			Console.WriteLine("---@field ParentActor ActorInitActorReference");
			Console.WriteLine("---@field LineBuildDirection LineBuildDirection");
			Console.WriteLine("---@field LineBuildParent string[]");
			Console.WriteLine("---@field RuntimeCargo ActorInstance[]");
			Console.WriteLine("---@field Cargo string[]");
			Console.WriteLine("---@field DeployState DeployState");
			Console.WriteLine("---@field Experience integer");
			Console.WriteLine("---@field Health integer");
			Console.WriteLine("---@field HuskSpeed integer");
			Console.WriteLine("---@field Plug string");
			Console.WriteLine("---@field ProductionSpawnLocation CPos");
			Console.WriteLine("---@field ScriptTags string[]");
			Console.WriteLine("---@field TurretFacing WAngle");
			Console.WriteLine("---@field SpawnedByMap string");
			Console.WriteLine("---@field BodyAnimationFrame integer");
			Console.WriteLine();
			Console.WriteLine("---@class CPos");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine();
			Console.WriteLine("---@class WPos");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine("---@field Z integer");
			Console.WriteLine();
			Console.WriteLine("---@class WAngle");
			Console.WriteLine("---@field Angle integer");
			Console.WriteLine();
			Console.WriteLine("---@class WDist");
			Console.WriteLine("---@field Length integer");
			Console.WriteLine();
			Console.WriteLine("---@class WVec");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine("---@field Z integer");
			Console.WriteLine();
			Console.WriteLine("---@class CVec");
			Console.WriteLine("---@field X integer");
			Console.WriteLine("---@field Y integer");
			Console.WriteLine();
		}

		void WriteGlobals(IEnumerable<Type> globalTables)
		{
			foreach (var t in globalTables)
			{
				var name = t.GetCustomAttributes<ScriptGlobalAttribute>(true).First().Name;
				Console.WriteLine("---Global variable provided by the game scripting engine.");

				foreach (var obsolete in t.GetCustomAttributes(false).OfType<ObsoleteAttribute>())
				{
					Console.WriteLine("---@deprecated");
					Console.WriteLine($"--- {obsolete.Message}");
				}

				Console.WriteLine(name + " = {");

				var members = ScriptMemberWrapper.WrappableMembers(t);
				foreach (var member in members.OrderBy(m => m.Name))
				{
					Console.WriteLine();

					var parameterString = "";

					var propertyInfo = member as PropertyInfo;
					if (propertyInfo != null)
					{
						var attributes = propertyInfo.GetCustomAttributes(false);
						foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
							Console.WriteLine($"    ---@deprecated {obsolete.Message}");
					}

					var methodInfo = member as MethodInfo;
					if (methodInfo != null)
					{
						var parameters = methodInfo.GetParameters();
						foreach (var parameter in parameters)
							Console.WriteLine($"    ---@param {parameter.EmmyLuaString()}");

						parameterString = parameters.Select(p => p.Name).JoinWith(", ");

						var attributes = methodInfo.GetCustomAttributes(false);
						foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
							Console.WriteLine($"    ---@deprecated {obsolete.Message}");

						var returnType = methodInfo.ReturnType.EmmyLuaString();
						Console.WriteLine($"    ---@return {returnType}");
					}

					if (member.HasAttribute<DescAttribute>())
					{
						var lines = member.GetCustomAttributes<DescAttribute>(true).First().Lines;
						foreach (var line in lines)
							Console.WriteLine($"    --- {line}");
					}

					Console.WriteLine($"    {member.Name} = function({parameterString}) end;");
				}

				Console.WriteLine("}");
				Console.WriteLine();
			}
		}

		void WriteScriptProperties(Type type, IEnumerable<Type> implementingTypes)
		{
			var className = $"{type.Name}Instance";
			var tableName = $"{type.Name.ToLowerInvariant()}Instance";
			Console.WriteLine($"---@class {className}");
			Console.WriteLine("local " + tableName + " = {");

			var properties = implementingTypes.SelectMany(t =>
			{
				var required = ScriptMemberWrapper.RequiredTraitNames(t);
				return ScriptMemberWrapper.WrappableMembers(t).Select(memberInfo => (memberInfo, required));
			});

			foreach (var property in properties)
			{
				Console.WriteLine();

				var body = "";
				var isActivity = false;

				var methodInfo = property.memberInfo as MethodInfo;
				if (methodInfo != null)
				{
					var parameters = methodInfo.GetParameters();
					foreach (var parameter in parameters)
						Console.WriteLine($"    ---@param {parameter.EmmyLuaString()}");

					body = parameters.Select(p => p.Name).JoinWith(", ");

					var attributes = methodInfo.GetCustomAttributes(false);
					foreach (var obsolete in attributes.OfType<ObsoleteAttribute>())
						Console.WriteLine($"    ---@deprecated {obsolete.Message}");

					var returnType = methodInfo.ReturnType.EmmyLuaString();
					Console.WriteLine($"    ---@return {returnType}");

					isActivity = methodInfo.HasAttribute<ScriptActorPropertyActivityAttribute>();
				}

				var propertyInfo = property.memberInfo as PropertyInfo;
				if (propertyInfo != null)
				{
					Console.WriteLine($"    ---@class {className}");
					Console.Write($"    ---@field {propertyInfo.EmmyLuaString()} ");
				}

				if (property.memberInfo.HasAttribute<DescAttribute>())
				{
					var lines = property.memberInfo.GetCustomAttributes<DescAttribute>(true).First().Lines;

					if (propertyInfo != null)
						Console.WriteLine(lines.JoinWith(" "));
					else
						foreach (var line in lines)
							Console.WriteLine($"    --- {line}");
				}

				if (isActivity)
					Console.WriteLine("    --- *Queued Activity*");

				if (property.required.Any())
						Console.WriteLine($"    --- **Requires {(property.required.Length == 1 ? "Trait" : "Traits")}:** {property.required.Select(GetDocumentationUrl).JoinWith(", ")}");

				if (methodInfo != null)
					Console.WriteLine($"    {methodInfo.Name} = function({body}) end;");
			}

			Console.WriteLine("}");
			Console.WriteLine();
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

			var parameterType = parameterInfo.ParameterType.EmmyLuaString();

			// A hack for ActorGlobal.Create().
			if (parameterInfo.Name == "initTable")
				parameterType = "initTable";

			return $"{parameterInfo.Name}{optional} {parameterType}";
		}

		public static string EmmyLuaString(this PropertyInfo propertyInfo)
		{
			return $"{propertyInfo.Name} {propertyInfo.PropertyType.EmmyLuaString()}";
		}
	}
}
