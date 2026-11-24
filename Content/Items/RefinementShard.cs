using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.Items
{
	/// <summary>
	/// Found in chests. The Smith at the homestead uses them to refine a weapon a level at a time; each level costs
	/// more than the last. Borrows the Etherian Mana sprite.
	/// </summary>
	public class RefinementShard : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.DD2EnergyCrystal}";

		public override void SetStaticDefaults()
		{
			if (Main.itemAnimations[ItemID.DD2EnergyCrystal] != null)
				Main.RegisterItemAnimation(Type, Main.itemAnimations[ItemID.DD2EnergyCrystal]);
			ItemID.Sets.AnimatesAsSoul[Type] = ItemID.Sets.AnimatesAsSoul[ItemID.DD2EnergyCrystal];
			ItemID.Sets.ItemNoGravity[Type] = true;
			Item.ResearchUnlockCount = 0;
		}

		public override void SetDefaults()
		{
			Item.CloneDefaults(ItemID.DD2EnergyCrystal);
			Item.maxStack = Item.CommonMaxStack;
			Item.value = 0;
			Item.rare = ItemRarityID.LightPurple;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.RemoveAll(t => t.Mod == "Terraria" && t.Name.StartsWith("Tooltip"));
			tooltips.Add(new TooltipLine(Mod, "Use", "Bring these to the Smith at the homestead to refine a weapon") { OverrideColor = new Color(255, 170, 90) });
			tooltips.Add(new TooltipLine(Mod, "Cost", "Each refinement level costs more shards than the last") { OverrideColor = new Color(170, 170, 190) });
		}

		/// <summary>Shards to go from refinement level <paramref name="level"/> to the next: 2, 4, 6 ... 18.</summary>
		public static int CostFor(int level) => 2 * level;

		/// <summary>Gold fee on top of the shards.</summary>
		public static long FeeFor(int level) => Common.Utilities.Coins.Gold * level;
	}
}
