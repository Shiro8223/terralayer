using System;
using System.Collections.Generic;
using Terralayer.Common.Heirs;
using Terralayer.Common.Utilities;
using Terralayer.Content.Items;
using Terraria;
using Terraria.ID;

namespace Terralayer.Common.Items
{
	public enum ChestKind
	{
		Wooden,   // rare finds in ordinary rooms
		Gold,     // treasure vaults
		Champion, // appears when an ambush room is cleared
		Guardian  // appears when a floor's guardian falls
	}

	/// <summary>
	/// Terralayer's loot. Weapons come in evolution lines of five stages; chests only ever hand out the early stages,
	/// and the late ones (Excalibur, Horseman's Blade...) are reached by merging and evolving. Every chest is one main
	/// item plus a couple of small extras.
	/// </summary>
	public static class Loot
	{
		public const int Stages = 5;

		/// <summary>Lines[path][line][stage]. Merging two copies can evolve a weapon to the next stage of its line.</summary>
		private static readonly Dictionary<HeirClass, int[][]> Lines = new() {
			[HeirClass.Knight] = new[] {
				new int[] { ItemID.CopperBroadsword, ItemID.EnchantedSword, ItemID.Muramasa, ItemID.Excalibur, ItemID.TrueExcalibur },
				new int[] { ItemID.IronBroadsword, ItemID.BladeofGrass, ItemID.NightsEdge, ItemID.TrueNightsEdge, ItemID.TerraBlade },
				new int[] { ItemID.CactusSword, ItemID.FieryGreatsword, ItemID.BreakerBlade, ItemID.ChlorophyteSaber, ItemID.TheHorsemansBlade },
			},
			[HeirClass.Ranger] = new[] {
				new int[] { ItemID.WoodenBow, ItemID.GoldBow, ItemID.MoltenFury, ItemID.HallowedRepeater, ItemID.Tsunami },
				new int[] { ItemID.FlintlockPistol, ItemID.Handgun, ItemID.PhoenixBlaster, ItemID.Uzi, ItemID.VenusMagnum },
				new int[] { ItemID.Boomstick, ItemID.Minishark, ItemID.Shotgun, ItemID.Megashark, ItemID.ChainGun },
			},
			[HeirClass.Arcanist] = new[] {
				new int[] { ItemID.AmethystStaff, ItemID.DiamondStaff, ItemID.CrystalStorm, ItemID.CrystalSerpent, ItemID.RazorbladeTyphoon },
				new int[] { ItemID.WandofSparking, ItemID.Flamelash, ItemID.CursedFlames, ItemID.RainbowRod, ItemID.ShadowbeamStaff },
				new int[] { ItemID.Vilethorn, ItemID.WaterBolt, ItemID.SkyFracture, ItemID.MagicalHarp, ItemID.BubbleGun },
			},
			[HeirClass.Shadowblade] = new[] {
				new int[] { ItemID.Katana, ItemID.Starfury, ItemID.DeathSickle, ItemID.PsychoKnife, ItemID.InfluxWaver },
				new int[] { ItemID.WoodYoyo, ItemID.Rally, ItemID.Valor, ItemID.Amarok, ItemID.Kraken },
				new int[] { ItemID.Code1, ItemID.Cascade, ItemID.FormatC, ItemID.Yelets, ItemID.TheEyeOfCthulhu },
			},
			[HeirClass.Berserker] = new[] {
				new int[] { ItemID.Spear, ItemID.Trident, ItemID.DarkLance, ItemID.Gungnir, ItemID.NorthPole },
				new int[] { ItemID.BallOHurt, ItemID.BlueMoon, ItemID.Sunfury, ItemID.DaoofPow, ItemID.Flairon },
				new int[] { ItemID.WoodenBoomerang, ItemID.EnchantedBoomerang, ItemID.ThornChakram, ItemID.LightDisc, ItemID.PossessedHatchet },
			},
			[HeirClass.Beastcaller] = new[] {
				new int[] { ItemID.SlimeStaff, ItemID.HornetStaff, ItemID.SpiderStaff, ItemID.OpticStaff, ItemID.RavenStaff },
				new int[] { ItemID.BabyBirdStaff, ItemID.ImpStaff, ItemID.PirateStaff, ItemID.Smolstar, ItemID.PygmyStaff },
				new int[] { ItemID.BlandWhip, ItemID.ThornWhip, ItemID.BoneWhip, ItemID.SwordWhip, ItemID.ScytheWhip },
			},
			[HeirClass.Riftwalker] = new[] {
				new int[] { ItemID.MagicMissile, ItemID.SpaceGun, ItemID.LaserRifle, ItemID.HeatRay, ItemID.ChargedBlasterCannon },
				new int[] { ItemID.ThunderStaff, ItemID.MagicDagger, ItemID.ShadowFlameKnife, ItemID.VampireKnives, ItemID.ScourgeoftheCorruptor },
				new int[] { ItemID.ZapinatorGray, ItemID.ZapinatorOrange, ItemID.MeteorStaff, ItemID.LaserMachinegun, ItemID.SpectreStaff },
			},
			[HeirClass.Chronomancer] = new[] {
				new int[] { ItemID.SapphireStaff, ItemID.FlowerofFrost, ItemID.FrostStaff, ItemID.BlizzardStaff, ItemID.StaffoftheFrostHydra },
				new int[] { ItemID.TheUndertaker, ItemID.Revolver, ItemID.ClockworkAssaultRifle, ItemID.SniperRifle, ItemID.Xenopopper },
				new int[] { ItemID.LeadBroadsword, ItemID.LightsBane, ItemID.Cutlass, ItemID.Keybrand, ItemID.Seedler },
			},
			// Tidecaller (secret): weapons of the deep
			[HeirClass.Fishron] = new[] {
				new int[] { ItemID.Swordfish, ItemID.PurpleClubberfish, ItemID.ObsidianSwordfish, ItemID.Bladetongue, ItemID.Anchor },
				new int[] { ItemID.Harpoon, ItemID.Musket, ItemID.Toxikarp, ItemID.Gatligator, ItemID.TacticalShotgun },
				new int[] { ItemID.AquaScepter, ItemID.GoldenShower, ItemID.CrystalVileShard, ItemID.VenomStaff, ItemID.TempestStaff },
			},
		};

