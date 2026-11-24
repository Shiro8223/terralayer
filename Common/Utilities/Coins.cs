using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace Terralayer.Common.Utilities
{
	/// <summary>Helpers for working with Terraria coins, which Terralayer uses as its "gold".</summary>
	public static class Coins
	{
		public const long Silver = 100;
		public const long Gold = 10_000;
		public const long Platinum = 1_000_000;

		/// <summary>Formats a copper amount as colored coin text, e.g. "1g 20s".</summary>
		public static string Format(long copper)
		{
			if (copper <= 0)
				return "[c/F68A60:0c]";

			long p = copper / Platinum;
			long g = copper / Gold % 100;
			long s = copper / Silver % 100;
			long c = copper % 100;

			var parts = new List<string>();
			if (p > 0) parts.Add($"[c/DCDCC6:{p}p]");
			if (g > 0) parts.Add($"[c/E0C95A:{g}g]");
			if (s > 0) parts.Add($"[c/B5C0C1:{s}s]");
			if (c > 0) parts.Add($"[c/F68A60:{c}c]");
			return string.Join(" ", parts);
		}

		/// <summary>Plain (untagged) coin text for places that do not render chat tags.</summary>
		public static string FormatPlain(long copper)
		{
			if (copper <= 0)
				return "0c";

			long p = copper / Platinum;
			long g = copper / Gold % 100;
			long s = copper / Silver % 100;
			long c = copper % 100;

			var parts = new List<string>();
			if (p > 0) parts.Add($"{p}p");
			if (g > 0) parts.Add($"{g}g");
			if (s > 0) parts.Add($"{s}s");
			if (c > 0) parts.Add($"{c}c");
			return string.Join(" ", parts);
		}

		public static long ValueOf(Item item)
		{
			if (item == null || item.IsAir)
				return 0;

			return item.type switch {
				ItemID.CopperCoin => item.stack,
				ItemID.SilverCoin => item.stack * Silver,
				ItemID.GoldCoin => item.stack * Gold,
				ItemID.PlatinumCoin => item.stack * Platinum,
				_ => 0
			};
		}

		/// <summary>Coins carried in the main inventory (not piggy bank / safe). This is the "purse" the toll applies to.</summary>
		public static long InPurse(Player player)
		{
			long total = 0;
			for (int i = 0; i < 59; i++)
				total += ValueOf(player.inventory[i]);
			return total;
		}

		/// <summary>The banks a player can stash coins in (piggy bank, safe, defender's forge, void vault).</summary>
		public static Item[][] Banks(Player player) => new[] { player.bank.item, player.bank2.item, player.bank3.item, player.bank4.item };

		/// <summary>Coins stashed in the player's banks: the Gatehouse safe, mostly.</summary>
		public static long InBanks(Player player)
		{
			long total = 0;
			foreach (Item[] bank in Banks(player))
				foreach (Item i in bank)
					total += ValueOf(i);
			return total;
		}

		/// <summary>"Purse: 3g 20s   Safe: 40g" (chat tags).</summary>
		public static string PurseAndSafe(Player player)
		{
			long safe = InBanks(player);
			return $"Purse: {Format(InPurse(player))}" + (safe > 0 ? $"   Safe: {Format(safe)}" : "");
		}

		/// <summary>
		/// Removes <paramref name="percent"/>% of the coins in the player's banks and puts the change back in the safe.
		/// Returns the amount taken. Client-side.
		/// </summary>
		public static long TakeBankShare(Player player, int percent)
		{
			long total = InBanks(player);
			long take = total * System.Math.Clamp(percent, 0, 100) / 100;
			if (take <= 0)
				return 0;
			foreach (Item[] bank in Banks(player))
				foreach (Item i in bank)
					if (ValueOf(i) > 0)
						i.TurnToAir();
			PutInBank(player.bank3.item, total - take);
			return take;
		}

		/// <summary>Puts coins into a bank's free slots (stacking onto matching coins first).</summary>
		public static void PutInBank(Item[] bank, long copper)
		{
			foreach ((int type, int stack) in Split(copper)) {
				int left = stack;
				for (int i = 0; i < bank.Length && left > 0; i++) {
					if (bank[i].type == type && bank[i].stack < bank[i].maxStack) {
						int add = System.Math.Min(left, bank[i].maxStack - bank[i].stack);
						bank[i].stack += add;
						left -= add;
					}
				}
				for (int i = 0; i < bank.Length && left > 0; i++) {
					if (bank[i].IsAir) {
						bank[i] = new Item(type, System.Math.Min(left, 999));
						left -= bank[i].stack;
					}
				}
			}
		}

		/// <summary>Removes <paramref name="percent"/>% of the coins in the player's purse. Returns the amount taken. Client-side.</summary>
		public static long TakePurseShare(Player player, int percent)
		{
			long total = InPurse(player);
			long take = total * System.Math.Clamp(percent, 0, 100) / 100;
			if (take <= 0)
				return 0;

			for (int i = 0; i < 59; i++) {
				if (ValueOf(player.inventory[i]) > 0)
					player.inventory[i].TurnToAir();
			}

			// Straight into the inventory: spawned coins would be left lying at the gate when the descent teleports you.
			PutInInventory(player, total - take);
			return take;
		}

		/// <summary>Gives coins straight into the player's inventory (spawns them on the player).</summary>
		public static void GiveCoins(Player player, long copper)
		{
			IEntitySource source = player.GetSource_Misc("Terralayer");
			foreach ((int type, int stack) in Split(copper))
				player.QuickSpawnItem(source, type, stack);
		}

		/// <summary>Puts coins straight into inventory slots (coin slots first). Works while dead, unlike QuickSpawnItem.</summary>
		public static void PutInInventory(Player player, long copper)
		{
			foreach ((int type, int stack) in Split(copper)) {
				var coin = new Item(type, stack);
				int[] order = { 50, 51, 52, 53 };
				bool placed = false;
				foreach (int i in order.Concat(Enumerable.Range(0, 50))) {
					Item slot = player.inventory[i];
					if (slot.type == type && slot.stack + stack <= slot.maxStack) {
						slot.stack += stack;
						placed = true;
						break;
					}
					if (slot.IsAir) {
						player.inventory[i] = coin;
						placed = true;
						break;
					}
				}
				if (!placed)
					player.QuickSpawnItem(player.GetSource_Misc("Terralayer"), type, stack);
			}
		}

		/// <summary>Drops coins as items in the world.</summary>
		public static void Drop(IEntitySource source, Rectangle area, long copper)
		{
			foreach ((int type, int stack) in Split(copper))
				Item.NewItem(source, area, type, stack);
		}

		public static IEnumerable<(int type, int stack)> Split(long copper)
		{
			if (copper <= 0)
				yield break;

			long p = copper / Platinum;
			long g = copper / Gold % 100;
			long s = copper / Silver % 100;
			long c = copper % 100;

			while (p > 0) {
				int stack = (int)System.Math.Min(p, 999);
				yield return (ItemID.PlatinumCoin, stack);
				p -= stack;
			}
			if (g > 0) yield return (ItemID.GoldCoin, (int)g);
			if (s > 0) yield return (ItemID.SilverCoin, (int)s);
			if (c > 0) yield return (ItemID.CopperCoin, (int)c);
		}
	}
}
