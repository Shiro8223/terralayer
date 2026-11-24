using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.Utilities;
using Terraria.ID;

namespace Terralayer.Common.Depths
{
	public enum ShrineType
	{
		Blood,      // give max life for a powerful rune
		Fortune,    // pay gold for gear
		Change,     // swap your ability for another path's
		Wayward,    // fling yourself into a random room
		Trial,      // summon a champion now; it drops a strong weapon
		Spring,     // full heal and protective blessings
		Gamble,     // stake part of your purse on a coin flip
		Refinement, // +1 refinement on your held weapon, paid in max life
		Count
	}

	public record ShrineInfo(string Name, Color Color);

	/// <summary>
	/// What a shrine's chamber is built from. <paramref name="Floor2"/> alternates with <paramref name="Floor"/> in a
	/// checker pattern when they differ.
	/// </summary>
	public record ShrineTheme(ushort Floor, ushort Floor2, ushort Dais, ushort Trim, ushort Wall, ushort Wall2, int Torch, int Banner, int Platform = 0);

	public static class Shrines
	{
		private static readonly ShrineInfo[] Infos = {
			new("Blood Altar", new Color(255, 110, 110)),
			new("Fortune Altar", new Color(255, 215, 90)),
			new("Altar of Change", new Color(190, 140, 255)),
			new("Wayward Shrine", new Color(120, 220, 255)),
			new("Shrine of Trials", new Color(255, 150, 60)),
			new("Healing Spring", new Color(120, 255, 160)),
			new("Gambler's Shrine", new Color(255, 240, 140)),
			new("Shrine of Refinement", new Color(220, 220, 255)),
		};

		private static readonly ShrineTheme[] Themes = {
			// Blood: crimtane and flesh, red light
			new(TileID.CrimtaneBrick, TileID.CrimtaneBrick, TileID.FleshBlock, TileID.CrimstoneBrick, WallID.CrimtaneBrick, WallID.Flesh, 19, 0, 34),
			// Fortune: gold and platinum
			new(TileID.GoldBrick, TileID.GoldBrick, TileID.PlatinumBrick, TileID.GoldBrick, WallID.GoldBrick, WallID.PlatinumBrick, 6, 3, 31),
			// Change: glowing amethyst and crystal
			new(TileID.AmethystGemspark, TileID.AmethystGemspark, TileID.CrystalBlock, TileID.PearlstoneBrick, WallID.AmethystGemspark, WallID.PearlstoneBrick, 4, 2, 30),
			// Wayward: sky stone and clouds
			new(TileID.Sunplate, TileID.Sunplate, TileID.Cloud, TileID.Sunplate, WallID.DiscWall, WallID.Cloud, 23, 2, 22),
			// Trial: hellstone and obsidian
			new(TileID.HellstoneBrick, TileID.HellstoneBrick, TileID.ObsidianBrick, TileID.ObsidianBrick, WallID.ObsidianBrick, WallID.HellstoneBrick, 10, 0, 13),
			// Spring: marble and water
			new(TileID.MarbleBlock, TileID.MarbleBlock, TileID.Marble, TileID.MarbleBlock, WallID.MarbleBlock, WallID.Marble, 9, 2, 29),
			// Gamble: a red-and-black checkerboard
			new(TileID.RubyGemspark, TileID.ObsidianBrick, TileID.GoldBrick, TileID.ObsidianBrick, WallID.ObsidianBrick, WallID.RubyGemspark, 2, 0, 31),
			// Refinement: cold forged metal
			new(TileID.CobaltBrick, TileID.CobaltBrick, TileID.SilverBrick, TileID.SilverBrick, WallID.CobaltBrick, WallID.SilverBrick, 5, 2, 9),
		};

		public static ShrineType TypeOf(RoomPlan room) => (ShrineType)new Random(room.Seed).Next((int)ShrineType.Count);

		public static ShrineTheme Theme(ShrineType t) => Themes[(int)t];

		public static ShrineInfo Get(ShrineType t) => Infos[(int)t];

		public static long FortunePrice(int floor) => Coins.Gold * 2 * Math.Max(1, floor);

		public static string Offer(ShrineType t, int floor) => t switch {
			ShrineType.Blood => "give 15% max life (this descent) for a powerful rune",
			ShrineType.Fortune => $"offer {Coins.FormatPlain(FortunePrice(floor))} for gear from deeper down",
			ShrineType.Change => "trade your ability for another path's (until this heir dies)",
			ShrineType.Wayward => "be flung into a random room on this floor",
			ShrineType.Trial => "summon a champion; defeat it for a strong weapon",
			ShrineType.Spring => "restore your life and mana, and gain protective blessings",
			ShrineType.Gamble => "stake 20% of your purse: win 2.5x, or lose it",
			ShrineType.Refinement => "+1 refinement on your held weapon, for 10% max life (this descent)",
			_ => ""
		};
	}
}