		// Thin pools, mobility early. No wings before tier 4. No basic Grappling Hook: every heir already starts with one.
		private static readonly int[][] Accessories = {
			// (No Band of Regeneration: life only comes back through restoration in Terralayer.)
			new int[] { ItemID.CloudinaBottle, ItemID.HermesBoots, ItemID.Aglet, ItemID.ShinyRedBalloon, ItemID.FeralClaws, ItemID.ClimbingClaws },
			new int[] { ItemID.AmethystHook, ItemID.BlizzardinaBottle, ItemID.LuckyHorseshoe, ItemID.AnkletoftheWind, ItemID.ObsidianSkull, ItemID.SharkToothNecklace, ItemID.BandofStarpower, ItemID.FrogLeg, ItemID.ShoeSpikes },
			new int[] { ItemID.RubyHook, ItemID.SandstorminaBottle, ItemID.RocketBoots, ItemID.MagmaStone, ItemID.CelestialMagnet, ItemID.PanicNecklace, ItemID.HoneyComb, ItemID.TitanGlove, ItemID.CobaltShield },
			new int[] { ItemID.DualHook, ItemID.FairyWings, ItemID.WarriorEmblem, ItemID.RangerEmblem, ItemID.SorcererEmblem, ItemID.SummonerEmblem, ItemID.CrossNecklace, ItemID.StarCloak },
			new int[] { ItemID.IlluminantHook, ItemID.LeafWings, ItemID.AvengerEmblem, ItemID.CharmofMyths, ItemID.SpectreBoots, ItemID.FleshKnuckles },
		};

