using Terralayer.Common.Depths;
using Terralayer.Common.Items;
using Terralayer.Common.Net;
using Terralayer.Common.Players;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Commands
{
	/// <summary>Testing shortcuts. Type /tl in chat.</summary>
	public class TerralayerCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "tl";
		public override string Usage => "/tl enter [floor] | home | heir | unlock <1-5> | coins <gold> | build | boss | boss all | max | chart";
		public override string Description => "Terralayer testing commands.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (!TerralayerWorld.Active) {
				caller.Reply("This isn't a Terralayer world. Create a new world with the mod enabled.");
				return;
			}
			Player player = caller.Player;
			var lp = player.GetModPlayer<LegacyPlayer>();
			string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

			switch (sub) {
				case "enter":
					int floor = args.Length > 1 && int.TryParse(args[1], out int f) ? f : 1;
					if (floor > TerralayerWorld.Instance.MaxFloorUnlocked)
						World(WorldAction.SetMaxFloor, floor);
					lp.InRun = false;
					lp.RequestEnterRun(floor);
					break;
				case "home":
					lp.EndRun(RunEnd.Retreat);
					break;
				case "heir":
					lp.PendingHeirChoice = true;
					lp.HeirOptions.Clear();
					break;
				case "unlock":
					World(WorldAction.SetMaxFloor, args.Length > 1 && int.TryParse(args[1], out int n) ? n : FloorDefs.Count);
					caller.Reply("Floors unlocked.");
					break;
				case "coins":
					Coins.GiveCoins(player, (args.Length > 1 && int.TryParse(args[1], out int g) ? g : 50) * Coins.Gold);
					break;
				case "build":
					World(WorldAction.BuildAll, 0);
					break;
				case "boss" when args.Length > 1 && args[1].ToLowerInvariant() == "all":
					World(WorldAction.ToggleAllBosses, 0);
					break;
				case "max":
					GiveMax(player);
					caller.Reply("Test gear handed out: Zenith, Rod of Harmony, solar armor and endgame accessories.");
					break;
				case "boss": {
					var plan = DepthsSystem.Instance.Plan;
					if (plan == null || !lp.InRun) {
						caller.Reply("Enter the Depths first (/tl enter).");
						break;
					}
					RoomPlan arena = plan.Rooms[plan.BossRoom];
					player.Teleport(FloorPlan.StandPosition(arena.Anchors[(int)Dir.Left], player));
					break;
				}
				case "chart":
					lp.ChartActive = true;
					break;
				default:
					caller.Reply(Usage);
					break;
			}
		}

		/// <summary>Overpowered test kit.</summary>
		private static void GiveMax(Player p)
		{
			var source = p.GetSource_Misc("TerralayerTest");
			p.QuickSpawnItem(source, RefinedWeapon.Create(ItemID.Zenith, Rarity.Legendary, RefinedWeapon.MaxLevel));
			foreach (int type in new[] { ItemID.RodOfHarmony, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings,
				ItemID.EmpressFlightBooster, ItemID.TerrasparkBoots, ItemID.CelestialShell, ItemID.AnkhShield, ItemID.MasterNinjaGear, ItemID.FishronWings })
				p.QuickSpawnItem(source, type);
			p.QuickSpawnItem(source, ItemID.SuperHealingPotion, 30);
		}

		private static void World(WorldAction action, int arg)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerralayerNet.SendWorldAction(action, arg, "");
			else
				TerralayerWorld.Instance.ApplyAction(action, arg, "");
		}
	}
}
