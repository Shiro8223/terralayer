using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Utilities;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Terralayer.UI
{
	/// <summary>
	/// Pick one of three heirs. After a death this is the death screen itself: the fallen heir's epitaph over a dark,
	/// ember-lit crypt, with the candidates below. The heir you pick rises at once.
	/// </summary>
	public class HeirSelectUI : UIState
	{
		private UIPanel root;
		private DeathBackdrop backdrop;
		public bool DeathMode { get; private set; }

		public override void OnInitialize()
		{
			backdrop = new DeathBackdrop();
			backdrop.Width.Set(0, 1f);
			backdrop.Height.Set(0, 1f);
			Append(backdrop);

			root = UIFactory.Panel(1020, 540);
			Append(root);
		}

		public void Rebuild(LegacyPlayer lp, bool deathMode)
		{
			DeathMode = deathMode && lp.LastDeath != null;
			backdrop.Record = DeathMode ? lp.LastDeath : null;
			root.RemoveAllChildren();

			if (DeathMode) {
				root.Width.Set(1080, 0);
				root.Height.Set(400, 0);
				root.VAlign = 1f;
				root.Top.Set(-28, 0);
				root.BackgroundColor = new Color(22, 13, 18) * 0.94f;
				root.BorderColor = new Color(140, 40, 50);

				var title = new UIText("Choose who carries the name", 0.48f, large: true) { HAlign = 0.5f };
				title.TextColor = new Color(235, 220, 200);
				root.Append(title);
				for (int i = 0; i < lp.HeirOptions.Count; i++)
					root.Append(Card(lp, lp.HeirOptions[i], i, lp.HeirOptions.Count, 44, true));
			}
			else {
				root.Width.Set(1020, 0);
				root.Height.Set(540, 0);
				root.VAlign = 0.5f;
				root.Top.Set(0, 0);
				root.BackgroundColor = new Color(33, 30, 52) * 0.96f;
				root.BorderColor = Color.Black;

				var title = new UIText("Choose Your Heir", 0.7f, large: true) { HAlign = 0.5f };
				title.Top.Set(2, 0);
				root.Append(title);

				string sub = lp.Generation == 0
					? $"The house of {lp.Player.name} begins. Pick who carries the name into the Depths."
					: $"Generation {lp.Generation + 1} of the house of {lp.Player.name}.";
				if (lp.Ancestors.Count > 0)
					sub += $"  Last heir: {lp.Ancestors[0].Fate}";
				var subText = UIFactory.Text(sub, 0.82f, new Color(200, 200, 220));
				subText.Top.Set(44, 0);
				root.Append(subText);

				for (int i = 0; i < lp.HeirOptions.Count; i++)
					root.Append(Card(lp, lp.HeirOptions[i], i, lp.HeirOptions.Count, 90, false));
			}
			Recalculate();
		}

		private static UIElement Card(LegacyPlayer lp, HeirData heir, int index, int count, int top, bool death)
		{
			ClassInfo info = ClassDefs.Get(heir.Class);
			var card = new UIPanel();
			float pct = 1f / count;
			card.Left.Set(4, pct * index);
			card.Width.Set(-8, pct);
			card.Top.Set(top, 0);
			card.Height.Set(-top - 4, 1f);
			card.BackgroundColor = death
				? Color.Lerp(new Color(30, 20, 28), info.Color, 0.12f) * 0.97f
				: Color.Lerp(new Color(40, 44, 80), info.Color, 0.14f) * 0.96f;
			card.BorderColor = death ? info.Color * 0.6f : Color.Black;

			const int buttons = -46;
			var list = new UIList();
			list.Width.Set(-22, 1f);
			list.Height.Set(buttons, 1f);
			list.ListPadding = 5f;
			list.ManualSortMethod = _ => { };
			card.Append(list);

			// Long cards scroll (mouse wheel or drag the bar).
			var scrollbar = new UIScrollbar { HAlign = 1f };
			scrollbar.Top.Set(4, 0);
			scrollbar.Height.Set(buttons - 8, 1f);
			card.Append(scrollbar);
			list.SetScrollbar(scrollbar);

			ClassKit kit = heir.Kit;
			list.Add(UIFactory.Text(heir.FullName, 1.1f, info.Color, wrapped: false));
			list.Add(UIFactory.Text($"{heir.ClassLabel} ({info.Name} path): {info.Passive}", 0.78f, new Color(215, 215, 215)));
			string armor = string.Join(", ", kit.Armor.Where(a => a > 0).Select(a => Lang.GetItemNameValue(a)));
			list.Add(UIFactory.Text($"Armor: {armor}" + (kit.Accessories.Length > 0 ? "  +  " + string.Join(", ", kit.Accessories.Select(a => Lang.GetItemNameValue(a))) : ""), 0.75f, new Color(190, 200, 230)));
			string kind = info.Kind == AbilityKind.Sustained ? $"toggle, drains {info.Cost:0}/s" : $"costs {info.Cost:0} charge";
			list.Add(UIFactory.Text($"[F] {info.AbilityName} ({kind}): {info.AbilityDescription}", 0.78f, new Color(180, 220, 255)));

			foreach (HeirTrait t in heir.Traits) {
				TraitInfo ti = TraitDefs.Get(t);
				string gold = ti.GoldBonus > 0 ? $" [c/FFD24A:(+{ti.GoldBonus * 100:0}% gold)]" : "";
				list.Add(UIFactory.Text($"{ti.Name}: {ti.Description}{gold}", 0.78f, ti.Positive ? new Color(140, 230, 140) : new Color(240, 140, 120)));
			}

			if (heir.GoldBonus > 0)
				list.Add(UIFactory.Text($"Gold found: +{heir.GoldBonus * 100:0}%", 0.85f, new Color(255, 210, 74)));

			string weapon = Lang.GetItemNameValue(heir.WeaponType) + (heir.WeaponLevel > 1 ? $" +{heir.WeaponLevel - 1}" : "");
			list.Add(UIFactory.Text("Starts with: " + RarityDefs.Tag(heir.WeaponRarity, $"{RarityDefs.Name(heir.WeaponRarity)} {weapon}"), 0.8f));
			foreach (RuneSocket rune in heir.Runes)
				list.Add(UIFactory.Text("Socketed rune: " + RarityDefs.Tag(rune.Rarity, RuneDefs.Get(rune.Type).Name), 0.8f));

			var choose = new TextButton(death ? "Take up the name" : "Choose", () => lp.ChooseHeir(heir));
			choose.Width.Set(0, 1f);
			choose.Height.Set(38, 0);
			choose.VAlign = 1f;
			card.Append(choose);

			return card;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			UIFactory.CaptureMouse(root);
		}
	}

	/// <summary>
	/// The death screen's backdrop: darkness closing in from a blood-red vignette, embers drifting up, the heir's
	/// epitaph between two guttering candles.
	/// </summary>
	public class DeathBackdrop : UIElement
	{
		public DeathRecord Record;

		private class Ember
		{
			public Vector2 Pos, Vel;
			public float Life, MaxLife, Size;
		}

		private readonly List<Ember> embers = new();
		private static readonly Color Bone = new(235, 222, 196);
		private static readonly Color Blood = new(205, 35, 45);

		protected override void DrawSelf(SpriteBatch sb)
		{
			if (Record == null)
				return;
			CalculatedStyle d = GetDimensions();
			var screen = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
			float t = (Main.GameUpdateCount - Record.Time) / 60f;
			float fade = MathHelper.Clamp(t / 1.2f, 0f, 1f);
			float textFade = MathHelper.Clamp((t - 0.5f) / 1.2f, 0f, 1f);

			// Darkness, then a red-black vignette.
			global::Terralayer.UI.Draw.Rect(sb, screen, Color.Black * (0.80f * fade));
			sb.Draw(GenTextures.Vignette, screen, new Color(120, 8, 16) * fade);
			sb.Draw(GenTextures.Vignette, screen, Color.Black * (0.55f * fade));

			DrawEmbers(sb, screen, fade);

			float cx = screen.Center.X;
			float y = screen.Y + Math.Max(40, screen.Height * 0.07f);

			// Title with a slow blood-red pulse behind it.
			Texture2D glow = GenTextures.Glow;
			float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
			sb.Draw(glow, new Rectangle((int)cx - 360, (int)y - 30, 720, 110), new Color(160, 10, 20, 0) * (0.55f * pulse * textFade));
			Utils.DrawBorderStringBig(sb, "YOU HAVE FALLEN", new Vector2(cx, y), Blood * textFade, 1.05f, 0.5f, 0f);
			y += 76;
			Ornament(sb, cx, y, 300, textFade);
			y += 22;

			// The epitaph, flanked by candles.
			Candle(sb, new Vector2(cx - 330, y + 70), textFade, 0f);
			Candle(sb, new Vector2(cx + 330, y + 70), textFade, 1.7f);
			Main.instance.LoadItem(ItemID.Tombstone);
			Texture2D stone = TextureAssets.Item[ItemID.Tombstone].Value;
			sb.Draw(stone, new Vector2(cx, y + 16), null, Color.White * textFade, 0f, stone.Size() / 2f, 1.4f, SpriteEffects.None, 0f);
			y += 40;

			ClassInfo info = ClassDefs.Get(Record.Class);
			Centered(sb, $"Here lies {Record.Name}, {Record.ClassName}  -  generation {Record.Generation}", cx, y, info.Color, 1.0f, textFade);
			y += 28;
			Centered(sb, Record.Fate, cx, y, new Color(205, 180, 180), 0.85f, textFade);
			y += 24;
			string where = Record.Floor > 0 ? $"Fell on floor {Record.Floor}: {Record.FloorName}" : "Fell outside the Depths";
			Centered(sb, $"{where}   |   Rooms cleared: {Record.RoomsCleared}   |   Guardians slain: {Record.Guardians}", cx, y, Bone * 0.9f, 0.8f, textFade);
			y += 24;
			long safe = Coins.InBanks(Main.LocalPlayer);
			string coins = (Record.CoinsLost > 0 ? $"[c/FF6E6E:Lost {Coins.FormatPlain(Record.CoinsLost)} ({Record.LossPercent}%)]   |   " : "")
				+ $"[c/FFD75A:Kept {Coins.FormatPlain(Record.CoinsKept)}]"
				+ (safe > 0 ? $"   |   Safe untouched: {Coins.FormatPlain(safe)}" : "");
			Centered(sb, coins, cx, y, Bone, 0.8f, textFade);
			y += 24;
		}

		private void DrawEmbers(SpriteBatch sb, Rectangle screen, float fade)
		{
			if (!Main.gamePaused) {
				if (embers.Count < 70 && Main.rand.NextBool(2))
					embers.Add(new Ember {
						Pos = new Vector2(screen.X + Main.rand.NextFloat(screen.Width), screen.Bottom + 6),
						Vel = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.6f)),
						MaxLife = Main.rand.NextFloat(240, 520),
						Size = Main.rand.NextFloat(0.06f, 0.16f),
					});
				foreach (Ember e in embers) {
					e.Life++;
					e.Vel.X += Main.rand.NextFloat(-0.03f, 0.03f);
					e.Pos += e.Vel;
				}
				embers.RemoveAll(e => e.Life >= e.MaxLife || e.Pos.Y < screen.Y - 10);
			}
			Texture2D glow = GenTextures.Glow;
			foreach (Ember e in embers) {
				float a = (float)Math.Sin(Math.PI * e.Life / e.MaxLife) * fade;
				sb.Draw(glow, e.Pos, null, new Color(255, 120, 40, 0) * a, 0f, glow.Size() / 2f, e.Size, SpriteEffects.None, 0f);
				sb.Draw(glow, e.Pos, null, new Color(255, 230, 160, 0) * a, 0f, glow.Size() / 2f, e.Size * 0.35f, SpriteEffects.None, 0f);
			}
		}

		/// <summary>Two lines tapering out from a diamond.</summary>
		private static void Ornament(SpriteBatch sb, float cx, float y, float half, float alpha)
		{
			Color c = new Color(170, 60, 60) * alpha;
			for (int i = 0; i < 12; i++) {
				float f = 1f - i / 12f;
				float x0 = 14 + half * i / 12f;
				float x1 = 14 + half * (i + 1) / 12f;
				global::Terralayer.UI.Draw.Rect(sb, new Rectangle((int)(cx + x0), (int)y, (int)(x1 - x0) + 1, 2), c * f);
				global::Terralayer.UI.Draw.Rect(sb, new Rectangle((int)(cx - x1), (int)y, (int)(x1 - x0) + 1, 2), c * f);
			}
			sb.Draw(TextureAssets.MagicPixel.Value, new Vector2(cx, y + 1), new Rectangle(0, 0, 1, 1), new Color(220, 80, 80) * alpha, MathHelper.PiOver4, new Vector2(0.5f), 9f, SpriteEffects.None, 0f);
		}

		private static void Candle(SpriteBatch sb, Vector2 at, float alpha, float phase)
		{
			Main.instance.LoadItem(ItemID.Candle);
			Texture2D tex = TextureAssets.Item[ItemID.Candle].Value;
			Rectangle frame = Main.itemAnimations[ItemID.Candle] != null ? Main.itemAnimations[ItemID.Candle].GetFrame(tex) : tex.Frame();
			sb.Draw(tex, at, frame, Color.White * alpha, 0f, frame.Size() / 2f, 1.6f, SpriteEffects.None, 0f);
			float flicker = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + phase) + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 23f + phase);
			Texture2D glow = GenTextures.Glow;
			sb.Draw(glow, at - new Vector2(0, frame.Height * 0.8f), null, new Color(255, 170, 70, 0) * (0.8f * alpha), 0f, glow.Size() / 2f, 1.4f * flicker, SpriteEffects.None, 0f);
		}

		/// <summary>Centered text that understands [c/RRGGBB:...] colour tags.</summary>
		private static void Centered(SpriteBatch sb, string text, float cx, float y, Color color, float scale, float alpha)
		{
			if (string.IsNullOrEmpty(text) || alpha <= 0f)
				return;
			TextSnippet[] snippets = ChatManager.ParseMessage(text, color).ToArray();
			foreach (TextSnippet s in snippets)
				s.Color *= alpha;
			Vector2 size = ChatManager.GetStringSize(FontAssets.MouseText.Value, snippets, new Vector2(scale));
			ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, snippets, new Vector2(cx - size.X / 2f, y), 0f, Vector2.Zero, new Vector2(scale), out _);
		}
	}
}
