using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Net;
using Terralayer.Common.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Terralayer.Common.Players
{
	/// <summary>
	/// Room-clear rewards: three cards, each with a rarity. The first card is always a Common restoration card (life
	/// and ability charge don't come back on their own in the Depths); the other two roll any rarity.
	/// </summary>
	public partial class LegacyPlayer
	{
		private int Depth => Math.Max(1, DepthsSystem.Instance.Floor);

		/// <summary>The class weapon you carry, in whatever form you've evolved it into (0 if none).</summary>
		private int RefineWeaponType()
		{
			if (Heir == null)
				return 0;
			Item carried = Player.inventory.FirstOrDefault(i => RefinedWeapon.Of(i) != null && Loot.SameLine(i.type, Heir.WeaponType));
			return carried?.type ?? Heir.WeaponType;
		}

		/// <summary>The carried copy of the class weapon, or null.</summary>
		private Item CarriedClassWeapon()
		{
			int type = RefineWeaponType();
			return type > 0 ? Player.inventory.FirstOrDefault(i => i.type == type && RefinedWeapon.Of(i) != null) : null;
		}

		/// <summary>The weapon a Reforge works on: the held weapon, else the class weapon.</summary>
		private Item ReforgeTarget() => RefinedWeapon.Of(Player.HeldItem) != null ? Player.HeldItem : CarriedClassWeapon();

		private bool Valid(CardType t)
		{
			Item w = CarriedClassWeapon();
			var r = RefinedWeapon.Of(w);
			return t switch {
				CardType.Recharge or CardType.CatchBreath => Charge < MaxCharge - 15,
				CardType.Clarity => Charge < MaxCharge - 15 || Player.statMana < Player.statManaMax2,
				CardType.Refine or CardType.DoubleRefine or CardType.Masterwork => r != null && r.Level < RefinedWeapon.MaxLevel,
				CardType.Reforge => ReforgeTarget() != null,
				CardType.Ascension => r != null && (Loot.EvolutionOf(w.type) > 0 || r.Level < RefinedWeapon.MaxLevel),
				_ => true
			};
		}

		private void RollRoomReward()
		{
			var rng = new Random(Main.rand.Next());
			var used = new HashSet<CardType>();
			for (int slot = 0; slot < 3; slot++) {
				Rarity rarity = slot == 0 ? Rarity.Common : RewardCards.RollRarity(rng);
				OfferedCards[slot] = RollCard(rarity, rng, used);
				used.Add(OfferedCards[slot].Type);
			}
			OfferSerial++;
			RewardOffered = true;
			SoundEngine.PlaySound(SoundID.Item4, Player.Center);
		}

		private RewardCard RollCard(Rarity rarity, Random rng, HashSet<CardType> used)
		{
			// Fall back a rarity at a time if nothing of this one fits right now.
			for (Rarity r = rarity; r >= Rarity.Common; r--) {
				var pool = RewardCards.OfRarity(r).Where(t => !used.Contains(t) && Valid(t)).ToList();
				if (pool.Count == 0)
					continue;
				var card = new RewardCard { Type = pool[rng.Next(pool.Count)] };
				FillCard(ref card, rng);
				return card;
			}
			return new RewardCard { Type = CardType.Mend };
		}

		/// <summary>Rolls whatever a card carries (so the card can say exactly what you'll get).</summary>
		private void FillCard(ref RewardCard card, Random rng)
		{
			switch (card.Type) {
				case CardType.Boon:
				case CardType.TwinBoon:
					card.Boon = Boons.Random(rng);
					break;
				case CardType.Arsenal:
					card.Item = Loot.ArsenalWeapon(Class, Depth, RefineWeaponType(), rng);
					card.ItemRarity = RarityDefs.Roll(1, rng);
					break;
				case CardType.GildedArsenal: {
					int[] pool = Loot.WeaponPool(rng.NextDouble() < 0.85 ? Class : (HeirClass)rng.Next((int)HeirClass.Count), Math.Min(3, Loot.StageForFloor(Depth, rng) + 1));
					card.Item = pool[rng.Next(pool.Length)];
					card.ItemRarity = (Rarity)Math.Max((int)Rarity.Rare, (int)RarityDefs.Roll(2, rng));
					break;
				}
				case CardType.Charm:
					card.Item = Loot.RollAccessory(Depth, rng).type;
					break;
				case CardType.Trinket:
					card.Item = Loot.RollAccessory(Depth + 1, rng).type;
					break;
			}
		}

		// ------------------------------------------------------------------ what a card says

		public string CardText(RewardCard c)
		{
			int heal(float f) => (int)(Player.statLifeMax2 * f * HealScale);
			Item w = CarriedClassWeapon();
			string weapon = w != null ? w.Name : "class weapon";
			BoonInfo b = Boons.Get(c.Boon);
			return c.Type switch {
				CardType.Mend => $"Restore 35% of your life (+{heal(0.35f)}).",
				CardType.Recharge => "Restore 50 ability charge.",
				CardType.CatchBreath => $"Restore 20% life (+{heal(0.2f)}) and 30 ability charge.",
				CardType.Bandage => $"Restore 10% life now, then regenerate for 30 seconds.",
				CardType.Clarity => $"Refill your mana, restore 25 ability charge and 10% life.",
				CardType.Rations => $"Well Fed for 5 minutes (small boost to most stats) and restore 15% life.",
				CardType.SparePotion => $"A {Lang.GetItemNameValue(Loot.Healing(Depth, 1).type)} for later.",
				CardType.Tonic => "Ironskin (+8 defense) for 2 minutes and restore 15% life.",

				CardType.Refine => RefineText(1),
				CardType.Reforge => $"Reforge your {ReforgeTarget()?.Name ?? weapon}: a new modifier, the best of three rolls. Refinement and runes stay.",
				CardType.Boon => $"{b.Name}: {b.PerStack} for the rest of this descent.",
				CardType.CoinPurse => $"{Coins.Format(PurseGold())}.",
				CardType.Satchel => $"Two {Lang.GetItemNameValue(Loot.Healing(Depth, 1).type)}s and a random buff potion.",
				CardType.FullRestore => "Restore all of your life, mana and ability charge.",
				CardType.Charm => $"An accessory: {Lang.GetItemNameValue(c.Item)}.",

				CardType.Arsenal => $"A new weapon for your path: {RarityDefs.Tag(c.ItemRarity, $"{RarityDefs.Name(c.ItemRarity)} {Lang.GetItemNameValue(c.Item)}")}.",
				CardType.TwinBoon => $"{b.Name} twice: {b.PerStack}, two stacks.",
				CardType.RuneStone => "A rune stone, likelier than usual to be rare.",
				CardType.Armory => "An armor piece from deeper down, with a rune already inscribed.",
				CardType.Trinket => $"An accessory from the next floor: {Lang.GetItemNameValue(c.Item)}.",
				CardType.DoubleRefine => RefineText(2),

				CardType.Mystery => "Something rattles inside. A rune, deeper gear, gold, a blessing... or a trap.",
				CardType.AscendantRune => "A rune of Rare quality or better.",
				CardType.Triumph => "Three random Boons for this descent.",
				CardType.Masterwork => $"Your {weapon} gains a full refinement level.",
				CardType.GildedArsenal => $"A weapon from a stage deeper: {RarityDefs.Tag(c.ItemRarity, $"{RarityDefs.Name(c.ItemRarity)} {Lang.GetItemNameValue(c.Item)}")}.",

				CardType.Ascension => w != null && Loot.EvolutionOf(w.type) > 0
					? $"Your {weapon} evolves into {Lang.GetItemNameValue(Loot.EvolutionOf(w.type))} right now (refinement resets to +0; rarity and runes stay)."
					: $"Your {weapon} gains two refinement levels.",
				CardType.LineBlessing => "Five random Boons for this descent.",
				CardType.LegendaryRune => "A Legendary rune.",
				_ => ""
			};
		}

		/// <summary>The icon a card shows: the weapon it gives or works on where that makes sense.</summary>
		public int CardIcon(RewardCard c) => c.Type switch {
			CardType.Arsenal or CardType.GildedArsenal or CardType.Charm or CardType.Trinket => c.Item,
			CardType.Refine or CardType.DoubleRefine or CardType.Masterwork or CardType.Ascension => CarriedClassWeapon()?.type ?? RewardCards.Get(c.Type).Icon(),
			CardType.Reforge => ReforgeTarget()?.type ?? RewardCards.Get(c.Type).Icon(),
			_ => RewardCards.Get(c.Type).Icon()
		};

		private string RefineText(int copies)
		{
			Item carried = CarriedClassWeapon();
			var r = RefinedWeapon.Of(carried);
			string name = carried?.Name ?? Lang.GetItemNameValue(RefineWeaponType());
			if (r != null && r.Level >= RefinedWeapon.MaxLevel)
				return $"A copy of your {name}. It is already fully refined.";
			string what = copies == 1 ? "A copy" : $"{copies} copies";
			return $"{what} of your {name} to fuse in (+{copies} fusion{(copies > 1 ? "s" : "")}" + (r != null ? $"; now {r.Fusion}/{r.Level} toward Lv {r.Level + 1})." : ").");
		}

		private long PurseGold() => (long)(Coins.Gold * (1 + Depth * 1.5f) * GoldFind);

		private float HealScale => Traits.Contains(HeirTrait.Hemophiliac) ? 0.8f : 1f;

		// ------------------------------------------------------------------ picking

		public void PickCard(int slot)
		{
			if (!RewardOffered || slot < 0 || slot >= OfferedCards.Length)
				return;
			RewardCard c = OfferedCards[slot];
			var rng = new Random(Main.rand.Next());
			switch (c.Type) {
				case CardType.Mend: Restore(0.35f, 0); break;
				case CardType.Recharge: Restore(0f, 50); break;
				case CardType.CatchBreath: Restore(0.20f, 30); break;
				case CardType.Bandage:
					Restore(0.10f, 0);
					Player.AddBuff(BuffID.Regeneration, 60 * 30);
					break;
				case CardType.Clarity:
					Restore(0.10f, 25);
					Player.statMana = Player.statManaMax2;
					Player.ManaEffect(Player.statManaMax2);
					break;
				case CardType.Rations:
					Restore(0.15f, 0);
					Player.AddBuff(BuffID.WellFed, 60 * 300);
					break;
				case CardType.SparePotion: Give(Loot.Healing(Depth, 1)); break;
				case CardType.Tonic:
					Restore(0.15f, 0);
					Player.AddBuff(BuffID.Ironskin, 60 * 120);
					break;

				case CardType.Refine: GiveRefineCopies(1); break;
				case CardType.Reforge: Reforge(); break;
				case CardType.Boon: AddBoons(c.Boon, 1); break;
				case CardType.CoinPurse:
					Coins.PutInInventory(Player, PurseGold());
					SoundEngine.PlaySound(SoundID.Coins, Player.Center);
					break;
				case CardType.Satchel:
					Give(Loot.Healing(Depth, 2));
					Give(Loot.RollPotion(Depth, rng) is Item p && p.healLife == 0 ? p : new Item(ItemID.SwiftnessPotion));
					break;
				case CardType.FullRestore:
					Restore(1f, MaxCharge);
					Player.statMana = Player.statManaMax2;
					break;
				case CardType.Charm:
				case CardType.Trinket: {
					Give(new Item(c.Item));
					break;
				}

				case CardType.Arsenal:
				case CardType.GildedArsenal: {
					Item weapon = RefinedWeapon.Create(c.Item, c.ItemRarity);
					Give(weapon);
					GiveAmmoFor(weapon);
					break;
				}
				case CardType.TwinBoon: AddBoons(c.Boon, 2); break;
				case CardType.RuneStone: GiveAnnounced(Loot.RollRune(1, rng)); break;
				case CardType.Armory: GiveAnnounced(Loot.RollArmor(Depth + 1, rng, 1)); break;
				case CardType.DoubleRefine: GiveRefineCopies(2); break;

				case CardType.Mystery: OpenMysteryCache(rng); break;
				case CardType.AscendantRune: GiveAnnounced(Loot.RuneOfRarity((Rarity)Math.Max((int)Rarity.Rare, (int)RarityDefs.Roll(2, rng)), rng)); break;
				case CardType.Triumph: RandomBoons(3, rng); break;
				case CardType.Masterwork: {
					var r = RefinedWeapon.Of(CarriedClassWeapon());
					if (r != null) {
						r.AddFusions(r.Level - r.Fusion);
						CombatText.NewText(Player.Hitbox, RarityDefs.Color(Rarity.Epic), $"Refined to +{r.Level - 1}!", true);
					}
					break;
				}

				case CardType.Ascension: Ascend(); break;
				case CardType.LineBlessing: RandomBoons(5, rng); break;
				case CardType.LegendaryRune: GiveAnnounced(Loot.RuneOfRarity(Rarity.Legendary, rng)); break;
			}
			Color color = RarityDefs.Color(RewardCards.Get(c.Type).Rarity);
			CombatText.NewText(Player.Hitbox, color, RewardCards.Get(c.Type).Name, true);
			SoundEngine.PlaySound(SoundID.Item37, Player.Center);
			RewardOffered = false;
			PendingRoomRewards = Math.Max(0, PendingRoomRewards - 1);
		}

		/// <summary>Restoration: a share of max life and some ability charge.</summary>
		private void Restore(float lifeShare, float charge)
		{
			int heal = (int)(Player.statLifeMax2 * lifeShare * HealScale);
			if (heal > 0)
				Player.Heal(heal);
			if (charge > 0) {
				Charge = Math.Min(MaxCharge, Charge + charge);
				CombatText.NewText(Player.Hitbox, new Color(230, 190, 60), $"+{(int)charge} charge");
			}
		}

		private void GiveRefineCopies(int copies)
		{
			int type = RefineWeaponType();
			for (int i = 0; i < copies && type > 0; i++)
				Give(RefinedWeapon.Create(type, Rarity.Common)); // found copies: they fuse on the next inventory pass
		}

		private void GiveAnnounced(Item item)
		{
			Give(item);
			Main.NewText($"You receive {item.Name}.", RarityDefs.Color(RewardCards.Get(CardType.RuneStone).Rarity));
		}

		private void AddBoons(BoonStat stat, int stacks)
		{
			RunBoons[(int)stat] += stacks;
			Player.GetModPlayer<Quests.QuestPlayer>().OnBoonTaken();
			BoonInfo b = Boons.Get(stat);
			Main.NewText($"{b.Name}: {b.PerStack}" + (stacks > 1 ? $" x{stacks}" : "") + " for this descent.", b.Color);
		}

		private void RandomBoons(int count, Random rng)
		{
			var names = new List<string>();
			for (int i = 0; i < count; i++) {
				BoonStat s = Boons.Random(rng);
				RunBoons[(int)s]++;
				names.Add($"{Boons.Get(s).Name} ({Boons.Get(s).PerStack})");
			}
			Player.GetModPlayer<Quests.QuestPlayer>().OnBoonTaken();
			Main.NewText("Blessed: " + string.Join(", ", names), new Color(255, 215, 140));
		}

		private void Reforge()
		{
			Item target = ReforgeTarget();
			if (target == null)
				return;
			int slot = Array.IndexOf(Player.inventory, target);
			Item reforged = RefinedWeapon.Reforged(target);
			if (slot < 0 || reforged == null) {
				Main.NewText("The reforge found nothing better.", Color.Gray);
				return;
			}
			Player.inventory[slot] = reforged;
			Main.NewText($"Reforged: {reforged.Name}.", RarityDefs.Color(Rarity.Uncommon));
		}

		private void Ascend()
		{
			Item w = CarriedClassWeapon();
			var r = RefinedWeapon.Of(w);
			if (r == null)
				return;
			int slot = Array.IndexOf(Player.inventory, w);
			Item evolved = Loot.EvolutionOf(w.type) > 0 ? RefinedWeapon.Evolve(w) : null;
			if (evolved != null && slot >= 0) {
				string from = w.Name;
				Player.inventory[slot] = evolved;
				SoundEngine.PlaySound(SoundID.Item119, Player.Center);
				Main.NewText($"{from} ascends into {evolved.Name}!", RarityDefs.Color(Rarity.Legendary));
				Player.GetModPlayer<Quests.QuestPlayer>().OnEvolved();
			}
			else {
				r.AddFusions(r.Level - r.Fusion);
				r.AddFusions(r.Level - r.Fusion);
			}
		}

		/// <summary>The Mystery Cache: a rune, deeper gear, gold, a blessing - or an ambush.</summary>
		private void OpenMysteryCache(Random rng)
		{
			var depths = DepthsSystem.Instance;
			double roll = rng.NextDouble();
			Color c = new(200, 160, 255);
			if (roll < 0.28) {
				Item rune = Loot.RollRune(1, rng);
				Give(rune);
				Main.NewText($"The cache holds a {rune.Name}!", c);
			}
			else if (roll < 0.50) {
				Item acc = Loot.RollAccessory(Depth + 1, rng);
				Give(acc);
				Main.NewText($"The cache holds {acc.Name}!", c);
			}
			else if (roll < 0.72) {
				long gold = Coins.Gold * Depth * (1 + rng.Next(3));
				Coins.PutInInventory(Player, gold);
				SoundEngine.PlaySound(SoundID.Coins, Player.Center);
				Main.NewText($"The cache spills {Coins.Format(gold)}!", c);
			}
			else if (roll < 0.92) {
				RandomBoons(2, rng);
			}
			else {
				if (Main.netMode == NetmodeID.MultiplayerClient)
					TerralayerNet.RequestTrial();
				else
					depths.SpawnTrialChampion(Player);
				SoundEngine.PlaySound(SoundID.Roar, Player.Center);
				Main.NewText("It was a trap! A champion bursts from the cache. Defeat it for a strong weapon.", new Color(255, 140, 60));
			}
		}
	}
}
