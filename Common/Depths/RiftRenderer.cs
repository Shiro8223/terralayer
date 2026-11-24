using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terralayer.Common.Players;
using Terralayer.Common.World;
using Terralayer.UI;
using Terraria;
using Terraria.ModLoader;

namespace Terralayer.Common.Depths
{
	/// <summary>Draws rifts and shrines in the world, lights them up, and labels the one you're standing in.</summary>
	[Autoload(Side = ModSide.Client)]
	public class RiftRenderer : ModSystem
	{
		private Asset<Texture2D> riftTexture;
		private Asset<Texture2D> shrineTexture;
		private Asset<Texture2D> forgeTexture;

		public override void Load()
		{
			riftTexture = ModContent.Request<Texture2D>("Terralayer/Assets/Rift");
			shrineTexture = ModContent.Request<Texture2D>("Terralayer/Assets/Shrine");
			forgeTexture = ModContent.Request<Texture2D>("Terralayer/Assets/Runeforge");
		}

		/// <summary>Whether a rift is shown, its tint, and what its label says.</summary>
		public static (bool visible, Color color, string label) Look(LegacyPlayer lp, DepthsSystem depths, RiftPlan rift)
		{
			bool active = depths.RiftActive(rift);
			switch (rift.Kind) {
				case RiftKind.Retreat:
					return (true, new Color(255, 200, 90), "Retreat home (ends the descent)");
				case RiftKind.ReturnHome:
					return (active, new Color(255, 215, 70), depths.Floor >= FloorDefs.Count ? "Return in glory" : "Return home with your spoils (+15% gold)");
				case RiftKind.Descend:
					return (active, new Color(190, 110, 255), depths.Floor >= FloorDefs.Count ? "Return in glory" : $"Descend to floor {depths.Floor + 1} (gold streak +25%)");
			}

			RiftPlan target = depths.Plan.Rifts[rift.Target];
			RoomPlan room = depths.Plan.Rooms[target.Room];
			if (!active) {
				bool midFight = rift.Room == depths.Plan.BossRoom;
				return (true, new Color(110, 110, 120), midFight ? "Sealed during the fight" : "Sealed: clear this room to go on");
			}
			if (room.Kind == RoomKind.Boss)
				return (true, new Color(255, 60, 60), "Guardian's arena");
			bool visited = lp.VisitedRooms.Contains(room.Id);
			string label = DepthsMap.KindRevealed(lp, room) ? DepthsMap.KindName(DepthsMap.ShownKind(lp, room)) : "Unexplored";
			if (depths.RoomSealed(depths.Plan.Rooms[rift.Room]))
				label = "Fall back: " + label;
			return (true, visited ? new Color(140, 255, 170) : new Color(90, 200, 255), label);
		}

		public override void PostUpdateEverything()
		{
			var depths = DepthsSystem.Instance;
			if (!TerralayerWorld.Active)
				return;
			Point forge = TerralayerWorld.Instance.RuneforgePos;
			if (ScreenTiles(20).Contains(forge))
				Lighting.AddLight(new Vector2(forge.X * 16 + 8, (forge.Y - 4) * 16), 0.7f, 0.4f, 1f);
			if (depths.Plan == null)
				return;
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			Rectangle view = ScreenTiles(20);
			foreach (RiftPlan rift in depths.Plan.Rifts) {
				if (!view.Contains(rift.Pos))
					continue;
				var (visible, color, _) = Look(lp, depths, rift);
				if (!visible)
					continue;
				Lighting.AddLight(new Vector2(rift.Pos.X * 16 + 8, (rift.Pos.Y - 2) * 16), color.ToVector3() * 0.9f);
				if (Main.rand.NextBool(6)) {
					Dust d = Dust.NewDustDirect(new Vector2((rift.Pos.X - 1) * 16, (rift.Pos.Y - 4) * 16), 48, 64, Terraria.ID.DustID.MagicMirror, newColor: color);
					d.noGravity = true;
					d.velocity *= 0.3f;
				}
			}
		}

