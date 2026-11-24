using Terralayer.Common.Quests;
using Terralayer.Common.Systems;
using Terralayer.Common.World;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.NPCs
{
	/// <summary>
	/// Keeps the quest board on the plaza from day one. Not a townsperson (no house needed): the world keeps exactly
	/// one standing by the spawn point.
	/// </summary>
	public class Herald : ModNPC
	{
		public override string Texture => $"Terraria/Images/NPC_{NPCID.DD2Bartender}";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.DD2Bartender];
			NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.DD2Bartender];
			NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.DD2Bartender];
		}

		public override void SetDefaults()
		{
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.FaceClosestPlayer;
			NPC.lifeMax = 250;
			NPC.dontTakeDamage = true;
			NPC.dontTakeDamageFromHostiles = true;
			NPC.immortal = true;
			AnimationType = NPCID.DD2Bartender;
		}

		public override bool CheckActive() => false;

		public override bool CanChat() => true;

		public override string GetChat()
		{
			var qp = Main.LocalPlayer.GetModPlayer<QuestPlayer>();
			int ready = qp.Active.FindAll(q => q.Complete).Count;
			if (ready > 0)
				return $"Back in one piece, and with {ready} deed{(ready > 1 ? "s" : "")} worth paying for! Let's see the board.";
			return Main.rand.Next(4) switch {
				0 => "Bounties, wagers, dares. Take up to three. The harder the deed, the more stars beside it, and the fatter the purse.",
				1 => "Every heir thinks they'll be the one to beat the Depths with a copper blade. Prove it, and I'll pay.",
				2 => "The stars on a notice tell you how much it'll hurt. Green's a stroll. Crimson's a funeral.",
				_ => "You don't have to finish a quest in one go. Fail it, die, try again with the next heir. The board waits.",
			};
		}

		public override void SetChatButtons(ref string button, ref string button2) => button = "Quest Board";

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (!firstButton)
				return;
			Main.npcChatText = "";
			Main.LocalPlayer.SetTalkNPC(-1);
			ModContent.GetInstance<UISystem>().OpenQuestBoard();
		}

		/// <summary>Where the Herald stands: on the plaza, just left of the spawn point.</summary>
		public static void EnsurePresent()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || !TerralayerWorld.Active || NPC.AnyNPCs(ModContent.NPCType<Herald>()))
				return;
			var w = TerralayerWorld.Instance;
			Common.Depths.DepthsSystem.SpawnGate(() => NPC.NewNPC(Terraria.Entity.GetSource_NaturalSpawn(), (w.LobbyCenterX - 3) * 16 + 8, w.LobbyGroundY * 16, ModContent.NPCType<Herald>()));
		}
	}
}
