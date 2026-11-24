using Microsoft.Xna.Framework;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace Terralayer.UI
{
	/// <summary>
	/// Shown when a merge rolls an evolution: the weapon can advance to the next stage of its line (keeping its
	/// refinement, rarity and runes), or you can keep what you have.
	/// </summary>
	public class EvolveUI : UIState
	{
		private UIPanel root;
		private RichText text;

		public override void OnInitialize()
		{
			root = new UIPanel { HAlign = 0.5f, VAlign = 0.82f };
			root.Width.Set(520, 0);
			root.Height.Set(130, 0);
			root.BackgroundColor = new Color(50, 30, 80) * 0.95f;
			root.BorderColor = new Color(200, 160, 255);
			Append(root);

			var title = new UIText("Evolution!", 0.5f, large: true) { HAlign = 0.5f };
			root.Append(title);

			text = UIFactory.Text("", 0.85f, Color.White);
			text.Top.Set(34, 0);
			root.Append(text);

			var evolve = new TextButton("Evolve", () => Main.LocalPlayer.GetModPlayer<LegacyPlayer>().AcceptEvolution());
			evolve.Width.Set(-6, 0.5f);
			evolve.Height.Set(36, 0);
			evolve.VAlign = 1f;
			root.Append(evolve);

			var keep = new TextButton("Keep current weapon", () => Main.LocalPlayer.GetModPlayer<LegacyPlayer>().DeclineEvolution());
			keep.Width.Set(-6, 0.5f);
			keep.Height.Set(36, 0);
			keep.VAlign = 1f;
			keep.HAlign = 1f;
			root.Append(keep);
		}

		public override void Update(GameTime gameTime)
		{
			// Rebuilt from the weapon in the slot every frame. (It used to be cached by slot number, so a second
			// evolution in the same slot - A to B, then B to C - kept showing the first prompt, "A into B".)
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			if (lp.PendingEvolutionSlot >= 0) {
				Item item = Main.LocalPlayer.inventory[lp.PendingEvolutionSlot];
				var r = RefinedWeapon.Of(item);
				int next = Loot.EvolutionOf(item.type);
				if (r != null && next > 0) {
					string lvl = r.Level > 1 ? $" +{r.Level - 1}" : "";
					text.SetText($"The merge stirs something in your {RarityDefs.Tag(r.Rarity, item.Name + lvl)}. It can become {RarityDefs.Tag(r.Rarity, Lang.GetItemNameValue(next))}. Its rarity and runes stay, but refinement resets to +0.");
				}
			}
			base.Update(gameTime);
			UIFactory.CaptureMouse(root);
		}
	}
}
