using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Secrets
{
	/// <summary>
	/// The hidden sea: a beach and a stretch of open water carved into the mountains at the world's right edge. It has
	/// to be there: the game only counts a place as ocean within 380 tiles of the edge and above the caverns, and
	/// Duke Fishron enrages anywhere else. Depths rooms are never placed over it.
	/// </summary>
	public static class OceanArena
	{
		public const int Width = 240;
		public const int Height = 60;

		public static int Left => Main.maxTilesX - 70 - Width;

		public static int FloorY => (int)Main.worldSurface - 40;

		/// <summary>The carved space: beach, sea and the air above them.</summary>
		public static Rectangle Area => new(Left, FloorY - Height, Width, Height + 16);

		/// <summary>Kept clear of Depths rooms.</summary>
		public static Rectangle Exclusion => new(Left - 40, FloorY - Height - 30, Width + 70, Height + 60);

		public static bool Contains(Vector2 worldPos) => Area.Contains(worldPos.ToTileCoordinates());

		public static Point AnglerSpot => new(Left + 46, FloorY);

		/// <summary>The way home (bottom-center tile).</summary>
		public static Point ReturnPortal => new(Left + 10, FloorY);

		public static Vector2 Arrival(Player p) => new((Left + 24) * 16 + 8 - p.width / 2f, FloorY * 16 - p.height - 1);

		/// <summary>Where Duke Fishron rises from the water.</summary>
		public static Vector2 DukeSpawn => new((Left + Width - 50) * 16, (FloorY - 6) * 16);

		public static void Build()
		{
			Gen.Run(() => {
				Rectangle a = Area;
				for (int x = a.Left; x < a.Right; x++)
					for (int y = a.Top; y < a.Bottom; y++)
						Main.tile[x, y].ClearEverything();

				int beachEnd = Left + 70;
				for (int x = Left; x < a.Right; x++) {
					// The beach, then a slope down to the sea bed.
					int surface = x < beachEnd ? FloorY : Math.Min(FloorY + 14, FloorY + (x - beachEnd) / 1 + 1);
					for (int y = surface; y < a.Bottom; y++)
						Gen.Solid(x, y, y == surface ? TileID.Sand : TileID.HardenedSand);
					if (surface > FloorY)
						Gen.Liquid(new Rectangle(x, FloorY + 1, 1, surface - FloorY - 1), LiquidID.Water);
				}

				// Palm-wood platforms over the water to fight from.
				for (int x = beachEnd + 12; x < a.Right - 12; x += 46) {
					Gen.Platform(x, FloorY - 8, 26, 17);
					Gen.Platform(x + 18, FloorY - 20, 22, 17);
				}
				Gen.FrameArea(a);
				foreach (int x in new[] { Left + 14, Left + 34, Left + 58 })
					WorldGen.GrowPalmTree(x, FloorY);
				for (int x = Left + 4; x < beachEnd; x += 16)
					Gen.PlaceOnFloor(x, FloorY, TileID.Lampposts);
				Gen.FrameArea(a);
				Gen.Resync(a);
			});
		}
	}
}
