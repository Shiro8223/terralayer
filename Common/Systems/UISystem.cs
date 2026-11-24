using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Players;
using Terralayer.Common.Quests;
using Terralayer.Common.Utilities;
using Terraria.ID;
using Terralayer.Common.World;
using Terralayer.UI;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terralayer.Common.Systems
{
	public class KeybindSystem : ModSystem
	{
		public static ModKeybind Ability { get; private set; }
		public static ModKeybind Estate { get; private set; }
		public static ModKeybind Map { get; private set; }

		public override void Load()
		{
			Ability = KeybindLoader.RegisterKeybind(Mod, "ClassAbility", "F");
			Estate = KeybindLoader.RegisterKeybind(Mod, "OpenEstate", "U");
			Map = KeybindLoader.RegisterKeybind(Mod, "DepthsMap", "N");
		}

		public override void Unload()
		{
			Ability = null;
			Estate = null;
			Map = null;
		}

		public static string KeyName(ModKeybind bind) => bind?.GetAssignedKeys().FirstOrDefault() ?? "unbound";
	}

	/// <summary>Terralayer's screens and HUD. Replaces vanilla's life/mana display with its own bars in Terralayer worlds.</summary>
	[Autoload(Side = ModSide.Client)]
	public class UISystem : ModSystem
	{
		private UserInterface heirInterface;
		private UserInterface screenInterface; // Estate, Skill Tree or Runeforge: one at a time
		private UserInterface wipeInterface;
		private HeirSelectUI heirUI;
		private EstateUI estateUI;
		private SkillTreeUI skillTreeUI;
		private RuneforgeUI runeforgeUI;
		private WipeWarningUI wipeUI;
		private UserInterface evolveInterface;
		private EvolveUI evolveUI;
		private UserInterface rewardInterface;
		private RoomRewardUI rewardUI;
		private QuestUI questUI;
		private SmithUI smithUI;
		private bool heirShownDead;
		public bool BigMap;

		/// <summary>Terralayer's full-screen map is up (opened with the map key, or by Rift Jump).</summary>
		public bool MapOpen => BigMap || Main.LocalPlayer.GetModPlayer<LegacyPlayer>().RiftSelecting;

		/// <summary>The heir choice is up as the death screen.</summary>
		public bool DeathScreenUp => heirInterface?.CurrentState != null && heirUI.DeathMode;

		public override void Load()
		{
			heirUI = new HeirSelectUI();
			heirUI.Activate();
			estateUI = new EstateUI();
			estateUI.Activate();
			skillTreeUI = new SkillTreeUI();
			skillTreeUI.Activate();
			runeforgeUI = new RuneforgeUI();
			runeforgeUI.Activate();
			questUI = new QuestUI();
			questUI.Activate();
			smithUI = new SmithUI();
			smithUI.Activate();
			wipeUI = new WipeWarningUI();
			wipeUI.Activate();
			evolveUI = new EvolveUI();
			evolveUI.Activate();
			evolveInterface = new UserInterface();
			rewardUI = new RoomRewardUI();
			rewardUI.Activate();
			rewardInterface = new UserInterface();
			heirInterface = new UserInterface();
			screenInterface = new UserInterface();
			wipeInterface = new UserInterface();

			// Vanilla's fullscreen map never opens in a Terralayer world: its key opens Terralayer's map instead.
			On_Player.TryOpeningFullscreenMap += (orig, self) => {
				if (TerralayerWorld.Active && self.whoAmI == Main.myPlayer && !Main.gameMenu)
					ToggleMap();
				else
					orig(self);
			};
		}

		public bool ScreenOpen => screenInterface?.CurrentState != null;

		private bool CanOpenScreen(bool allowInRun = false)
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			if (lp.InRun && !allowInRun) {
				Main.NewText("That can only be done back at the homestead.", Color.Gray);
				return false;
			}
			return !lp.PendingHeirChoice && !lp.PendingWipeConfirm;
		}

		public void CloseAll() => screenInterface?.SetState(null);

		public void ToggleEstate()
		{
			if (screenInterface.CurrentState == estateUI)
				CloseAll();
			else
				OpenEstate();
		}

		public void OpenEstate()
		{
			if (!CanOpenScreen())
				return;
			estateUI.Rebuild();
			screenInterface.SetState(estateUI);
		}

		public void OpenSkillTree()
		{
			if (CanOpenScreen())
				screenInterface.SetState(skillTreeUI);
		}

		public void OpenRuneforge(bool field)
		{
			if (!CanOpenScreen(allowInRun: field))
				return;
			runeforgeUI.Open(field);
			screenInterface.SetState(runeforgeUI);
		}

		public void OpenSmith()
		{
			if (!CanOpenScreen())
				return;
			smithUI.Open();
			screenInterface.SetState(smithUI);
		}

		public void OpenQuestBoard()
		{
			if (!CanOpenScreen())
				return;
			questUI.Rebuild();
			screenInterface.SetState(questUI);
		}

		public void ToggleMap()
		{
			var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
			if (lp.RiftSelecting) {
				lp.RiftSelecting = false;
				return;
			}
			if (!BigMap && (lp.PendingHeirChoice || Main.LocalPlayer.dead))
				return;
			BigMap = !BigMap;
			Terraria.Audio.SoundEngine.PlaySound(BigMap ? Terraria.ID.SoundID.MenuOpen : Terraria.ID.SoundID.MenuClose);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.gameMenu || !TerralayerWorld.Active)
				return;

			Player player = Main.LocalPlayer;
			var lp = player.GetModPlayer<LegacyPlayer>();

			Toggle(wipeInterface, wipeUI, lp.PendingWipeConfirm);

			// After a death the heir choice *is* the death screen; otherwise it's the plain picker.
			bool wantHeir = !lp.PendingWipeConfirm && lp.PendingHeirChoice;
			if (wantHeir && (heirInterface.CurrentState == null || heirShownDead != player.dead)) {
				lp.EnsureHeirOptions();
				heirShownDead = player.dead;
				heirUI.Rebuild(lp, player.dead);
				CloseAll();
			}
			Toggle(heirInterface, heirUI, wantHeir);

			// Anything that still opens vanilla's fullscreen map (the inventory's map button) opens ours instead.
			if (Main.mapFullscreen) {
				Main.mapFullscreen = false;
				BigMap = true;
			}
			// Esc / the inventory key closes the map rather than opening the inventory.
			if (MapOpen && Main.playerInventory) {
				Main.playerInventory = false;
				BigMap = false;
				lp.RiftSelecting = false;
			}
			if (MapOpen && (player.dead || lp.PendingHeirChoice || ScreenOpen)) {
				BigMap = false;
				lp.RiftSelecting = false;
			}

			if (ScreenOpen && (player.dead || lp.InRun && screenInterface.CurrentState != runeforgeUI))
				CloseAll();
			Toggle(evolveInterface, evolveUI, lp.PendingEvolutionSlot >= 0 && !player.dead);
			evolveInterface.Update(gameTime);
			// The evolution popup takes the same spot, so a reward waits until it's answered.
			Toggle(rewardInterface, rewardUI, lp.RewardOffered && !player.dead && lp.PendingEvolutionSlot < 0);
			rewardInterface.Update(gameTime);

			wipeInterface.Update(gameTime);
			heirInterface.Update(gameTime);
			screenInterface.Update(gameTime);
		}

		private static void Toggle(UserInterface ui, UIState state, bool want)
		{
			if (want && ui.CurrentState == null)
				ui.SetState(state);
			else if (!want && ui.CurrentState != null)
				ui.SetState(null);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (!TerralayerWorld.Active)
				return;

			// Terralayer's map replaces vanilla's: no minimap, no fullscreen map.
			foreach (GameInterfaceLayer layer in layers)
				if (layer.Name == "Vanilla: Map / Minimap")
					layer.Active = false;

			// The death screen replaces vanilla's "You were slain..." and clears the clutter around it.
			if (DeathScreenUp)
				foreach (GameInterfaceLayer layer in layers)
					if (layer.Name is "Vanilla: Death Text" or "Vanilla: Map / Minimap" or "Vanilla: Info Accessories Bar" or "Vanilla: Builder Accessories Bar" or "Vanilla: Hotbar")
						layer.Active = false;

			int bars = layers.FindIndex(l => l.Name == "Vanilla: Resource Bars");
			if (bars >= 0) {
				layers[bars].Active = false;
				layers.Insert(bars, new LegacyGameInterfaceLayer("Terralayer: Resource Bars", () => {
					DrawResources();
					return true;
				}, InterfaceScaleType.UI));
			}

			int health = layers.FindIndex(l => l.Name == "Vanilla: Entity Health Bars");
			if (health >= 0) {
				layers.Insert(health, new LegacyGameInterfaceLayer("Terralayer: Charge Bar", () => {
					DrawChargeUnderPlayer();
					return true;
				}, InterfaceScaleType.Game));
			}

			int mouse = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (mouse >= 0) {
				layers.Insert(mouse, new LegacyGameInterfaceLayer("Terralayer: Screens", () => {
					var lp = Main.LocalPlayer.GetModPlayer<LegacyPlayer>();
					if (!DeathScreenUp) {
						if (MapOpen)
							DepthsMap.DrawFull(Main.spriteBatch, lp.RiftSelecting);
						else {
							int bottom = DepthsMap.DrawMinimap(Main.spriteBatch);
							DrawPurse(Math.Max(bottom, 96) + 8);
						}
					}
					screenInterface.Draw(Main.spriteBatch, new GameTime());
					evolveInterface.Draw(Main.spriteBatch, new GameTime());
					rewardInterface.Draw(Main.spriteBatch, new GameTime());
					heirInterface.Draw(Main.spriteBatch, new GameTime());
					wipeInterface.Draw(Main.spriteBatch, new GameTime());
					return true;
				}, InterfaceScaleType.UI));
			}
		}

		/// <summary>Life, mana and ability charge as bars, top right.</summary>
		// The bars ease toward their values instead of snapping, so a max-mana or max-life change (equipping gear)
		// reads as the bar growing rather than jumping about.
		private static float shownLife = -1f, shownMana = -1f;

		/// <summary>Right edge shared by the bars, the minimap and the purse. (UI layers already work in UI-scaled space.)</summary>
		public static int HudRight => UIWidth - 24;

		/// <summary>The screen's width in UI-scaled pixels, from the real resolution (Main.screenWidth depends on zoom state).</summary>
		public static int UIWidth => (int)(Terraria.GameInput.PlayerInput.RealScreenWidth / Main.UIScale);

		public static int UIHeight => (int)(Terraria.GameInput.PlayerInput.RealScreenHeight / Main.UIScale);

		public const int HudWidth = 270;

		/// <summary>Life, mana and ability charge as bars, top right. Hover the ability bar to see what it does.</summary>
		private static void DrawResources()
		{
			Player p = Main.LocalPlayer;
			var lp = p.GetModPlayer<LegacyPlayer>();
			if (p.dead)
				return;
			SpriteBatch sb = Main.spriteBatch;
			int x = HudRight - HudWidth;
			int y = 18;
			const int h = 18;
			Point mouse = Main.MouseScreen.ToPoint();

			int lifeMax = Math.Max(1, p.statLifeMax2);
			int manaMax = Math.Max(1, p.statManaMax2);
			int life = Math.Clamp(p.statLife, 0, lifeMax);
			int mana = Math.Clamp(p.statMana, 0, manaMax);
			float lifeFrac = life / (float)lifeMax, manaFrac = mana / (float)manaMax;
			shownLife = shownLife < 0 ? lifeFrac : MathHelper.Lerp(shownLife, lifeFrac, 0.18f);
			shownMana = shownMana < 0 ? manaFrac : MathHelper.Lerp(shownMana, manaFrac, 0.18f);

			var lifeBar = new Rectangle(x, y, HudWidth, h);
			var manaBar = new Rectangle(x, y + h + 5, HudWidth, h);
			Draw.Bar(sb, lifeBar, shownLife, new Color(200, 40, 50), new Color(50, 10, 15) * 0.9f, $"Life {life} / {lifeMax}");
			Draw.Bar(sb, manaBar, shownMana, new Color(50, 90, 220), new Color(10, 15, 50) * 0.9f, $"Mana {mana} / {manaMax}");
			if (lifeBar.Contains(mouse))
				Main.hoverItemName = "Life doesn't regenerate on its own here.\nRestore it with restoration rewards from cleared rooms, potions and regeneration effects.";

			if (lp.Heir != null) {
				ClassInfo info = lp.AbilityInfo;
				float pulse = lp.AbilityOn ? 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) : 1f;
				Color fill = lp.AbilityOn ? new Color(255, 150, 40) * pulse : new Color(230, 190, 60);
				string state = lp.AbilityOn ? "ACTIVE" : info.Kind == AbilityKind.Burst ? $"{(int)(lp.Charge / info.Cost)} uses" : lp.Charge >= ClassDefs.SustainMinimum ? "ready" : "charging";
				var bar = new Rectangle(x, y + (h + 5) * 2, HudWidth, h);
				Draw.Bar(sb, bar, lp.Charge / LegacyPlayer.MaxCharge, fill, new Color(45, 35, 10) * 0.9f,
					$"[{KeybindSystem.KeyName(KeybindSystem.Ability)}] {info.AbilityName}: {(int)lp.Charge}% ({state})");
				if (bar.Contains(mouse)) {
					string cost = info.Kind == AbilityKind.Sustained
						? $"Toggle: drains {info.Cost:0} charge a second while on (needs {ClassDefs.SustainMinimum:0} to start)"
						: $"Costs {info.Cost:0} charge";
					Main.hoverItemName = $"{info.AbilityName} ({info.Name} path)\n{info.AbilityDescription}\n{cost}. Charge builds as you hit enemies; restoration rewards refill it.";
				}
			}
		}

		/// <summary>The money panel: purse and safe as coin stacks, under the minimap (or the bars at home).</summary>
		private static void DrawPurse(int top)
		{
			Player p = Main.LocalPlayer;
			if (p.dead || Main.playerInventory)
				return;
			SpriteBatch sb = Main.spriteBatch;
			long purse = Coins.InPurse(p), safe = Coins.InBanks(p);
			int rows = safe > 0 ? 2 : 1;
			var panel = new Rectangle(HudRight - HudWidth, top, HudWidth, 8 + rows * 24);
			Draw.Rect(sb, panel, new Color(28, 22, 14) * 0.78f);
			Draw.Border(sb, panel, new Color(200, 165, 80) * 0.8f, 2);
			CoinRow(sb, "Purse", purse, new Vector2(panel.X + 8, panel.Y + 6));
			if (safe > 0)
				CoinRow(sb, "Safe", safe, new Vector2(panel.X + 8, panel.Y + 30));
		}

		private static void CoinRow(SpriteBatch sb, string label, long copper, Vector2 at)
		{
			Utils.DrawBorderString(sb, label, at + new Vector2(0, 2), new Color(230, 210, 160), 0.75f);
			float x = at.X + 56;
			(int item, long amount)[] coins = {
				(ItemID.PlatinumCoin, copper / Coins.Platinum), (ItemID.GoldCoin, copper / Coins.Gold % 100),
				(ItemID.SilverCoin, copper / Coins.Silver % 100), (ItemID.CopperCoin, copper % 100),
			};
			foreach ((int item, long amount) in coins) {
				Main.instance.LoadItem(item);
				Texture2D tex = Terraria.GameContent.TextureAssets.Item[item].Value;
				Rectangle frame = Main.itemAnimations[item] != null ? Main.itemAnimations[item].GetFrame(tex) : tex.Frame();
				float alpha = amount > 0 ? 1f : 0.35f;
				sb.Draw(tex, new Vector2(x + 8, at.Y + 9), frame, Color.White * alpha, 0f, frame.Size() / 2f, 0.8f, SpriteEffects.None, 0f);
				Utils.DrawBorderString(sb, amount.ToString(), new Vector2(x + 20, at.Y + 2), Color.White * alpha, 0.75f);
				x += 50;
			}
		}

		/// <summary>A thin charge bar under the player while it isn't full, so you can watch it drain.</summary>
		private static void DrawChargeUnderPlayer()
		{
			Player p = Main.LocalPlayer;
			var lp = p.GetModPlayer<LegacyPlayer>();
			if (p.dead || lp.Heir == null || (lp.Charge >= LegacyPlayer.MaxCharge && !lp.AbilityOn))
				return;
			Vector2 pos = p.Bottom - Main.screenPosition + new Vector2(0, 8);
			var r = new Rectangle((int)pos.X - 22, (int)pos.Y, 44, 5);
			Draw.Rect(Main.spriteBatch, r, Color.Black * 0.7f);
			Color fill = lp.AbilityOn ? new Color(255, 150, 40) : new Color(230, 190, 60);
			Draw.Rect(Main.spriteBatch, new Rectangle(r.X + 1, r.Y + 1, (int)(42 * lp.Charge / LegacyPlayer.MaxCharge), 3), fill);
		}
	}
}
