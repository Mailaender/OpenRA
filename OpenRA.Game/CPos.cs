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
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Interop.BasicDescriptors;
using OpenRA.Scripting;

namespace OpenRA
{
	[MoonSharpUserData]
	public struct CPos : IScriptBindable, IEquatable<CPos>, IMemberDescriptor
	{
		public readonly int X, Y;
		public readonly byte Layer;

		public CPos(int x, int y) { X = x; Y = y; Layer = 0; }
		public CPos(int x, int y, byte layer) { X = x; Y = y; Layer = layer; }
		public static readonly CPos Zero = new CPos(0, 0, 0);

		public static explicit operator CPos(int2 a) { return new CPos(a.X, a.Y); }

		public static CPos operator +(CVec a, CPos b) { return new CPos(a.X + b.X, a.Y + b.Y, b.Layer); }
		public static CPos operator +(CPos a, CVec b) { return new CPos(a.X + b.X, a.Y + b.Y, a.Layer); }
		public static CPos operator -(CPos a, CVec b) { return new CPos(a.X - b.X, a.Y - b.Y, a.Layer); }
		public static CVec operator -(CPos a, CPos b) { return new CVec(a.X - b.X, a.Y - b.Y); }

		public static bool operator ==(CPos me, CPos other) { return me.X == other.X && me.Y == other.Y && me.Layer == other.Layer; }
		public static bool operator !=(CPos me, CPos other) { return !(me == other); }

		public override int GetHashCode() { return X.GetHashCode() ^ Y.GetHashCode() ^ Layer.GetHashCode(); }

		public bool Equals(CPos other) { return X == other.X && Y == other.Y && Layer == other.Layer; }
		public override bool Equals(object obj) { return obj is CPos && Equals((CPos)obj); }

		public override string ToString() { return X + "," + Y; }

		public MPos ToMPos(Map map)
		{
			return ToMPos(map.Grid.Type);
		}

		public MPos ToMPos(MapGridType gridType)
		{
			if (gridType == MapGridType.Rectangular)
				return new MPos(X, Y);

			// Convert from RectangularIsometric cell (x, y) position to rectangular map position (u, v)
			//  - The staggered rows make this fiddly (hint: draw a diagram!)
			// (a) Consider the relationships:
			//  - +1x (even -> odd) adds (0, 1) to (u, v)
			//  - +1x (odd -> even) adds (1, 1) to (u, v)
			//  - +1y (even -> odd) adds (-1, 1) to (u, v)
			//  - +1y (odd -> even) adds (0, 1) to (u, v)
			// (b) Therefore:
			//  - ax + by adds (a - b)/2 to u (only even increments count)
			//  - ax + by adds a + b to v
			var u = (X - Y) / 2;
			var v = X + Y;
			return new MPos(u, v);
		}

		#region Scripting interface

		public DynValue this[Script runtime, DynValue key]
		{
			get
			{
				switch (key.ToString())
				{
				case "X": return DynValue.FromObject(runtime, X);
				case "Y": return DynValue.FromObject(runtime, Y);
				default: throw new ScriptRuntimeException("CPos does not define a member '{0}'".F(key));
				}
			}

			set
			{
				throw new ScriptRuntimeException("CPos is read-only. Use CPos.New to create a new value");
			}
		}

		public DynValue GetValue(Script script, object obj)
		{
			throw new NotImplementedException();
		}

		public bool IsStatic
		{
			get { return false; }
		}

		public MemberDescriptorAccess MemberAccess
		{
			get { return MemberDescriptorAccess.CanRead; }
		}

		public string Name
		{
			get { return "CPos"; }
		}

		public void SetValue(Script script, object obj, DynValue value)
		{
			throw new ScriptRuntimeException("CPos is immutable. Use CPos.New to create a new value.");
		}

		#endregion
	}
}