using System;
using System.Collections.Generic;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.NPCs
{
	/// <summary>
	/// Base for the homestead's residents. Each one belongs to a structure and only lives in Terralayer worlds once
	/// that structure is built. Until custom sprites exist they borrow a vanilla NPC's sprite sheet and head icon.
	/// </summary>
	public abstract class LegacyTownNPC : ModNPC
	{
		protected abstract int VanillaTemplate { get; }
		protected abstract int VanillaHeadIndex { get; }
		protected abstract string[] Names { get; }
		protected abstract string Structure { get; }

		public override string Texture => $"Terraria/Images/NPC_{VanillaTemplate}";

		public override string HeadTexture => $"Terraria/Images/NPC_Head_{VanillaHeadIndex}";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[VanillaTemplate];
			NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[VanillaTemplate];
			NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[VanillaTemplate];
			NPCID.Sets.HatOffsetY[Type] = NPCID.Sets.HatOffsetY[VanillaTemplate];
			NPCID.Sets.NoTownNPCHappiness[Type] = true; // no "Happiness" button: they're staff, not tenants
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 10;
			NPC.defense = 15;
			NPC.lifeMax = 250;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;
			AnimationType = VanillaTemplate;
		}

		public override bool CanTownNPCSpawn(int numTownNPCs) => TerralayerWorld.Active && TerralayerWorld.Instance.HasBuilt(Structure);

		public override List<string> SetNPCNameList() => new(Names);

		protected static string Pick(params string[] lines) => lines[Main.rand.Next(lines.Length)];

		protected static void CloseChat()
		{
			Main.npcChatText = "";
			Main.LocalPlayer.SetTalkNPC(-1);
		}

		protected static string ShopButton => Terraria.Localization.Language.GetTextValue("LegacyInterface.28");

		protected static Item Priced(int type, int gold, int silver = 0) => new(type) { shopCustomPrice = Item.buyPrice(gold: gold, silver: silver) };

		protected static Condition FloorOpened(int floor) =>
			new($"Mods.Terralayer.Conditions.Floor{floor}", () => TerralayerWorld.Instance.MaxFloorUnlocked >= floor);
	}
}