		private static Rectangle ScreenTiles(int margin) =>
			new((int)(Main.screenPosition.X / 16) - margin, (int)(Main.screenPosition.Y / 16) - margin,
				Main.screenWidth / 16 + margin * 2, Main.screenHeight / 16 + margin * 2);

		public override void PostDrawTiles()
		{
			var depths = DepthsSystem.Instance;
			if (!TerralayerWorld.Active)
				return;
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			SpriteBatch sb = Main.spriteBatch;
			Rectangle view = ScreenTiles(6);

			var world = TerralayerWorld.Instance;
			Texture2D tex = riftTexture.Value;
			int frame = (int)(Main.GameUpdateCount / 5 % 6);
			float pulse = 1f + 0.05f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
			string prompt = lp.InteractInRange ? "Right-click" : "Too far away";

			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

			// Homestead: the Runeforge, and (once earned) the secret portal and the Moon arena's way home.
			DrawForge(sb, world.RuneforgePos, view, lp.HoverForge && !lp.InRun, prompt, lp.InteractInRange, "Runeforge (inscribe runes)");
			if (world.AllGuardiansSlain) {
				Point mp = Homestead.MoonPortalPos(world);
				if (view.Contains(mp)) {
					Vector2 at = new Vector2(mp.X * 16 + 8, (mp.Y - 2) * 16) - Main.screenPosition;
					sb.Draw(tex, at, new Rectangle(0, frame * 64, 48, 64), new Color(90, 255, 200) * 0.95f, 0f, new Vector2(24, 32), pulse * 1.6f, SpriteEffects.None, 0f);
					if (lp.HoverMoonPortal)
						Utils.DrawBorderString(sb, $"{prompt}: ???", at - new Vector2(0, 64), lp.InteractInRange ? new Color(90, 255, 200) : Color.Gray, 0.9f, 0.5f, 1f);
				}
			}
			if (world.Moon != TerralayerWorld.MoonState.Idle) {
				Point rp = Homestead.MoonReturnPos(world);
				if (view.Contains(rp)) {
					Vector2 at = new Vector2(rp.X * 16 + 8, (rp.Y - 2) * 16) - Main.screenPosition;
					sb.Draw(tex, at, new Rectangle(0, frame * 64, 48, 64), new Color(255, 215, 70) * 0.9f, 0f, new Vector2(24, 32), pulse, SpriteEffects.None, 0f);
					if (lp.HoverMoonReturn)
						Utils.DrawBorderString(sb, $"{prompt}: Return home", at - new Vector2(0, 48), lp.InteractInRange ? new Color(255, 215, 70) : Color.Gray, 0.9f, 0.5f, 1f);
				}
			}

			// The hidden sea's rifts.
			if (Main.LocalPlayer.GetModPlayer<Secrets.SecretPlayer>().MetAngler && !lp.InRun)
				DrawPortal(sb, tex, Homestead.SeaPortalPos(world), view, frame, pulse, new Color(80, 200, 255), lp.HoverSea, $"{prompt}: The hidden sea", lp.InteractInRange);
			if (world.SeaBuilt)
				DrawPortal(sb, tex, Secrets.OceanArena.ReturnPortal, view, frame, pulse, new Color(255, 215, 70), lp.HoverSeaReturn, $"{prompt}: Return home", lp.InteractInRange);

			if (depths.Plan == null) {
				sb.End();
				return;
			}

			foreach (RoomPlan room in depths.Plan.Rooms) {
				if (!view.Contains(room.Feature))
					continue;
				if (room.Kind == RoomKind.Forge)
					DrawForge(sb, room.Feature, view, lp.HoverForge && lp.InRun, prompt, lp.InteractInRange, "Field Runeforge (inscribe carried gear)");
				if (room.Kind != RoomKind.Shrine || lp.UsedShrines.Contains(room.Id))
					continue;
				ShrineInfo info = Shrines.Get(Shrines.TypeOf(room));
				Vector2 pos = new Vector2(room.Feature.X * 16 + 8, room.Feature.Y * 16 - 16) - Main.screenPosition;
				sb.Draw(shrineTexture.Value, pos, null, info.Color, 0f, new Vector2(24, 40), 1.6f, SpriteEffects.None, 0f);
				Lighting.AddLight(new Vector2(room.Feature.X * 16 + 8, (room.Feature.Y - 4) * 16), info.Color.ToVector3() * 0.6f);
			}

			foreach (RiftPlan rift in depths.Plan.Rifts) {
				if (!view.Contains(rift.Pos))
					continue;
				var (visible, color, _) = Look(lp, depths, rift);
				if (!visible)
					continue;
				Vector2 pos = new Vector2(rift.Pos.X * 16 + 8, (rift.Pos.Y - 2) * 16) - Main.screenPosition;
				sb.Draw(tex, pos, new Rectangle(0, frame * 64, 48, 64), color * 0.9f, 0f, new Vector2(24, 32), pulse, SpriteEffects.None, 0f);
			}

			if (lp.HoverRift != null) {
				var (visible, color, label) = Look(lp, depths, lp.HoverRift);
				if (visible) {
					Vector2 at = new Vector2(lp.HoverRift.Pos.X * 16 + 8, (lp.HoverRift.Pos.Y - 5) * 16) - Main.screenPosition;
					Utils.DrawBorderString(sb, $"{prompt}: {label}", at, lp.InteractInRange ? color : Color.Gray, 0.9f, 0.5f, 1f);
				}
			}
			else if (lp.HoverShrine) {
				RoomPlan room = depths.RoomOf(Main.LocalPlayer);
				if (room != null) {
					ShrineType type = Shrines.TypeOf(room);
					ShrineInfo info = Shrines.Get(type);
					Vector2 at = new Vector2(room.Feature.X * 16 + 8, (room.Feature.Y - 6) * 16) - Main.screenPosition;
					Utils.DrawBorderString(sb, $"{prompt}: {info.Name} - {Shrines.Offer(type, depths.Floor)}", at, lp.InteractInRange ? info.Color : Color.Gray, 0.9f, 0.5f, 1f);
				}
			}

			sb.End();
		}

