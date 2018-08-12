#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Platforms;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Scripting
{
	// Tag interfaces specifying the type of bindings to create
	public interface IScriptBindable { }

	// For objects that need the context to create their bindings
	public interface IScriptNotifyBind
	{
		void OnScriptBind(ScriptContext context);
	}

	// For traitinfos that provide actor / player commands
	public sealed class ScriptPropertyGroupAttribute : Attribute
	{
		public readonly string Category;
		public ScriptPropertyGroupAttribute(string category) { Category = category; }
	}

	// For property groups that are safe to initialize invoke on destroyed actors
	public sealed class ExposedForDestroyedActors : Attribute { }

	public sealed class ScriptActorPropertyActivityAttribute : Attribute { }

	public abstract class ScriptActorProperties
	{
		protected readonly Actor Self;
		protected readonly ScriptContext Context;
		public ScriptActorProperties(ScriptContext context, Actor self)
		{
			Self = self;
			Context = context;
		}
	}

	public abstract class ScriptPlayerProperties
	{
		protected readonly Player Player;
		protected readonly ScriptContext Context;
		public ScriptPlayerProperties(ScriptContext context, Player player)
		{
			Player = player;
			Context = context;
		}
	}

	/// <summary>
	/// Provides global bindings in Lua code.
	/// </summary>
	/// <remarks>
	/// Instance methods and properties declared in derived classes will be made available in Lua. Use
	/// <see cref="ScriptGlobalAttribute"/> on your derived class to specify the name exposed in Lua. It is recommended
	/// to apply <see cref="DescAttribute"/> against each method or property to provide a description of what it does.
	///
	/// Any parameters to your method that are <see cref="LuaValue"/>s will be disposed automatically when your method
	/// completes. If you need to return any of these values, or need them to live longer than your method, you must
	/// use <see cref="LuaValue.CopyReference"/> to get your own copy of the value. Any copied values you return will
	/// be disposed automatically, but you assume responsibility for disposing any other copies.
	/// </remarks>
	public abstract class ScriptGlobal : ScriptObjectWrapper
	{
		protected override string DuplicateKeyError(string memberName) { return "Table '{0}' defines multiple members '{1}'".F(Name, memberName); }
		protected override string MemberNotFoundError(string memberName) { return "Table '{0}' does not define a property '{1}'".F(Name, memberName); }

		public readonly string Name;
		public ScriptGlobal(ScriptContext context)
			: base(context)
		{
			// GetType resolves the actual (subclass) type
			var type = GetType();
			var names = type.GetCustomAttributes<ScriptGlobalAttribute>(true);
			if (names.Length != 1)
				throw new InvalidOperationException("[ScriptGlobal] attribute not found for global table '{0}'".F(type));

			Name = names.First().Name;
			Bind(new[] { this });
		}

		protected IEnumerable<T> FilteredObjects<T>(IEnumerable<T> objects, Closure filter)
		{
			if (filter != null)
			{
				objects = objects.Where(a =>
				{
					return filter.Call(a).CastToBool();
				});
			}

			return objects;
		}
	}

	public sealed class ScriptGlobalAttribute : Attribute
	{
		public readonly string Name;
		public ScriptGlobalAttribute(string name) { Name = name; }
	}

	public sealed class ScriptContext : IDisposable
	{
		public World World { get; private set; }
		public WorldRenderer WorldRenderer { get; private set; }

		readonly MoonSharp.Interpreter.Script runtime;
		readonly Closure tick;

		readonly Type[] knownActorCommands;
		public readonly Cache<ActorInfo, Type[]> ActorCommands;
		public readonly Type[] PlayerCommands;

		bool disposed;

		public ScriptContext(World world, WorldRenderer worldRenderer,
			IEnumerable<string> scripts)
		{
			Script.GlobalOptions.Platform = new LimitedPlatformAccessor();
			runtime = new MoonSharp.Interpreter.Script();
			runtime.Options.DebugPrint = (Action<string>)LogDebugMessage;

			Log.AddChannel("lua", "lua.log");

			World = world;
			WorldRenderer = worldRenderer;
			knownActorCommands = Game.ModData.ObjectCreator
				.GetTypesImplementing<ScriptActorProperties>()
				.ToArray();

			ActorCommands = new Cache<ActorInfo, Type[]>(FilterActorCommands);

			var knownPlayerCommands = Game.ModData.ObjectCreator
				.GetTypesImplementing<ScriptPlayerProperties>()
				.ToArray();
			PlayerCommands = FilterCommands(world.Map.Rules.Actors["player"], knownPlayerCommands);

			runtime.Globals["GameDir"] = Platform.GameDir;
			tick = runtime.Globals.Get("Tick").Function;

			// Register globals
			runtime.Globals["FatalError"] = (Action<string>)FatalError;

			UserData.RegisterAssembly();

			var bindings = Game.ModData.ObjectCreator.GetTypesImplementing<ScriptGlobal>();
			foreach (var b in bindings)
			{
				var ctor = b.GetConstructors(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(c => {
					var p = c.GetParameters();
					return p.Length == 1 && p.First().ParameterType == typeof(ScriptContext);
				});

				if (ctor == null)
					throw new InvalidOperationException("{0} must define a constructor that takes a ScriptContext context parameter".F(b.Name));

				var binding = (ScriptGlobal)ctor.Invoke(new[] { this });


				runtime.Globals[binding.Name] = UserData.Create(binding);
			}

			foreach (var s in scripts)
				runtime.LoadStream(world.Map.Open(s));
		}

		void LogDebugMessage(string message)
		{
			Console.WriteLine("Lua debug: {0}", message);
			Log.Write("lua", message);
		}

		public bool FatalErrorOccurred { get; private set; }
		public void FatalError(string message)
		{
			var stacktrace = new StackTrace().ToString();
			Console.WriteLine("Fatal Lua Error: {0}", message);
			Console.WriteLine(stacktrace);

			Log.Write("lua", "Fatal Lua Error: {0}", message);
			Log.Write("lua", stacktrace);

			FatalErrorOccurred = true;

			World.AddFrameEndTask(w =>
			{
				World.EndGame();
				World.SetPauseState(true);
				World.PauseStateLocked = true;
			});
		}

		public void RegisterMapActor(string name, Actor a)
		{
			if (runtime.Globals.Get(name) != null)
				throw new MoonSharp.Interpreter.DynamicExpressionException("The global name '{0}' is reserved, and may not be used by a map actor".F(name));

			runtime.Globals[name] = a;
		}

		public void WorldLoaded()
		{
			if (FatalErrorOccurred)
				return;

			runtime.Globals.Get("WorldLoaded").Function.Call();
		}

		public void Tick(Actor self)
		{
			if (FatalErrorOccurred || disposed)
				return;

			using (new PerfSample("tick_lua"))
				tick.Call();
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
		}

		static IEnumerable<Type> ExtractRequiredTypes(Type t)
		{
			// Returns the inner types of all the Requires<T> interfaces on this type
			var outer = t.GetInterfaces()
				.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Requires<>));

			return outer.SelectMany(i => i.GetGenericArguments());
		}

		static readonly object[] NoArguments = new object[0];
		Type[] FilterActorCommands(ActorInfo ai)
		{
			return FilterCommands(ai, knownActorCommands);
		}

		Type[] FilterCommands(ActorInfo ai, Type[] knownCommands)
		{
			var method = typeof(ActorInfo).GetMethod("HasTraitInfo");
			return knownCommands.Where(c => ExtractRequiredTypes(c)
				.All(t => (bool)method.MakeGenericMethod(t).Invoke(ai, NoArguments)))
				.ToArray();
		}

		public Table CreateTable() { return new Table(runtime); }
	}
}
