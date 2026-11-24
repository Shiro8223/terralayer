using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terralayer.Content.Items;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terralayer.UI
{
	/// <summary>
	/// The Smith's forge, in three tabs: Refine (spend Refinement Shards to raise a weapon a level), Tinker (merge
	/// accessories into their combined form, as a Tinkerer's Workshop would) and Sell (gear you aren't using).
	/// </summary>
	public class SmithUI : UIState
	{
		private enum Tab { Refine, Tinker, Sell }

		private static readonly Color Iron = new(38, 30, 28);
		private static readonly Color Ember = new(240, 140, 60);

		private UIPanel root;
		private RichText purse, shards;
		private UIList list;
		private readonly TextButton[] tabs = new TextButton[3];
		private Tab tab;
		private bool needsRebuild;

		public override void OnInitialize()
		{
			root = UIFactory.Panel(780, 620, Iron * 0.97f);
			root.BorderColor = Ember;
			Append(root);

			var title = new UIText("The Smithy", 0.55f, large: true);
			title.Top.Set(2, 0);
			root.Append(title);

			shards = new RichText("", 0.85f, Color.White, false);
			shards.Top.Set(10, 0);
			shards.Left.Set(220, 0);
			root.Append(shards);

			purse = new RichText("", 0.85f, Color.White, false) { HAlign = 1f };
			purse.Top.Set(10, 0);
			purse.Left.Set(-100, 0);
			root.Append(purse);

			var close = new TextButton("Close", () => ModContent.GetInstance<UISystem>().CloseAll());
			close.Width.Set(90, 0);
			close.Height.Set(34, 0);
			close.HAlign = 1f;
			root.Append(close);

			string[] names = { "Refine weapons", "Tinker accessories", "Sell gear" };
			for (int i = 0; i < 3; i++) {
				var which = (Tab)i;
				tabs[i] = new TextButton(names[i], () => { tab = which; needsRebuild = true; }, 0.82f);
				tabs[i].Left.Set(i * 192, 0);
				tabs[i].Top.Set(44, 0);
				tabs[i].Width.Set(186, 0);
				tabs[i].Height.Set(34, 0);
				root.Append(tabs[i]);
			}

			list = new UIList();
			list.Top.Set(88, 0);
			list.Width.Set(-26, 1f);
			list.Height.Set(-88, 1f);
			list.ListPadding = 4f;
			list.ManualSortMethod = _ => { };
			root.Append(list);

			var scrollbar = new UIScrollbar { HAlign = 1f };
			scrollbar.Top.Set(92, 0);
			scrollbar.Height.Set(-96, 1f);
			root.Append(scrollbar);
			list.SetScrollbar(scrollbar);
		}

		public void Open()
		{
			tab = Tab.Refine;
			Rebuild();
		}

		private void Rebuild()
		{
			list.Clear();
			for (int i = 0; i < 3; i++)
				tabs[i].WithEnabled((Tab)i != tab);
			switch (tab) {
				case Tab.Refine: BuildRefine(); break;
				case Tab.Tinker: BuildTinker(); break;
				case Tab.Sell: BuildSell(); break;
			}
		}

		private static Player Me => Main.LocalPlayer;

		private static int ShardCount() => Me.inventory.Where(i => i.type == ModContent.ItemType<RefinementShard>()).Sum(i => i.stack);

		// ------------------------------------------------------------------ refine

		private void BuildRefine()
		{
			list.Add(Note("Spend Refinement Shards from the Depths to raise a weapon one refinement level. Each level costs more: 2 shards for +1, then 4, 6 and so on, plus a gold fee. A weapon at +3 can evolve when you merge a copy into it, but evolving resets it to +0."));
			int have = ShardCount();
			bool any = false;
			for (int i = 0; i < 50; i++) {
				Item item = Me.inventory[i];
				var r = RefinedWeapon.Of(item);
				if (r == null)
					continue;
				any = true;
				int slot = i;
				string name = RarityDefs.Tag(r.Rarity, $"{RarityDefs.Name(r.Rarity)} {item.Name}" + (r.Level > 1 ? $" +{r.Level - 1}" : ""));
				if (r.Level >= RefinedWeapon.MaxLevel) {
					list.Add(Row(item.type, r.Rarity, name, "Fully refined.", "Maxed", null));
					continue;
				}
				int cost = RefinementShard.CostFor(r.Level);
				long fee = RefinementShard.FeeFor(r.Level);
				string evolve = Loot.EvolutionOf(item.type) > 0
					? r.Level + 1 >= RefinedWeapon.EvolveLevelRequired(0) ? $"  At +{r.Level}, it can evolve into {Lang.GetItemNameValue(Loot.EvolutionOf(item.type))}." : ""
					: "";
				string desc = $"+{r.Level - 1} to +{r.Level}: [c/E6A0FF:{cost} shards] and {Coins.Format(fee)} (you have {have}).{evolve}";
				bool can = have >= cost && Me.CanAfford(fee);
				list.Add(Row(item.type, r.Rarity, name, desc, "Refine", can ? () => Refine(slot, cost, fee) : null));
			}
			if (!any)
				list.Add(Note("You carry no weapons."));
		}

		private void Refine(int slot, int cost, long fee)
		{
			var r = RefinedWeapon.Of(Me.inventory[slot]);
			if (r == null || ShardCount() < cost || !Me.BuyItem(fee))
				return;
			int left = cost;
			for (int i = 0; i < Me.inventory.Length && left > 0; i++) {
				Item it = Me.inventory[i];
				if (it.type != ModContent.ItemType<RefinementShard>())
					continue;
				int take = Math.Min(left, it.stack);
				it.stack -= take;
				left -= take;
				if (it.stack <= 0)
					it.TurnToAir();
			}
			r.AddFusions(r.Level - r.Fusion);
			SoundEngine.PlaySound(SoundID.Item37);
			SoundEngine.PlaySound(SoundID.Coins);
			CombatText.NewText(Me.Hitbox, RarityDefs.Color(r.Rarity), $"Refined to +{r.Level - 1}!", true);
			needsRebuild = true;
		}

		// ------------------------------------------------------------------ tinker

		private void BuildTinker()
		{
			list.Add(Note("Merge accessories you carry or wear into their combined form. The ingredients are used up; the result gets a fresh modifier and goes where an equipped ingredient was."));
			var counts = Tinkering.Owned(Me);
			var ready = new List<Recipe>();
			var close = new List<Recipe>();
			foreach (Recipe rec in Tinkering.Recipes) {
				int missing = rec.requiredItem.Count(i => !counts.TryGetValue(i.type, out int n) || n < 1);
				if (missing == 0)
					ready.Add(rec);
				else if (missing < rec.requiredItem.Count)
					close.Add(rec);
			}
			list.Add(Header(ready.Count > 0 ? "Ready to merge" : "Nothing you carry merges yet"));
			foreach (Recipe rec in ready) {
				Recipe recipe = rec;
				long fee = Tinkering.Fee(rec);
				list.Add(Row(rec.createItem.type, Rarity.Rare, Lang.GetItemNameValue(rec.createItem.type),
					"From " + string.Join(" + ", rec.requiredItem.Select(i => Lang.GetItemNameValue(i.type))) + $".  Fee: {Coins.Format(fee)}",
					"Merge", Me.CanAfford(fee) ? () => { if (Tinkering.Merge(Me, recipe)) needsRebuild = true; } : null));
			}
			if (close.Count > 0) {
				list.Add(Header("You have part of these"));
				foreach (Recipe rec in close.Take(12)) {
					string parts = string.Join(" + ", rec.requiredItem.Select(i => (counts.TryGetValue(i.type, out int n) && n > 0 ? "[c/78FF8C:" : "[c/FF7878:") + Lang.GetItemNameValue(i.type) + "]"));
					list.Add(Row(rec.createItem.type, Rarity.Common, Lang.GetItemNameValue(rec.createItem.type), parts, "Missing", null));
				}
			}
		}

		// ------------------------------------------------------------------ sell

		private void BuildSell()
		{
			list.Add(Note("Sell weapons, armor and accessories from your pack. Equipped gear, the item in your hand, favourited items and your class weapon are never offered."));
			bool any = false;
			var lp = Me.GetModPlayer<LegacyPlayer>();
			for (int i = 0; i < 50; i++) {
				Item item = Me.inventory[i];
				if (!Selling.Sellable(item, i, lp))
					continue;
				any = true;
				int slot = i;
				long price = Selling.Price(item);
				var r = RefinedWeapon.Of(item);
				Rarity rarity = r?.Rarity ?? Rarity.Common;
				string name = r != null ? RarityDefs.Tag(r.Rarity, item.Name + (r.Level > 1 ? $" +{r.Level - 1}" : "")) : item.Name;
				list.Add(Row(item.type, rarity, name, $"Sells for {Coins.Format(price)}", "Sell", () => {
					if (Selling.Sell(Me, slot))
						needsRebuild = true;
				}));
			}
			if (!any)
				list.Add(Note("Nothing to sell: your pack holds no spare weapons, armor or accessories."));
		}

		// ------------------------------------------------------------------ widgets

		private static UIElement Header(string text)
		{
			var t = UIFactory.Text(text, 0.92f, Ember, wrapped: false);
			t.Height.Set(26, 0);
			return t;
		}

		private static UIElement Note(string text)
		{
			var t = UIFactory.Text(text, 0.78f, new Color(225, 210, 195));
			t.Height.Set(44, 0);
			return t;
		}

		private static UIElement Row(int icon, Rarity rarity, string name, string description, string action, Action onClick)
		{
			var row = new UIPanel();
			row.Width.Set(0, 1f);
			row.Height.Set(60, 0);
			row.BackgroundColor = new Color(62, 46, 38) * 0.92f;
			row.BorderColor = Color.Lerp(new Color(20, 14, 12), RarityDefs.Color(rarity), 0.35f);

			var iconEl = new CardIcon { Item = icon, Glow = RarityDefs.Color(rarity) };
			iconEl.Width.Set(36, 0);
			iconEl.Height.Set(36, 0);
			iconEl.VAlign = 0.5f;
			row.Append(iconEl);

			var nameText = new RichText(name, 0.88f, Color.White, false);
			nameText.Left.Set(46, 0);
			nameText.Top.Set(-6, 0);
			row.Append(nameText);

			var desc = new RichText(description, 0.72f, new Color(225, 210, 200), true);
			desc.Left.Set(46, 0);
			desc.Top.Set(16, 0);
			desc.Width.Set(-170, 1f);
			row.Append(desc);

			var button = new TextButton(action, onClick ?? (() => { }), 0.8f).WithEnabled(onClick != null);
			button.Width.Set(104, 0);
			button.Height.Set(34, 0);
			button.HAlign = 1f;
			button.VAlign = 0.5f;
			row.Append(button);
			return row;
		}

		public override void Update(GameTime gameTime)
		{
			if (needsRebuild) {
				needsRebuild = false;
				Rebuild();
			}
			base.Update(gameTime);
			shards.SetText($"[i:{ModContent.ItemType<RefinementShard>()}] {ShardCount()} shards");
			purse.SetText(Coins.PurseAndSafe(Me));
			UIFactory.CaptureMouse(root);
		}
	}

	/// <summary>Accessory merging, using the game's own Tinkerer's Workshop recipes.</summary>
	public class Tinkering : ModSystem
	{
		private static List<Recipe> recipes;

		/// <summary>Every Tinkerer's Workshop recipe that turns accessories into an accessory.</summary>
		public static List<Recipe> Recipes => recipes ??= Enumerable.Range(0, Recipe.numRecipes).Select(i => Main.recipe[i])
			.Where(r => r?.createItem != null && !r.createItem.IsAir && r.createItem.accessory
				&& r.requiredTile.Contains(TileID.TinkerersWorkbench) && r.requiredItem.Count >= 2
				&& r.requiredItem.All(i => i.accessory && i.stack == 1))
			.ToList();

		public override void Unload() => recipes = null;

		/// <summary>Accessory slots worn (not vanity).</summary>
		private static IEnumerable<int> WornSlots(Player p) => Enumerable.Range(3, 7).Where(i => i < p.armor.Length);

		public static Dictionary<int, int> Owned(Player p)
		{
			var counts = new Dictionary<int, int>();
			void Add(Item i)
			{
				if (i != null && !i.IsAir && i.accessory && AccessoryForge.Of(i)?.Innate != true)
					counts[i.type] = counts.GetValueOrDefault(i.type) + 1;
			}
			for (int i = 0; i < 50; i++)
				Add(p.inventory[i]);
			foreach (int s in WornSlots(p))
				Add(p.armor[s]);
			return counts;
		}

		public static long Fee(Recipe r) => Math.Max(2 * Coins.Gold, r.createItem.value / 4 / Coins.Silver * Coins.Silver);

		public static bool Merge(Player p, Recipe r)
		{
			var counts = Owned(p);
			if (r.requiredItem.Any(i => counts.GetValueOrDefault(i.type) < 1) || !p.BuyItem(Fee(r)))
				return false;
			int wornSlot = -1;
			foreach (Item need in r.requiredItem) {
				int inv = Array.FindIndex(p.inventory, 0, 50, i => i.type == need.type);
				if (inv >= 0) {
					p.inventory[inv].TurnToAir();
					continue;
				}
				int worn = WornSlots(p).FirstOrDefault(s => p.armor[s].type == need.type, -1);
				if (worn >= 0) {
					p.armor[worn].TurnToAir();
					if (wornSlot < 0)
						wornSlot = worn;
				}
			}
			var result = new Item(r.createItem.type);
			if (wornSlot >= 0)
				p.armor[wornSlot] = result;
			else
				p.QuickSpawnItem(p.GetSource_Misc("TerralayerTinker"), result, 1);
			SoundEngine.PlaySound(SoundID.Item37);
			Main.NewText($"The Smith merges them into {result.Name}.", new Color(255, 170, 90));
			return true;
		}
	}

	/// <summary>Selling spare gear to the Smith.</summary>
	public static class Selling
	{
		public static bool IsGear(Item item) =>
			RefinedWeapon.Of(item) != null || RunedArmor.Of(item) != null || item.accessory && !item.vanity;

		/// <summary>Spare gear only: not in hand, not favourited, and never the heir's own class weapon.</summary>
		public static bool Sellable(Item item, int slot, LegacyPlayer lp)
		{
			if (item == null || item.IsAir || item.favorited || slot == lp.Player.selectedItem || !IsGear(item) || AccessoryForge.Of(item)?.Innate == true || Common.Secrets.EternalItem.Is(item))
				return false;
			if (lp.Heir != null && RefinedWeapon.Of(item) != null && Loot.SameLine(item.type, lp.Heir.WeaponType))
				return false;
			return true;
		}

		/// <summary>A fifth of the item's value (at least 10 silver), more for rarity, refinement and an inscribed rune.</summary>
		public static long Price(Item item)
		{
			long price = Math.Max(10 * Coins.Silver, item.value / 5);
			var r = RefinedWeapon.Of(item);
			if (r != null)
				price = price * (10 + 4 * (int)r.Rarity + 2 * (r.Level - 1)) / 10;
			var a = RunedArmor.Of(item);
			if (a != null && a.HasRune)
				price += 30 * Coins.Silver * (1 + (int)a.Rune.Rarity);
			return price;
		}

		public static bool Sell(Player p, int slot)
		{
			Item item = p.inventory[slot];
			if (!Sellable(item, slot, p.GetModPlayer<LegacyPlayer>()))
				return false;
			long price = Price(item);
			string name = item.Name;
			item.TurnToAir();
			Coins.PutInInventory(p, price);
			SoundEngine.PlaySound(SoundID.Coins);
			Main.NewText($"Sold {name} for {Coins.Format(price)}.", new Color(255, 215, 120));
			return true;
		}
	}
}
