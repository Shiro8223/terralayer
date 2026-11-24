using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Quests;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terralayer.UI
{
	/// <summary>
	/// The Herald's quest board: five posted notices on the left, your (up to three) accepted quests on the right.
	/// Each notice carries a 1-5 star rating in its own colour and the array of rewards it pays.
	/// </summary>
	public class QuestUI : UIState
	{
		private static readonly Color Wood = new(46, 34, 24);
		private static readonly Color Brass = new(215, 175, 100);

		private UIPanel root;
		private RichText purse, activeHeader, doneText;
		private UIList board, mine;
		private TextButton reroll;
		private bool needsRebuild;

		public override void OnInitialize()
		{
			root = UIFactory.Panel(1080, 660, Wood * 0.97f);
			root.BorderColor = Brass;
			Append(root);

			var title = new UIText("The Herald's Quest Board", 0.55f, large: true);
			title.Top.Set(2, 0);
			root.Append(title);

			// Right-aligned beside the buttons, so it never runs into the title.
			purse = new RichText("", 0.85f, Color.White, false) { HAlign = 1f };
			purse.Top.Set(10, 0);
			purse.Left.Set(-236, 0);
			root.Append(purse);

			var close = new TextButton("Close", () => ModContent.GetInstance<UISystem>().CloseAll());
			close.Width.Set(90, 0);
			close.Height.Set(34, 0);
			close.HAlign = 1f;
			root.Append(close);

			reroll = new TextButton($"Repost ({Coins.FormatPlain(QuestDefs.RerollCost)})", () => {
				if (Main.LocalPlayer.GetModPlayer<QuestPlayer>().Reroll())
					needsRebuild = true;
			}, 0.8f);
			reroll.Width.Set(130, 0);
			reroll.Height.Set(34, 0);
			reroll.HAlign = 1f;
			reroll.Left.Set(-96, 0);
			root.Append(reroll);

			var sub = UIFactory.Text("Take up to three quests. Finish them in the Depths, then come back to claim the reward. Failing costs nothing: try again with the next heir. More stars, harder deed, richer purse.",
				0.78f, new Color(225, 205, 170));
			sub.Top.Set(40, 0);
			root.Append(sub);

			var postedHeader = UIFactory.Text("Posted notices", 0.95f, Brass, wrapped: false);
			postedHeader.Top.Set(84, 0);
			root.Append(postedHeader);

			activeHeader = new RichText("", 0.95f, Brass, false);
			activeHeader.Top.Set(84, 0);
			activeHeader.Left.Set(8, 0.58f);
			root.Append(activeHeader);

			doneText = new RichText("", 0.75f, new Color(200, 185, 160), false);
			doneText.Top.Set(88, 0);
			doneText.HAlign = 1f;
			root.Append(doneText);

			board = MakeList(0f, 0.58f, out UIScrollbar bs);
			root.Append(board);
			root.Append(bs);
			mine = MakeList(0.58f, 0.42f, out UIScrollbar ms);
			root.Append(mine);
			root.Append(ms);
		}

		private static UIList MakeList(float left, float width, out UIScrollbar scrollbar)
		{
			var list = new UIList();
			list.Left.Set(left > 0 ? 8 : 0, left);
			list.Top.Set(112, 0);
			list.Width.Set(-30, width);
			list.Height.Set(-112, 1f);
			list.ListPadding = 6f;
			list.ManualSortMethod = _ => { };
			scrollbar = new UIScrollbar();
			scrollbar.Left.Set(left > 0 ? -14 : -22, left + width);
			scrollbar.Top.Set(116, 0);
			scrollbar.Height.Set(-120, 1f);
			list.SetScrollbar(scrollbar);
			return list;
		}

		public void Rebuild()
		{
			var qp = Main.LocalPlayer.GetModPlayer<QuestPlayer>();
			qp.EnsureBoard();
			string weapon = qp.StartingWeaponName();
			bool full = qp.Active.Count >= QuestDefs.MaxActive;

			board.Clear();
			foreach (Quest q in qp.Board) {
				Quest quest = q;
				board.Add(new QuestCard(quest, weapon, false, full ? "Board full" : "Accept", full ? null : () => {
					if (qp.Accept(quest))
						needsRebuild = true;
				}));
			}

			mine.Clear();
			if (qp.Active.Count == 0)
				mine.Add(Note("No quests yet. Accept a notice on the left; it stays here, across heirs, until you claim or abandon it."));
			foreach (Quest q in qp.Active) {
				Quest quest = q;
				if (quest.Complete)
					mine.Add(new QuestCard(quest, weapon, true, "Claim", () => { qp.Claim(quest); needsRebuild = true; }));
				else
					mine.Add(new QuestCard(quest, weapon, true, "Abandon", () => { qp.Abandon(quest); needsRebuild = true; }));
			}
			activeHeader.SetText($"Your quests ({qp.Active.Count}/{QuestDefs.MaxActive})");
			doneText.SetText($"Quests completed: {qp.Completed}");
		}

		private static UIElement Note(string text)
		{
			var t = UIFactory.Text(text, 0.8f, new Color(210, 195, 170));
			t.Height.Set(60, 0);
			return t;
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

	/// <summary>One notice: title, star rating, description, progress, rewards and a button.</summary>
	public class QuestCard : UIPanel
	{
		private readonly Color color;

		public QuestCard(Quest q, string weapon, bool accepted, string action, Action onClick)
		{
			color = QuestDefs.StarColor(q.Stars);
			Width.Set(0, 1f);
			Height.Set(accepted ? 158 : 142, 0);
			BackgroundColor = Color.Lerp(new Color(62, 47, 33), color, 0.12f) * 0.96f;
			BorderColor = color * 0.85f;
			SetPadding(10);

			var title = new RichText(QuestDefs.Title(q), 0.92f, color, false);
			title.Top.Set(-4, 0);
			Append(title);

			var stars = new StarRating(q.Stars) { HAlign = 1f };
			stars.Top.Set(-4, 0);
			Append(stars);

			var desc = new RichText(QuestDefs.Describe(q, weapon), 0.74f, new Color(235, 225, 205), true);
			desc.Top.Set(20, 0);
			desc.Width.Set(-104, 1f);
			Append(desc);

			if (accepted) {
				string status;
				Color sc;
				int target = QuestDefs.Target(q);
				if (q.Complete) {
					status = "Complete! Claim your reward.";
					sc = new Color(120, 255, 140);
				}
				else if (target > 1) {
					status = q.Type == QuestType.Pilgrim ? $"Best descent so far: {q.Progress}/{target}" : $"Progress: {q.Progress}/{target}";
					sc = new Color(220, 210, 150);
				}
				else {
					status = "In progress: judged during your descents.";
					sc = new Color(190, 180, 160);
				}
				var st = new RichText(status, 0.74f, sc, false);
				st.Top.Set(-58, 1f);
				Append(st);
			}

			var rewards = new RewardRow(q.Rewards);
			rewards.Top.Set(-32, 1f);
			rewards.Width.Set(-120, 1f);
			rewards.Height.Set(30, 0);
			Append(rewards);

			var button = new TextButton(action, onClick ?? (() => { }), 0.8f).WithEnabled(onClick != null);
			button.Width.Set(104, 0);
			button.Height.Set(32, 0);
			button.HAlign = 1f;
			button.VAlign = 1f;
			Append(button);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			// A coloured spine down the left edge.
			CalculatedStyle d = GetDimensions();
			global::Terralayer.UI.Draw.Rect(spriteBatch, new Rectangle((int)d.X + 3, (int)d.Y + 6, 4, (int)d.Height - 12), color * 0.9f);
		}
	}

	/// <summary>Five stars, the first <c>stars</c> lit in the rating's colour, with the tier name underneath.</summary>
	public class StarRating : UIElement
	{
		private readonly int stars;

		public StarRating(int stars)
		{
			this.stars = Math.Clamp(stars, 1, 5);
			Width.Set(94, 0);
			Height.Set(36, 0);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			Color c = QuestDefs.StarColor(stars);
			Texture2D star = GenTextures.Star;
			Texture2D glow = GenTextures.Glow;
			float twinkle = stars == 5 ? 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) : 1f;
			for (int i = 0; i < 5; i++) {
				var at = new Vector2(d.X + 9 + i * 19, d.Y + 10);
				bool lit = i < stars;
				if (lit)
					sb.Draw(glow, at, null, new Color(c.R, c.G, c.B, 0) * 0.55f, 0f, glow.Size() / 2f, 0.5f * twinkle, SpriteEffects.None, 0f);
				sb.Draw(star, at, null, Color.Black * 0.8f, 0f, star.Size() / 2f, 0.31f, SpriteEffects.None, 0f);
				sb.Draw(star, at, null, lit ? c : new Color(55, 44, 34), 0f, star.Size() / 2f, 0.26f * (lit ? twinkle : 1f), SpriteEffects.None, 0f);
			}
			Utils.DrawBorderString(sb, QuestDefs.Tier(stars), new Vector2(d.X + d.Width / 2f, d.Y + 22), c, 0.68f, 0.5f, 0f);
		}
	}

	/// <summary>The rewards a quest pays, as item icons with short labels; hover one for the details.</summary>
	public class RewardRow : UIElement
	{
		private readonly List<QuestReward> rewards;

		public RewardRow(List<QuestReward> rewards) => this.rewards = rewards;

		protected override void DrawSelf(SpriteBatch sb)
		{
			CalculatedStyle d = GetDimensions();
			float x = d.X;
			Utils.DrawBorderString(sb, "Rewards:", new Vector2(x, d.Y + 8), new Color(200, 185, 160), 0.72f);
			x += FontAssets.MouseText.Value.MeasureString("Rewards:").X * 0.72f + 8;
			foreach (QuestReward r in rewards) {
				int item = QuestDefs.RewardIcon(r);
				Main.instance.LoadItem(item);
				Texture2D tex = TextureAssets.Item[item].Value;
				Rectangle frame = Main.itemAnimations[item] != null ? Main.itemAnimations[item].GetFrame(tex) : tex.Frame();
				float scale = Math.Min(1f, 22f / Math.Max(frame.Width, frame.Height));
				var icon = new Vector2(x + 11, d.Y + 14);
				sb.Draw(tex, icon, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
				string label = QuestDefs.RewardShort(r);
				float w = FontAssets.MouseText.Value.MeasureString(label).X * 0.72f;
				Utils.DrawBorderString(sb, label, new Vector2(x + 24, d.Y + 8), QuestDefs.RewardColor(r), 0.72f);
				var hit = new Rectangle((int)x, (int)d.Y, (int)(26 + w), 28);
				if (hit.Contains(Main.MouseScreen.ToPoint()))
					Main.hoverItemName = QuestDefs.RewardText(r);
				x += 30 + w + 10;
			}
		}
	}
}
