using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terralayer.Common.World;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Items
{
	public struct RuneSocket
	{
		public RuneType Type;
		public Rarity Rarity;

		public RuneSocket(RuneType type, Rarity rarity)
		{
			Type = type;
			Rarity = rarity;
		}
	}

	/// <summary>
	/// Every weapon carries a refinement level, a rarity and rune sockets. Two copies of the same weapon merge into one
	/// with their levels added together. Only takes effect in Terralayer worlds.
	/// </summary>
	public class RefinedWeapon : GlobalItem
	{
		public const int MaxLevel = 10;

		public int Level = 1;
		/// <summary>The highest refinement this weapon has ever reached (its mastery).</summary>
		public int Peak = 1;
		/// <summary>Fusions banked toward the next level. Level N needs N fusions to reach N+1.</summary>
		public int Fusion;
		public Rarity Rarity = Rarity.Common;
		public List<RuneSocket> Runes = new();
		/// <summary>Bought from a shop: can't be fed into another weapon as merge material.</summary>
		public bool Bought;

		public override bool InstancePerEntity => true;

		public static bool IsWeapon(Item item) =>
			item.damage > 0 && item.maxStack == 1 && !item.accessory && item.useStyle != ItemUseStyleID.None &&
			item.ammo == AmmoID.None && !item.consumable && item.pick == 0 && item.axe == 0 && item.hammer == 0 &&
			item.createTile < 0 && item.headSlot < 0 && item.bodySlot < 0 && item.legSlot < 0;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => lateInstantiation && IsWeapon(entity);

		public override GlobalItem Clone(Item from, Item to)
		{
			var clone = (RefinedWeapon)base.Clone(from, to);
			clone.Runes = new List<RuneSocket>(Runes);
			return clone;
		}

		public int RuneSlots => RarityDefs.RuneSlots(Rarity);

		public float DamageMultiplier => (1f + RefineBonus(Level)) * (1f + RarityDefs.DamageBonus(Rarity));

		/// <summary>Diminishing returns: +6% per level to Lv4, +4% to Lv7, +2% to Lv10 (max +36%).</summary>
		public static float RefineBonus(int level)
		{
			float b = 0f;
			for (int l = 2; l <= level; l++)
				b += l <= 4 ? 0.06f : l <= 7 ? 0.04f : 0.02f;
			return b;
		}

		/// <summary>Everything ever fused into this weapon, counting the levels themselves.</summary>
		public int TotalFusions => Level * (Level - 1) / 2 + Fusion;

		/// <summary>Adds fusions and levels up along the curve (Lv N -> N+1 costs N fusions).</summary>
		public void AddFusions(int n)
		{
			int total = TotalFusions + Math.Max(0, n);
			int level = 1;
			while (level < MaxLevel && (level + 1) * level / 2 <= total)
				level++;
			Level = level;
			Fusion = level >= MaxLevel ? 0 : total - level * (level - 1) / 2;
			Peak = Math.Max(Peak, Level);
		}

		/// <summary>Evolving needs the weapon refined to +3 (Lv 4) first, every time: evolving resets refinement to +0.</summary>
		public static int EvolveLevelRequired(int stage) => 4;

		/// <summary>Chance per merge that an eligible weapon offers to evolve. Later stages are rarer.</summary>
		public static float EvolveChance(int stage) => stage switch { 1 => 0.40f, 2 => 0.25f, 3 => 0.15f, _ => 0.08f };

		public bool EvolutionReady(Item item)
		{
			int stage = Loot.StageOf(item.type);
			return stage > 0 && Loot.EvolutionOf(item.type) > 0 && Level >= EvolveLevelRequired(stage);
		}

		/// <summary>Rough power score of a weapon.</summary>
		public float Score(Item item) => item.damage * DamageMultiplier * (1f + 0.15f * Runes.Count);

		public static RefinedWeapon Of(Item item) => item != null && !item.IsAir && item.TryGetGlobalItem(out RefinedWeapon r) ? r : null;

		public static Item Create(int type, Rarity rarity, int level = 1, IEnumerable<RuneSocket> runes = null)
		{
			var item = new Item(type);
			var r = Of(item);
			if (r != null) {
				r.Rarity = rarity;
				r.Level = Math.Clamp(level, 1, MaxLevel);
				r.Peak = r.Level;
				if (runes != null)
					r.Runes = runes.Take(r.RuneSlots).ToList();
				item.rare = RarityDefs.Vanilla(rarity);
			}
			return item;
		}

		/// <summary>Can <paramref name="other"/> be folded into <paramref name="target"/>? Shop-bought copies can't be material.</summary>
		public static bool CanMerge(Item target, Item other)
		{
			var t = Of(target);
			var o = Of(other);
			return t != null && o != null && target.type == other.type && !o.Bought && t.Level < MaxLevel;
		}

		/// <summary>Folds <paramref name="other"/> into <paramref name="target"/>: +1 refinement, best rarity, spare runes carried over.</summary>
		public static void Merge(Item target, Item other)
		{
			var t = Of(target);
			var o = Of(other);
			if (t == null || o == null)
				return;

			// A plain found copy is one fusion; an already-refined copy brings everything invested in it.
			t.AddFusions(1 + o.TotalFusions);
			t.Peak = Math.Max(t.Peak, o.Peak);
			if (o.Rarity > t.Rarity)
				t.Rarity = o.Rarity;
			foreach (RuneSocket rune in o.Runes) {
				if (t.Runes.Count >= t.RuneSlots)
					break;
				t.Runes.Add(rune);
			}
			target.rare = RarityDefs.Vanilla(t.Rarity);
		}

		/// <summary>
		/// The next stage of this weapon's line. Rarity, runes and mastery carry over, but refinement starts again
		/// at +0: that's the price of evolving, and it stops refinement and evolution stacking out of control.
		/// </summary>
		public static Item Evolve(Item from)
		{
			int next = Loot.EvolutionOf(from.type);
			var f = Of(from);
			if (next <= 0 || f == null)
				return null;
			Item evolved = Create(next, f.Rarity, 1, f.Runes);
			var e = Of(evolved);
			if (e != null) {
				e.Peak = f.Peak;
				e.Fusion = 0;
				e.Bought = f.Bought;
			}
			return evolved;
		}

		/// <summary>
		/// A copy of <paramref name="from"/> with a new modifier (prefix): the most valuable of three reforge rolls that
		/// differs from the current one. Refinement, rarity, runes and mastery carry over. Null if nothing rolled.
		/// </summary>
		public static Item Reforged(Item from, int rolls = 3)
		{
			var f = Of(from);
			if (f == null)
				return null;
			int best = 0;
			long bestValue = -1;
			for (int i = 0; i < rolls; i++) {
				var trial = new Item(from.type);
				if (!trial.Prefix(-2) || trial.prefix == from.prefix)
					continue;
				if (trial.value > bestValue) {
					bestValue = trial.value;
					best = trial.prefix;
				}
			}
			if (best <= 0)
				return null;
			var item = new Item(from.type);
			item.Prefix(best);
			var r = Of(item);
			if (r != null) {
				r.Rarity = f.Rarity;
				r.Level = f.Level;
				r.Peak = f.Peak;
				r.Fusion = f.Fusion;
				r.Bought = f.Bought;
				r.Runes = new List<RuneSocket>(f.Runes);
				item.rare = RarityDefs.Vanilla(f.Rarity);
			}
			return item;
		}

		public bool TrySocket(RuneSocket rune)
		{
			if (Runes.Count >= RuneSlots)
				return false;
			Runes.Add(rune);
			return true;
		}

		// ------------------------------------------------------------------ stats

		public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
		{
			if (TerralayerWorld.Active)
				damage *= DamageMultiplier;
		}

		public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
		{
			if (TerralayerWorld.Active)
				crit += (Level - 1) * 0.5f + RarityDefs.CritBonus(Rarity);
		}

		public override float UseSpeedMultiplier(Item item, Player player) => TerralayerWorld.Active ? 1f + Math.Min(0.08f, 0.01f * (Level - 1)) : 1f;

		public override void UpdateInventory(Item item, Player player) => item.rare = RarityDefs.Vanilla(Rarity);

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (!TerralayerWorld.Active)
				return;

			TooltipLine name = tooltips.Find(t => t.Name == "ItemName" && t.Mod == "Terraria");
			if (name != null) {
				name.Text = $"{RarityDefs.Name(Rarity)} {name.Text}" + (Level > 1 ? $" +{Level - 1}" : "");
				name.OverrideColor = RarityDefs.Color(Rarity);
			}

			int bonus = (int)Math.Round((DamageMultiplier - 1f) * 100);
			tooltips.Add(new TooltipLine(Mod, "Refinement", $"Refinement Lv {Level}/{MaxLevel}  (+{bonus}% damage)  -  mastery peak Lv {Math.Max(Peak, Level)}") { OverrideColor = RarityDefs.Color(Rarity) });
			if (Level < MaxLevel)
				tooltips.Add(new TooltipLine(Mod, "Fusion", $"Next refinement: {Fusion}/{Level} fusions") { OverrideColor = new Microsoft.Xna.Framework.Color(170, 190, 230) });
			int next = Loot.EvolutionOf(item.type);
			if (next > 0) {
				int stage = Loot.StageOf(item.type);
				int need = EvolveLevelRequired(stage);
				string when = Level >= need ? $"{(int)(EvolveChance(stage) * 100)}% chance per merge" : $"needs refinement +{need - 1}; refinement resets to +0 when it evolves";
				tooltips.Add(new TooltipLine(Mod, "Evolves", $"Can evolve into {Lang.GetItemNameValue(next)} ({when})") { OverrideColor = new Microsoft.Xna.Framework.Color(200, 170, 255) });
			}
			if (Bought)
				tooltips.Add(new TooltipLine(Mod, "Bought", "Shop-bought: can't be fused into another weapon") { OverrideColor = new Microsoft.Xna.Framework.Color(170, 150, 150) });
			foreach (RuneSocket rune in Runes)
				tooltips.Add(new TooltipLine(Mod, "Rune", $"  {RuneDefs.Get(rune.Type).Name} Rune ({RarityDefs.Name(rune.Rarity)}): {RuneDefs.Describe(rune)}") { OverrideColor = RarityDefs.Color(rune.Rarity) });
			for (int i = Runes.Count; i < RuneSlots; i++)
				tooltips.Add(new TooltipLine(Mod, "EmptySocket", "  Empty rune socket") { OverrideColor = new Microsoft.Xna.Framework.Color(140, 140, 150) });
			tooltips.Add(new TooltipLine(Mod, "MergeHint", "Pick up a found copy to merge it (+1 refinement)") { OverrideColor = new Microsoft.Xna.Framework.Color(150, 150, 170) });
		}

		// ------------------------------------------------------------------ runes

		public override bool? UseItem(Item item, Player player)
		{
			if (TerralayerWorld.Active && Runes.Count > 0 && player.whoAmI == Main.myPlayer)
				RuneEffects.OnUse(player, item, Runes);
			return null;
		}

		public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Vector2 velocity, int type, int damage, float knockback)
		{
			if (TerralayerWorld.Active && Runes.Count > 0 && player.whoAmI == Main.myPlayer)
				RuneEffects.OnShoot(player, item, Runes, source, position, velocity, type, damage, knockback);
			return true;
		}

		public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (TerralayerWorld.Active && Runes.Count > 0 && player.whoAmI == Main.myPlayer)
				RuneEffects.OnHit(player, target, damageDone, player.GetWeaponDamage(item), Runes);
		}

		// ------------------------------------------------------------------ persistence

		public override void SaveData(Item item, TagCompound tag)
		{
			tag["level"] = Level;
			tag["peak"] = Peak;
			tag["fusion"] = Fusion;
			tag["bought"] = Bought;
			tag["rarity"] = (byte)Rarity;
			tag["runes"] = Runes.Select(r => (int)r.Type * 16 + (int)r.Rarity).ToList();
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			Level = Math.Clamp(tag.GetInt("level"), 1, MaxLevel);
			Peak = Math.Max(Level, tag.GetInt("peak"));
			Fusion = Math.Clamp(tag.GetInt("fusion"), 0, Level);
			Bought = tag.GetBool("bought");
			Rarity = (Rarity)tag.GetByte("rarity");
			Runes = tag.GetList<int>("runes").Select(v => new RuneSocket((RuneType)(v / 16), (Rarity)(v % 16))).ToList();
		}

		public override void NetSend(Item item, BinaryWriter writer)
		{
			writer.Write((byte)Level);
			writer.Write((byte)Peak);
			writer.Write((byte)Fusion);
			writer.Write(Bought);
			writer.Write((byte)Rarity);
			writer.Write((byte)Runes.Count);
			foreach (RuneSocket r in Runes) {
				writer.Write((byte)r.Type);
				writer.Write((byte)r.Rarity);
			}
		}

		public override void NetReceive(Item item, BinaryReader reader)
		{
			Level = reader.ReadByte();
			Peak = reader.ReadByte();
			Fusion = reader.ReadByte();
			Bought = reader.ReadBoolean();
			Rarity = (Rarity)reader.ReadByte();
			int n = reader.ReadByte();
			Runes = new List<RuneSocket>();
			for (int i = 0; i < n; i++)
				Runes.Add(new RuneSocket((RuneType)reader.ReadByte(), (Rarity)reader.ReadByte()));
		}
	}

	/// <summary>Carries a weapon's runes onto the projectiles it fires (and the minions it summons).</summary>
	public class RuneProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public List<RuneSocket> Runes;
		public int WeaponDamage;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!TerralayerWorld.Active)
				return;

			if (source is EntitySource_ItemUse use) {
				var r = RefinedWeapon.Of(use.Item);
				if (r != null && r.Runes.Count > 0) {
					Runes = new List<RuneSocket>(r.Runes);
					WeaponDamage = projectile.damage;
				}
			}
			else if (source is EntitySource_Parent parent && parent.Entity is Projectile p && p.TryGetGlobalProjectile(out RuneProjectile pr) && pr.Runes != null) {
				Runes = pr.Runes;
				WeaponDamage = pr.WeaponDamage;
			}
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Runes != null && projectile.owner == Main.myPlayer)
				RuneEffects.OnHit(Main.player[projectile.owner], target, damageDone, Math.Max(WeaponDamage, projectile.damage), Runes);
		}
	}
}
