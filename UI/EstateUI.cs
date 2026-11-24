using System;
using Microsoft.Xna.Framework;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terralayer.UI
{
	/// <summary>
	/// The Estate: build the next homestead structure (strictly in order), buy permanent upgrades unlocked by those
	/// structures, unlock classes, and review the family legacy.
	/// </summary>
	public class EstateUI : UIState
	{
		private UIPanel root;
		private RichText purse;
		private UIList list;
		private bool needsRebuild;

		public override void OnInitialize()
		{
			root = UIFactory.Panel(720, 620);
			Append(root);

			var title = new UIText("The Estate", 0.6f, large: true);
			title.Top.Set(2, 0);
			root.Append(title);

			purse = new RichText("", 0.9f, Color.White, false);
			purse.Top.Set(10, 0);
			purse.Left.Set(210, 0);
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

		public void Rebuild()
		{
			list.Clear();
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			var world = TerralayerWorld.Instance;

			// --- Homestead ---
			list.Add(Header("Homestead - each building brings new services and upgrades"));
			StructureDef next = world.NextStructure;
			if (next != null) {
				list.Add(Row($"Build: {next.Name}", next.Description, Coins.Format(next.Cost),
					() => Buy(next.Cost, () => world.RequestBuild(next.Key))));
			}
			else {
				list.Add(Row("The homestead is complete", "Every building stands. Your house is legendary.", "", null));
			}
			string built = string.Join(", ", Structures.All.FindAll(s => world.HasBuilt(s.Key)).ConvertAll(s => s.Name));
			list.Add(Note("Built: " + built));

			// --- Skill tree ---
			list.Add(Header("Skill Tree - permanent upgrades, shared by every heir"));
			int learned = 0;
			for (int i = 0; i < lp.Upgrades.Length; i++)
				learned += lp.Upgrades[i];
			list.Add(Row("Open the Family Skill Tree", $"{learned} levels learned. New branches open as you raise buildings.", "",
				() => ModContent.GetInstance<UISystem>().OpenSkillTree(), "Open"));

			list.Add(Note("Classes are unlocked on the Classes page of the Skill Tree: each tier needs its parent class and that floor's guardian trophy."));

			// --- Legacy ---
			list.Add(Header("Legacy"));
			list.Add(Note(Content.NPCs.Steward.Lineage(lp)));
			list.Add(Note($"Generation {lp.Generation}. Guardian trophies: {lp.Trophies.Count} (+{lp.Trophies.Count * 2}% damage, +{lp.Trophies.Count * 10} max life). Deepest floor opened: {world.MaxFloorUnlocked}."));
		}

		private void Buy(long cost, Action onBought)
		{
			if (!Main.LocalPlayer.BuyItem(cost)) {
				Main.NewText("You cannot afford that.", Color.OrangeRed);
				return;
			}
			SoundEngine.PlaySound(SoundID.Coins);
			onBought();
			needsRebuild = true;
		}

		private static UIElement Header(string text)
		{
			var t = UIFactory.Text(text, 0.95f, new Color(255, 215, 120), wrapped: false);
			t.Height.Set(28, 0);
			return t;
		}

		private static UIElement Note(string text)
		{
			var t = UIFactory.Text(text, 0.8f, new Color(200, 200, 215));
			t.Height.Set(40, 0);
			return t;
		}

		private static UIElement Row(string name, string description, string price, Action buy, string buttonText = "Buy")
		{
			var row = new UIPanel();
			row.Width.Set(0, 1f);
			row.Height.Set(58, 0);
			row.BackgroundColor = new Color(45, 50, 90) * 0.9f;

			var nameText = new RichText(name, 0.92f, Color.White, false);
			nameText.Top.Set(-6, 0);
			row.Append(nameText);

			var desc = new RichText(description, 0.72f, new Color(200, 200, 210), true);
			desc.Top.Set(16, 0);
			desc.Width.Set(-240, 1f);
			row.Append(desc);

			var priceText = new RichText(price, 0.82f, Color.White, false) { HAlign = 1f };
			priceText.Left.Set(-96, 0);
			priceText.Top.Set(6, 0);
			row.Append(priceText);

			if (buy != null) {
				var button = new TextButton(buttonText, buy, 0.85f);
				button.Width.Set(80, 0);
				button.Height.Set(34, 0);
				button.HAlign = 1f;
				button.VAlign = 0.5f;
				row.Append(button);
			}
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

	/// <summary>Shown when a character carrying outside gear enters a Terralayer world.</summary>
	public class WipeWarningUI : UIState
	{
		private UIPanel root;

		public override void OnInitialize()
		{
			root = UIFactory.Panel(640, 330, new Color(60, 24, 30) * 0.97f);
			Append(root);

			var title = new UIText("This is a Terralayer world", 0.6f, large: true) { HAlign = 0.5f };
			title.Top.Set(4, 0);
			root.Append(title);

			var body = UIFactory.Text(
				"Terralayer is its own game. To play here, this character will be stripped of EVERYTHING it carries:\n" +
				"- every item, coin, armor piece, accessory, dye, pet, mount and hook\n" +
				"- the contents of its piggy bank, safe, forge and void vault\n" +
				"- life crystals, life fruit and mana crystals it has consumed\n" +
				"It will also be set to Softcore. This cannot be undone. Use a fresh character if you want to keep this one intact.",
				0.85f, new Color(240, 220, 220));
			body.Top.Set(52, 0);
			root.Append(body);

			var accept = new TextButton("Strip this character and enter", () => Main.LocalPlayer.GetModPlayer<LegacyPlayer>().AcceptWorld());
			accept.Width.Set(-10, 0.5f);
			accept.Height.Set(44, 0);
			accept.VAlign = 1f;
			root.Append(accept);

			var leave = new TextButton("Leave this world", () => WorldGen.SaveAndQuit());
			leave.Width.Set(-10, 0.5f);
			leave.Height.Set(44, 0);
			leave.VAlign = 1f;
			leave.HAlign = 1f;
			root.Append(leave);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			UIFactory.CaptureMouse(root);
		}
	}
}