		private static readonly int[][] Armor = {
			new int[] { ItemID.TinHelmet, ItemID.TinChainmail, ItemID.TinGreaves, ItemID.SilverHelmet, ItemID.SilverChainmail, ItemID.SilverGreaves },
			new int[] { ItemID.TungstenHelmet, ItemID.TungstenChainmail, ItemID.TungstenGreaves, ItemID.JungleHat, ItemID.JungleShirt, ItemID.JunglePants },
			new int[] { ItemID.MoltenHelmet, ItemID.MoltenBreastplate, ItemID.MoltenGreaves, ItemID.NecroHelmet, ItemID.NecroBreastplate, ItemID.NecroGreaves },
			new int[] { ItemID.CobaltHelmet, ItemID.CobaltBreastplate, ItemID.CobaltLeggings, ItemID.MythrilHelmet, ItemID.MythrilChainmail, ItemID.MythrilGreaves },
			new int[] { ItemID.AdamantiteHelmet, ItemID.AdamantiteBreastplate, ItemID.AdamantiteLeggings, ItemID.TitaniumMask, ItemID.TitaniumBreastplate, ItemID.TitaniumLeggings },
		};

		private static readonly int[] HealingPotion = { ItemID.LesserHealingPotion, ItemID.LesserHealingPotion, ItemID.HealingPotion, ItemID.HealingPotion, ItemID.GreaterHealingPotion };
		private static readonly int[] ManaPotion = { ItemID.LesserManaPotion, ItemID.LesserManaPotion, ItemID.ManaPotion, ItemID.ManaPotion, ItemID.GreaterManaPotion };
		/// <summary>Only potions that help in a fight. (No spelunker, gravitation, gills, featherfall and the like.)</summary>
		private static readonly int[] BuffPotions = {
			ItemID.IronskinPotion, ItemID.RegenerationPotion, ItemID.SwiftnessPotion, ItemID.ArcheryPotion, ItemID.MagicPowerPotion,
			ItemID.SummoningPotion, ItemID.ThornsPotion, ItemID.WrathPotion, ItemID.RagePotion, ItemID.EndurancePotion, ItemID.ManaRegenerationPotion,
		};

		public static int ClampTier(int tier) => Math.Clamp(tier, 1, Stages);

		/// <summary>Stage-N weapons across all lines of a path (the Smith sells stage 1).</summary>
		public static int[] WeaponPool(HeirClass c, int stage)
		{
			int[][] lines = Lines[c];
			var pool = new int[lines.Length];
			for (int i = 0; i < lines.Length; i++)
				pool[i] = lines[i][ClampTier(stage) - 1];
			return pool;
		}

		/// <summary>The next stage of a weapon's evolution line, or 0 if it has none (or is the last stage).</summary>
		public static int EvolutionOf(int itemType)
		{
			foreach (var pair in Lines)
				foreach (int[] line in pair.Value) {
					int i = Array.IndexOf(line, itemType);
					if (i >= 0 && i + 1 < line.Length)
						return line[i + 1];
				}
			return 0;
		}

		/// <summary>1-based stage of a weapon within its line, or 0 if it isn't in a line.</summary>
		public static int StageOf(int itemType)
		{
			foreach (var pair in Lines)
				foreach (int[] line in pair.Value) {
					int i = Array.IndexOf(line, itemType);
					if (i >= 0)
						return i + 1;
				}
			return 0;
		}

		/// <summary>A weapon from the path at this floor's stage, from a different line than <paramref name="avoidLineOf"/>.</summary>
		public static int ArsenalWeapon(HeirClass path, int floor, int avoidLineOf, Random rng)
		{
			int[] pool = WeaponPool(path, StageForFloor(floor, rng));
			var others = Array.FindAll(pool, w => !SameLine(w, avoidLineOf));
			int[] from = others.Length > 0 ? others : pool;
			return from[rng.Next(from.Length)];
		}

