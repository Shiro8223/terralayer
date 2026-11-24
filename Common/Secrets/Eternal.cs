using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terralayer.Common.World;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Secrets
{
	/// <summary>
	/// Secret-quest rewards are eternal: they survive an heir's death (the family keeps them) and can't be sold.
	/// </summary>
	public class EternalItem : GlobalItem
	{
		public bool Eternal;

		public override bool InstancePerEntity => true;

		public static bool Is(Item item) => item != null && !item.IsAir && item.TryGetGlobalItem(out EternalItem e) && e.Eternal;

		public static Item Make(int type, int stack = 1)
		{
			var item = new Item(type, stack);
			if (item.TryGetGlobalItem(out EternalItem e))
				e.Eternal = true;
			return item;
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (Eternal && TerralayerWorld.Active)
				tooltips.Add(new TooltipLine(Mod, "Eternal", "Eternal: a secret's reward. Your family keeps it through every death.") { OverrideColor = new Color(255, 220, 120) });
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			if (Eternal)
				tag["eternal"] = true;
		}

		public override void LoadData(Item item, TagCompound tag) => Eternal = tag.ContainsKey("eternal");

		public override void NetSend(Item item, BinaryWriter writer) => writer.Write(Eternal);

		public override void NetReceive(Item item, BinaryReader reader) => Eternal = reader.ReadBoolean();
	}
}
