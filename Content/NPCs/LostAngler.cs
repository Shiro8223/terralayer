using Microsoft.Xna.Framework;
using Terralayer.Common.Secrets;
using Terralayer.Common.World;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Content.NPCs
{
	/// <summary>
	/// The Angler, waiting at the hidden sea for anyone who makes it past the pirates. Gives the Golden Bug Net, takes
	/// five golden creatures for a Golden Fishing Rod and a Truffle Worm, and baits the sea for Duke Fishron.
	/// </summary>
	public class LostAngler : ModNPC
	{
		public override string Texture => $"Terraria/Images/NPC_{NPCID.Angler}";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Angler];
			NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.Angler];
			NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.Angler];
		}

		public override void SetDefaults()
		{
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 34;
			NPC.aiStyle = NPCAIStyleID.FaceClosestPlayer;
			NPC.lifeMax = 250;
			NPC.dontTakeDamage = true;
			NPC.dontTakeDamageFromHostiles = true;
			NPC.immortal = true;
			AnimationType = NPCID.Angler;
		}

		public override bool CheckActive() => false;

		public override bool CanChat() => true;

		private static SecretPlayer Me => Main.LocalPlayer.GetModPlayer<SecretPlayer>();

		public override string GetChat()
		{
			SecretPlayer sp = Me;
			if (!sp.MetAngler) {
				sp.MetAngler = true;
				Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_GiftOrReward(), EternalItem.Make(ItemID.GoldenBugNet));
				Main.NewText("The Angler hands you a Golden Bug Net. Golden creatures will now turn up in the Depths: catch five and bring them here. A rift home has opened behind you, and one at the homestead leads back.", new Color(255, 220, 120));
				SoundEngine.PlaySound(SoundID.Item4);
				return "I never thought someone would make it here. Prove it wasn't a fluke.";
			}
			if (!sp.AnglerRewarded) {
				int have = SecretPlayer.CountGoldenCreatures(Main.LocalPlayer);
				return $"Five golden creatures. Caught with that net, down in those holes you crawl through. You've got {have}.";
			}
			return sp.FishronBaited
				? "Back for another go at the big one? It'll cost you a golden creature to bait him now."
				: "That Truffle Worm is the only bait he bites. Say the word and I'll chum the water.";
		}

		public override void SetChatButtons(ref string button, ref string button2)
		{
			SecretPlayer sp = Me;
			if (!sp.MetAngler)
				return;
			if (!sp.AnglerRewarded)
				button = $"Hand over {SecretPlayer.GoldenCreaturesNeeded} golden creatures";
			else
				button = sp.FishronBaited ? "Bait the sea (1 golden creature)" : "Bait the sea (Truffle Worm)";
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (!firstButton)
				return;
			Player p = Main.LocalPlayer;
			SecretPlayer sp = Me;
			if (!sp.AnglerRewarded) {
				if (!SecretPlayer.TakeGoldenCreatures(p, SecretPlayer.GoldenCreaturesNeeded)) {
					Main.npcChatText = $"That's not five. Come back with {SecretPlayer.GoldenCreaturesNeeded} golden creatures.";
					return;
				}
				sp.AnglerRewarded = true;
				p.QuickSpawnItem(p.GetSource_GiftOrReward(), EternalItem.Make(ItemID.GoldenFishingRod));
				p.QuickSpawnItem(p.GetSource_GiftOrReward(), ItemID.TruffleWorm);
				SoundEngine.PlaySound(SoundID.ResearchComplete);
				Main.npcChatText = "Huh. Not a fluke. Here: a golden rod, and the one bait the Duke can't resist. When you're ready, I'll chum the water.";
				return;
			}
			if (NPC.AnyNPCs(NPCID.DukeFishron)) {
				Main.npcChatText = "He's already out there! Go!";
				return;
			}
			if (!sp.FishronBaited) {
				int worm = System.Array.FindIndex(p.inventory, i => i.type == ItemID.TruffleWorm);
				if (worm < 0) {
					Main.npcChatText = "You need the Truffle Worm I gave you.";
					return;
				}
				if (--p.inventory[worm].stack <= 0)
					p.inventory[worm].TurnToAir();
				sp.FishronBaited = true;
			}
			else if (!SecretPlayer.TakeGoldenCreatures(p, 1)) {
				Main.npcChatText = "No golden creature, no bait. Go catch one.";
				return;
			}
			Main.npcChatText = "";
			p.SetTalkNPC(-1);
			SecretEvents.Instance.SummonDuke(p);
		}
	}
}