		/// <summary>Are two weapons stages of the same evolution line?</summary>
		public static bool SameLine(int a, int b)
		{
			if (a == b)
				return true;
			foreach (var pair in Lines)
				foreach (int[] line in pair.Value)
					if (Array.IndexOf(line, a) >= 0 && Array.IndexOf(line, b) >= 0)
						return true;
			return false;
		}

		public static HeirClass ClassOf(int itemType)
		{
			foreach (var pair in Lines)
				foreach (int[] line in pair.Value)
					if (Array.IndexOf(line, itemType) >= 0)
						return pair.Key;
			return HeirClass.Knight;
		}

		/// <summary>Which stage chests hand out on a floor: never beyond stage 3. The rest comes from evolving.</summary>
		public static int StageForFloor(int floor, Random rng) => floor switch {
			<= 1 => 1,
			2 => rng.NextDouble() < 0.25 ? 2 : 1,
			3 => 2,
			4 => rng.NextDouble() < 0.3 ? 3 : 2,
			_ => 3
		};

		/// <summary>Rolls a weapon: 85% for <paramref name="favored"/>'s path.</summary>
		public static Item RollWeapon(int floor, HeirClass favored, int rarityBias, Random rng)
		{
			// The odd off-path weapon never comes from the secret Tidecaller lines.
			HeirClass cls = rng.NextDouble() < 0.85 ? favored : (HeirClass)rng.Next((int)HeirClass.Fishron);
			int[] pool = WeaponPool(cls, StageForFloor(floor, rng));
			return RefinedWeapon.Create(pool[rng.Next(pool.Length)], RarityDefs.Roll(rarityBias, rng));
		}

		public static Item RollAccessory(int floor, Random rng)
		{
			int[] pool = Accessories[ClampTier(floor) - 1];
			return new Item(pool[rng.Next(pool.Length)]); // no modifier: forge stages come from merging copies
		}

		/// <summary>
		/// A found armor piece. It always comes with an armor rune already inscribed, so a new piece has something to
		/// offer over the one you're wearing (the rune lasts as long as the piece, i.e. this descent).
		/// </summary>
		public static Item RollArmor(int floor, Random rng, int runeBias = 0)
		{
			int[] pool = Armor[ClampTier(floor) - 1];
			var item = new Item(pool[rng.Next(pool.Length)]);
			var r = RunedArmor.Of(item);
			if (r != null) {
				r.HasRune = true;
				r.Rune = new ArmorSocket((ArmorRuneType)rng.Next((int)ArmorRuneType.Count), RarityDefs.Roll(runeBias, rng));
			}
			return item;
		}

		/// <summary>Half weapon runes, half armor runes.</summary>
		public static Item RollRune(int rarityBias, Random rng) => RuneOfRarity(RarityDefs.Roll(rarityBias, rng), rng);

		/// <summary>A random weapon or armor rune of exactly this rarity.</summary>
		public static Item RuneOfRarity(Rarity r, Random rng) => rng.NextDouble() < 0.5
			? RuneItem.Create((RuneType)rng.Next((int)RuneType.Count), r, rng)
			: ArmorRuneItem.Create((ArmorRuneType)rng.Next((int)ArmorRuneType.Count), r, rng);

		public static Item Healing(int floor, int stack) => new(HealingPotion[ClampTier(floor) - 1], stack);

		public static Item RollPotion(int floor, Random rng)
		{
			double r = rng.NextDouble();
			if (r < 0.5)
				return new Item(HealingPotion[ClampTier(floor) - 1], 1 + rng.Next(2));
			if (r < 0.65)
				return new Item(ManaPotion[ClampTier(floor) - 1], 2);
			return new Item(BuffPotions[rng.Next(BuffPotions.Length)]);
		}

