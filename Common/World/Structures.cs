using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terralayer.Common.Utilities;
using Terralayer.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.World
{
	/// <summary>
	/// A building on the homestead. They are bought from the Steward strictly in order; each one physically appears
	/// on its plot, may bring a resident, and unlocks new Estate upgrades.
	/// </summary>
	public record StructureDef(
		string Key,
		string Name,
		string Description,
		long Cost,
		int Width,
		int Height,
		bool House,
		ushort Block,
		ushort Wall,
		ushort Roof,
		Func<int> Resident,
		Action<int, int, int, int> Furnish); // left, right, floorY, topY

	public static class Structures
	{
		public static readonly List<StructureDef> All = new() {
			new("Hall", "Founder's Hall", "Home of the Steward, keeper of the family estate.", 0,
				26, 10, true, TileID.WoodBlock, WallID.Planked, TileID.RedDynastyShingles,
				() => ModContent.NPCType<Steward>(),
				(l, r, f, t) => {
					Gen.PlaceOnFloor(l + 4, f, TileID.Beds);
					Gen.PlaceOnFloor(l + 9, f, TileID.Fireplace);
					Gen.PlaceOnFloor(r - 9, f, TileID.Bookcases);
					Gen.PlaceOnFloor(r - 5, f, TileID.GrandfatherClocks);
					Gen.Place((l + r) / 2, t + 1, TileID.Chandeliers);
				}),
			new("Gatehouse", "Gatehouse", "The Gatekeeper's post, and the only way down into the Depths.", 0,
				20, 10, true, TileID.GrayBrick, WallID.GrayBrick, TileID.BlueDynastyShingles,
				() => ModContent.NPCType<Gatekeeper>(),
				(l, r, f, t) => {
					Gen.PlaceOnFloor(r - 5, f, TileID.Safes);
					Gen.Place(l + 4, t + 1, TileID.Banners, 0);
					Gen.Place(r - 4, t + 1, TileID.Banners, 0);
				}),
			new("Smithy", "Smithy", "A forge and a Smith. Unlocks Forge Mastery (stronger starting weapons) and Might.", 10 * Coins.Gold,
				24, 9, true, TileID.StoneSlab, WallID.StoneSlab, TileID.RedDynastyShingles,
				() => ModContent.NPCType<Smith>(),
				(l, r, f, t) => {
					Gen.PlaceOnFloor(l + 6, f, TileID.Anvils);
					Gen.PlaceOnFloor(l + 10, f, TileID.Furnaces);
					Gen.PlaceOnFloor(r - 9, f, TileID.Hellforge);
					Gen.PlaceOnFloor(r - 5, f, TileID.MythrilAnvil);
					Gen.Place((l + r) / 2, t + 1, TileID.Chandeliers, 1);
				}),
			new("Yard", "Training Yard", "Sparring ground for your heirs. Unlocks Vitality, Bulwark, Fleetfoot and the Berserker class.", 25 * Coins.Gold,
				30, 0, false, TileID.WoodBlock, WallID.WhiteDynasty, TileID.WoodBlock,
				null,
				(l, r, f, t) => {
					for (int x = l; x <= r; x++)
						Gen.Solid(x, f, TileID.WoodBlock);
					for (int x = l; x <= r; x += 1)
						for (int y = f - 2; y < f; y++)
							Gen.WallAt(x, y, WallID.WoodenFence);
					for (int x = l + 4; x <= r - 4; x += 6)
						Gen.PlaceOnFloor(x, f, TileID.Statues, 0);
					Gen.PlaceOnFloor(l + 1, f, TileID.Lampposts);
					Gen.PlaceOnFloor(r - 1, f, TileID.Lampposts);
				}),
			new("Sanctum", "Rune Sanctum", "A Runeweaver joins you. Unlocks Rune Affinity and the Shadowblade class.", 45 * Coins.Gold,
				24, 11, true, TileID.PearlstoneBrick, WallID.PearlstoneBrick, TileID.BlueDynastyShingles,
				() => ModContent.NPCType<Runeweaver>(),
				(l, r, f, t) => {
					Gen.PlaceOnFloor(l + 6, f, TileID.CrystalBall);
					Gen.PlaceOnFloor(l + 10, f, TileID.AlchemyTable);
					Gen.PlaceOnFloor(r - 9, f, TileID.Bookcases);
					Gen.PlaceOnFloor(r - 5, f, TileID.BewitchingTable);
					Gen.Place((l + r) / 2, t + 1, TileID.Chandeliers, 2);
				}),
			new("Infirmary", "Infirmary", "A Healer moves in. Unlocks Field Kit (free potions) and Second Wind.", 70 * Coins.Gold,
				24, 9, true, TileID.MarbleBlock, WallID.MarbleBlock, TileID.RedDynastyShingles,
				() => ModContent.NPCType<Healer>(),
				(l, r, f, t) => {
					Gen.PlaceOnFloor(l + 4, f, TileID.Beds);
					Gen.PlaceOnFloor(l + 10, f, TileID.Sinks);
					Gen.PlaceOnFloor(r - 6, f, TileID.Beds);
					Gen.Place((l + r) / 2, t + 1, TileID.Chandeliers, 3);
				}),
			new("Tower", "Cartographer's Tower", "A Cartographer arrives. Unlocks Scouting, floor charts and the Beastcaller class.", 100 * Coins.Gold,
				18, 16, true, TileID.GrayBrick, WallID.Planked, TileID.BlueDynastyShingles,
				() => ModContent.NPCType<Cartographer>(),
				(l, r, f, t) => {
					Gen.Platform(l + 1, f - 8, r - l - 1, 0);
					Gen.PlaceOnFloor(l + 4, f - 8, TileID.Bookcases);
					Gen.PlaceOnFloor(r - 4, f - 8, TileID.Bookcases);
					Gen.Torch(l + 2, f - 11, 0);
				}),
			new("Treasury", "Treasury", "Vaults for the family fortune. Unlocks Toll Relief and Fortune.", 150 * Coins.Gold,
				22, 9, true, TileID.GoldBrick, WallID.GoldBrick, TileID.RedDynastyShingles,
				null,
				(l, r, f, t) => {
					for (int x = l + 3; x <= r - 3; x += 2)
						Gen.PlaceOnFloor(x, f, x % 4 == 0 ? TileID.GoldCoinPile : TileID.SilverCoinPile);
					Gen.Place((l + r) / 2, t + 1, TileID.Chandeliers, 0);
				}),
			new("Reliquary", "Reliquary", "A hall of ancestral portraits. Unlocks Ancestral Blessing (a free Boon every descent).", 220 * Coins.Gold,
				26, 11, true, TileID.ObsidianBrick, WallID.ObsidianBrick, TileID.BlueDynastyShingles,
				null,
				(l, r, f, t) => {
					for (int x = l + 4; x <= r - 4; x += 5)
						Gen.PlaceOnFloor(x, f, TileID.Statues, 0);
					Gen.Place((l + r) / 2, t + 1, TileID.Chandeliers, 0);
				}),
			new("Keep", "Grand Keep", "The seat of a great house. Unlocks Charge Mastery and Legacy of Kings.", 350 * Coins.Gold,
				34, 14, true, TileID.GrayBrick, WallID.BlueDynasty, TileID.RedDynastyShingles,
				null,
				(l, r, f, t) => {
					Gen.PlaceOnFloor((l + r) / 2, f, TileID.Thrones);
					Gen.Place(l + 8, t + 1, TileID.Chandeliers, 0);
					Gen.Place(r - 8, t + 1, TileID.Chandeliers, 0);
					Gen.Place(l + 3, t + 1, TileID.Banners, 1);
					Gen.Place(r - 3, t + 1, TileID.Banners, 1);
				}),
		};

		public static StructureDef Get(string key) => All.Find(s => s.Key == key);

		public static int IndexOf(string key) => All.FindIndex(s => s.Key == key);

		/// <summary>Plot centers alternate left/right of spawn, moving outward.</summary>
		public static int PlotCenter(int index, int lobbyCenterX)
		{
			int side = index % 2 == 0 ? -1 : 1;
			int ring = index / 2;
			return lobbyCenterX + side * (36 + ring * 54);
		}

		public static Rectangle Footprint(StructureDef d, int cx, int groundY)
		{
			int left = cx - d.Width / 2;
			return new Rectangle(left - 4, groundY - Math.Max(d.Height, 6) - d.Width / 2 - 4, d.Width + 8, Math.Max(d.Height, 6) + d.Width / 2 + 5);
		}

		/// <summary>Builds a structure on its plot and moves its resident in. Server / singleplayer / world gen.</summary>
		public static void Build(StructureDef d, int cx, int groundY)
		{
			Gen.Run(() => {
				int left = cx - d.Width / 2;
				int right = left + d.Width - 1;
				int floorY = groundY;
				int topY = groundY - d.Height;
				Rectangle area = Footprint(d, cx, groundY);

				// Clear the plot above ground and remove anything growing there.
				for (int x = area.Left; x < area.Right; x++) {
					for (int y = area.Top; y < groundY; y++) {
						Gen.Air(x, y);
						Gen.WallAt(x, y, 0);
					}
				}

				for (int x = left; x <= right; x++)
					Gen.Solid(x, floorY, d.Block);

				if (d.House) {
					for (int y = topY; y < floorY; y++) {
						Gen.Solid(left, y, d.Block);
						Gen.Solid(right, y, d.Block);
					}
					for (int x = left; x <= right; x++)
						Gen.Solid(x, topY, d.Block);
					Gen.Wall(new Rectangle(left + 1, topY + 1, d.Width - 2, d.Height - 1), d.Wall);

					// Pitched roof, two tiles inward per row.
					for (int i = 1; ; i++) {
						int l = left - 1 + i * 2;
						int r = right + 1 - i * 2;
						if (l > r)
							break;
						for (int x = l; x <= r; x++)
							Gen.Solid(x, topY - i, d.Roof);
					}
					Gen.Solid(left - 1, topY, d.Roof);
					Gen.Solid(right + 1, topY, d.Roof);

					// Doors on both sides.
					foreach (int side in new[] { left, right }) {
						for (int y = floorY - 3; y < floorY; y++)
							Gen.Air(side, y);
						WorldGen.PlaceDoor(side, floorY - 2, TileID.ClosedDoor);
					}

					Gen.FrameArea(area);
					Gen.Torch(left + 2, topY + 2, 0);
					Gen.Torch(right - 2, topY + 2, 0);
					if (d.Resident != null) {
						int mid = (left + right) / 2;
						Gen.PlaceOnFloor(mid, floorY, TileID.Tables);
						Gen.PlaceOnFloor(mid + 2, floorY, TileID.Chairs);
					}
				}

				Gen.FrameArea(area);
				d.Furnish?.Invoke(left, right, floorY, topY);
				Gen.FrameArea(area);
				Gen.Resync(area);

				if (d.Resident != null)
					MoveIn(d.Resident(), (left + right) / 2, floorY);
			});
		}

		public static void MoveIn(int npcType, int x, int floorY)
		{
			if (NPC.AnyNPCs(npcType))
				return;
			int index = NPC.NewNPC(Entity.GetSource_TownSpawn(), x * 16 + 8, floorY * 16, npcType);
			if (index >= Main.maxNPCs)
				return;
			NPC npc = Main.npc[index];
			npc.homeTileX = x;
			npc.homeTileY = floorY;
			npc.homeless = false;
			WorldGen.QuickFindHome(index);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
		}
	}
}
