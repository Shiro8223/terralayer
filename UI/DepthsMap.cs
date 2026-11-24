using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Players;
using Terralayer.Common.Quests;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace Terralayer.UI
{
	/// <summary>
	/// Terralayer's map, which replaces vanilla's entirely. The Depths are rooms scattered across the world and joined
	/// by rifts, so this draws the room graph. Full screen (the map key) it also carries everything the HUD used to
	/// show: heir, traits, ability, floor progress, boons and quests. In a descent a minimap sits where vanilla's was.
	/// A Riftwalker's Rift Jump opens it to pick a destination.
	/// </summary>
	public static class DepthsMap
	{
		public static (string icon, Color color) Style(RoomKind kind) => kind switch {
			RoomKind.Start => ("S", new Color(120, 220, 120)),
			RoomKind.Ambush => ("!", new Color(255, 140, 60)),
			RoomKind.Treasure => ("$", new Color(255, 210, 60)),
			RoomKind.Shrine => ("+", new Color(230, 90, 120)),
			RoomKind.Merchant => ("M", new Color(90, 220, 230)),
			RoomKind.Forge => ("R", new Color(200, 140, 255)),
			RoomKind.Boss => ("B", new Color(255, 70, 70)),
			_ => ("", new Color(150, 150, 170))
		};

		public static string KindName(RoomKind kind) => kind switch {
			RoomKind.Start => "Entrance",
			RoomKind.Ambush => "Ambush",
			RoomKind.Treasure => "Treasure",
			RoomKind.Shrine => "Shrine",
			RoomKind.Merchant => "Merchant",
			RoomKind.Forge => "Runeforge",
			RoomKind.Boss => "Guardian",
			_ => "Chamber"
		};

		/// <summary>Rooms the player knows about: visited, next to a visited room, or everything with a chart.</summary>
		public static HashSet<int> Known(LegacyPlayer lp, FloorPlan plan)
		{
			if (lp.ChartActive)
				return plan.Rooms.Select(r => r.Id).ToHashSet();
			var known = new HashSet<int>(lp.VisitedRooms);
			foreach (int id in lp.VisitedRooms)
				foreach (int link in plan.Rooms[id].Links)
					if (link >= 0)
						known.Add(link);
			known.Add(plan.StartRoom);
			return known;
		}

		public static bool KindRevealed(LegacyPlayer lp, RoomPlan room) =>
			lp.VisitedRooms.Contains(room.Id) || lp.ChartActive || room.Kind == RoomKind.Boss || lp.Upgrades[(int)UpgradeType.Scouting] > 0;

		/// <summary>
		/// What kind a room looks like. An ambush passes for an ordinary chamber until its second wave springs
		/// (unless a chart or Scouting gave it away).
		/// </summary>
		public static RoomKind ShownKind(LegacyPlayer lp, RoomPlan room)
		{
			if (room.Kind != RoomKind.Ambush || lp.ChartActive || lp.Upgrades[(int)UpgradeType.Scouting] > 0)
				return room.Kind;
			var depths = DepthsSystem.Instance;
			RoomState s = room.Id < depths.Rooms.Length ? depths.Rooms[room.Id] : null;
			return s != null && (s.Sprung || s.Cleared) ? RoomKind.Ambush : RoomKind.Combat;
		}

		// The UI layer has already scaled the screen size for the UI scale.
		private static int ScreenW => UISystem.UIWidth;
		private static int ScreenH => UISystem.UIHeight;

		private static bool Live(LegacyPlayer lp) => !Main.gameMenu && !lp.PendingHeirChoice && !lp.Player.dead && !Main.mapFullscreen;

		// ------------------------------------------------------------------ layout of the room graph

		private class Graph
		{
			public List<RoomPlan> Rooms;
			public HashSet<int> Known;
			public int MinX, MinY, Cw, Ch, Gap;
			public Point Origin;

			public Rectangle Cell(RoomPlan r) => new(Origin.X + (r.Grid.X - MinX) * (Cw + Gap), Origin.Y + (r.Grid.Y - MinY) * (Ch + Gap), Cw, Ch);
		}

		/// <summary>Fits the known rooms into <paramref name="area"/>, cells no bigger than <paramref name="maxCw"/>.</summary>
		private static Graph Fit(LegacyPlayer lp, FloorPlan plan, Rectangle area, int maxCw, bool select, bool alignRight)
		{
			var depths = DepthsSystem.Instance;
			HashSet<int> known = Known(lp, plan);
			if (select)
				for (int i = 0; i < depths.Rooms.Length && i < plan.Rooms.Count; i++)
					if (depths.Rooms[i].Entered)
						known.Add(i);
			var rooms = plan.Rooms.Where(r => known.Contains(r.Id)).ToList();
			var g = new Graph { Rooms = rooms, Known = known };
			g.MinX = rooms.Min(r => r.Grid.X);
			g.MinY = rooms.Min(r => r.Grid.Y);
			int cols = rooms.Max(r => r.Grid.X) - g.MinX + 1;
			int rows = rooms.Max(r => r.Grid.Y) - g.MinY + 1;
			// Cells are 3:2 with a gap of 40% of the width; shrink until the graph fits.
			float byW = area.Width / (cols * 1.4f - 0.4f);
			float byH = area.Height / (rows * (2f / 3f + 0.4f) - 0.4f);
			g.Cw = Math.Max(10, (int)Math.Min(maxCw, Math.Min(byW, byH)));
			g.Ch = g.Cw * 2 / 3;
			g.Gap = (int)(g.Cw * 0.4f);
			int gw = cols * (g.Cw + g.Gap) - g.Gap;
			int gh = rows * (g.Ch + g.Gap) - g.Gap;
			g.Origin = alignRight
				? new Point(area.Right - gw, area.Y)
				: new Point(area.Center.X - gw / 2, area.Center.Y - gh / 2);
			return g;
		}

		private static void DrawGraph(SpriteBatch sb, LegacyPlayer lp, FloorPlan plan, Graph g, bool select, int hovered, bool big)
		{
			var depths = DepthsSystem.Instance;
			RoomPlan current = depths.RoomOf(lp.Player);
			float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f);

			// Corridors first so rooms draw over them.
			foreach (RoomPlan r in g.Rooms)
				for (int d = 0; d < 4; d++) {
					int link = r.Links[d];
					if (link < 0 || link < r.Id && g.Known.Contains(link) || !g.Known.Contains(link))
						continue;
					Draw.Line(sb, g.Cell(r).Center.ToVector2(), g.Cell(plan.Rooms[link]).Center.ToVector2(), new Color(110, 100, 160), big ? 3 : 2);
				}

			foreach (RoomPlan r in g.Rooms) {
				Rectangle rect = g.Cell(r);
				bool visited = lp.VisitedRooms.Contains(r.Id);
				bool revealed = KindRevealed(lp, r);
				RoomKind kind = ShownKind(lp, r);
				(string icon, Color color) = Style(kind);
				RoomState state = r.Id < depths.Rooms.Length ? depths.Rooms[r.Id] : null;
				bool target = select && lp.CanRiftTo(r.Id);

				Color fill = visited ? Color.Lerp(new Color(60, 56, 90), color, 0.35f) : new Color(30, 28, 45);
				if (state != null && state.Locked)
					fill = new Color(110, 40, 40);
				if (select && !target)
					fill *= 0.45f;
				if (target && hovered == r.Id)
					fill = Color.Lerp(fill, new Color(110, 230, 255), 0.5f);
				Draw.Rect(sb, rect, fill);

				Color border = current?.Id == r.Id ? Color.White : visited ? color : new Color(90, 90, 110);
				int thickness = current?.Id == r.Id ? (big ? 3 : 2) : 1;
				if (target) {
					border = new Color(110, 230, 255) * (hovered == r.Id ? 1f : 0.5f + 0.5f * pulse);
					thickness = hovered == r.Id ? 3 : 2;
				}
				Draw.Border(sb, rect, border, thickness);

				string label = revealed ? icon : "?";
				if (state != null && state.Cleared && visited && r.Kind is RoomKind.Combat or RoomKind.Ambush)
					label = big ? "ok" : "";
				if (label.Length > 0 && rect.Height >= 10)
					Utils.DrawBorderString(sb, label, new Vector2(rect.Center.X, rect.Center.Y + 2), revealed ? color : Color.Gray, Math.Clamp(rect.Height / 26f, 0.45f, 1f), 0.5f, 0.5f);
			}
		}

		// ------------------------------------------------------------------ minimap

		/// <summary>Top right, under the life/mana/charge bars, during a descent. Returns its bottom edge (0 if not drawn).</summary>
		public static int DrawMinimap(SpriteBatch sb)
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			var depths = DepthsSystem.Instance;
			FloorPlan plan = depths.Plan;
			if (!Live(lp) || !lp.InRun || plan == null || Main.playerInventory)
				return 0; // (the inventory's equipment slots live where the minimap sits)

			const int width = UISystem.HudWidth;
			int right = UISystem.HudRight;
			var caption = new Rectangle(right - width, 102, width, 40);
			var area = new Rectangle(right - width + 8, caption.Bottom + 6, width - 16, 120);
			Graph g = Fit(lp, plan, area, 22, false, alignRight: false);
			int gw = g.Rooms.Max(r => g.Cell(r).Right) - g.Origin.X;
			int gh = g.Rooms.Max(r => g.Cell(r).Bottom) - g.Origin.Y;
			// Hug the graph, anchored to the top right.
			g.Origin = new Point(right - 8 - gw, area.Y);
			var panel = new Rectangle(caption.X, caption.Y, width, caption.Height + 12 + gh);

			Draw.Rect(sb, panel, new Color(15, 12, 28) * 0.72f);
			Draw.Border(sb, panel, new Color(120, 110, 170) * 0.8f, 2);
			int total = plan.Rooms.Count(r => r.Kind is RoomKind.Combat or RoomKind.Ambush);
			int cleared = total - depths.ChambersRemaining();
			Utils.DrawBorderString(sb, $"Floor {depths.Floor}: {plan.Info.Name}", new Vector2(panel.X + 8, panel.Y + 5), new Color(255, 215, 140), 0.78f);
			string status = depths.Guardian switch {
				GuardianState.Awakening => "[c/FF7850:The guardian awakens!]",
				GuardianState.Fighting => "[c/FF5050:GUARDIAN FIGHT]",
				GuardianState.Defeated => "[c/FFD75A:Guardian slain]",
				_ => depths.RoomSealed(depths.RoomOf(lp.Player)) ? $"Cleared {cleared}/{total}  [c/FF7070:room sealed]" : $"Cleared {cleared}/{total}"
			};
			DrawTagged(sb, status, new Vector2(panel.X + 8, panel.Y + 22), new Color(210, 210, 225), 0.72f);
			DrawGraph(sb, lp, plan, g, false, -1, false);
			Utils.DrawBorderString(sb, "[M] map", new Vector2(panel.Right - 6, panel.Y + 5), new Color(170, 170, 190), 0.62f, 1f);
			return panel.Bottom;
		}

		// ------------------------------------------------------------------ full screen

		/// <summary>The full map with its stats sidebar. In <paramref name="select"/> mode, clicking an explored room Rift Jumps there.</summary>
		public static void DrawFull(SpriteBatch sb, bool select)
		{
			Player player = Main.LocalPlayer;
			var lp = player.GetModPlayer<LegacyPlayer>();
			if (!Live(lp))
				return;
			var depths = DepthsSystem.Instance;
			FloorPlan plan = lp.InRun ? depths.Plan : null;

			var screen = new Rectangle(0, 0, ScreenW, ScreenH);
			Draw.Rect(sb, screen, Color.Black * 0.55f);
			var frame = new Rectangle(40, 40, ScreenW - 80, ScreenH - 80);
			Draw.Rect(sb, frame, new Color(14, 11, 26) * 0.96f);
			Draw.Border(sb, frame, select ? new Color(110, 230, 255) : new Color(130, 118, 185), 2);
			if (frame.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;

			// Header bar
			var header = new Rectangle(frame.X + 2, frame.Y + 2, frame.Width - 4, 44);
			Draw.Rect(sb, header, new Color(32, 27, 54));
			string title = select ? "Rift Jump: choose an explored room"
				: plan != null ? $"Floor {depths.Floor}: {plan.Info.Name}"
				: "The Homestead";
			Utils.DrawBorderString(sb, title, new Vector2(header.X + 16, header.Center.Y + 2), select ? new Color(140, 235, 255) : Color.White, 1.05f, 0f, 0.5f);
			string hint = select
				? $"Click a glowing room   |   [{KeybindSystem.KeyName(KeybindSystem.Ability)}] or [M] to cancel"
				: "[M] or Esc to close";
			Utils.DrawBorderString(sb, hint, new Vector2(header.Right - 16, header.Center.Y + 2), new Color(180, 180, 200), 0.75f, 1f, 0.5f);

			// Sidebar
			const int sideW = 330;
			var side = new Rectangle(frame.Right - sideW - 2, header.Bottom, sideW, frame.Bottom - header.Bottom - 2);
			Draw.Rect(sb, side, new Color(22, 18, 38));
			Draw.Rect(sb, new Rectangle(side.X, side.Y, 2, side.Height), new Color(70, 62, 110));
			DrawStats(sb, lp, new Rectangle(side.X + 14, side.Y + 12, side.Width - 26, side.Height - 20));

			// Map area and legend
			var area = new Rectangle(frame.X + 30, header.Bottom + 26, side.X - frame.X - 60, frame.Bottom - header.Bottom - 26 - 50);
			if (plan == null) {
				Utils.DrawBorderString(sb, "You are home. The floor map fills in as you explore the Depths.", area.Center.ToVector2(), new Color(190, 190, 210), 0.95f, 0.5f, 0.5f);
				Utils.DrawBorderString(sb, $"Deepest floor opened: {TerralayerWorld.Instance.MaxFloorUnlocked}", area.Center.ToVector2() + new Vector2(0, 30), new Color(150, 150, 170), 0.8f, 0.5f, 0.5f);
				return;
			}
			Graph g = Fit(lp, plan, area, 78, select, alignRight: false);
			int hovered = -1;
			Point mouse = Main.MouseScreen.ToPoint();
			foreach (RoomPlan r in g.Rooms)
				if (g.Cell(r).Contains(mouse))
					hovered = r.Id;
			DrawGraph(sb, lp, plan, g, select, hovered, true);

			string legend = "S entrance   ! ambush   $ treasure   + shrine   R runeforge   M merchant   B guardian   ? unexplored   red = sealed";
			Utils.DrawBorderString(sb, legend, new Vector2((frame.X + side.X) / 2f, frame.Bottom - 26), new Color(170, 170, 195), 0.7f, 0.5f, 0.5f);

			if (hovered >= 0) {
				RoomPlan h = plan.Rooms[hovered];
				if (select)
					Main.hoverItemName = lp.CanRiftTo(hovered)
						? $"Rift Jump to: {(KindRevealed(lp, h) ? KindName(ShownKind(lp, h)) : "explored room")}"
						: depths.RoomOf(player)?.Id == hovered ? "You are here" : "Not explored yet";
				else
					Main.hoverItemName = KindRevealed(lp, h) ? KindName(ShownKind(lp, h)) : "Unexplored";

				// Either mouse button picks the room (everything else in Terralayer is right-click).
				bool click = Main.mouseLeft && Main.mouseLeftRelease || Main.mouseRight && Main.mouseRightRelease;
				if (select && click && lp.CanRiftTo(hovered)) {
					Main.mouseLeftRelease = false;
					Main.mouseRightRelease = false;
					lp.CompleteRiftJump(hovered);
				}
			}
		}

		/// <summary>The sidebar: what the top-of-screen HUD used to show.</summary>
		private static void DrawStats(SpriteBatch sb, LegacyPlayer lp, Rectangle box)
		{
			float y = box.Y;
			void Line(string text, Color color, float scale = 0.8f, float gap = 4f)
			{
				foreach (string l in Wrap(text, box.Width, scale)) {
					DrawTagged(sb, l, new Vector2(box.X, y), color, scale);
					y += 22 * scale / 0.8f;
				}
				y += gap;
			}
			void Heading(string text)
			{
				y += 6;
				Utils.DrawBorderString(sb, text, new Vector2(box.X, y), new Color(255, 215, 120), 0.85f);
				y += 22;
				Draw.Rect(sb, new Rectangle(box.X, (int)y - 3, box.Width, 1), new Color(255, 215, 120) * 0.35f);
				y += 2;
			}

			if (lp.Heir != null) {
				ClassInfo info = lp.ClassInfo;
				Line(lp.HeirTitle, info.Color, 1f);
				Line($"{lp.Heir.ClassLabel}, generation {lp.Generation}", new Color(210, 210, 220));
				foreach (HeirTrait t in lp.Traits) {
					TraitInfo ti = TraitDefs.Get(t);
					Line($"{ti.Name}: {ti.Description}", ti.Positive ? new Color(140, 230, 140) : new Color(240, 140, 120), 0.72f, 1f);
				}
				if (lp.Heir.GoldBonus > 0)
					Line($"Gold found +{lp.Heir.GoldBonus * 100:0}%", new Color(255, 210, 74), 0.72f);
				ClassInfo ab = lp.AbilityInfo;
				Line($"[{KeybindSystem.KeyName(KeybindSystem.Ability)}] {ab.AbilityName}: {(int)lp.Charge}% charge", ab.Color, 0.76f);
			}

			var depths = DepthsSystem.Instance;
			if (lp.InRun && depths.Plan != null) {
				Heading("This descent");
				int total = depths.Plan.Rooms.Count(r => r.Kind is RoomKind.Combat or RoomKind.Ambush);
				Line($"Chambers cleared: {total - depths.ChambersRemaining()}/{total}", new Color(220, 220, 230), 0.76f, 1f);
				Line($"Gold x{lp.GoldFind * depths.StreakMultiplier:0.00}" + (depths.RunLifeMult > 1.001f ? $"   Foes x{depths.RunLifeMult:0.00} life" : ""), new Color(255, 210, 120), 0.76f, 1f);
				string boons = Boons.Summary(lp.RunBoons);
				Line("Boons: " + (boons.Length > 0 ? boons : "none yet"), new Color(255, 215, 140), 0.74f);
			}

			var qp = lp.Player.GetModPlayer<QuestPlayer>();
			Heading($"Quests ({qp.Active.Count}/{QuestDefs.MaxActive})");
			if (qp.Active.Count == 0)
				Line("None accepted. Visit the Herald on the plaza.", new Color(170, 170, 185), 0.74f);
			foreach (Quest q in qp.Active) {
				string mark = q.Complete ? "  [c/78FF8C:done - claim it]" : QuestDefs.Target(q) > 1 ? $"  {q.Progress}/{QuestDefs.Target(q)}" : "";
				Line($"{new string('*', q.Stars)} {QuestDefs.Title(q)}{mark}", QuestDefs.StarColor(q.Stars), 0.74f, 1f);
			}

			y = Math.Max(y + 6, box.Bottom - 24);
			DrawTagged(sb, Coins.PurseAndSafe(lp.Player), new Vector2(box.X, y), Color.White, 0.76f);
		}

		// ------------------------------------------------------------------ text helpers

		private static void DrawTagged(SpriteBatch sb, string text, Vector2 at, Color color, float scale)
		{
			var snippets = ChatManager.ParseMessage(text, color).ToArray();
			ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, snippets, at, 0f, Vector2.Zero, new Vector2(scale), out _);
		}

		/// <summary>Word-wraps text; a [c/RRGGBB:...] colour tag is kept whole so it never splits across lines.</summary>
		private static IEnumerable<string> Wrap(string text, float width, float scale)
		{
			var font = FontAssets.MouseText.Value;
			string line = "";
			var words = System.Text.RegularExpressions.Regex.Matches(text, @"\[c/[0-9A-Fa-f]{6}:[^\]]*\]|\S+").Select(m => m.Value);
			foreach (string word in words) {
				string attempt = line.Length == 0 ? word : line + " " + word;
				if (line.Length > 0 && font.MeasureString(StripTags(attempt)).X * scale > width) {
					yield return line;
					line = word;
				}
				else
					line = attempt;
			}
			if (line.Length > 0)
				yield return line;
		}

		private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\[c/[0-9A-Fa-f]{6}:([^\]]*)\]", "$1");
	}
}