		/// <summary>The headline item: a weapon, an accessory or an armor piece.</summary>
		public static Item RollMain(int floor, HeirClass favored, int bias, Random rng, double weaponChance = 0.45, double accessoryChance = 0.3)
		{
			double r = rng.NextDouble();
			if (r < weaponChance)
				return RollWeapon(floor, favored, bias, rng);
			if (r < weaponChance + accessoryChance)
				return RollAccessory(floor, rng);
			return RollArmor(floor, rng, bias);
		}

		/// <summary>One MAIN item, then SIDES: a potion and some coins (and a rune in the best chests).</summary>
		public static List<Item> ChestContents(ChestKind kind, int floor, HeirClass favored, float goldMult, Random rng)
		{
			var items = new List<Item>();
			long goldBase = (long)(Coins.Silver * 12 * Math.Pow(1.8, floor - 1));

			switch (kind) {
				case ChestKind.Wooden:
					items.Add(RollMain(floor, favored, 0, rng));
					items.Add(Shards(1 + rng.Next(2) + floor / 2));
					items.Add(RollPotion(floor, rng));
					AddGold(items, (long)(goldBase * (0.6 + rng.NextDouble()) * goldMult));
					break;
				case ChestKind.Gold:
					items.Add(RollMain(floor, favored, 1, rng));
					items.Add(Shards(2 + rng.Next(3) + floor / 2));
					items.Add(RollPotion(floor, rng));
					if (rng.NextDouble() < 0.25)
						items.Add(RollRune(0, rng));
					AddGold(items, (long)(goldBase * (1.5 + rng.NextDouble()) * goldMult));
					break;
				case ChestKind.Champion:
					items.Add(RollMain(floor, favored, 1, rng, 0.55, 0.3));
					items.Add(Shards(3 + rng.Next(3) + floor));
					items.Add(Healing(floor, 2));
					AddGold(items, (long)(goldBase * (2 + rng.NextDouble()) * goldMult));
					break;
				case ChestKind.Guardian:
					items.Add(RollMain(floor, favored, 1, rng, 0.6, 0.25));
					items.Add(Shards(5 + rng.Next(4) + floor));
					items.Add(RollRune(0, rng));
					items.Add(Healing(floor, 2));
					AddGold(items, (long)(goldBase * (6 + rng.NextDouble() * 3) * goldMult));
					break;
			}
			AddAmmo(items, endless: kind is ChestKind.Champion or ChestKind.Guardian);
			return items;
		}

		/// <summary>Refinement shards for the Smith.</summary>
		public static Item Shards(int count) => new(Terraria.ModLoader.ModContent.ItemType<RefinementShard>(), System.Math.Max(1, count));

		public static int EndlessAmmoFor(Item weapon) =>
			weapon.useAmmo == AmmoID.Arrow ? ItemID.EndlessQuiver
			: weapon.useAmmo == AmmoID.Bullet ? ItemID.EndlessMusketPouch
			: 0;

		public static Item AmmoStackFor(Item weapon) =>
			weapon.useAmmo == AmmoID.Arrow ? new Item(ItemID.WoodenArrow, 300)
			: weapon.useAmmo == AmmoID.Bullet ? new Item(ItemID.MusketBall, 300)
			: null;

		private static void AddAmmo(List<Item> items, bool endless)
		{
			foreach (Item weapon in items.ToArray()) {
				if (weapon.useAmmo == AmmoID.None)
					continue;
				Item ammo = endless && EndlessAmmoFor(weapon) > 0 ? new Item(EndlessAmmoFor(weapon)) : AmmoStackFor(weapon);
				if (ammo != null)
					items.Insert(items.IndexOf(weapon) + 1, ammo);
			}
		}

		private static void AddGold(List<Item> items, long copper)
		{
			foreach ((int type, int stack) in Coins.Split(copper))
				items.Add(new Item(type, stack));
		}

		public static void Fill(Chest chest, List<Item> items)
		{
			for (int i = 0; i < chest.item.Length; i++)
				chest.item[i] = i < items.Count ? items[i] : new Item();
		}
	}
}
