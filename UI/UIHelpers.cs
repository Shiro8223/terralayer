using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using Terraria.UI.Chat;
using ReLogic.Graphics;

namespace Terralayer.UI
{
	/// <summary>A text panel that behaves like a button: hover highlight, click sound, optional disabled state.</summary>
	public class TextButton : UITextPanel<string>
	{
		private static readonly Color Normal = new Color(63, 82, 151) * 0.9f;
		private static readonly Color Hover = new Color(90, 115, 200);
		private static readonly Color Disabled = new Color(50, 50, 60) * 0.8f;

		public bool Enabled = true;
		private readonly Action onClick;

		public TextButton(string text, Action onClick, float scale = 0.9f) : base(text, scale)
		{
			this.onClick = onClick;
			BackgroundColor = Normal;
			OnLeftClick += (_, _) => {
				if (!Enabled)
					return;
				SoundEngine.PlaySound(SoundID.MenuOpen);
				this.onClick?.Invoke();
			};
			OnMouseOver += (_, _) => {
				if (Enabled) {
					BackgroundColor = Hover;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
			};
			OnMouseOut += (_, _) => BackgroundColor = Enabled ? Normal : Disabled;
		}

		public TextButton WithEnabled(bool enabled)
		{
			Enabled = enabled;
			BackgroundColor = enabled ? Normal : Disabled;
			return this;
		}
	}

	public static class UIFactory
	{
		public static RichText Text(string text, float scale = 0.9f, Color? color = null, bool wrapped = true)
		{
			var t = new RichText(text, scale, color ?? Color.White, wrapped);
			if (wrapped)
				t.Width.Set(0, 1f);
			return t;
		}

		public static UIPanel Panel(float width, float height, Color? color = null)
		{
			var p = new UIPanel { HAlign = 0.5f, VAlign = 0.5f };
			p.Width.Set(width, 0);
			p.Height.Set(height, 0);
			p.BackgroundColor = color ?? new Color(33, 30, 52) * 0.96f;
			return p;
		}

		/// <summary>Blocks clicks from reaching the world while the mouse is over <paramref name="panel"/>.</summary>
		public static void CaptureMouse(UIElement panel)
		{
			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}
	}

	/// <summary>
	/// Text that understands chat tags ([c/RRGGBB:coloured text], [i:item]) and wraps without breaking them.
	/// Vanilla's UIText splits tags apart when it wraps, which is where raw "[c/..." codes came from.
	/// Its height follows its content, so it lays out correctly in lists.
	/// </summary>
	public class RichText : UIElement
	{
		private string text;
		private TextSnippet[] snippets;
		private readonly float scale;
		private readonly Color color;
		private readonly bool wrap;

		public RichText(string text, float scale, Color color, bool wrap)
		{
			this.scale = scale;
			this.color = color;
			this.wrap = wrap;
			SetText(text);
		}

		public void SetText(string value)
		{
			if (value == text)
				return;
			text = value ?? "";
			snippets = ChatManager.ParseMessage(text, color).ToArray();
			Measure();
		}

		private DynamicSpriteFont Font => FontAssets.MouseText.Value;

		private void Measure()
		{
			float maxWidth = wrap ? GetInnerDimensions().Width : -1f;
			if (wrap && maxWidth <= 1f)
				return; // not laid out yet
			Vector2 size = ChatManager.GetStringSize(Font, snippets, new Vector2(scale), maxWidth);
			MinHeight.Set(size.Y + 4, 0);
			Height.Set(size.Y + 4, 0);
			if (!wrap) {
				MinWidth.Set(size.X, 0);
				Width.Set(size.X, 0);
			}
		}

		public override void Recalculate()
		{
			base.Recalculate();
			float before = Height.Pixels;
			Measure();
			if (System.Math.Abs(Height.Pixels - before) > 0.5f || !wrap)
				base.Recalculate();
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle d = GetInnerDimensions();
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, Font, snippets, new Vector2(d.X, d.Y + 2), 0f, Vector2.Zero,
				new Vector2(scale), out _, wrap ? d.Width : -1f);
		}
	}

	/// <summary>Immediate-mode drawing helpers for the HUD and the map.</summary>
	public static class Draw
	{
		public static void Rect(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(TextureAssets.MagicPixel.Value, r, c);

		public static void Border(SpriteBatch sb, Rectangle r, Color c, int t = 2)
		{
			Rect(sb, new Rectangle(r.X, r.Y, r.Width, t), c);
			Rect(sb, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
			Rect(sb, new Rectangle(r.X, r.Y, t, r.Height), c);
			Rect(sb, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
		}

		public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color c, float thickness = 2f)
		{
			Vector2 d = b - a;
			sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), c, (float)Math.Atan2(d.Y, d.X), Vector2.Zero, new Vector2(d.Length(), thickness), SpriteEffects.None, 0f);
		}

		/// <summary>A labelled bar: background, fill, border and centered text.</summary>
		public static void Bar(SpriteBatch sb, Rectangle r, float fraction, Color fill, Color back, string text)
		{
			Rect(sb, r, back);
			Rect(sb, new Rectangle(r.X, r.Y, (int)(r.Width * MathHelper.Clamp(fraction, 0f, 1f)), r.Height), fill);
			Border(sb, r, Color.Black * 0.8f, 2);
			Utils.DrawBorderString(sb, text, new Vector2(r.Center.X, r.Y + r.Height / 2f + 2), Color.White, 0.75f, 0.5f, 0.5f);
		}
	}
}