		private static void DrawPortal(SpriteBatch sb, Texture2D tex, Point p, Rectangle view, int frame, float pulse, Color color, bool hovered, string label, bool inRange)
		{
			if (!view.Contains(p))
				return;
			Vector2 at = new Vector2(p.X * 16 + 8, (p.Y - 2) * 16) - Main.screenPosition;
			sb.Draw(tex, at, new Rectangle(0, frame * 64, 48, 64), color * 0.9f, 0f, new Vector2(24, 32), pulse, SpriteEffects.None, 0f);
			Lighting.AddLight(new Vector2(p.X * 16 + 8, (p.Y - 2) * 16), color.ToVector3() * 0.8f);
			if (hovered)
				Utils.DrawBorderString(sb, label, at - new Vector2(0, 48), inRange ? color : Color.Gray, 0.9f, 0.5f, 1f);
		}

		private void DrawForge(SpriteBatch sb, Point forge, Rectangle view, bool hovered, string prompt, bool inRange, string label)
		{
			if (!view.Contains(forge))
				return;
			Vector2 at = new Vector2(forge.X * 16 + 8, forge.Y * 16) - Main.screenPosition;
			Color light = Lighting.GetColor(forge.X, forge.Y - 2);
			sb.Draw(forgeTexture.Value, at, null, Color.Lerp(light, Color.White, 0.35f), 0f, new Vector2(40, 64), 1f, SpriteEffects.None, 0f);
			Lighting.AddLight(new Vector2(forge.X * 16 + 8, (forge.Y - 4) * 16), 0.7f, 0.4f, 1f);
			if (hovered)
				Utils.DrawBorderString(sb, $"{prompt}: {label}", at - new Vector2(0, 72), inRange ? new Color(230, 180, 255) : Color.Gray, 0.9f, 0.5f, 1f);
		}
	}
}
