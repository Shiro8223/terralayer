using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Depths;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace Terralayer.UI
{
	/// <summary>
	/// After a room is cleared: pick one of three cards. Each card wears its rarity's colour (Common white, Uncommon
	/// green, Rare blue, Epic purple, Legendary orange). Extra clears queue up behind it.
	/// </summary>
	public class RoomRewardUI : UIState
	{
		private UIPanel root;
		private UIText title;
		private readonly RichText[] names = new RichText[3];
		private readonly RichText[] rarities = new RichText[3];
		private readonly RichText[] bodies = new RichText[3];
		private readonly UIPanel[] cards = new UIPanel[3];
		private readonly CardIcon[] icons = new CardIcon[3];
		private int shownSerial = -1;

		public override void OnInitialize()
		{
			root = new UIPanel { HAlign = 0.5f, VAlign = 0.86f };
			root.Width.Set(840, 0);
			root.Height.Set(214, 0);
			root.BackgroundColor = new Color(24, 22, 40) * 0.96f;
			root.BorderColor = new Color(255, 215, 120);
			Append(root);

			title = new UIText("Room cleared! Choose a reward", 0.5f, large: true) { HAlign = 0.5f };
			root.Append(title);

			for (int i = 0; i < 3; i++) {
				int slot = i;
				var card = new UIPanel();
				card.Left.Set(4, i / 3f);
				card.Width.Set(-8, 1 / 3f);
				card.Top.Set(38, 0);
				card.Height.Set(-38, 1f);
				root.Append(card);
				cards[i] = card;

				icons[i] = new CardIcon();
				icons[i].Width.Set(36, 0);
				icons[i].Height.Set(36, 0);
				icons[i].Top.Set(-4, 0);
				card.Append(icons[i]);

				names[i] = UIFactory.Text("", 0.95f, Color.White, wrapped: false);
				names[i].Left.Set(42, 0);
				names[i].Top.Set(-4, 0);
				card.Append(names[i]);

				rarities[i] = UIFactory.Text("", 0.7f, Color.White, wrapped: false);
				rarities[i].Left.Set(42, 0);
				rarities[i].Top.Set(16, 0);
				card.Append(rarities[i]);

				bodies[i] = UIFactory.Text("", 0.76f, new Color(225, 225, 235));
				bodies[i].Top.Set(40, 0);
				card.Append(bodies[i]);

				var button = new TextButton("Choose", () => Main.LocalPlayer.GetModPlayer<LegacyPlayer>().PickCard(slot), 0.85f);
				button.Width.Set(0, 1f);
				button.Height.Set(32, 0);
				button.VAlign = 1f;
				card.Append(button);
			}
		}

		public override void Update(GameTime gameTime)
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			bool changed = lp.OfferSerial != shownSerial;
			shownSerial = lp.OfferSerial;
			for (int i = 0; i < 3; i++) {
				RewardCard c = lp.OfferedCards[i];
				CardInfo info = RewardCards.Get(c.Type);
				Color color = RarityDefs.Color(info.Rarity);
				if (changed) {
					cards[i].BackgroundColor = Color.Lerp(new Color(34, 32, 54), color, 0.16f) * 0.97f;
					cards[i].BorderColor = color;
					names[i].SetText(info.Name);
					rarities[i].SetText(RarityDefs.Tag(info.Rarity, RarityDefs.Name(info.Rarity)) + (RewardCards.IsRestoration(c.Type) ? "  [c/8CF0A0:restoration]" : ""));
				}
				// Rebuilt live: amounts and fusion counts change as you play.
				bodies[i].SetText(lp.CardText(c));
				icons[i].Item = lp.CardIcon(c);
				icons[i].Glow = color;
			}
			if (changed)
				root.Recalculate();
			title.SetText(lp.PendingRoomRewards > 1 ? $"Room cleared! Choose a reward ({lp.PendingRoomRewards} waiting)" : "Room cleared! Choose a reward");
			base.Update(gameTime);
			UIFactory.CaptureMouse(root);
		}
	}

	/// <summary>An item icon on a soft glow in the card's rarity colour.</summary>
	public class CardIcon : UIElement
	{
		public int Item;
		public Color Glow;

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (Item <= 0)
				return;
			CalculatedStyle d = GetDimensions();
			Vector2 center = d.Center();
			Texture2D glow = GenTextures.Glow;
			sb.Draw(glow, center, null, new Color(Glow.R, Glow.G, Glow.B, 0) * 0.55f, 0f, glow.Size() / 2f, 0.75f, SpriteEffects.None, 0f);
			Main.instance.LoadItem(Item);
			Texture2D tex = TextureAssets.Item[Item].Value;
			Rectangle frame = Main.itemAnimations[Item] != null ? Main.itemAnimations[Item].GetFrame(tex) : tex.Frame();
			float scale = Math.Min(1f, 30f / Math.Max(frame.Width, frame.Height));
			sb.Draw(tex, center, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}
	}
}
