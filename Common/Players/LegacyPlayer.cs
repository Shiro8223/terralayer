using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terralayer.Common.Config;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Net;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terralayer.Content.Items;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Players
{
	public enum RunEnd
	{
		Retreat,   // walked out through the start room's rift
		Victory,   // took the gold rift after a guardian
		Conquered  // beat the final floor
	}

	/// <summary>
	/// Everything Terralayer tracks about a character: the current heir and lineage, upgrades, unlocked classes,
	/// trophies, the ability charge bar and the state of the current descent.
	/// </summary>
	/// <summary>What the death screen shows about the heir who just fell.</summary>
	public class DeathRecord
	{
		public string Name = "";
		public string ClassName = "";
		public HeirClass Class;
		public int Generation;
		public string Fate = "";
		public int Floor;
		public string FloorName = "";
		public int RoomsCleared;
		public int Guardians;
		public long CoinsLost;
		public long CoinsKept;
		public int LossPercent;
		public uint Time;
	}

	public partial class LegacyPlayer : ModPlayer
	{
		// ---- Current heir ----
		public HeirData Heir;
		public HeirClass Class => Heir?.Class ?? HeirClass.Knight;
		public ClassKit Kit => Heir?.Kit;
		public List<HeirTrait> Traits => Heir?.Traits ?? new List<HeirTrait>();
		public int Generation;
		/// <summary>Set by an Altar of Change: this heir uses another path's ability.</summary>
		public HeirClass? AbilityOverride;

		// ---- Lineage ----
		public bool PendingHeirChoice = true;
		public List<HeirData> HeirOptions = new();
		public List<AncestorRecord> Ancestors = new();

		// ---- Meta progression ----
		public int[] Upgrades = new int[(int)UpgradeType.Count];
		public HashSet<string> UnlockedKits = new();
		public HashSet<int> Trophies = new();
		public HashSet<string> InitializedWorlds = new();

		// ---- Current descent ----
		public bool InRun;
		public int RunFloor;
		public bool SecondWindUsed;
		public bool RevealNextFloor;
		public bool ChartActive;
		public float BloodDebt;
		public HashSet<int> VisitedRooms = new();
		public HashSet<int> UsedShrines = new();
		private int visitedSeed;

		// ---- Runtime ----
		public float Charge = 100f;
		public bool AbilityOn;
		public float GoldFind = 1f;
		public float ArmorGoldBonus;
		public int QuakeCounter;
		public bool PendingWipeConfirm;
		public int PendingEvolutionSlot = -1;

		// ---- Room-clear rewards (pick one of three) ----
		public int[] RunBoons = new int[(int)BoonStat.Count];
		public int PendingRoomRewards;
		public bool RewardOffered;
		/// <summary>The three cards on offer after a room clear (see LegacyPlayer.Rewards.cs).</summary>
		public RewardCard[] OfferedCards = new RewardCard[3];
		/// <summary>Bumped whenever a new set of cards is rolled, so the UI knows to rebuild.</summary>
		public int OfferSerial;
		public int PendingEvolutionType;
		public RiftPlan HoverRift;
		public bool HoverShrine;
		public bool HoverForge;
		public bool HoverMoonPortal;
		public bool HoverMoonReturn;
		public bool HoverSea;
		public bool HoverSeaReturn;
		public bool InteractInRange;
		private RunEnd? pendingEnd;
		/// <summary>The fallen heir's epitaph, for the death screen. Not saved.</summary>
		public DeathRecord LastDeath;
		/// <summary>Random boons granted at the start of the next descent (quest rewards). Saved.</summary>
		public int PendingBlessings;
		public int RunRoomsCleared;
		/// <summary>The Runeweaver's current travelling stock (a seed) and which of its runes are sold. Saved.</summary>
		public int RuneStockSeed;
		public List<int> RuneStockSold = new();
		public int RunGuardians;
		private int riftCooldown;
		private bool rightWasHeld;
		private int retreatConfirm;
		private int abilityTapCooldown;
		private long stashedCoins;
		private bool refill;
		private readonly Dictionary<RuneType, int> runeCooldowns = new();

		public const float MaxCharge = 100f;
		public const int InteractRange = 10 * 16;

		public HeirClass AbilityClass => AbilityOverride ?? Class;
		public ClassInfo ClassInfo => ClassDefs.Get(Class);
		public ClassInfo AbilityInfo => ClassDefs.Get(AbilityClass);
		public string HeirTitle => Heir == null ? Player.name : Heir.FullName;
		public string ClassName => Kit?.Name ?? ClassInfo.Name;

		/// <summary>
		/// The stronger the family has grown, the tougher the Depths fight back: +3.5% enemy life per skill-tree level
		/// and +5% per guardian trophy, up to 3.5x. Locked in when a descent starts.
		/// </summary>
		public float EnemyLifeScaling => Math.Min(3.5f, 1f + 0.035f * Upgrades.Sum() + 0.05f * Trophies.Count);

		public int TollPercent
		{
			get {
				int baseToll = TerralayerConfig.Instance.TollPercent - 8 * Upgrades[(int)UpgradeType.TollRelief];
				if (TerralayerWorld.Instance.LayoutPinned)
					baseToll += TerralayerConfig.Instance.PinnedLayoutExtraToll;
				return Math.Clamp(baseToll, 0, 100);
			}
		}

		/// <summary>Coins in the safe are tolled at half the purse rate.</summary>
		public int SafeTollPercent => TollPercent / 2;

		public override void Initialize()
		{
			UnlockedKits = new HashSet<string>(ClassKits.All.Where(ClassKits.IsStarter).Select(k => k.Key));
		}

		// ------------------------------------------------------------------ save / load

		public override void SaveData(TagCompound tag)
		{
			if (Heir != null)
				tag["heir"] = Heir.Save();
			tag["generation"] = Generation;
			tag["abilityOverride"] = AbilityOverride.HasValue ? (int)AbilityOverride.Value : -1;
			tag["pending"] = PendingHeirChoice;
			tag["options"] = HeirOptions.Select(o => o.Save()).ToList();
			tag["ancestors"] = Ancestors.Select(a => a.Save()).ToList();
			tag["upgrades"] = Upgrades;
			tag["unlockedKits"] = UnlockedKits.ToList();
			tag["trophies"] = Trophies.ToList();
			tag["worlds"] = InitializedWorlds.ToList();
			tag["revealNext"] = RevealNextFloor;
			tag["blessings"] = PendingBlessings;
			tag["runeStock"] = RuneStockSeed;
			tag["runeSold"] = RuneStockSold;
		}

		public override void LoadData(TagCompound tag)
		{
			// Every field loads on its own: a field saved in an older format must never take the rest down with it.
			Heir = Safe(() => tag.ContainsKey("heir") ? HeirData.Load(tag.GetCompound("heir")) : null, null);
			Generation = Safe(() => tag.GetInt("generation"), 0);
			int ov = Safe(() => tag.ContainsKey("abilityOverride") ? tag.GetInt("abilityOverride") : -1, -1);
			AbilityOverride = ov >= 0 && ov < (int)HeirClass.Count ? (HeirClass)ov : null;
			PendingHeirChoice = Safe(() => !tag.ContainsKey("pending") || tag.GetBool("pending"), true) || Heir == null;
			HeirOptions = Safe(() => tag.GetList<TagCompound>("options").Select(HeirData.Load).ToList(), new List<HeirData>());
			Ancestors = Safe(() => tag.GetList<TagCompound>("ancestors").Select(AncestorRecord.Load).ToList(), new List<AncestorRecord>());
			int[] saved = Safe(() => tag.GetIntArray("upgrades"), Array.Empty<int>());
			Upgrades = new int[(int)UpgradeType.Count];
			Array.Copy(saved, Upgrades, Math.Min(saved.Length, Upgrades.Length));
			foreach (string k in Safe(() => tag.GetList<string>("unlockedKits"), (IList<string>)new List<string>()))
				if (ClassKits.Get(k) != null)
					UnlockedKits.Add(k);
			Trophies = new HashSet<int>(Safe(() => tag.GetList<int>("trophies"), (IList<int>)new List<int>()));
			InitializedWorlds = new HashSet<string>(Safe(() => tag.GetList<string>("worlds"), (IList<string>)new List<string>()));
			RevealNextFloor = Safe(() => tag.GetBool("revealNext"), false);
			PendingBlessings = Safe(() => tag.GetInt("blessings"), 0);
			RuneStockSeed = Safe(() => tag.GetInt("runeStock"), 0);
			RuneStockSold = Safe(() => tag.GetList<int>("runeSold").ToList(), new List<int>());
			if (RuneStockSeed == 0)
				RefreshRuneStock(quiet: true);
		}

		private T Safe<T>(Func<T> read, T fallback)
		{
			try {
				return read();
			}
			catch (Exception e) {
				Mod.Logger.Warn($"Skipped an unreadable Terralayer save field: {e.Message}");
				return fallback;
			}
		}

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			ModPacket p = TerralayerNet.StartPlayerSync(Player.whoAmI);
			p.Write((byte)Class);
			p.Write(GoldFind);
			p.Send(toWho, fromWho);
		}

		public void ReceiveSync(BinaryReader r)
		{
			var cls = (HeirClass)r.ReadByte();
			Heir ??= new HeirData();
			Heir.Class = cls;
			GoldFind = r.ReadSingle();
		}

		// ------------------------------------------------------------------ entering a world

		public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath)
		{
			foreach (List<Item> list in itemsByMod.Values)
				list.Clear();
		}

		public override void OnEnterWorld()
		{
			if (!TerralayerWorld.Active)
				return;
			string id = TerralayerWorld.Instance.WorldId;
			if (InitializedWorlds.Contains(id)) {
				if (Heir != null && !PendingHeirChoice && !Player.inventory.Any(i => RefinedWeapon.Of(i) != null))
					GiveKit();
				else if (Heir != null && !InRun)
					EquipHook();
				if (Heir != null && !PendingHeirChoice)
					EnsureInnateAccessories();
				return;
			}
			if (CarriesAnything())
				PendingWipeConfirm = true;
			else
				AcceptWorld();
		}

		private bool CarriesAnything()
		{
			bool Any(Item[] items) => items.Any(i => i != null && !i.IsAir);
			return Any(Player.inventory) || Any(Player.armor) || Any(Player.dye) || Any(Player.miscEquips) || Any(Player.miscDyes)
				|| Any(Player.bank.item) || Any(Player.bank2.item) || Any(Player.bank3.item) || Any(Player.bank4.item)
				|| Player.ConsumedLifeCrystals > 0 || Player.ConsumedLifeFruit > 0 || Player.ConsumedManaCrystals > 0;
		}

		public void AcceptWorld()
		{
			PendingWipeConfirm = false;
			Wipe(keepCoins: false, keepBankCoins: false);
			Player.ConsumedLifeCrystals = 0;
			Player.ConsumedLifeFruit = 0;
			Player.ConsumedManaCrystals = 0;
			Player.extraAccessory = false;
			Player.difficulty = PlayerDifficultyID.SoftCore;
			InitializedWorlds.Add(TerralayerWorld.Instance.WorldId);
			if (Heir == null)
				PendingHeirChoice = true;
			else if (!PendingHeirChoice)
				GiveKit();
		}

		/// <summary>
		/// Empties inventory, every equipment loadout, modded accessory slots and banks; dismisses minions and pets and
		/// clears buffs. Coins (and runes) can be kept.
		/// </summary>
		public void Wipe(bool keepCoins, bool keepRunes = false, bool keepBankCoins = true)
		{
			for (int i = 0; i < Player.inventory.Length; i++) {
				Item it = Player.inventory[i];
				if (keepCoins && Coins.ValueOf(it) > 0)
					continue;
				if (keepBankCoins && Secrets.EternalItem.Is(it))
					continue; // secret-quest rewards stay with the family
				if (keepRunes && (it.ModItem is RuneItem || it.ModItem is ArmorRuneItem))
					continue;
				it.TurnToAir();
			}
			foreach (Item[] set in new[] { Player.armor, Player.dye, Player.miscEquips, Player.miscDyes })
				foreach (Item i in set)
					i.TurnToAir();
			// The safe is the family vault: coins stashed there survive every heir. Anything else in it doesn't.
			foreach (Item[] bank in Coins.Banks(Player))
				foreach (Item i in bank)
					if (!keepBankCoins || Coins.ValueOf(i) == 0)
						i.TurnToAir();
			foreach (EquipmentLoadout loadout in Player.Loadouts) {
				foreach (Item i in loadout.Armor)
					i.TurnToAir();
				foreach (Item i in loadout.Dye)
					i.TurnToAir();
			}
			ClearModdedAccessorySlots();
			Player.trashItem.TurnToAir();
			if (Player.whoAmI == Main.myPlayer && !Main.mouseItem.IsAir && Coins.ValueOf(Main.mouseItem) == 0)
				Main.mouseItem.TurnToAir();

			// Summons and pets outlive their items unless dismissed.
			foreach (Projectile proj in Main.ActiveProjectiles)
				if (proj.owner == Player.whoAmI && (proj.minion || proj.sentry || Main.projPet[proj.type]))
					proj.Kill();
			for (int b = Player.buffType.Length - 1; b >= 0; b--)
				if (Player.buffType[b] > 0)
					Player.DelBuff(b);
		}

		/// <summary>Extra accessory slots added by other mods live outside the vanilla arrays.</summary>
		private void ClearModdedAccessorySlots()
		{
			try {
				Type t = typeof(ModPlayer).Assembly.GetType("Terraria.ModLoader.Default.ModAccessorySlotPlayer");
				MethodInfo get = typeof(Player).GetMethods().First(m => m.Name == "GetModPlayer" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
				object slots = get.MakeGenericMethod(t).Invoke(Player, null);
				foreach (string name in new[] { "exAccessorySlot", "exDyesAccessory" })
					if (t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(slots) is Item[] items)
						foreach (Item i in items)
							i?.TurnToAir();
				// Each equipment loadout keeps its own copy of those slots; left alone, old gear came back (and kept
				// working) after a death.
				if (t.GetField("exLoadouts", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(slots) is Array loadouts)
					foreach (object l in loadouts)
						foreach (string prop in new[] { "ExAccessorySlot", "ExDyesAccessory" })
							if (l?.GetType().GetProperty(prop)?.GetValue(l) is Item[] copy)
								foreach (Item i in copy)
									i?.TurnToAir();
			}
			catch (Exception e) {
				Mod.Logger.Debug("Couldn't clear modded accessory slots: " + e.Message);
			}
		}

		/// <summary>The Runeweaver moves on to new stock (after each descent, or a death).</summary>
		public void RefreshRuneStock(bool quiet = false)
		{
			RuneStockSeed = Main.rand.Next(1, int.MaxValue);
			RuneStockSold.Clear();
			if (!quiet && TerralayerWorld.Instance.HasBuilt("Sanctum"))
				Main.NewText("The Runeweaver has new runes in stock.", new Color(200, 160, 255));
		}

		public override void PostBuyItem(NPC vendor, Item[] shopInventory, Item item)
		{
			if (vendor.type == ModContent.GetInstance<Content.NPCs.Runeweaver>().Type && (item.ModItem is RuneItem || item.ModItem is ArmorRuneItem)) {
				List<Item> stock = RuneStock.Generate(RuneStockSeed);
				for (int i = 0; i < stock.Count; i++)
					if (!RuneStockSold.Contains(i) && RuneStock.Same(stock[i], item)) {
						RuneStockSold.Add(i);
						break;
					}
			}
			var r = RefinedWeapon.Of(item);
			if (r != null)
				r.Bought = true; // can't be fed into another copy to refine it
		}

		// ------------------------------------------------------------------ stats

		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
		{
			health = StatModifier.Default;
			mana = StatModifier.Default;
			if (!TerralayerWorld.Active)
				return;

			health.Base += 15 * Upgrades[(int)UpgradeType.Vitality] + 10 * Trophies.Count + 15 * RunBoons[(int)BoonStat.Life];
			mana.Base += 20 * Upgrades[(int)UpgradeType.Arcana] + 20 * RunBoons[(int)BoonStat.Mana];

			float lifeMult = 1f + 0.05f * Upgrades[(int)UpgradeType.LegacyOfKings] + ClassDefs.LifeDelta(Class) - BloodDebt;
			float manaMult = 1f;
			foreach (HeirTrait t in Traits) {
				lifeMult += t switch { HeirTrait.GlassBones => -0.35f, HeirTrait.CursedPurse => -0.15f, _ => 0f };
				if (t == HeirTrait.FrailMind)
					manaMult -= 0.5f;
			}
			health *= Math.Max(0.25f, lifeMult);
			mana *= manaMult;
		}

		public override void ResetEffects()
		{
			GoldFind = 1f;
			ArmorGoldBonus = 0f;
		}

		public override void PostUpdateEquips()
		{
			if (!TerralayerWorld.Active)
				return;

			// Permanent Night Owl: the Depths are dark enough without fumbling for torches.
			Player.nightVision = true;

			ClassDefs.ApplyPassive(Player, Class);
			if (AbilityOn)
				ClassDefs.ApplySustained(Player, AbilityClass);
			ApplyAbilityEffects();
			float ignoredLife = 0f, ignoredMana = 0f;
			foreach (HeirTrait t in Traits)
				TraitDefs.Apply(Player, t, ref ignoredLife, ref ignoredMana);

			Player.GetDamage(DamageClass.Generic) += 0.05f * Upgrades[(int)UpgradeType.Might]
				+ 0.05f * Upgrades[(int)UpgradeType.LegacyOfKings] + 0.02f * Trophies.Count;
			Player.statDefense += 2 * Upgrades[(int)UpgradeType.Bulwark];
			Player.moveSpeed += 0.04f * Upgrades[(int)UpgradeType.Fleetfoot];

			// Wings are a luxury down here: about a third of their usual flight time.
			if (Player.wingTimeMax > 0)
				Player.wingTimeMax = Math.Max(20, (int)(Player.wingTimeMax * 0.35f));

			float boonGold = 0f;
			Boons.Apply(Player, RunBoons, ref boonGold);
			GoldFind = 1f + (Heir?.GoldBonus ?? 0f) + 0.08f * Upgrades[(int)UpgradeType.Fortune] + ArmorGoldBonus + boonGold;
		}

		public override void UpdateLifeRegen()
		{
			if (TerralayerWorld.Active && Traits.Contains(HeirTrait.Hemophiliac) && Player.lifeRegen > 0)
				Player.lifeRegen = 0;
		}

		/// <summary>
		/// No natural life regeneration in the Depths' world: life comes back from restoration rewards, potions and
		/// regeneration effects (buffs, accessories, runes, boons).
		/// </summary>
		public override void NaturalLifeRegen(ref float regen)
		{
			if (TerralayerWorld.Active)
				regen = 0f;
		}

		/// <summary>Hemophiliac heirs get less out of every heal.</summary>
		public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
		{
			if (TerralayerWorld.Active && Traits.Contains(HeirTrait.Hemophiliac))
				healValue = (int)(healValue * 0.8f);
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (TerralayerWorld.Active && Traits.Contains(HeirTrait.Brittle))
				modifiers.FinalDamage *= 1.25f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!TerralayerWorld.Active || target.friendly || target.lifeMax <= 5)
				return;
			float rate = 1f + 0.15f * Upgrades[(int)UpgradeType.ChargeMastery];
			if (!AbilityOn)
				Charge = Math.Min(MaxCharge, Charge + Math.Min(3f, damageDone * 0.04f) * rate);
			if (target.life <= 0 && Traits.Contains(HeirTrait.Bloodthirsty))
				Player.Heal(5);
			OnAbilityHit(target, damageDone);
		}

		public bool RuneReady(RuneType type, int cooldownTicks)
		{
			runeCooldowns.TryGetValue(type, out int until);
			if (Main.GameUpdateCount < until)
				return false;
			runeCooldowns[type] = (int)Main.GameUpdateCount + cooldownTicks;
			return true;
		}

		// ------------------------------------------------------------------ per tick

		public override void PostUpdate()
		{
			if (!TerralayerWorld.Active)
				return;
			// Permanent Shine (same glow as the potion).
			Lighting.AddLight(Player.Center, 0.8f, 0.95f, 1f);
			if (Traits.Contains(HeirTrait.NightEyes))
				Lighting.AddLight(Player.Center, 0.8f, 0.8f, 1f);
			UpdateCharge();

			if (Player.whoAmI != Main.myPlayer)
				return;

			if (refill && !Player.dead) {
				refill = false;
				Player.statLife = Player.statLifeMax2;
				Player.statMana = Player.statManaMax2;
			}

			// Run endings happen here, outside the item-use update, so nothing equipped lingers.
			if (pendingEnd.HasValue) {
				RunEnd how = pendingEnd.Value;
				pendingEnd = null;
				FinishRun(how);
			}

			UpdateAbilities();
			if (Main.GameUpdateCount % 15 == 0)
				MergeInventory();
			if (Main.GameUpdateCount % 60 == 0)
				RearmIfEmptyHanded();
			if (PendingRoomRewards > 0 && !RewardOffered && !Player.dead)
				RollRoomReward();
			TrackRun();
		}

		/// <summary>
		/// At home, alive, with an heir but no weapon at all (a lost save field, a wipe, anything): re-issue the kit.
		/// </summary>
		private void RearmIfEmptyHanded()
		{
			if (InRun || Player.dead || Heir == null || PendingHeirChoice || PendingWipeConfirm || pendingEnd.HasValue)
				return;
			if (Player.inventory.Any(i => RefinedWeapon.Of(i) != null) || RefinedWeapon.Of(Main.mouseItem) != null)
				return;
			Item weapon = Heir.CreateWeapon();
			Give(weapon);
			GiveAmmoFor(weapon);
			Main.NewText($"{HeirTitle} picks up a spare {weapon.Name}.", ClassInfo.Color);
		}

		/// <summary>Sustained abilities drain charge. There's no passive refill: charge comes from hitting things and from restoration.</summary>
		private void UpdateCharge()
		{
			if (AbilityOn) {
				Charge -= AbilityInfo.Cost / 60f;
				if (Charge <= 0f || Player.dead) {
					Charge = Math.Max(0f, Charge);
					AbilityOn = false;
				}
				if (Main.rand.NextBool(3)) {
					Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.GoldFlame);
					d.noGravity = true;
					d.velocity *= 0.4f;
				}
			}
			if (abilityTapCooldown > 0)
				abilityTapCooldown--;
		}

		/// <summary>
		/// Folds found duplicates into the weapon you already carry (+1 refinement), then maybe offers an evolution.
		/// Also fuses triples of identical runes. Local player only.
		/// </summary>
		private void MergeInventory()
		{
			var seen = new Dictionary<int, int>();
			for (int i = 0; i < 58; i++) {
				Item item = Player.inventory[i];
				if (RefinedWeapon.Of(item) == null)
					continue;
				if (!seen.TryGetValue(item.type, out int first)) {
					seen[item.type] = i;
					continue;
				}
				if (i == Player.selectedItem && Player.itemAnimation > 0)
					continue;

				int target = first, material = i;
				if (!RefinedWeapon.CanMerge(Player.inventory[target], Player.inventory[material])) {
					(target, material) = (material, target);
					if (!RefinedWeapon.CanMerge(Player.inventory[target], Player.inventory[material]))
						continue; // e.g. a shop-bought copy, or already at max refinement
				}

				RefinedWeapon.Merge(Player.inventory[target], Player.inventory[material]);
				Player.inventory[material].TurnToAir();
				seen[item.type] = target;
				var merged = RefinedWeapon.Of(Player.inventory[target]);
				string progress = merged.Level >= RefinedWeapon.MaxLevel ? "max" : $"{merged.Fusion}/{merged.Level}";
				CombatText.NewText(Player.Hitbox, RarityDefs.Color(merged.Rarity), $"Fused! {Player.inventory[target].Name} +{merged.Level - 1} ({progress})", true);
				SoundEngine.PlaySound(SoundID.Item37, Player.Center);

				// A coin-flip chance the merge can push the weapon to the next stage of its line - your choice.
				int stage = Loot.StageOf(Player.inventory[target].type);
				if (PendingEvolutionSlot < 0 && merged.EvolutionReady(Player.inventory[target]) && Main.rand.NextFloat() < RefinedWeapon.EvolveChance(stage)) {
					PendingEvolutionSlot = target;
					PendingEvolutionType = Player.inventory[target].type;
					SoundEngine.PlaySound(SoundID.Item29, Player.Center);
				}
			}

			FuseRunes();
			MergeAccessories();
		}

		/// <summary>Two copies of an accessory (carried or worn) merge into one a forge stage higher, kept where the worn one was.</summary>
		private void MergeAccessories()
		{
			var slots = new List<(Item[] arr, int index)>();
			for (int i = 3; i < 10 && i < Player.armor.Length; i++)
				slots.Add((Player.armor, i));
			for (int i = 0; i < 50; i++)
				slots.Add((Player.inventory, i));
			var first = new Dictionary<int, (Item[] arr, int index)>();
			foreach (var (arr, index) in slots) {
				Item item = arr[index];
				if (!AccessoryForge.CanMerge(item) || item.favorited && arr == Player.inventory)
					continue;
				if (!first.TryGetValue(item.type, out var keep)) {
					first[item.type] = (arr, index);
					continue;
				}
				Item merged = AccessoryForge.Merge(keep.arr[keep.index], item);
				keep.arr[keep.index] = merged;
				item.TurnToAir();
				var f = AccessoryForge.Of(merged);
				CombatText.NewText(Player.Hitbox, AccessoryForge.ColorFor(f.Stage), $"Forged! {merged.Name} (stage {f.Stage})", true);
				SoundEngine.PlaySound(SoundID.Item37, Player.Center);
				return; // one merge per pass
			}
		}

		private void FuseRunes()
		{
			var weapon = new Dictionary<(RuneType, Rarity), List<int>>();
			var armor = new Dictionary<(ArmorRuneType, Rarity), List<int>>();
			for (int i = 0; i < 58; i++) {
				if (Player.inventory[i].ModItem is RuneItem r && r.Rarity < Rarity.Legendary) {
					if (!weapon.TryGetValue((r.Kind, r.Rarity), out var list))
						weapon[(r.Kind, r.Rarity)] = list = new List<int>();
					list.Add(i);
				}
				else if (Player.inventory[i].ModItem is ArmorRuneItem a && a.Rarity < Rarity.Legendary) {
					if (!armor.TryGetValue((a.Kind, a.Rarity), out var list))
						armor[(a.Kind, a.Rarity)] = list = new List<int>();
					list.Add(i);
				}
			}
			// The three runes with the best odds fuse; the result keeps the best of them, plus a little.
			foreach (var pair in weapon.Where(p => p.Value.Count >= 3)) {
				var used = pair.Value.OrderByDescending(i => (Player.inventory[i].ModItem as RuneItem).Chance).Take(3).ToList();
				int best = (Player.inventory[used[0]].ModItem as RuneItem).Chance;
				Rarity next = pair.Key.Item2 + 1;
				foreach (int i in used)
					Player.inventory[i].TurnToAir();
				Player.inventory[used[0]] = RuneItem.Create(pair.Key.Item1, next, chance: RuneOdds.Fused(next, best));
				CombatText.NewText(Player.Hitbox, RarityDefs.Color(next), $"Runes fused! ({RuneOdds.Fused(next, best)}% odds)", true);
			}
			foreach (var pair in armor.Where(p => p.Value.Count >= 3)) {
				var used = pair.Value.OrderByDescending(i => (Player.inventory[i].ModItem as ArmorRuneItem).Chance).Take(3).ToList();
				int best = (Player.inventory[used[0]].ModItem as ArmorRuneItem).Chance;
				Rarity next = pair.Key.Item2 + 1;
				foreach (int i in used)
					Player.inventory[i].TurnToAir();
				Player.inventory[used[0]] = ArmorRuneItem.Create(pair.Key.Item1, next, chance: RuneOdds.Fused(next, best));
				CombatText.NewText(Player.Hitbox, RarityDefs.Color(next), $"Runes fused! ({RuneOdds.Fused(next, best)}% odds)", true);
			}
		}

		public void AcceptEvolution()
		{
			int slot = PendingEvolutionSlot;
			PendingEvolutionSlot = -1;
			if (slot < 0 || Player.inventory[slot].type != PendingEvolutionType)
				return;
			Item evolved = RefinedWeapon.Evolve(Player.inventory[slot]);
			if (evolved == null)
				return;
			string from = Player.inventory[slot].Name;
			Player.inventory[slot] = evolved;
			SoundEngine.PlaySound(SoundID.Item119, Player.Center);
			Main.NewText($"{from} evolves into {evolved.Name}!", RarityDefs.Color(RefinedWeapon.Of(evolved).Rarity));
			Player.GetModPlayer<Quests.QuestPlayer>().OnEvolved();
		}

		public void DeclineEvolution() => PendingEvolutionSlot = -1;

		// ------------------------------------------------------------------ class ability

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (!TerralayerWorld.Active)
				return;
			if (KeybindSystem.Ability.JustPressed)
				ToggleAbility();
			if (KeybindSystem.Estate.JustPressed)
				ModContent.GetInstance<UISystem>().ToggleEstate();
			if (KeybindSystem.Map.JustPressed)
				ModContent.GetInstance<UISystem>().ToggleMap();
		}

		public void Teleport(Vector2 pos, int style)
		{
			Player.RemoveAllGrapplingHooks();
			Player.Teleport(pos, style);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, Player.whoAmI, pos.X, pos.Y, style);
		}

		// ------------------------------------------------------------------ death and second wind

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
		{
			if (!TerralayerWorld.Active)
				return true;

			if (InRun && Upgrades[(int)UpgradeType.SecondWind] > 0 && !SecondWindUsed) {
				SecondWindUsed = true;
				Player.statLife = Math.Max(1, Player.statLifeMax2 * 3 / 10);
				Player.immune = true;
				Player.immuneTime = 120;
				CombatText.NewText(Player.Hitbox, new Color(255, 120, 120), "Second Wind!", true);
				SoundEngine.PlaySound(SoundID.Item119, Player.Center);
				return false;
			}

			if (Player.whoAmI == Main.myPlayer) {
				stashedCoins = Coins.InPurse(Player);
				for (int i = 0; i < Player.inventory.Length; i++)
					if (Coins.ValueOf(Player.inventory[i]) > 0)
						Player.inventory[i].TurnToAir();
			}
			return true;
		}

		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			if (!TerralayerWorld.Active || Player.whoAmI != Main.myPlayer)
				return;

			// The new-heir screen comes first, so nothing below can ever skip it.
			bool wasInRun = InRun;
			InRun = false;
			AbilityOn = false;
			ResetAbilityState();
			PendingEvolutionSlot = -1;
			PendingHeirChoice = true;
			ClearRunRewards();

			try {
				string fate = damageSource.GetDeathText(HeirTitle).ToString();
				LastDeath = new DeathRecord {
					Name = HeirTitle,
					ClassName = Heir?.ClassLabel ?? ClassName,
					Class = Class,
					Generation = Generation,
					Fate = fate,
					Floor = wasInRun ? RunFloor : 0,
					FloorName = wasInRun ? FloorDefs.Get(RunFloor).Name : "",
					RoomsCleared = RunRoomsCleared,
					Guardians = RunGuardians,
					LossPercent = TerralayerConfig.Instance.DeathCoinLossPercent,
					Time = Main.GameUpdateCount,
				};
				Player.GetModPlayer<Quests.QuestPlayer>().OnDeath();
				if (wasInRun)
					fate += $" (floor {RunFloor}: {FloorDefs.Get(RunFloor).Name})";
				if (Heir != null) {
					Ancestors.Insert(0, new AncestorRecord { Name = HeirTitle, Class = Class, Generation = Generation, Fate = fate });
					if (Ancestors.Count > 15)
						Ancestors.RemoveRange(15, Ancestors.Count - 15);
				}
				Wipe(keepCoins: false);
			}
			catch (Exception e) {
				Mod.Logger.Error("Error while handling an heir's death", e);
			}
			finally {
				// Death costs a share of the coins you were carrying; the rest comes home with your heir.
				long lost = stashedCoins * TerralayerConfig.Instance.DeathCoinLossPercent / 100;
				Coins.PutInInventory(Player, stashedCoins - lost);
				if (LastDeath != null) {
					LastDeath.CoinsLost = lost;
					LastDeath.CoinsKept = stashedCoins - lost;
				}
				if (lost > 0)
					Main.NewText($"In the confusion of death, {Coins.Format(lost)} slips away ({TerralayerConfig.Instance.DeathCoinLossPercent}%).", new Color(220, 150, 120));
				stashedCoins = 0;
				BloodDebt = 0f;
				RunRoomsCleared = 0;
				RunGuardians = 0;
				RefreshRuneStock(quiet: true);
				HeirOptions = RollHeirs();
			}
		}

		/// <summary>The death screen is the heir choice: stay down until someone takes up the name.</summary>
		public override void UpdateDead()
		{
			if (TerralayerWorld.Active && Player.whoAmI == Main.myPlayer && PendingHeirChoice && !PendingWipeConfirm)
				Player.respawnTimer = Math.Max(Player.respawnTimer, 2);
		}

		public List<HeirData> RollHeirs()
		{
			int startLevel = 1 + Upgrades[(int)UpgradeType.ForgeMastery];
			float affinity = 0.25f * Upgrades[(int)UpgradeType.RuneAffinity];
			return HeirGenerator.Roll(UnlockedKits, 3, startLevel, affinity);
		}

		public void EnsureHeirOptions()
		{
			if (HeirOptions.Count == 0)
				HeirOptions = RollHeirs();
		}

		public void ChooseHeir(HeirData heir)
		{
			Heir = heir;
			AbilityOverride = null;
			Generation++;
			PendingHeirChoice = false;
			HeirOptions.Clear();
			Charge = MaxCharge;
			AbilityOn = false;
			refill = true;
			Main.NewText($"Generation {Generation}: {HeirTitle} the {Heir.ClassLabel} takes up the family name.", ClassInfo.Color);
			GiveKit();
			if (Player.dead)
				Player.respawnTimer = 0; // rise at once
		}

		// ------------------------------------------------------------------ kit

		/// <summary>The class's armor (with its runes), accessories, weapon, a hook, ammo and potions.</summary>
		private void GiveKit()
		{
			if (Heir == null)
				return;
			Item weapon = Heir.CreateWeapon();
			Give(weapon);
			GiveAmmoFor(weapon);
			EquipHook();

			for (int s = 0; s < 3; s++) {
				Item piece = Heir.CreateArmor(s);
				if (piece == null)
					continue;
				if (Player.armor[s].IsAir)
					Player.armor[s] = piece;
				else
					Give(piece);
			}
			foreach (int acc in Heir.Kit.Accessories) {
				var item = new Item(acc);
				if (Class == HeirClass.Fishron && AccessoryForge.Of(item) is AccessoryForge f)
					f.Innate = true; // built-in Fishron Wings
				EquipAccessory(item);
			}
			EnsureInnateAccessories();

			Give(Loot.Healing(1, 2 + 2 * Upgrades[(int)UpgradeType.FieldKit]));
			if (Class == HeirClass.Ranger) {
				GiveIfMissing(ItemID.EndlessQuiver);
				GiveIfMissing(ItemID.EndlessMusketPouch);
			}
			if (Class == HeirClass.Arcanist)
				Give(new Item(ItemID.LesserManaPotion, 5));
		}

		/// <summary>Gives the heir any innate trait accessory they're missing, equipped if there's a free slot.</summary>
		public void EnsureInnateAccessories()
		{
			foreach (HeirTrait t in Traits) {
				int type = TraitDefs.InnateItem(t);
				if (type == 0 || Player.inventory.Any(i => i.type == type) || Player.armor.Any(i => i.type == type))
					continue;
				var item = new Item(type);
				var f = AccessoryForge.Of(item);
				if (f != null)
					f.Innate = true;
				EquipAccessory(item);
			}
		}

		/// <summary>Field Kit: every descent starts with at least 2 (+2 per level) lesser healing potions.</summary>
		private void TopUpPotions()
		{
			int want = 2 + 2 * Upgrades[(int)UpgradeType.FieldKit];
			int have = Player.inventory.Where(i => i.type == ItemID.LesserHealingPotion).Sum(i => i.stack);
			if (have < want)
				Give(new Item(ItemID.LesserHealingPotion, want - have));
		}

		private void EquipAccessory(Item item)
		{
			for (int i = 3; i < 8; i++)
				if (Player.armor[i].type == item.type)
					return;
			for (int i = 3; i < 8; i++) {
				if (Player.armor[i].IsAir) {
					Player.armor[i] = item;
					return;
				}
			}
			Give(item);
		}

		private void GiveAmmoFor(Item weapon)
		{
			int ammo = Loot.EndlessAmmoFor(weapon);
			if (ammo > 0)
				GiveIfMissing(ammo);
		}

		private void GiveIfMissing(int type)
		{
			if (!Player.inventory.Any(i => i.type == type))
				Give(new Item(type));
		}

		private void EquipHook()
		{
			if (!Player.miscEquips[4].IsAir || Player.inventory.Any(i => i.shoot > 0 && Main.projHook[i.shoot]))
				return;
			Player.miscEquips[4] = new Item(ItemID.GrapplingHook);
		}

		/// <summary>Quest reward: the heir's own weapon gains refinement levels (kept while the heir lives).</summary>
		public bool TemperHeirWeapon(int levels)
		{
			if (Heir == null || Heir.WeaponLevel >= RefinedWeapon.MaxLevel)
				return false;
			Heir.WeaponLevel = Math.Min(RefinedWeapon.MaxLevel, Heir.WeaponLevel + levels);
			foreach (Item item in Player.inventory) {
				var r = RefinedWeapon.Of(item);
				if (r != null && item.type == Heir.WeaponType && r.Level < Heir.WeaponLevel) {
					r.Level = Heir.WeaponLevel;
					r.Fusion = 0;
					r.Peak = Math.Max(r.Peak, r.Level);
				}
			}
			return true;
		}

		private void Give(Item item)
		{
			if (item == null || item.IsAir)
				return;
			for (int i = 0; i < 50; i++) {
				if (Player.inventory[i].IsAir) {
					Player.inventory[i] = item.Clone();
					return;
				}
			}
			Player.QuickSpawnItem(Player.GetSource_Misc("TerralayerKit"), item, item.stack);
		}

		// ------------------------------------------------------------------ the descent

		/// <summary>Every descent starts on floor 1 (the test command can still pick another).</summary>
		public void RequestEnterRun(int floor = 1)
		{
			if (PendingHeirChoice || Heir == null) {
				Main.NewText("Choose your heir first.", Color.OrangeRed);
				return;
			}
			if (InRun)
				return;

			long taken = Coins.TakePurseShare(Player, TollPercent);
			long fromSafe = Coins.TakeBankShare(Player, SafeTollPercent);
			if (taken > 0 || fromSafe > 0) {
				string parts = (taken > 0 ? $"{Coins.Format(taken)} from your purse ({TollPercent}%)" : "")
					+ (taken > 0 && fromSafe > 0 ? " and " : "")
					+ (fromSafe > 0 ? $"{Coins.Format(fromSafe)} from your safe ({SafeTollPercent}%)" : "");
				Main.NewText($"The Gatekeeper takes {parts} as toll.", new Color(220, 190, 120));
			}

			float goldMult = GoldFind;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerralayerNet.RequestRun(floor, Class, goldMult, EnemyLifeScaling);
			else
				ArriveInDepths(DepthsSystem.Instance.StartRun(floor, Player, Class, goldMult, EnemyLifeScaling), DepthsSystem.Instance.Floor);
		}

		public void ArriveInDepths(Vector2 position, int floor)
		{
			Teleport(position, TeleportationStyleID.TeleporterTile);
			Player.immune = true;
			Player.immuneTime = 90;
			bool newRun = !InRun;
			InRun = true;
			RunFloor = floor;
			riftCooldown = 45;
			if (newRun) {
				SecondWindUsed = false;
				BloodDebt = 0f;
				ClearRunRewards();
				RunRoomsCleared = 0;
				RunGuardians = 0;
				// Nothing regenerates down there, so every descent starts fresh.
				refill = true;
				Charge = MaxCharge;
				TopUpPotions();
				if (Upgrades[(int)UpgradeType.AncestralBlessing] > 0)
					PendingBlessings++;
				if (PendingBlessings > 0) {
					var rng = new Random(Main.rand.Next());
					var names = new List<string>();
					for (int i = 0; i < PendingBlessings; i++) {
						BoonStat b = Boons.Random(rng);
						RunBoons[(int)b]++;
						names.Add($"{Boons.Get(b).Name} ({Boons.Get(b).PerStack})");
					}
					PendingBlessings = 0;
					Main.NewText("Blessings go with you: " + string.Join(", ", names), new Color(255, 215, 140));
				}
			}
			ChartActive = false;
			Player.GetModPlayer<Quests.QuestPlayer>().OnArrive(floor, newRun);
			if (!newRun && Class == HeirClass.Riftwalker)
				RiftSurge();

			var info = FloorDefs.Get(floor);
			Main.NewText($"Floor {floor}: {info.Name}. {info.Flavor}", new Color(120, 220, 255));
			Main.NewText("New rooms stay sealed until the room you're in is cleared, but you can always fall back to rooms you've explored. Find the guardian's arena. Right-click rifts to travel.", new Color(170, 170, 190));

			if (RevealNextFloor) {
				RevealNextFloor = false;
				ChartActive = true;
				Main.NewText("The Cartographer's chart reveals this floor.", new Color(200, 230, 160));
			}
		}

		/// <summary>Ends the descent (on the next update). The heir comes home with everything they carry.</summary>
		public void EndRun(RunEnd how)
		{
			if (InRun)
				pendingEnd = how;
		}

		private void FinishRun(RunEnd how)
		{
			if (!InRun)
				return;
			long purse = Coins.InPurse(Player);
			long bonus = how switch {
				RunEnd.Victory => purse * 15 / 100,
				RunEnd.Conquered => purse / 2,
				_ => 0
			};
			Player.GetModPlayer<Quests.QuestPlayer>().OnRunEnd(how, purse + bonus);
			InRun = false;
			AbilityOn = false;
			ResetAbilityState();
			BloodDebt = 0f;
			PendingEvolutionSlot = -1;
			ClearRunRewards();
			Player.RemoveAllGrapplingHooks();
			Player.Spawn(PlayerSpawnContext.RecallFromItem);
			// Coming home alive keeps everything you carry: gear, gold, runes. Only death strips an heir.
			EquipHook();
			refill = true;
			Charge = MaxCharge;
			RefreshRuneStock();
			if (bonus > 0)
				Coins.PutInInventory(Player, bonus);

			string msg = how switch {
				RunEnd.Conquered => $"THE DEPTHS ARE CONQUERED! {HeirTitle} returns a legend. (+{Coins.FormatPlain(bonus)} glory bonus)",
				RunEnd.Victory => $"{HeirTitle} returns home victorious. (+{Coins.FormatPlain(bonus)} homecoming bonus)",
				_ => $"{HeirTitle} retreats to the homestead with everything they carry."
			};
			Main.NewText(msg, new Color(255, 215, 80));
		}

		private void TrackRun()
		{
			var depths = DepthsSystem.Instance;
			if (riftCooldown > 0)
				riftCooldown--;
			if (retreatConfirm > 0)
				retreatConfirm--;

			if (InRun && !Player.dead && TerralayerWorld.Instance.InLobby(Player.Center.ToTileCoordinates()) && !pendingEnd.HasValue) {
				EndRun(RunEnd.Retreat);
				return;
			}

			if (depths.Plan == null)
				return;
			if (visitedSeed != depths.Plan.Seed * 8 + depths.Floor) {
				visitedSeed = depths.Plan.Seed * 8 + depths.Floor;
				VisitedRooms.Clear();
				UsedShrines.Clear();
			}
			RoomPlan room = depths.RoomOf(Player);
			if (room != null && !Player.dead) {
				VisitedRooms.Add(room.Id);
				if (!InRun && !pendingEnd.HasValue) {
					InRun = true;
					RunFloor = depths.Floor;
				}
			}
		}

		// ------------------------------------------------------------------ right-click interactions

		public static Rectangle AltarArea(RoomPlan room) => new((room.Feature.X - 3) * 16, (room.Feature.Y - 5) * 16, 7 * 16, 5 * 16);

		/// <summary>Rifts, shrines, forges and portals: hover with the mouse and right-click, like a chest.</summary>
		public override bool PreItemCheck()
		{
			if (!TerralayerWorld.Active || Player.whoAmI != Main.myPlayer)
				return true;

			bool rightHeld = Main.mouseRight;
			bool rightPressed = rightHeld && !rightWasHeld;
			rightWasHeld = rightHeld;

			HoverRift = null;
			HoverShrine = HoverForge = HoverMoonPortal = HoverMoonReturn = HoverSea = HoverSeaReturn = false;
			InteractInRange = false;
			if (Player.dead || Main.mapFullscreen || Main.drawingPlayerChat || Player.talkNPC >= 0 || Main.playerInventory && Player.mouseInterface)
				return true;
			if (ModContent.GetInstance<UISystem>()?.MapOpen == true)
				return true;

			var world = TerralayerWorld.Instance;
			Point mouse = Main.MouseWorld.ToPoint();
			var secrets = Player.GetModPlayer<Secrets.SecretPlayer>();

			bool Hover(Rectangle area, int icon)
			{
				if (!area.Contains(mouse))
					return false;
				InteractInRange = Vector2.Distance(Player.Center, area.Center.ToVector2()) <= InteractRange;
				if (InteractInRange) {
					Player.cursorItemIconEnabled = true;
					Player.cursorItemIconID = icon;
				}
				return true;
			}

			// The hidden sea: a rift on the plaza (once you've met the Angler) and one home from the beach.
			if (secrets.MetAngler && !InRun && Hover(Homestead.PortalArea(Homestead.SeaPortalPos(world)), ItemID.GoldenBugNet)) {
				HoverSea = true;
				if (rightPressed && InteractInRange) {
					if (Main.netMode != NetmodeID.MultiplayerClient)
						Secrets.SecretEvents.Instance.OpenSea();
					Teleport(Secrets.OceanArena.Arrival(Player), TeleportationStyleID.TeleporterTile);
					return false;
				}
				return true;
			}
			if (world.SeaBuilt && Hover(Homestead.PortalArea(Secrets.OceanArena.ReturnPortal), ItemID.MagicMirror)) {
				HoverSeaReturn = true;
				if (rightPressed && InteractInRange) {
					if (InRun)
						EndRun(DepthsSystem.Instance.Floor >= FloorDefs.Count ? RunEnd.Conquered : RunEnd.Victory);
					else {
						Player.RemoveAllGrapplingHooks();
						Player.Spawn(PlayerSpawnContext.RecallFromItem);
					}
					return false;
				}
				return true;
			}

			if (!InRun) {
				if (Hover(world.RuneforgeArea, ModContent.ItemType<EmberRune>())) {
					HoverForge = true;
					if (rightPressed && InteractInRange) {
						ModContent.GetInstance<UISystem>().OpenRuneforge(field: false);
						return false;
					}
					return true;
				}
				if (world.AllGuardiansSlain && Hover(Homestead.PortalArea(Homestead.MoonPortalPos(world)), ItemID.CelestialSigil)) {
					HoverMoonPortal = true;
					if (rightPressed && InteractInRange) {
						if (Main.netMode == NetmodeID.MultiplayerClient)
							TerralayerNet.RequestMoonFight();
						else
							Teleport(world.StartMoonFight(Player), TeleportationStyleID.TeleporterTile);
						return false;
					}
					return true;
				}
				if (world.Moon != TerralayerWorld.MoonState.Idle && Hover(Homestead.PortalArea(Homestead.MoonReturnPos(world)), ItemID.MagicMirror)) {
					HoverMoonReturn = true;
					if (rightPressed && InteractInRange) {
						Player.RemoveAllGrapplingHooks();
						Player.Spawn(PlayerSpawnContext.RecallFromItem);
						return false;
					}
					return true;
				}
			}

			var depths = DepthsSystem.Instance;
			if (depths.Plan == null)
				return true;
			RoomPlan room = depths.RoomOf(Player);
			if (room == null)
				return true;

			foreach (RiftPlan rift in depths.Plan.Rifts) {
				if (rift.Room != room.Id)
					continue;
				bool shown = rift.Kind is RiftKind.Link or RiftKind.Retreat || depths.RiftActive(rift);
				Rectangle area = rift.Hitbox;
				area.Inflate(8, 8);
				if (shown && Hover(area, ItemID.MagicMirror)) {
					HoverRift = rift;
					break;
				}
			}
			if (HoverRift == null && room.Kind == RoomKind.Shrine && !UsedShrines.Contains(room.Id) && Hover(AltarArea(room), ItemID.LifeCrystal))
				HoverShrine = true;
			if (HoverRift == null && room.Kind == RoomKind.Forge && Hover(AltarArea(room), ModContent.ItemType<EmberRune>()))
				HoverForge = true;

			if (!rightPressed || !InteractInRange || riftCooldown > 0)
				return true;
			if (HoverShrine)
				UseShrine(room);
			else if (HoverForge)
				ModContent.GetInstance<UISystem>().OpenRuneforge(field: true);
			else if (HoverRift != null)
				UseRift(HoverRift);
			else
				return true;
			return false;
		}

		private void UseRift(RiftPlan rift)
		{
			var depths = DepthsSystem.Instance;
			riftCooldown = 30;
			if (!depths.RiftActive(rift)) {
				string why = rift.Kind != RiftKind.Link ? "Defeat the guardian first"
					: rift.Room == depths.Plan.BossRoom ? "No escape mid-fight!"
					: "Clear this room to go somewhere new (explored rooms stay open)";
				CombatText.NewText(Player.Hitbox, Color.Gray, why);
				return;
			}

			switch (rift.Kind) {
				case RiftKind.Link: {
					RiftPlan target = depths.Plan.Rifts[rift.Target];
					Teleport(FloorPlan.StandPosition(target.Pos, Player), TeleportationStyleID.TeleporterTile);
					Player.immune = true;
					Player.immuneTime = Math.Max(Player.immuneTime, 40);
					if (Class == HeirClass.Riftwalker)
						RiftSurge();
					break;
				}
				case RiftKind.Retreat:
					if (retreatConfirm <= 0) {
						retreatConfirm = 180;
						CombatText.NewText(Player.Hitbox, Color.Orange, "Right-click again to abandon the descent", true);
						return;
					}
					EndRun(RunEnd.Retreat);
					break;
				case RiftKind.ReturnHome:
					EndRun(depths.Floor >= FloorDefs.Count ? RunEnd.Conquered : RunEnd.Victory);
					break;
				case RiftKind.Descend:
					if (depths.Floor >= FloorDefs.Count) {
						EndRun(RunEnd.Conquered);
						return;
					}
					if (Main.netMode == NetmodeID.MultiplayerClient)
						TerralayerNet.RequestDescend();
					else
						depths.Descend();
					break;
			}
		}

		private void UseShrine(RoomPlan room)
		{
			riftCooldown = 30;
			var depths = DepthsSystem.Instance;
			var rng = new Random(Main.rand.Next());
			ShrineType type = Shrines.TypeOf(room);
			Color color = Shrines.Get(type).Color;

			switch (type) {
				case ShrineType.Blood:
					BloodDebt += 0.15f;
					Give(Loot.RollRune(2, rng));
					SoundEngine.PlaySound(SoundID.NPCDeath13, Player.Center);
					Main.NewText("The Blood Altar drinks deep and grants a powerful rune.", color);
					break;

				case ShrineType.Fortune: {
					long price = Shrines.FortunePrice(depths.Floor);
					if (!Player.BuyItem(price)) {
						Main.NewText($"The Fortune Altar wants {Coins.Format(price)}.", color);
						return;
					}
					Give(Loot.RollMain(depths.Floor + 1, Class, 1, rng, 0.3, 0.5));
					SoundEngine.PlaySound(SoundID.Coins, Player.Center);
					Main.NewText("The Fortune Altar accepts your offering.", color);
					break;
				}

				case ShrineType.Change: {
					var options = Enumerable.Range(0, (int)HeirClass.Count).Select(i => (HeirClass)i).Where(c => c != AbilityClass).ToList();
					AbilityOverride = options[rng.Next(options.Count)];
					AbilityOn = false;
					Charge = MaxCharge;
					SoundEngine.PlaySound(SoundID.Item6, Player.Center);
					Main.NewText($"The Altar of Change reshapes you: your ability is now {AbilityInfo.AbilityName}.", AbilityInfo.Color);
					break;
				}

				case ShrineType.Wayward: {
					var candidates = depths.Plan.Rooms.Where(r => r.Id != room.Id && r.Kind != RoomKind.Boss).ToList();
					RoomPlan dest = candidates[rng.Next(candidates.Count)];
					RiftPlan landing = depths.Plan.Rifts.FirstOrDefault(r => r.Room == dest.Id);
					Point at = landing?.Pos ?? dest.Feature;
					Teleport(FloorPlan.StandPosition(at, Player), TeleportationStyleID.RodOfDiscord);
					Main.NewText("The Wayward Shrine flings you elsewhere...", color);
					break;
				}

				case ShrineType.Trial:
					if (Main.netMode == NetmodeID.MultiplayerClient)
						TerralayerNet.RequestTrial();
					else
						depths.SpawnTrialChampion(Player);
					SoundEngine.PlaySound(SoundID.Roar, Player.Center);
					Main.NewText("The Shrine of Trials summons a champion. Prove yourself.", color);
					break;

				case ShrineType.Spring:
					Player.Heal(Player.statLifeMax2);
					Player.statMana = Player.statManaMax2;
					Player.AddBuff(BuffID.Regeneration, 60 * 180);
					Player.AddBuff(BuffID.Ironskin, 60 * 180);
					Player.AddBuff(BuffID.Endurance, 60 * 120);
					SoundEngine.PlaySound(SoundID.Item4, Player.Center);
					Main.NewText("The Healing Spring restores you.", color);
					break;

				case ShrineType.Gamble: {
					long stake = Math.Max(Coins.Gold, Coins.InPurse(Player) / 5);
					if (!Player.BuyItem(stake)) {
						Main.NewText($"The Gambler's Shrine wants a stake of at least {Coins.Format(stake)}.", color);
						return;
					}
					if (rng.NextDouble() < 0.5) {
						Coins.PutInInventory(Player, stake * 5 / 2);
						SoundEngine.PlaySound(SoundID.Coins, Player.Center);
						Main.NewText($"Fortune smiles! You win {Coins.Format(stake * 5 / 2)}.", color);
					}
					else {
						SoundEngine.PlaySound(SoundID.NPCDeath6, Player.Center);
						Main.NewText($"The shrine swallows your {Coins.Format(stake)}.", color);
					}
					break;
				}

				case ShrineType.Refinement: {
					var held = RefinedWeapon.Of(Player.HeldItem);
					if (held == null || held.Level >= RefinedWeapon.MaxLevel) {
						Main.NewText("Hold a weapon (below max refinement) to offer it to the shrine.", color);
						return;
					}
					BloodDebt += 0.10f;
					held.AddFusions(held.Level - held.Fusion); // a full level along the curve
					SoundEngine.PlaySound(SoundID.Item37, Player.Center);
					Main.NewText($"{Player.HeldItem.Name} is refined to +{held.Level - 1}.", color);
					break;
				}
			}
			UsedShrines.Add(room.Id);
			Player.GetModPlayer<Quests.QuestPlayer>().OnShrineUsed();
		}

		// ------------------------------------------------------------------ room-clear rewards

		/// <summary>Called when a room this player is in gets cleared.</summary>
		public void OnRoomCleared()
		{
			if (!InRun)
				return;
			PendingRoomRewards++;
			RunRoomsCleared++;
		}

		private void ClearRunRewards()
		{
			Array.Clear(RunBoons);
			PendingRoomRewards = 0;
			RewardOffered = false;
		}

		public void OnGuardianSlain(int floor)
		{
			RunGuardians++;
			Player.GetModPlayer<Quests.QuestPlayer>().OnGuardianSlain(floor);
			if (Trophies.Add(floor))
				Main.NewText($"Trophy earned: Guardian of the {FloorDefs.Get(floor).Name}! (+2% damage, +10 max life, permanently)", new Color(255, 215, 80));
		}
	}
}
