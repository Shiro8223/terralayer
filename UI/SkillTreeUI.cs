using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Heirs;
using Terralayer.Common.Players;
using Terralayer.Common.Systems;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terralayer.UI
{
	/// <summary>
	/// The family skill tree, in two pages. Upgrades: five branches of permanent bonuses. Classes: six paths of
	/// armor-set classes; each tier needs its parent class, that floor's guardian trophy, and ever more gold.
	/// </summary>
	public class SkillTreeUI : UIState
	{
		private UIPanel root;
		private RichText purse;
		private SkillTreeCanvas canvas;
		private TextButton upgradesTab, classesTab;

		public override void OnInitialize()
		{
			root = UIFactory.Panel(1140, 660);
			Append(root);

			var title = new UIText("Family Skill Tree", 0.6f, large: true);
			title.Top.Set(2, 0);
			root.Append(title);

			upgradesTab = new TextButton("Upgrades", () => SetPage(false), 0.85f);
			upgradesTab.Left.Set(360, 0);
			upgradesTab.Width.Set(110, 0);
			upgradesTab.Height.Set(34, 0);
			root.Append(upgradesTab);

			classesTab = new TextButton("Classes", () => SetPage(true), 0.85f);
			classesTab.Left.Set(476, 0);
			classesTab.Width.Set(110, 0);
			classesTab.Height.Set(34, 0);
			root.Append(classesTab);

			purse = new RichText("", 0.85f, Color.White, false);
			purse.Top.Set(10, 0);
			purse.Left.Set(600, 0);
			root.Append(purse);

			var close = new TextButton("Close", () => ModContent.GetInstance<UISystem>().CloseAll());
			close.Width.Set(90, 0);
			close.Height.Set(34, 0);
			close.HAlign = 1f;
			root.Append(close);

			canvas = new SkillTreeCanvas();
			canvas.Top.Set(46, 0);
			canvas.Width.Set(0, 1f);
			canvas.Height.Set(-46, 1f);
			root.Append(canvas);
			SetPage(false);
		}

		public void SetPage(bool classes)
		{
			canvas.Classes = classes;
			upgradesTab.WithEnabled(classes);
			classesTab.WithEnabled(!classes);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			purse.SetText(Coins.PurseAndSafe(Main.LocalPlayer));
			UIFactory.CaptureMouse(root);
		}
	}

	public class SkillTreeCanvas : UIElement
	{
		public bool Classes;
		private bool leftWasHeld;

		// ---- Upgrades page layout
		private const int NodeSize = 58;
		private const int ColSpacing = 176;
		private const int RowSpacing = 150;

		// ---- Classes page layout
		private const int ClassNode = 46;
		private const int ClassRow = 88;

		private Vector2 UpgradeCenter(int col, int row)
		{
			CalculatedStyle d = GetDimensions();
			float left = d.X + (d.Width - ColSpacing * (Upgrades.Branches.Length - 1)) / 2f;
			return new Vector2(left + col * ColSpacing, d.Y + 96 + row * RowSpacing);
		}

		/// <summary>Columns spread to fit however many paths there are.</summary>
		private float ClassCol => Math.Min(150f, (GetDimensions().Width - 110f) / ((int)HeirClass.Count - 1));

		private Vector2 ClassCenter(ClassKit k)
		{
			CalculatedStyle d = GetDimensions();
			float col = ClassCol;
			float left = d.X + 30f + (d.Width - 30f - col * ((int)HeirClass.Count - 1)) / 2f;
			return new Vector2(left + k.Col * col, d.Y + 64 + k.Tier * ClassRow);
		}

		private static bool ParentOk(LegacyPlayer lp, UpgradeInfo info) => info.Parent < 0 || lp.Upgrades[info.Parent] > 0;

		private static string KitBlocker(LegacyPlayer lp, ClassKit k)
		{
			if (lp.UnlockedKits.Contains(k.Key))
				return null;
			if (k.Path == HeirClass.Fishron && !lp.Player.GetModPlayer<Common.Secrets.SecretPlayer>().DukeSlain)
				return "A secret class. ???";
			if (k.Parent != null && !lp.UnlockedKits.Contains(k.Parent))
				return $"Requires the {ClassKits.Get(k.Parent).Name} class";
			int floor = ClassKits.RequiredFloor(k);
			if (floor > 0 && !lp.Trophies.Contains(floor))
				return $"Requires the trophy from floor {floor}'s guardian";
			return "";
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			bool leftHeld = Main.mouseLeft;
			bool clicked = leftHeld && !leftWasHeld;
			leftWasHeld = leftHeld;
			if (!clicked || !ContainsPoint(Main.MouseScreen))
				return;

			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			if (Classes) {
				foreach (ClassKit k in ClassKits.All)
					if (Vector2.Distance(Main.MouseScreen, ClassCenter(k)) <= ClassNode / 2f) {
						TryUnlock(lp, k);
						return;
					}
				return;
			}
			for (var t = (UpgradeType)0; t < UpgradeType.Count; t++) {
				UpgradeInfo info = Upgrades.Get(t);
				if (Vector2.Distance(Main.MouseScreen, UpgradeCenter(info.Col, info.Row)) <= NodeSize / 2f) {
					TryBuy(lp, t, info);
					return;
				}
			}
		}

		private static void Fail(string why)
		{
			Main.NewText(why, Color.OrangeRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		private static void TryUnlock(LegacyPlayer lp, ClassKit k)
		{
			string blocker = KitBlocker(lp, k);
			if (blocker == null) {
				Fail($"{k.Name} is already unlocked.");
				return;
			}
			if (blocker != "") {
				Fail(blocker + ".");
				return;
			}
			if (!Main.LocalPlayer.BuyItem(k.Cost)) {
				Fail("You cannot afford that.");
				return;
			}
			lp.UnlockedKits.Add(k.Key);
			SoundEngine.PlaySound(SoundID.Item37);
			SoundEngine.PlaySound(SoundID.Coins);
			Main.NewText($"The {k.Name} class is unlocked! Future heirs may be {k.Name}s.", ClassDefs.Get(k.Path).Color);
		}

		private static void TryBuy(LegacyPlayer lp, UpgradeType t, UpgradeInfo info)
		{
			int level = lp.Upgrades[(int)t];
			if (level >= info.MaxLevel)
				Fail("Already mastered.");
			else if (!TerralayerWorld.Instance.HasBuilt(info.Structure))
				Fail($"Build the {Structures.Get(info.Structure).Name} first.");
			else if (!ParentOk(lp, info))
				Fail($"Learn {Upgrades.Get((UpgradeType)info.Parent).Name} first.");
			else if (!Main.LocalPlayer.BuyItem(Upgrades.CostFor(t, level)))
				Fail("You cannot afford that.");
			else {
				lp.Upgrades[(int)t]++;
				SoundEngine.PlaySound(SoundID.Item37);
				SoundEngine.PlaySound(SoundID.Coins);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			string hover = Classes ? DrawClasses(sb, lp) : DrawUpgrades(sb, lp);
			if (hover != null)
				Main.hoverItemName = hover;
		}

		private static void Icon(SpriteBatch sb, int item, Vector2 at, float size, bool lit)
		{
			if (item <= 0)
				return;
			Main.instance.LoadItem(item);
			Texture2D tex = TextureAssets.Item[item].Value;
			float scale = Math.Min(1f, size / Math.Max(tex.Width, tex.Height));
			sb.Draw(tex, at, null, lit ? Color.White : Color.Gray * 0.55f, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		private string DrawUpgrades(SpriteBatch sb, LegacyPlayer lp)
		{
			var world = TerralayerWorld.Instance;
			for (int c = 0; c < Upgrades.Branches.Length; c++)
				Utils.DrawBorderString(sb, Upgrades.Branches[c], UpgradeCenter(c, 0) - new Vector2(0, 64), new Color(255, 215, 120), 1f, 0.5f, 0.5f);

			for (var t = (UpgradeType)0; t < UpgradeType.Count; t++) {
				UpgradeInfo info = Upgrades.Get(t);
				if (info.Parent < 0)
					continue;
				UpgradeInfo parent = Upgrades.Get((UpgradeType)info.Parent);
				bool lit = lp.Upgrades[info.Parent] > 0;
				global::Terralayer.UI.Draw.Line(sb, UpgradeCenter(parent.Col, parent.Row) + new Vector2(0, NodeSize / 2f), UpgradeCenter(info.Col, info.Row) - new Vector2(0, NodeSize / 2f), lit ? new Color(230, 190, 90) : new Color(70, 70, 90), lit ? 4 : 3);
			}

			string hover = null;
			for (var t = (UpgradeType)0; t < UpgradeType.Count; t++) {
				UpgradeInfo info = Upgrades.Get(t);
				int level = lp.Upgrades[(int)t];
				bool built = world.HasBuilt(info.Structure);
				bool parentOk = ParentOk(lp, info);
				bool maxed = level >= info.MaxLevel;
				bool available = built && parentOk && !maxed;
				Vector2 c = UpgradeCenter(info.Col, info.Row);
				var rect = new Rectangle((int)(c.X - NodeSize / 2f), (int)(c.Y - NodeSize / 2f), NodeSize, NodeSize);
				Color border = maxed ? new Color(110, 230, 110) : level > 0 ? new Color(100, 160, 255) : available ? new Color(255, 205, 80) : new Color(70, 70, 85);
				global::Terralayer.UI.Draw.Rect(sb, rect, built && parentOk ? new Color(40, 38, 64) : new Color(22, 20, 30));
				if (level > 0) {
					int h = rect.Height * level / info.MaxLevel;
					global::Terralayer.UI.Draw.Rect(sb, new Rectangle(rect.X, rect.Bottom - h, rect.Width, h), border * 0.25f);
				}
				global::Terralayer.UI.Draw.Border(sb, rect, border, available || level > 0 ? 3 : 2);
				Icon(sb, info.Icon, c, 38f, built && parentOk);
				Utils.DrawBorderString(sb, info.Name, c + new Vector2(0, NodeSize / 2f + 12), built ? Color.White : Color.Gray, 0.75f, 0.5f, 0.5f);
				Utils.DrawBorderString(sb, $"{level}/{info.MaxLevel}", c + new Vector2(0, NodeSize / 2f + 30), border, 0.75f, 0.5f, 0.5f);

				if (rect.Contains(Main.MouseScreen.ToPoint())) {
					string status = maxed ? "Mastered"
						: !built ? $"Requires the {Structures.Get(info.Structure).Name}"
						: !parentOk ? $"Requires {Upgrades.Get((UpgradeType)info.Parent).Name}"
						: $"Next level: {Coins.FormatPlain(Upgrades.CostFor(t, level))} (click to buy)";
					hover = $"{info.Name} ({level}/{info.MaxLevel})\n{info.Description}\n{status}";
				}
			}
			return hover;
		}

		private string DrawClasses(SpriteBatch sb, LegacyPlayer lp)
		{
			for (int p = 0; p < (int)HeirClass.Count; p++) {
				ClassInfo info = ClassDefs.Get((HeirClass)p);
				ClassKit starter = ClassKits.Starter((HeirClass)p);
				Utils.DrawBorderString(sb, info.Name, ClassCenter(starter) - new Vector2(0, 44), info.Color, 0.95f, 0.5f, 0.5f);
			}
			CalculatedStyle d = GetDimensions();
			for (int tier = 1; tier <= 5; tier++)
				Utils.DrawBorderString(sb, $"Floor {tier}", new Vector2(d.X + 14, d.Y + 64 + tier * ClassRow), new Color(150, 150, 170), 0.7f, 0f, 0.5f);

			foreach (ClassKit k in ClassKits.All) {
				if (k.Parent == null)
					continue;
				bool lit = lp.UnlockedKits.Contains(k.Parent);
				global::Terralayer.UI.Draw.Line(sb, ClassCenter(ClassKits.Get(k.Parent)) + new Vector2(0, ClassNode / 2f), ClassCenter(k) - new Vector2(0, ClassNode / 2f), lit ? new Color(230, 190, 90) : new Color(70, 70, 90), 3);
			}

			string hover = null;
			foreach (ClassKit k in ClassKits.All) {
				string blocker = KitBlocker(lp, k);
				bool owned = blocker == null;
				bool available = blocker == "";
				Vector2 c = ClassCenter(k);
				var rect = new Rectangle((int)(c.X - ClassNode / 2f), (int)(c.Y - ClassNode / 2f), ClassNode, ClassNode);
				Color border = owned ? new Color(110, 230, 110) : available ? new Color(255, 205, 80) : new Color(70, 70, 85);
				global::Terralayer.UI.Draw.Rect(sb, rect, owned || available ? new Color(40, 38, 64) : new Color(22, 20, 30));
				global::Terralayer.UI.Draw.Border(sb, rect, border, owned || available ? 3 : 2);
				int icon = k.Armor[1] > 0 ? k.Armor[1] : k.Weapon;
				Icon(sb, icon, c, 32f, owned || available);
				Utils.DrawBorderString(sb, k.Name, c + new Vector2(0, ClassNode / 2f + 10), owned ? Color.White : Color.Gray, 0.62f, 0.5f, 0.5f);

				if (rect.Contains(Main.MouseScreen.ToPoint())) {
					string armor = string.Join(", ", k.Armor.Where(a => a > 0).Select(a => Lang.GetItemNameValue(a)));
					string accs = k.Accessories.Length > 0 ? "\nAccessories: " + string.Join(", ", k.Accessories.Select(a => Lang.GetItemNameValue(a))) : "";
					string status = owned ? "Unlocked" : available ? $"Unlock: {Coins.FormatPlain(k.Cost)} (click)" : blocker;
					ClassInfo path = ClassDefs.Get(k.Path);
					string weapons = string.Join(" / ", ClassKits.VariantsOf(k).Select(v => v.Name == k.Name ? Lang.GetItemNameValue(v.Weapon) : $"{Lang.GetItemNameValue(v.Weapon)} ({v.Name})"));
					hover = $"{k.Name} ({path.Name} path, tier {k.Tier})\nArmor (no set bonus in Terralayer): {armor}{accs}\nStarts with: {weapons}\nPassive: {path.Passive}\nAbility: {path.AbilityName} - {path.AbilityDescription}\n{status}";
				}
			}
			return hover;
		}
	}
}
