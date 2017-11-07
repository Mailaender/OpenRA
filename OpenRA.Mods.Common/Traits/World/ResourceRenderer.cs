#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Visualizes the state of the `ResourceLayer`.", " Attach this to the world actor.")]
	public class ResourceRendererInfo : ITraitInfo, Requires<ResourceLayerInfo>
	{
		[FieldLoader.Require]
		[Desc("Only render these ResourceType Type names.")]
		public readonly string[] RenderTypes = null;

		public virtual object Create(ActorInitializer init) { return new ResourceRenderer(init.Self, this); }
	}

	public class ResourceRenderer : IWorldLoaded, IRenderOverlay, ITickRender, INotifyActorDisposing
	{
		protected readonly ResourceLayer ResourceLayer;

		protected readonly CellLayer<RendererCellContents> RenderContent;

		protected readonly ResourceRendererInfo Info;

		readonly HashSet<CPos> dirty = new HashSet<CPos>();

		readonly Dictionary<PaletteReference, TerrainSpriteLayer> spriteLayers = new Dictionary<PaletteReference, TerrainSpriteLayer>();

		public ResourceRenderer(Actor self, ResourceRendererInfo info)
		{
			Info = info;

			ResourceLayer = self.Trait<ResourceLayer>();
			ResourceLayer.CellChanged += AddDirtyCell;

			RenderContent = new CellLayer<RendererCellContents>(self.World.Map);
			RenderContent.CellEntryChanged += UpdateSpriteLayers;   // This is only OK because we only change the entry from ONE specific point.
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var resources = w.WorldActor.TraitsImplementing<ResourceType>()
				.ToDictionary(r => r.Info.ResourceType, r => r);

			// Build the sprite layer dictionary for rendering resources
			// All resources that have the same palette must also share a sheet and blend mode
			foreach (var r in resources)
			{
				var layer = spriteLayers.GetOrAdd(r.Value.Palette, pal =>
				{
					var first = r.Value.Variants.First().Value.First();
					return new TerrainSpriteLayer(w, wr, first.Sheet, first.BlendMode, pal, wr.World.Type != WorldType.Editor);
				});

				// Validate that sprites are compatible with this layer
				var sheet = layer.Sheet;
				if (r.Value.Variants.Any(kv => kv.Value.Any(s => s.Sheet != sheet)))
					throw new InvalidDataException("Resource sprites span multiple sheets. Try loading their sequences earlier.");

				var blendMode = layer.BlendMode;
				if (r.Value.Variants.Any(kv => kv.Value.Any(s => s.BlendMode != blendMode)))
					throw new InvalidDataException("Resource sprites specify different blend modes. "
						+ "Try using different palettes for resource types that use different blend modes.");
			}

			// Initialize the RenderContent with the initial map state
			// because the shroud may not be enabled.
			foreach (var cell in w.Map.AllCells)
			{
				var type = ResourceLayer.GetResourceType(cell);
				if (type != null && Info.RenderTypes.Contains(type.Info.Type))
					UpdateRenderedSprite(cell);
			}
		}

		protected void UpdateSpriteLayers(CPos cell)
		{
			var type = ResourceLayer.GetResourceType(cell);
			if (type != null && !Info.RenderTypes.Contains(type.Info.Type))
				return;

			var resource = RenderContent[cell];
			foreach (var kv in spriteLayers)
			{
				// resource.Type is meaningless (and may be null) if resource.Sprite is null
				if (resource.Sprite != null && type.Palette == kv.Key)
					kv.Value.Update(cell, resource.Sprite);
				else
					kv.Value.Update(cell, null);
			}
		}

		void AddDirtyCell(CPos cell, ResourceType resType)
		{
			if (resType == null || Info.RenderTypes.Contains(resType.Info.Type))
				dirty.Add(cell);
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			foreach (var kv in spriteLayers.Values)
				kv.Draw(wr.Viewport);
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			var remove = new List<CPos>();
			foreach (var c in dirty)
			{
				if (self.World.FogObscures(c))
					continue;

				UpdateRenderedSprite(c);
				remove.Add(c);
			}

			foreach (var r in remove)
				dirty.Remove(r);
		}

		protected virtual void UpdateRenderedSprite(CPos cell)
		{
			var content = ResourceLayer.GetResourceContent(cell);
			var density = content.Density;
			var type = content.Type;
			var renderContent = RenderContent[cell];

			if (type != null)
			{
				// The call chain for this method (that starts with AddDirtyCell()) guarantees
				// that the new content type would still be suitable for this renderer,
				// but that is a bit too fragile to rely on in case the code starts changing.
				if (!Info.RenderTypes.Contains(type.Info.Type))
					return;

				// Since renderContent is not nullable it will always have a value, but empty.
				// Also it is possible to have a frozen cell with resource X that upon revelaing turns out has changed to resource Y.
				if (renderContent.Variant == null || renderContent.Type != type)
					renderContent.Variant = ChooseRandomVariant(type);

				var sprites = type.Variants[renderContent.Variant];
				var maxDensity = ResourceLayer.GetMaxResourceDensity(cell);
				var frame = int2.Lerp(0, sprites.Length - 1, density, maxDensity);

				renderContent.Sprite = sprites[frame];
				renderContent.Type = type;
			}
			else
				renderContent = RendererCellContents.Empty;

			RenderContent[cell] = renderContent;
		}

		bool disposed;
		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			foreach (var kv in spriteLayers.Values)
				kv.Dispose();

			RenderContent.CellEntryChanged -= UpdateSpriteLayers;
			ResourceLayer.CellChanged -= AddDirtyCell;

			disposed = true;
		}

		protected virtual string ChooseRandomVariant(ResourceType t)
		{
			return t.Variants.Keys.Random(Game.CosmeticRandom);
		}

		public ResourceType GetRenderedResourceType(CPos cell) { return RenderContent[cell].Type; }

		// TODO: Temporary struct while the Layer/Renderer refactoring is ongoing. Rename after. (Perhaps "CellRenderContents" sounds better?)
		protected struct RendererCellContents
		{
			public static readonly RendererCellContents Empty = new RendererCellContents();
			public string Variant;
			public Sprite Sprite;
			public ResourceType Type;   // TODO: This should not be here but is only temporary while the refactoring is ongoing.
		}
	}
}
