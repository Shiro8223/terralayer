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
	/// The Runeforge. At home it inscribes onto gear that lasts between descents (the heir's weapon and armor).
	/// In a forge room in the Depths it inscribes onto whatever you are carrying, at a higher price.
	/// </summary>
	public class RuneforgeUI : UIState
	{
		public static long InscribeCost(Rarity r) => r switch {
			Rarity.Common => 2 * Coins.Gold,
			Rarity.Uncommon => 4 * Coins.Gold,
			Rarity.Rare => 8 * Coins.Gold,
			Rarity.Epic => 15 * Coins.Gold,
			_ => 30 * Coins.Gold
		};

		public const long ClearCost = 1 * Coins.Gold;

		/// <summary>Something runes can go onto.</summary>
		private class Target
		{
			public string Label;
			public bool Armor;
			public int Slots;
			public Func<string> Sockets;
			public Func<int> Used;
			public Action<object> Add;   // RuneSocket or ArmorSocket
			public Action Clear;
		}

		private UIPanel root;
		private UIText title;
		private RichText purse;
		private UIList list;
		private bool needsRebuild;
		private bool field;
		private int selected;

		public override void OnInitialize()
		{
			root = UIFactory.Panel(700, 580, new Color(40, 26, 56) * 0.96f);
			Append(root);

			title = new UIText("The Runeforge", 0.6f, large: true);
			title.Top.Set(2, 0);
			root.Append(title);

			purse = new RichText("", 0.9f, Color.White, false);
			purse.Top.Set(10, 0);
			purse.Left.Set(290, 0);
			root.Append(purse);

			var close = new TextButton("Close", () => ModContent.GetInstance<UISystem>().CloseAll());
			close.Width.Set(90, 0);
			close.Height.Set(34, 0);
			close.HAlign = 1f;
			root.Append(close);

			list = new UIList();
			list.Top.Set(48, 0);
			list.Width.Set(-26, 1f);
			list.Height.Set(-48, 1f);
			list.ListPadding = 4f;
			list.ManualSortMethod = _ => { };
			root.Append(list);

			var scrollbar = new UIScrollbar();
			scrollbar.Top.Set(52, 0);
			scrollbar.Height.Set(-56, 1f);
			scrollbar.HAlign = 1f;
			root.Append(scrollbar);
			list.SetScrollbar(scrollbar);
		}

		public void Open(bool inField)
		{
			if (field != inField)
				selected = 0;
			field = inField;
			Rebuild();
		}

		private long Cost(Rarity r) => field ? InscribeCost(r) * 3 / 2 : InscribeCost(r);

		/// <summary>Your hotbar weapons and worn armor. (Gear now comes home with a living heir, so both forges work on it.)</summary>
		private List<Target> Targets(LegacyPlayer lp)
		{
			var targets = new List<Target>();
			Player p = lp.Player;
			for (int i = 0; i < 10; i++) {
				Item item = p.inventory[i];
				var r = RefinedWeapon.Of(item);
				if (r == null)
					continue;
				targets.Add(WeaponTarget($"Carried: {item.Name} +{r.Level - 1}", r.Runes, r.RuneSlots, null));
			}
			for (int s = 0; s < 3; s++) {
				Item piece = p.armor[s];
				var ra = RunedArmor.Of(piece);
				if (ra == null)
					continue;
				targets.Add(ArmorTarget($"Worn: {piece.Name}", () => ra.HasRune ? ra.Rune : null,
					v => { ra.HasRune = v.HasValue; if (v.HasValue) ra.Rune = v.Value; }, null));
			}
			return targets;
		}

		private static Target WeaponTarget(string label, List<RuneSocket> runes, int slots, Action after) => new() {
			Label = label,
			Armor = false,
			Slots = slots,
			Used = () => runes.Count,
			Sockets = () => string.Join(", ", runes.Select(r => RarityDefs.Tag(r.Rarity, RuneDefs.Get(r.Type).Name)).Concat(Enumerable.Repeat("[c/888888:empty]", Math.Max(0, slots - runes.Count)))),
			Add = o => { runes.Add((RuneSocket)o); after?.Invoke(); },
			Clear = () => { runes.Clear(); after?.Invoke(); },
		};

		private static Target ArmorTarget(string label, Func<ArmorSocket?> get, Action<ArmorSocket?> set, Action after) => new() {
			Label = label,
			Armor = true,
			Slots = 1,
			Used = () => get().HasValue ? 1 : 0,
			Sockets = () => get() is ArmorSocket s ? RarityDefs.Tag(s.Rarity, ArmorRuneDefs.Get(s.Type).Name) : "[c/888888:empty]",
			Add = o => { set((ArmorSocket)o); after?.Invoke(); },
			Clear = () => { set(null); after?.Invoke(); },
		};

		public void Rebuild()
		{
			list.Clear();
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			title.SetText(field ? "Field Runeforge" : "The Runeforge");

			list.Add(Note(field
				? "A forge deep in the Depths: inscribe onto the weapons and armor you're carrying. Fees here are 50% higher, and the gear is still lost when the descent ends."
				: "Inscribe onto the weapons in your hotbar and the armor you wear. Your gear stays with your heir until they die. Weapon runes go on weapons, armor runes on armor."));
			list.Add(Note("Every rune shows its odds as [c/5AF064:success] : [c/FF5A5A:failure]. A failed inscription shatters the rune and the fee is still paid. Fusing three identical runes keeps the best odds and adds 6%."));

			List<Target> targets = Targets(lp);
			if (targets.Count == 0) {
				list.Add(Note("You carry no weapon in your hotbar and wear no armor."));
				return;
			}
			selected = Math.Clamp(selected, 0, targets.Count - 1);

			list.Add(Header("Inscribe onto"));
			for (int i = 0; i < targets.Count; i++) {
				int index = i;
				Target t = targets[i];
				bool sel = i == selected;
				list.Add(Row((sel ? "> " : "") + t.Label, $"{(t.Armor ? "Armor socket" : "Rune sockets")} ({t.Used()}/{t.Slots}): {t.Sockets()}",
					sel ? "Selected" : "Select", sel ? null : () => { selected = index; needsRebuild = true; }));
			}

			Target target = targets[selected];
			bool full = target.Used() >= target.Slots;
			if (target.Used() > 0) {
				list.Add(Row("Clear sockets", "Destroys the runes in this item and frees its sockets.", Coins.FormatPlain(ClearCost), () => {
					if (!Pay(ClearCost))
						return;
					target.Clear();
					SoundEngine.PlaySound(SoundID.Item14);
					needsRebuild = true;
				}));
			}

			list.Add(Header(full ? "Your runes (this item has no free socket)" : target.Armor ? "Your armor runes" : "Your weapon runes"));
			bool any = false;
			Item[] inv = Main.LocalPlayer.inventory;
			for (int i = 0; i < inv.Length; i++) {
				int slot = i;
				if (!target.Armor && inv[i].ModItem is RuneItem rune) {
					any = true;
					long cost = Cost(rune.Rarity);
					list.Add(Row(RarityDefs.Tag(rune.Rarity, $"{RarityDefs.Name(rune.Rarity)} {RuneDefs.Get(rune.Kind).Name} Rune") + "   " + RuneOdds.Tag(rune.Chance),
						RuneDefs.Describe(rune.Socket), Coins.FormatPlain(cost),
						full ? null : () => Inscribe(slot, cost, target, () => (inv[slot].ModItem as RuneItem)?.Socket, () => (inv[slot].ModItem as RuneItem)?.Chance ?? 0)));
				}
				else if (target.Armor && inv[i].ModItem is ArmorRuneItem arune) {
					any = true;
					long cost = Cost(arune.Rarity);
					list.Add(Row(RarityDefs.Tag(arune.Rarity, $"{RarityDefs.Name(arune.Rarity)} {ArmorRuneDefs.Get(arune.Kind).Name} Rune") + "   " + RuneOdds.Tag(arune.Chance),
						ArmorRuneDefs.Describe(arune.Socket), Coins.FormatPlain(cost),
						full ? null : () => Inscribe(slot, cost, target, () => (inv[slot].ModItem as ArmorRuneItem)?.Socket, () => (inv[slot].ModItem as ArmorRuneItem)?.Chance ?? 0)));
				}
			}
			if (!any)
				list.Add(Note($"You carry no {(target.Armor ? "armor" : "weapon")} runes. Find them in the Depths, or buy them from the Runeweaver or a Peddler."));
		}

		/// <summary>Pays the fee and rolls the rune's odds: it either takes, or shatters.</summary>
		private void Inscribe(int slot, long cost, Target target, Func<object> socket, Func<int> odds)
		{
			object s = socket();
			int chance = odds();
			if (s == null || !Pay(cost))
				return;
			Main.LocalPlayer.inventory[slot].TurnToAir();
			needsRebuild = true;
			if (Main.rand.Next(100) < chance) {
				target.Add(s);
				SoundEngine.PlaySound(SoundID.Item37);
				Main.NewText($"The Runeforge sears the rune into place. ({chance}% odds held)", new Color(120, 240, 130));
			}
			else {
				SoundEngine.PlaySound(SoundID.Shatter);
				Main.NewText($"The rune cracks under the heat and shatters. ({100 - chance}% chance of failure)", new Color(255, 100, 100));
			}
		}

		private static bool Pay(long cost)
		{
			if (Main.LocalPlayer.BuyItem(cost)) {
				SoundEngine.PlaySound(SoundID.Coins);
				return true;
			}
			Main.NewText("You cannot afford that.", Color.OrangeRed);
			return false;
		}

		private static UIElement Header(string text)
		{
			var t = UIFactory.Text(text, 0.95f, new Color(255, 200, 255), wrapped: false);
			t.Height.Set(28, 0);
			return t;
		}

		private static UIElement Note(string text)
		{
			var t = UIFactory.Text(text, 0.8f, new Color(210, 200, 225));
			t.Height.Set(46, 0);
			return t;
		}

		private static UIElement Row(string name, string description, string action, Action onClick)
		{
			var row = new UIPanel();
			row.Width.Set(0, 1f);
			row.Height.Set(58, 0);
			row.BackgroundColor = new Color(60, 40, 90) * 0.9f;

			var nameText = new RichText(name, 0.88f, Color.White, false);
			nameText.Top.Set(-6, 0);
			row.Append(nameText);

			var desc = new RichText(description, 0.72f, new Color(210, 200, 225), true);
			desc.Top.Set(16, 0);
			desc.Width.Set(-130, 1f);
			row.Append(desc);

			var button = new TextButton(action, onClick ?? (() => { }), 0.8f).WithEnabled(onClick != null);
			button.Width.Set(110, 0);
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
			purse.SetText(Coins.PurseAndSafe(Main.LocalPlayer));
			UIFactory.CaptureMouse(root);
		}
	}
}
