using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Heirs
{
	/// <summary>
	/// A combat path. Each class belongs to one; the path decides the ability, a small passive and which weapons
	/// show up most. (Kept under the old name so saves and loot code stay compatible: new paths go at the end.)
	/// </summary>
	public enum HeirClass
	{
		Knight,       // melee guardian
		Ranger,       // bows and guns
		Arcanist,     // magic
		Shadowblade,  // fast melee, yoyos
		Berserker,    // spears, flails, boomerangs
		Beastcaller,  // summons and whips
		Riftwalker,   // rift magic; travels between explored rooms
		Chronomancer, // frost, clockwork guns and old blades; rewinds time
		Fishron,      // Tidecaller: secret class, unlocked by defeating Duke Fishron
		Count
	}

	public enum AbilityKind
	{
		Burst,      // spend a chunk of charge instantly
		Sustained   // toggle on; drains charge until empty or toggled off
	}

	public record ClassInfo(
		string Name,
		string Title,
		string Passive,
		string AbilityName,
		string AbilityDescription,
		AbilityKind Kind,
		float Cost,
		Color Color);

	/// <summary>
	/// The eight paths. Armour no longer carries set bonuses in Terralayer, so a path's identity is its ability and
	/// its passive.
	/// </summary>
	public static class ClassDefs
	{
		private static readonly ClassInfo[] Infos = {
			new("Guardian", "Ser", "+10% max life, +4 defense, knockback immunity while blocking",
				"Aegis Charge", "Shield-charge toward the cursor, untouchable, bowling enemies over. Then take 40% less damage for 3 seconds.",
				AbilityKind.Burst, 35f, new Color(150, 180, 255)),
			new("Marksman", "Warden", "+8% ranged damage, +6% ranged crit",
				"Arrowstorm", "Vault backwards and call a storm of arrows down on the cursor for 2 seconds.",
				AbilityKind.Burst, 40f, new Color(140, 220, 120)),
			new("Arcane", "Magus", "+20 max mana, +8% magic damage",
				"Arcane Orbit", "Three arcane orbs circle you, burning what they touch and firing at nearby foes. Spells cost 40% less mana.",
				AbilityKind.Sustained, 24f, new Color(190, 140, 255)),
			new("Shadow", "Shade", "+10% movement speed, +5% crit chance",
				"Shade Step", "Blink up to 20 tiles toward the cursor, untouchable for a moment. Your next hit within 2 seconds is a 150% critical ambush.",
				AbilityKind.Burst, 25f, new Color(170, 170, 200)),
			new("Warrior", "Raider", "+10% max life, +8% melee speed",
				"Earthshatter", "Plunge to the ground and split it: shockwaves erupt to both sides. Then rage for 5 seconds (+20% damage, lifesteal).",
				AbilityKind.Burst, 40f, new Color(255, 110, 90)),
			new("Beastmaster", "Keeper", "+8% summon damage, +1 minion slot",
				"Spirit Pack", "Howl up a pack of three spectral wolves that hunt for 10 seconds. Your minions and whips deal 25% more meanwhile.",
				AbilityKind.Burst, 50f, new Color(255, 200, 110)),
			new("Riftwalker", "Wayfarer", "+8% magic and ranged damage; rift travel grants a burst of speed",
				"Rift Jump", "Open the floor map and step straight into any room you have already explored. (At home: a short blink.)",
				AbilityKind.Burst, 50f, new Color(110, 230, 255)),
			new("Chronomancer", "Horologist", "+6% attack speed; potion sickness wears off 25% faster",
				"Rewind", "Snap back to where you stood 3 seconds ago, with the life and mana you had then (if they were more), shedding every debuff.",
				AbilityKind.Burst, 45f, new Color(255, 225, 140)),
			new("Tidecaller", "Tidelord", "+8% damage, swims like a fish; Fishron Wings are part of the heir",
				"Typhoon", "Loose six razor typhoons that seek out enemies near the cursor.",
				AbilityKind.Burst, 40f, new Color(90, 200, 225)),
		};

		public static ClassInfo Get(HeirClass c) => Infos[(int)c];

		public const float SustainMinimum = 20f;

		public static float LifeDelta(HeirClass c) => c is HeirClass.Knight or HeirClass.Berserker ? 0.10f : 0f;

		public static void ApplyPassive(Player p, HeirClass c)
		{
			switch (c) {
				case HeirClass.Knight:
					p.statDefense += 4;
					break;
				case HeirClass.Ranger:
					p.GetDamage(DamageClass.Ranged) += 0.08f;
					p.GetCritChance(DamageClass.Ranged) += 6;
					break;
				case HeirClass.Arcanist:
					p.statManaMax2 += 20;
					p.GetDamage(DamageClass.Magic) += 0.08f;
					break;
				case HeirClass.Shadowblade:
					p.moveSpeed += 0.10f;
					p.GetCritChance(DamageClass.Generic) += 5;
					break;
				case HeirClass.Berserker:
					p.GetAttackSpeed(DamageClass.Melee) += 0.08f;
					break;
				case HeirClass.Beastcaller:
					p.GetDamage(DamageClass.Summon) += 0.08f;
					p.maxMinions += 1;
					break;
				case HeirClass.Riftwalker:
					p.GetDamage(DamageClass.Magic) += 0.08f;
					p.GetDamage(DamageClass.Ranged) += 0.08f;
					break;
				case HeirClass.Fishron:
					p.GetDamage(DamageClass.Generic) += 0.08f;
					p.accFlipper = true;
					break;
				case HeirClass.Chronomancer:
					p.GetAttackSpeed(DamageClass.Generic) += 0.06f;
					p.potionDelayTime = (int)(p.potionDelayTime * 0.75f);
					p.restorationDelayTime = (int)(p.restorationDelayTime * 0.75f);
					break;
			}
		}

		public static void ApplySustained(Player p, HeirClass c)
		{
			switch (c) {
				case HeirClass.Arcanist:
					p.manaCost -= 0.4f;
					p.manaRegenBonus += 60;
					break;
			}
		}
	}

	/// <summary>
	/// A class: an armor set, a starting weapon, and on higher tiers accessories. Unlocked on the Classes page of the
	/// skill tree; each tier needs that floor's guardian beaten.
	/// </summary>
	public record ClassKit(
		string Key,
		string Name,
		HeirClass Path,
		int Tier,
		string Parent,
		int[] Armor,        // head, body, legs (0 = none)
		int[] Accessories,
		int Weapon,
		long Cost,
		int Col);           // column on the skill-tree page

	/// <summary>A flavour of a class that starts with a different weapon, e.g. an Acolyte who tames a slime instead of a finch.</summary>
	public record ClassVariant(string Name, int Weapon);

	public static class ClassKits
	{
		private const long G = Utilities.Coins.Gold;

		public static readonly List<ClassKit> All = new() {
			// Guardian path
			new("Squire", "Squire", HeirClass.Knight, 0, null, new int[] { ItemID.CopperHelmet, ItemID.CopperChainmail, ItemID.CopperGreaves }, new int[0], ItemID.CopperBroadsword, 0, 0),
			new("Knight", "Knight", HeirClass.Knight, 1, "Squire", new int[] { ItemID.IronHelmet, ItemID.IronChainmail, ItemID.IronGreaves }, new int[0], ItemID.IronBroadsword, 15 * G, 0),
			new("Templar", "Templar", HeirClass.Knight, 2, "Knight", new int[] { ItemID.GoldHelmet, ItemID.GoldChainmail, ItemID.GoldGreaves }, new int[0], ItemID.BladeofGrass, 45 * G, 0),
			new("MoltenChampion", "Molten Champion", HeirClass.Knight, 3, "Templar", new int[] { ItemID.MoltenHelmet, ItemID.MoltenBreastplate, ItemID.MoltenGreaves }, new int[] { ItemID.ObsidianShield }, ItemID.FieryGreatsword, 120 * G, 0),
			new("Paladin", "Paladin", HeirClass.Knight, 4, "MoltenChampion", new int[] { ItemID.HallowedHelmet, ItemID.HallowedPlateMail, ItemID.HallowedGreaves }, new int[] { ItemID.PaladinsShield }, ItemID.BreakerBlade, 300 * G, 0),
			new("Juggernaut", "Juggernaut", HeirClass.Knight, 5, "Paladin", new int[] { ItemID.BeetleHelmet, ItemID.BeetleScaleMail, ItemID.BeetleLeggings }, new int[] { ItemID.WormScarf, ItemID.FrozenTurtleShell }, ItemID.ChlorophyteSaber, 750 * G, 0),

			// Marksman path
			new("Hunter", "Hunter", HeirClass.Ranger, 0, null, new int[] { ItemID.WoodHelmet, ItemID.WoodBreastplate, ItemID.WoodGreaves }, new int[0], ItemID.WoodenBow, 0, 1),
			new("Marksman", "Marksman", HeirClass.Ranger, 1, "Hunter", new int[] { ItemID.PlatinumHelmet, ItemID.PlatinumChainmail, ItemID.PlatinumGreaves }, new int[0], ItemID.FlintlockPistol, 15 * G, 1),
			new("FossilRanger", "Fossil Ranger", HeirClass.Ranger, 2, "Marksman", new int[] { ItemID.FossilHelm, ItemID.FossilShirt, ItemID.FossilPants }, new int[0], ItemID.GoldBow, 45 * G, 1),
			new("NecroGunner", "Necro Gunner", HeirClass.Ranger, 3, "FossilRanger", new int[] { ItemID.NecroHelmet, ItemID.NecroBreastplate, ItemID.NecroGreaves }, new int[0], ItemID.Minishark, 120 * G, 1),
			new("FrostRanger", "Frost Ranger", HeirClass.Ranger, 4, "NecroGunner", new int[] { ItemID.FrostHelmet, ItemID.FrostBreastplate, ItemID.FrostLeggings }, new int[] { ItemID.MagicQuiver }, ItemID.MoltenFury, 300 * G, 1),
			new("ShroomiteSniper", "Shroomite Sniper", HeirClass.Ranger, 5, "FrostRanger", new int[] { ItemID.ShroomiteHelmet, ItemID.ShroomiteBreastplate, ItemID.ShroomiteLeggings }, new int[] { ItemID.ReconScope }, ItemID.HallowedRepeater, 750 * G, 1),

			// Arcane path
			new("Apprentice", "Apprentice", HeirClass.Arcanist, 0, null, new int[] { ItemID.WizardHat, ItemID.AmethystRobe, 0 }, new int[0], ItemID.AmethystStaff, 0, 2),
			new("Magus", "Magus", HeirClass.Arcanist, 1, "Apprentice", new int[] { ItemID.JungleHat, ItemID.JungleShirt, ItemID.JunglePants }, new int[0], ItemID.WandofSparking, 15 * G, 2),
			new("MeteorMage", "Meteor Mage", HeirClass.Arcanist, 2, "Magus", new int[] { ItemID.MeteorHelmet, ItemID.MeteorSuit, ItemID.MeteorLeggings }, new int[0], ItemID.SpaceGun, 45 * G, 2),
			new("CrystalSorcerer", "Crystal Sorcerer", HeirClass.Arcanist, 3, "MeteorMage", new int[] { ItemID.MythrilHood, ItemID.MythrilChainmail, ItemID.MythrilGreaves }, new int[] { ItemID.ManaFlower }, ItemID.CrystalStorm, 120 * G, 2),
			new("HallowedMage", "Hallowed Mage", HeirClass.Arcanist, 4, "CrystalSorcerer", new int[] { ItemID.HallowedHeadgear, ItemID.HallowedPlateMail, ItemID.HallowedGreaves }, new int[] { ItemID.CelestialCuffs }, ItemID.CursedFlames, 300 * G, 2),
			new("SpectreArchmage", "Spectre Archmage", HeirClass.Arcanist, 5, "HallowedMage", new int[] { ItemID.SpectreHood, ItemID.SpectreRobe, ItemID.SpectrePants }, new int[] { ItemID.ManaCloak }, ItemID.RainbowRod, 750 * G, 2),

			// Shadow path
			new("Footpad", "Footpad", HeirClass.Shadowblade, 0, null, new int[] { ItemID.LeadHelmet, ItemID.LeadChainmail, ItemID.LeadGreaves }, new int[0], ItemID.WoodYoyo, 0, 3),
			new("Shade", "Shade", HeirClass.Shadowblade, 1, "Footpad", new int[] { ItemID.ShadowHelmet, ItemID.ShadowScalemail, ItemID.ShadowGreaves }, new int[0], ItemID.Katana, 15 * G, 3),
			new("Duelist", "Duelist", HeirClass.Shadowblade, 2, "Shade", new int[] { ItemID.CrimsonHelmet, ItemID.CrimsonScalemail, ItemID.CrimsonGreaves }, new int[0], ItemID.Rally, 45 * G, 3),
			new("CrystalAssassin", "Crystal Assassin", HeirClass.Shadowblade, 3, "Duelist", new int[] { ItemID.CrystalNinjaHelmet, ItemID.CrystalNinjaChestplate, ItemID.CrystalNinjaLeggings }, new int[] { ItemID.Tabi }, ItemID.Starfury, 120 * G, 3),
			new("NinjaMaster", "Ninja Master", HeirClass.Shadowblade, 5, "CrystalAssassin", new int[] { ItemID.NinjaHood, ItemID.NinjaShirt, ItemID.NinjaPants }, new int[] { ItemID.MasterNinjaGear }, ItemID.Katana, 750 * G, 3),

			// Warrior path
			new("Brawler", "Brawler", HeirClass.Berserker, 0, null, new int[] { ItemID.CactusHelmet, ItemID.CactusBreastplate, ItemID.CactusLeggings }, new int[0], ItemID.Spear, 0, 4),
			new("Gladiator", "Gladiator", HeirClass.Berserker, 1, "Brawler", new int[] { ItemID.GladiatorHelmet, ItemID.GladiatorBreastplate, ItemID.GladiatorLeggings }, new int[0], ItemID.BallOHurt, 15 * G, 4),
			new("Berserker", "Berserker", HeirClass.Berserker, 3, "Gladiator", new int[] { ItemID.AdamantiteHelmet, ItemID.AdamantiteBreastplate, ItemID.AdamantiteLeggings }, new int[] { ItemID.FeralClaws }, ItemID.Trident, 120 * G, 4),
			new("Warlord", "Warlord", HeirClass.Berserker, 5, "Berserker", new int[] { ItemID.TurtleHelmet, ItemID.TurtleScaleMail, ItemID.TurtleLeggings }, new int[] { ItemID.FireGauntlet }, ItemID.Gungnir, 750 * G, 4),

			// Beastmaster path
			new("Acolyte", "Acolyte", HeirClass.Beastcaller, 0, null, new int[] { 0, ItemID.FlinxFurCoat, 0 }, new int[0], ItemID.BabyBirdStaff, 0, 5),
			new("Beekeeper", "Beekeeper", HeirClass.Beastcaller, 1, "Acolyte", new int[] { ItemID.BeeHeadgear, ItemID.BeeBreastplate, ItemID.BeeGreaves }, new int[0], ItemID.SlimeStaff, 15 * G, 5),
			new("Whipmaster", "Whipmaster", HeirClass.Beastcaller, 2, "Beekeeper", new int[] { ItemID.ObsidianHelm, ItemID.ObsidianShirt, ItemID.ObsidianPants }, new int[0], ItemID.ThornWhip, 45 * G, 5),
			new("SpiderQueen", "Spider Queen", HeirClass.Beastcaller, 3, "Whipmaster", new int[] { ItemID.SpiderMask, ItemID.SpiderBreastplate, ItemID.SpiderGreaves }, new int[0], ItemID.HornetStaff, 120 * G, 5),
			new("TikiShaman", "Tiki Shaman", HeirClass.Beastcaller, 5, "SpiderQueen", new int[] { ItemID.TikiMask, ItemID.TikiShirt, ItemID.TikiPants }, new int[] { ItemID.PygmyNecklace, ItemID.PapyrusScarab }, ItemID.SpiderStaff, 750 * G, 5),

			// Riftwalker path
			new("Initiate", "Rift Initiate", HeirClass.Riftwalker, 0, null, new int[] { ItemID.MagicHat, ItemID.GypsyRobe, 0 }, new int[0], ItemID.MagicMissile, 0, 6),
			new("Waystrider", "Waystrider", HeirClass.Riftwalker, 1, "Initiate", new int[] { ItemID.AncientCobaltHelmet, ItemID.AncientCobaltBreastplate, ItemID.AncientCobaltLeggings }, new int[0], ItemID.ZapinatorGray, 15 * G, 6),
			new("Voidwalker", "Voidwalker", HeirClass.Riftwalker, 3, "Waystrider", new int[] { ItemID.OrichalcumHeadgear, ItemID.OrichalcumBreastplate, ItemID.OrichalcumLeggings }, new int[] { ItemID.CelestialMagnet }, ItemID.MagicDagger, 120 * G, 6),
			new("RiftLord", "Rift Lord", HeirClass.Riftwalker, 5, "Voidwalker", new int[] { ItemID.ApprenticeAltHead, ItemID.ApprenticeAltShirt, ItemID.ApprenticeAltPants }, new int[] { ItemID.CelestialEmblem }, ItemID.HeatRay, 750 * G, 6),

			// Chronomancer path
			new("Timekeeper", "Timekeeper", HeirClass.Chronomancer, 0, null, new int[] { ItemID.TungstenHelmet, ItemID.TungstenChainmail, ItemID.TungstenGreaves }, new int[0], ItemID.SapphireStaff, 0, 7),
			new("Hourwarden", "Hourwarden", HeirClass.Chronomancer, 1, "Timekeeper", new int[] { ItemID.PumpkinHelmet, ItemID.PumpkinBreastplate, ItemID.PumpkinLeggings }, new int[0], ItemID.TheUndertaker, 15 * G, 7),
			new("Chronarch", "Chronarch", HeirClass.Chronomancer, 3, "Hourwarden", new int[] { ItemID.TitaniumHeadgear, ItemID.TitaniumBreastplate, ItemID.TitaniumLeggings }, new int[] { ItemID.CrossNecklace }, ItemID.FlowerofFrost, 120 * G, 7),
			// Tidecaller: secret. Not a starter; unlocked after Duke Fishron falls at the hidden sea.
			new("Tidecaller", "Tidecaller", HeirClass.Fishron, 0, null, new int[] { ItemID.AnglerHat, ItemID.AnglerVest, ItemID.AnglerPants }, new int[] { ItemID.FishronWings }, ItemID.Swordfish, 250 * G, 8),
			new("Timeless", "the Timeless", HeirClass.Chronomancer, 5, "Chronarch", new int[] { ItemID.SpectreMask, ItemID.SpectreRobe, ItemID.SpectrePants }, new int[] { ItemID.CharmofMyths }, ItemID.Keybrand, 750 * G, 7),
		};

		/// <summary>
		/// Alternative starting weapons per class. An heir of that class rolls one of these or the class's own weapon
		/// (which is always variant zero, named after the class).
		/// </summary>
		private static readonly Dictionary<string, ClassVariant[]> Variants = new() {
			["Squire"] = new ClassVariant[] { new("Cactus Knight", ItemID.CactusSword) },
			["Knight"] = new ClassVariant[] { new("Blade-Sworn", ItemID.CopperBroadsword) },
			["Hunter"] = new ClassVariant[] { new("Shotgunner", ItemID.Boomstick), new("Pistoleer", ItemID.FlintlockPistol) },
			["Marksman"] = new ClassVariant[] { new("Bowyer", ItemID.WoodenBow) },
			["Apprentice"] = new ClassVariant[] { new("Thornmage", ItemID.Vilethorn), new("Pyromancer", ItemID.WandofSparking) },
			["Magus"] = new ClassVariant[] { new("Hydromancer", ItemID.WaterBolt) },
			["Footpad"] = new ClassVariant[] { new("Blademaster", ItemID.Katana), new("Spinner", ItemID.Code1) },
			["Shade"] = new ClassVariant[] { new("Yoyo Adept", ItemID.Rally) },
			["Brawler"] = new ClassVariant[] { new("Flail Swinger", ItemID.BallOHurt), new("Boomeranger", ItemID.WoodenBoomerang) },
			["Gladiator"] = new ClassVariant[] { new("Lancer", ItemID.Trident) },
			["Acolyte"] = new ClassVariant[] { new("Slime Tamer", ItemID.SlimeStaff), new("Lasher", ItemID.BlandWhip) },
			["Beekeeper"] = new ClassVariant[] { new("Impcaller", ItemID.ImpStaff) },
			["Initiate"] = new ClassVariant[] { new("Zapper", ItemID.ZapinatorGray), new("Stormcaller", ItemID.ThunderStaff) },
			["Waystrider"] = new ClassVariant[] { new("Spellknife", ItemID.MagicDagger) },
			["Timekeeper"] = new ClassVariant[] { new("Clockhand", ItemID.TheUndertaker), new("Hourblade", ItemID.LeadBroadsword) },
			["Hourwarden"] = new ClassVariant[] { new("Frostbinder", ItemID.SapphireStaff) },
			["Tidecaller"] = new ClassVariant[] { new("Harpooner", ItemID.Harpoon), new("Tidecaster", ItemID.AquaScepter) },
		};

		/// <summary>Every way this class can start: its own weapon first, then any variants.</summary>
		public static List<ClassVariant> VariantsOf(ClassKit k)
		{
			var list = new List<ClassVariant> { new(k.Name, k.Weapon) };
			if (Variants.TryGetValue(k.Key, out ClassVariant[] extra))
				list.AddRange(extra);
			return list;
		}

		public static ClassKit Get(string key) => All.Find(k => k.Key == key);

		/// <summary>The tier-0 class of a path (used for old saves and as a fallback).</summary>
		public static ClassKit Starter(HeirClass path) => All.Find(k => k.Path == path && k.Tier == 0);

		/// <summary>Tier-0 classes come unlocked, except the secret Tidecaller.</summary>
		public static bool IsStarter(ClassKit k) => k.Tier == 0 && k.Path != HeirClass.Fishron;

		/// <summary>Guardian trophy (floor number) required before this tier can be unlocked.</summary>
		public static int RequiredFloor(ClassKit k) => k.Tier;
	}
}
