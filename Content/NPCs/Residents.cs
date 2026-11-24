using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terralayer.Content.Items;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.NPCs
{
	/// <summary>Starts with you. Runs the Estate (buildings, upgrades, classes) and keeps the family history.</summary>
	[AutoloadHead]
	public class Steward : LegacyTownNPC
	{
		protected override int VanillaTemplate => NPCID.Clothier;
		protected override int VanillaHeadIndex => 7;
		protected override string[] Names => new[] { "Edmund", "Harlow", "Percival", "Ambrose", "Tobias", "Ignatius" };
		protected override string Structure => "Hall";

		public override string GetChat()
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			StructureDef next = TerralayerWorld.Instance.NextStructure;
			string build = next != null ? $" Next on my list: the {next.Name}, for {Coins.FormatPlain(next.Cost)}." : "";
			return Pick(
				$"Welcome home, {lp.HeirTitle}. You have no tools, no pick, no axe - but you have coin, and coin builds.{build}",
				$"Every coin spent here outlives the one who earned it. The Gatekeeper's toll does not.{build}",
				$"Generation {lp.Generation}. The portraits keep multiplying.{build}");
		}

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = "Estate";
			button2 = "Skill Tree";
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			CloseChat();
			if (firstButton)
				ModContent.GetInstance<UISystem>().OpenEstate();
			else
				ModContent.GetInstance<UISystem>().OpenSkillTree();
		}

		public static string Lineage(LegacyPlayer lp)
		{
			if (lp.Ancestors.Count == 0)
				return "The family book is still blank.";
			var sb = new StringBuilder("Recent ancestors: ");
			foreach (AncestorRecord a in lp.Ancestors.Take(3))
				sb.Append($"Gen {a.Generation} {ClassDefs.Get(a.Class).Name} - {a.Fate}   ");
			return sb.ToString();
		}

	}

	/// <summary>Starts with you. Takes the toll and sends you into the Depths.</summary>
	[AutoloadHead]
	public class Gatekeeper : LegacyTownNPC
	{
		protected override int VanillaTemplate => NPCID.TaxCollector;
		protected override int VanillaHeadIndex => 23;
		protected override string[] Names => new[] { "Mordecai", "Silas", "Obadiah", "Crane", "Vex", "Thaddeus" };
		protected override string Structure => "Gatehouse";

		public override string GetChat()
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			long purse = Coins.InPurse(Main.LocalPlayer) * lp.TollPercent / 100;
			long safe = Coins.InBanks(Main.LocalPlayer) * lp.SafeTollPercent / 100;
			string today = $"{lp.TollPercent}% of your purse ({Coins.FormatPlain(purse)}) and {lp.SafeTollPercent}% of what's in the safe ({Coins.FormatPlain(safe)})";
			return Pick(
				$"Down is easy. Up is the hard part. Toll today: {today}. The safe's the cheaper place to keep it.",
				$"Coin in your pocket pays me {lp.TollPercent}%. Coin in the safe pays half that. Whatever you find down there, you keep - if you live.",
				$"The Depths reshape themselves after every descent. Rifts link the rooms. Find the guardian. Toll: {today}.");
		}

		public override void SetChatButtons(ref string button, ref string button2) => button = $"Descend (floor 1: {FloorDefs.Get(1).Name})";

		/// <summary>Every descent starts at the top: floor 1.</summary>
		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (!firstButton)
				return;
			CloseChat();
			Main.LocalPlayer.GetModPlayer<LegacyPlayer>().RequestEnterRun(1);
		}
	}

	/// <summary>
	/// Smithy. Doesn't sell weapons: it refines yours with the Refinement Shards you find in chests, merges
	/// accessories together, and buys the gear you aren't using.
	/// </summary>
	[AutoloadHead]
	public class Smith : LegacyTownNPC
	{
		protected override int VanillaTemplate => NPCID.Demolitionist;
		protected override int VanillaHeadIndex => 4;
		protected override string[] Names => new[] { "Brunhild", "Torvald", "Hagen", "Magda", "Osric", "Grete" };
		protected override string Structure => "Smithy";

		public override string GetChat() => Pick(
			"Bring me shards from the Depths and I'll hammer another edge onto that blade. Each one costs more than the last.",
			"Two trinkets that ought to be one? I can put them together. For a fee.",
			"Got gear you're not wearing? I'll buy it. Better coin in your purse than rust in your pack.",
			"I don't sell weapons. The good ones are found, fused and earned.");

		public override void SetChatButtons(ref string button, ref string button2) => button = "Forge";

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (!firstButton)
				return;
			CloseChat();
			ModContent.GetInstance<UISystem>().OpenSmith();
		}
	}

	/// <summary>Rune Sanctum. Sells common runes; three identical runes fuse into a rarer one.</summary>
	[AutoloadHead]
	public class Runeweaver : LegacyTownNPC
	{
		protected override int VanillaTemplate => NPCID.Wizard;
		protected override int VanillaHeadIndex => 10;
		protected override string[] Names => new[] { "Ysmay", "Corvin", "Liesel", "Aurelio", "Thessaly", "Morwen" };
		protected override string Structure => "Sanctum";

		public override string GetChat() => Pick(
			"My stock travels with the threads: six runes, never the same twice. Come back after your next descent and it'll all have changed.",
			"Feeling lucky? A sealed pouch holds one rune. Could be a common scrap, could be a legend with perfect odds.",
			"Carry three runes of one kind and one rarity and they'll fuse into something rarer, and steadier: the fused rune keeps the best odds of the three.");

		public override void SetChatButtons(ref string button, ref string button2) => button = ShopButton;

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
		}

		public override void AddShops() => new NPCShop(Type, "Shop").Add(ModContent.ItemType<RunePouch>()).Register();

		/// <summary>The stock is rebuilt each time the shop opens: this heir's current travelling stock, minus what's sold, then pouches.</summary>
		public override void ModifyActiveShop(string shopName, Item[] items)
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			for (int i = 0; i < items.Length; i++)
				items[i] = new Item();
			int slot = 0;
			List<Item> stock = RuneStock.Generate(lp.RuneStockSeed);
			for (int i = 0; i < stock.Count; i++)
				if (!lp.RuneStockSold.Contains(i))
					items[slot++] = stock[i];
			var pouch = new Item(ModContent.ItemType<RunePouch>()) { shopCustomPrice = (int)RunePouch.Price };
			items[slot] = pouch;
		}

		/// <summary>Gives each rune on sale fresh "unproven" odds (shop runes top out 20 points lower).</summary>
		public static void RerollOdds(Item[] items)
		{
			foreach (Item item in items) {
				if (item?.ModItem is RuneItem r)
					r.Chance = RuneOdds.Roll(r.Rarity, null, shop: true);
				else if (item?.ModItem is ArmorRuneItem a)
					a.Chance = RuneOdds.Roll(a.Rarity, null, shop: true);
			}
		}
	}

	/// <summary>Infirmary. Sells potions to carry into the next descent.</summary>
	[AutoloadHead]
	public class Healer : LegacyTownNPC
	{
		protected override int VanillaTemplate => NPCID.Nurse;
		protected override int VanillaHeadIndex => 3;
		protected override string[] Names => new[] { "Agnes", "Rosamund", "Clement", "Wilhelmina", "Faye", "Barnaby" };
		protected override string Structure => "Infirmary";

		public override string GetChat() => Pick(
			"Potions, bandages, a stern talking-to. The first two cost money.",
			"The Field Kit upgrade means I pack potions for every heir, free. You're welcome.",
			"I've stitched up three generations of your family. Mostly the same wounds.");

		public override void SetChatButtons(ref string button, ref string button2) => button = ShopButton;

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
		}

		public override void AddShops()
		{
			new NPCShop(Type, "Shop")
				.Add(Priced(ItemID.LesserHealingPotion, 0, 50))
				.Add(Priced(ItemID.LesserManaPotion, 0, 30))
				.Add(Priced(ItemID.HealingPotion, 1), FloorOpened(2))
				.Add(Priced(ItemID.ManaPotion, 0, 60), FloorOpened(2))
				.Add(Priced(ItemID.GreaterHealingPotion, 3), FloorOpened(4))
				.Add(Priced(ItemID.IronskinPotion, 1))
				.Add(Priced(ItemID.RegenerationPotion, 1))
				.Add(Priced(ItemID.SwiftnessPotion, 1))
				.Add(Priced(ItemID.ArcheryPotion, 1), FloorOpened(2))
				.Add(Priced(ItemID.MagicPowerPotion, 1), FloorOpened(2))
				.Add(Priced(ItemID.SummoningPotion, 1), FloorOpened(2))
				.Add(Priced(ItemID.WrathPotion, 2), FloorOpened(3))
				.Add(Priced(ItemID.RagePotion, 2), FloorOpened(3))
				.Add(Priced(ItemID.EndurancePotion, 2), FloorOpened(3))
				.Add(Priced(ItemID.LifeforcePotion, 3), FloorOpened(4))
				.Register();
		}
	}

	/// <summary>Cartographer's Tower. Sells charts that reveal the next floor, and can pin the layout in place.</summary>
	[AutoloadHead]
	public class Cartographer : LegacyTownNPC
	{
		public const long ChartPrice = 3 * Coins.Gold;

		protected override int VanillaTemplate => NPCID.Painter;
		protected override int VanillaHeadIndex => 17;
		protected override string[] Names => new[] { "Mercator", "Ines", "Ptolemy", "Odile", "Ansel", "Rosalind" };
		protected override string Structure => "Tower";

		public override string GetChat() => TerralayerWorld.Instance.LayoutPinned
			? "Your layout is pinned: same rooms, same rifts, every descent. The Gatekeeper charges extra for it."
			: Pick("The Depths reshape after every descent. I can pin them in place, for a price at the gate.",
				$"A chart of the first floor you reach next time? {Coins.FormatPlain(ChartPrice)}. Every room, every rift.");

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = $"Buy Chart ({Coins.FormatPlain(ChartPrice)})";
			button2 = TerralayerWorld.Instance.LayoutPinned ? "Unpin Layout" : "Pin Layout";
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (!firstButton) {
				bool now = !TerralayerWorld.Instance.LayoutPinned;
				if (Main.netMode == NetmodeID.MultiplayerClient)
					Common.Net.TerralayerNet.SendWorldAction(Common.Net.WorldAction.TogglePin, 0, "");
				else
					TerralayerWorld.Instance.ApplyAction(Common.Net.WorldAction.TogglePin, 0, "");
				Main.npcChatText = now ? "Pinned. Your next descents will match the last one." : "Unpinned. The Depths will shift again.";
				return;
			}
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			if (lp.RevealNextFloor) {
				Main.npcChatText = "You already carry one of my charts.";
				return;
			}
			if (!Main.LocalPlayer.BuyItem(ChartPrice)) {
				Main.npcChatText = "Come back with coin.";
				return;
			}
			SoundEngine.PlaySound(SoundID.Coins);
			lp.RevealNextFloor = true;
			Main.npcChatText = "Here. It fills itself in the moment you arrive.";
		}
	}

	/// <summary>A wandering trader found in merchant rooms deep in the Depths.</summary>
	public class Peddler : ModNPC
	{
		public override string Texture => $"Terraria/Images/NPC_{NPCID.TravellingMerchant}";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.TravellingMerchant];
			NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.TravellingMerchant];
			NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.TravellingMerchant];
		}

		public override void SetDefaults()
		{
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.FaceClosestPlayer;
			NPC.lifeMax = 250;
			NPC.dontTakeDamage = true;
			NPC.dontTakeDamageFromHostiles = true;
			NPC.immortal = true;
			AnimationType = NPCID.TravellingMerchant;
		}

		public override bool CheckActive() => false;

		public override bool CanChat() => true;

		public override string GetChat() => Main.rand.Next(3) switch {
			0 => "Ah, a customer! Prices are high down here. Supply and demand, and nobody supplies anything.",
			1 => "Wings, boots, hooks - things that keep your feet off the spikes. Interested?",
			_ => "I've seen a lot of heirs come through. Some even twice. Different faces, same family nose."
		};

		public override void SetChatButtons(ref string button, ref string button2) => button = Terraria.Localization.Language.GetTextValue("LegacyInterface.28");

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = "Shop";
		}

		private static Condition OnFloor(int f) => new($"Mods.Terralayer.Conditions.Floor{f}", () => DepthsSystem.Instance.Floor >= f);

		private static Item Priced(int type, int gold) => new(type) { shopCustomPrice = Item.buyPrice(gold: gold) };

		public override void AddShops()
		{
			var shop = new NPCShop(Type, "Shop")
				.Add(Priced(ItemID.HealingPotion, 2))
				.Add(Priced(ItemID.GreaterHealingPotion, 5), OnFloor(3))
				.Add(Priced(ItemID.ManaPotion, 1))
				.Add(Priced(ItemID.EndlessQuiver, 10))
				.Add(Priced(ItemID.EndlessMusketPouch, 10))
				.Add(Priced(ItemID.CloudinaBottle, 8))
				.Add(Priced(ItemID.HermesBoots, 8))
				.Add(Priced(ItemID.RocketBoots, 14), OnFloor(2))
				.Add(Priced(ItemID.DualHook, 25), OnFloor(3))
				.Add(Priced(ItemID.FairyWings, 60), OnFloor(4));
			foreach (RuneType r in new[] { RuneType.Vampiric, RuneType.Reaping, RuneType.Storm, RuneType.Ember }) {
				Item rune = RuneItem.Create(r, Rarity.Uncommon);
				rune.shopCustomPrice = Item.buyPrice(gold: 10);
				shop.Add(rune);
			}
			foreach (ArmorRuneType r in new[] { ArmorRuneType.Vigor, ArmorRuneType.Fortitude, ArmorRuneType.Warding, ArmorRuneType.Swiftness }) {
				Item rune = ArmorRuneItem.Create(r, Rarity.Uncommon);
				rune.shopCustomPrice = Item.buyPrice(gold: 10);
				shop.Add(rune);
			}
			shop.Register();
		}

		/// <summary>Every visit, the rune stock shows fresh inscription odds (shop runes are unproven: lower odds).</summary>
		public override void ModifyActiveShop(string shopName, Item[] items) => Runeweaver.RerollOdds(items);
	}
}
