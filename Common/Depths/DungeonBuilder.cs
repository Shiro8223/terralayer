using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths.Rooms;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Depths
{
	/// <summary>Turns a <see cref="FloorPlan"/> into tiles. Server / singleplayer only.</summary>
	public static class DungeonBuilder
	{
		private static HeirClass favored;
		private static float goldMult;
		private static int tier;
		private static Random lootRng;

		/// <summary>Treasure rooms on floor 2+ sometimes hold a Mimic instead of a chest. Deterministic per room.</summary>
		public static bool IsMimicVault(RoomPlan r, int floor) => floor >= 2 && new Random(r.Seed ^ 0x3A5).NextDouble() < 0.2;

		/// <summary>Secret: on floors 4 and 5 a treasure room may hide a Hallowed Mimic, which carries the Key of Light.</summary>
		public static bool IsHallowedMimicVault(RoomPlan r, int floor) => floor >= 4 && !IsMimicVault(r, floor) && new Random(r.Seed ^ 0x7A1).NextDouble() < 0.25;

		/// <summary>Chest style of a placeable chest item (e.g. ItemID.CrimsonChest).</summary>
		public static int ChestStyle(int chestItem) => new Item(chestItem).placeStyle;

		public static void BuildFloor(FloorPlan plan, IEnumerable<Rectangle> previous, HeirClass runClass, float runGoldMult)
		{
			favored = runClass;
			goldMult = runGoldMult;
			tier = plan.Floor;
			lootRng = new Random(plan.Seed ^ 0x77);

			Gen.Run(() => {
				// Fill in the previous floor's rooms so nothing stale is left lying around.
				foreach (Rectangle old in previous) {
					Gen.ClearEntities(old, includeFriendly: true);
					Gen.Reset(old, TileID.Stone, WallID.Stone);
					Gen.Resync(old);
				}

				foreach (RoomPlan room in plan.Rooms)
					BuildRoom(plan, room);
			});
		}

		private static void BuildRoom(FloorPlan plan, RoomPlan r)
		{
			FloorInfo f = plan.Info;
			RoomArchetype arch = RoomArchetype.For(r.Arch);
			var rng = new Random(r.Seed ^ 0x51ED);

			Gen.ClearEntities(r.Bounds, includeFriendly: true);
			Gen.Reset(r.Bounds, f.Brick, f.BrickWall);
			Gen.Air(r.Interior);
			arch.Build(r, f, rng);

			foreach (RiftPlan rift in plan.Rifts.Where(x => x.Room == r.Id))
				Gen.Ledge(rift.Pos.X, rift.Pos.Y, 2, f.Brick);

			Gen.FrameArea(r.Bounds);
			arch.Decorate(r, f, rng);

			if (r.Arch == Archetype.HellBridge) {
				// Secret: a crimson chest at the very far end of the bridge, holding a Pirate Map.
				int c = PlaceChest(r.Interior.Right - 8, Rooms.HellBridgeRoom.BridgeY(r), ChestStyle(ItemID.CrimsonChest), null);
				if (c >= 0)
					Main.chest[c].item[0] = new Item(ItemID.PirateMap);
			}
			if (r.Kind == RoomKind.Treasure && !IsMimicVault(r, plan.Floor) && !IsHallowedMimicVault(r, plan.Floor))
				PlaceLootChest(r.Feature.X, r.Feature.Y, ChestKind.Gold, r.Interior);
			else if (r.Kind == RoomKind.Combat && rng.NextDouble() < 0.15 && RoomArchetype.FindFloorSpot(r.Interior, rng, 2, out Point spot))
				PlaceLootChest(spot.X, spot.Y, ChestKind.Wooden);

			Gen.FrameArea(r.Bounds);
			Gen.Resync(r.Bounds);
		}

		/// <summary>Places a chest resting on <paramref name="floorY"/> near <paramref name="x"/> and fills it.</summary>
		public static int PlaceLootChest(int x, int floorY, ChestKind kind, Rectangle? fallbackArea = null)
		{
			int index = PlaceChest(x, floorY, kind == ChestKind.Wooden ? 0 : 1, fallbackArea);
			if (index >= 0) {
				lootRng ??= new Random();
				Loot.Fill(Main.chest[index], Loot.ChestContents(kind, tier, favored, goldMult, lootRng));
			}
			return index;
		}

		/// <summary>Places an empty chest of <paramref name="style"/> resting on <paramref name="floorY"/> near <paramref name="x"/>.</summary>
		public static int PlaceChest(int x, int floorY, int style, Rectangle? fallbackArea)
		{
			int index = -1;
			Gen.Run(() => {
				foreach (int dx in new[] { 0, 1, -1, 2, -2, 3, -3, 4, -4, 6, -6 }) {
					int cx = x + dx;
					if (!Gen.IsSolid(cx, floorY) || !Gen.IsSolid(cx + 1, floorY))
						continue;
					for (int y = floorY - 2; y < floorY; y++) {
						Gen.Air(cx, y);
						Gen.Air(cx + 1, y);
					}
					index = WorldGen.PlaceChest(cx, floorY - 1, TileID.Containers, false, style);
					if (index >= 0)
						break;
				}
				// Couldn't fit at the intended spot: look anywhere in the room for flat ground.
				var rng = new Random(x * 7 + floorY);
				for (int tries = 0; index < 0 && fallbackArea.HasValue && tries < 6; tries++)
					if (Rooms.RoomArchetype.FindFloorSpot(fallbackArea.Value, rng, 2, out Point spot))
						index = WorldGen.PlaceChest(spot.X, spot.Y - 1, TileID.Containers, false, style);
			});
			return index;
		}

		/// <summary>Drops chest-style loot as items instead (used for Mimics, or if a chest won't fit).</summary>
		public static void DropLoot(Rectangle area, ChestKind kind, int floor, HeirClass cls, float mult)
		{
			var rng = new Random(Main.rand.Next());
			foreach (Item item in Loot.ChestContents(kind, floor, cls, mult, rng))
				Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("Terralayer"), area, item);
		}
	}
}
