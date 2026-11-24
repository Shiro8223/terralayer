using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Terralayer.UI
{
	/// <summary>
	/// Small textures drawn in code instead of shipped as art: a soft glow, a five-pointed star (tinted for quest
	/// ratings) and a vignette for the death screen. Created lazily on the draw thread.
	/// </summary>
	public class GenTextures : ModSystem
	{
		private static Texture2D glow, star, vignette;

		public static Texture2D Glow => glow ??= Make(64, (x, y) => {
			float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32)) / 32f;
			float a = MathHelper.Clamp(1f - d, 0f, 1f);
			return a * a;
		});

		public static Texture2D Star => star ??= Make(64, (x, y) => {
			// 4x4 supersampling for smooth edges.
			int inside = 0;
			for (int sx = 0; sx < 4; sx++)
				for (int sy = 0; sy < 4; sy++)
					if (InStar(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))
						inside++;
			return inside / 16f;
		});

		public static Texture2D Vignette => vignette ??= Make(128, (x, y) => {
			float dx = (x + 0.5f - 64f) / 64f;
			float dy = (y + 0.5f - 64f) / 64f;
			float d = (float)Math.Sqrt(dx * dx + dy * dy);
			float t = MathHelper.Clamp((d - 0.35f) / 0.75f, 0f, 1f);
			return t * t * (3f - 2f * t);
		});

		private static readonly Vector2[] StarPoints = BuildStar();

		private static Vector2[] BuildStar()
		{
			var pts = new Vector2[10];
			for (int i = 0; i < 10; i++) {
				float r = i % 2 == 0 ? 30f : 12.5f;
				float a = -MathHelper.PiOver2 + i * MathHelper.Pi / 5f;
				pts[i] = new Vector2(32f + r * (float)Math.Cos(a), 34f + r * (float)Math.Sin(a));
			}
			return pts;
		}

		private static bool InStar(float x, float y)
		{
			bool inside = false;
			for (int i = 0, j = StarPoints.Length - 1; i < StarPoints.Length; j = i++) {
				Vector2 a = StarPoints[i], b = StarPoints[j];
				if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
					inside = !inside;
			}
			return inside;
		}

		/// <summary>A white texture whose alpha (premultiplied) comes from <paramref name="alpha"/>.</summary>
		private static Texture2D Make(int size, Func<int, int, float> alpha)
		{
			var tex = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			var data = new Color[size * size];
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++) {
					float a = MathHelper.Clamp(alpha(x, y), 0f, 1f);
					data[y * size + x] = new Color(a, a, a, a);
				}
			tex.SetData(data);
			return tex;
		}

		public override void Unload()
		{
			Texture2D g = glow, s = star, v = vignette;
			glow = star = vignette = null;
			Main.QueueMainThreadAction(() => {
				g?.Dispose();
				s?.Dispose();
				v?.Dispose();
			});
		}
	}
}
