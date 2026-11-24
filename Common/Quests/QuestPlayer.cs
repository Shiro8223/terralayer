using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Players;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terralayer.Common.Quests
{
	/// <summary>
	/// The Herald's quest board for one character: posted quests, up to three accepted ones, and the bookkeeping
	/// that judges them during descents. Tracking runs on the local client.
	/// </summary>
	public class QuestPlayer : ModPlayer
	{
		public List<Quest> Board = new();
		public List<Quest> Active = new();
		public int Completed;

		// ---- this descent (not saved)
		private bool runActive;
		private int runStartFloor;
		private int startWeapon;
		private bool usedOtherWeapon;
		private bool evolved;
		private bool tookBoon;
		private bool drankPotion;
		private int shrines;
		private uint floorArrival;
		private int guardianHits;
		private GuardianState lastGuardian;
		private long runStartPurse;

		public int ShrinesThisRun => runActive ? shrines : 0;

		private LegacyPlayer Legacy => Player.GetModPlayer<LegacyPlayer>();

		public override void SaveData(TagCompound tag)
		{
			tag["questBoard"] = Board.Select(q => q.Save()).ToList();
			tag["questActive"] = Active.Select(q => q.Save()).ToList();
			tag["questsDone"] = Completed;
		}

		public override void LoadData(TagCompound tag)
		{
			try {
				Board = tag.GetList<TagCompound>("questBoard").Select(Quest.Load).Where(q => q != null).ToList();
				Active = tag.GetList<TagCompound>("questActive").Select(Quest.Load).Where(q => q != null).Take(QuestDefs.MaxActive).ToList();
				Completed = tag.GetInt("questsDone");
			}
			catch (Exception e) {
				Mod.Logger.Warn("Skipped unreadable quest data: " + e.Message);
				Board = new List<Quest>();
				Active = new List<Quest>();
			}
		}

		// ------------------------------------------------------------------ the board

		private static int MaxFloor => TerralayerWorld.Instance.MaxFloorUnlocked;

		public void EnsureBoard()
		{
			var rng = new Random(Main.rand.Next());
			while (Board.Count < QuestDefs.BoardSize)
				Board.Add(QuestDefs.Generate(rng, MaxFloor, Board.Concat(Active)));
		}

		public bool Accept(Quest q)
		{
			if (Active.Count >= QuestDefs.MaxActive || !Board.Remove(q))
				return false;
			Active.Add(q);
			EnsureBoard();
			SoundEngine.PlaySound(SoundID.Unlock);
			Main.NewText($"Quest accepted: {QuestDefs.Title(q)} ({new string('*', q.Stars)} {QuestDefs.Tier(q.Stars)})", QuestDefs.StarColor(q.Stars));
			return true;
		}

		public void Abandon(Quest q)
		{
			if (Active.Remove(q))
				Main.NewText($"Quest abandoned: {QuestDefs.Title(q)}.", Color.Gray);
		}

		public bool Reroll()
		{
			if (!Player.BuyItem(QuestDefs.RerollCost)) {
				Main.NewText("The Herald wants a couple of gold to repost the board.", Color.OrangeRed);
				return false;
			}
			SoundEngine.PlaySound(SoundID.Coins);
			Board.Clear();
			EnsureBoard();
			return true;
		}

		public void Claim(Quest q)
		{
			if (!q.Complete || !Active.Remove(q))
				return;
			var lp = Legacy;
			var rng = new Random(Main.rand.Next());
			var got = new List<string>();
			foreach (QuestReward r in q.Rewards) {
				switch (r.Kind) {
					case RewardKind.Gold:
						Coins.PutInInventory(Player, r.Amount * Coins.Gold);
						break;
					case RewardKind.WeaponRune:
						Give(Content.Items.RuneItem.Create((RuneType)rng.Next((int)RuneType.Count), r.Rarity, rng));
						break;
					case RewardKind.ArmorRune:
						Give(Content.Items.ArmorRuneItem.Create((ArmorRuneType)rng.Next((int)ArmorRuneType.Count), r.Rarity, rng));
						break;
					case RewardKind.Potions:
						Give(Loot.Healing(MaxFloor, r.Amount));
						break;
					case RewardKind.Tempering:
						if (!lp.TemperHeirWeapon(r.Amount))
							Coins.PutInInventory(Player, r.Amount * 10 * Coins.Gold);
						break;
					case RewardKind.Blessing:
						lp.PendingBlessings += r.Amount;
						break;
				}
				got.Add(QuestDefs.RewardText(r));
			}
			Completed++;
			EnsureBoard();
			SoundEngine.PlaySound(SoundID.ResearchComplete);
			Main.NewText($"Reward claimed for {QuestDefs.Title(q)}: {string.Join(", ", got)}.", QuestDefs.StarColor(q.Stars));
		}

		private void Give(Item item)
		{
			if (item == null || item.IsAir)
				return;
			for (int i = 0; i < 50; i++)
				if (Player.inventory[i].IsAir) {
					Player.inventory[i] = item;
					return;
				}
			Player.QuickSpawnItem(Player.GetSource_Misc("TerralayerQuest"), item, item.stack);
		}

		// ------------------------------------------------------------------ judging

		private void Complete(Quest q)
		{
			if (q.Complete)
				return;
			q.Complete = true;
			q.Progress = QuestDefs.Target(q);
			SoundEngine.PlaySound(SoundID.AchievementComplete);
			Main.NewText($"Quest complete: {QuestDefs.Title(q)} ({new string('*', q.Stars)})! Return to the Herald to claim your reward.", QuestDefs.StarColor(q.Stars));
		}

		private IEnumerable<Quest> Open => Active.Where(q => !q.Complete).ToList();

		/// <summary>The weapon the Purist quest holds you to.</summary>
		public string StartingWeaponName()
		{
			var lp = Legacy;
			if (lp.Heir == null || lp.PendingHeirChoice)
				return "your heir's starting weapon";
			return Lang.GetItemNameValue(lp.Heir.WeaponType);
		}

		public void OnArrive(int floor, bool newRun)
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			var lp = Legacy;
			if (newRun || !runActive) {
				runActive = true;
				runStartFloor = floor;
				startWeapon = lp.Heir?.WeaponType ?? 0;
				usedOtherWeapon = evolved = tookBoon = drankPotion = false;
				shrines = 0;
				runStartPurse = Coins.InPurse(Player);
			}
			floorArrival = Main.GameUpdateCount;
			guardianHits = 0;
			foreach (Quest q in Open)
				if (q.Type == QuestType.DeepDive && runStartFloor == 1 && floor >= q.Param)
					Complete(q);
		}

		public void OnGuardianSlain(int floor)
		{
			if (Player.whoAmI != Main.myPlayer || !runActive)
				return;
			var lp = Legacy;
			int burdens = lp.Traits.Count(t => !TraitDefs.Get(t).Positive);
			uint elapsed = Main.GameUpdateCount - floorArrival;
			foreach (Quest q in Open) {
				if (q.Param != floor)
					continue;
				bool done = q.Type switch {
					QuestType.Guardian => true,
					QuestType.Purist => !usedOtherWeapon && !evolved,
					QuestType.Flawless => guardianHits <= 3,
					QuestType.Swift => elapsed <= QuestDefs.SwiftMinutes(floor) * 3600,
					QuestType.Ascetic => !tookBoon,
					QuestType.Sober => !drankPotion,
					QuestType.Burdened => burdens >= 2,
					_ => false
				};
				if (done)
					Complete(q);
			}
		}

		public void OnRunEnd(Players.RunEnd how, long purseAtEnd)
		{
			if (Player.whoAmI != Main.myPlayer || !runActive)
				return;
			long earned = purseAtEnd - runStartPurse;
			foreach (Quest q in Open)
				if (q.Type == QuestType.Hoarder && earned >= q.Param * Coins.Gold)
					Complete(q);
			runActive = false;
		}

		public void OnDeath() => runActive = false;

		public void OnEvolved() => evolved = true;

		public void OnBoonTaken() => tookBoon = true;

		public void OnShrineUsed()
		{
			if (!runActive)
				return;
			shrines++;
			foreach (Quest q in Open)
				if (q.Type == QuestType.Pilgrim) {
					q.Progress = Math.Max(q.Progress, Math.Min(shrines, q.Param));
					if (shrines >= q.Param)
						Complete(q);
				}
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer || !runActive || !TerralayerWorld.Active)
				return;
			if (Player.HasBuff(BuffID.PotionSickness))
				drankPotion = true;
			// Each attempt at the guardian counts its own hits.
			GuardianState g = DepthsSystem.Instance.Guardian;
			if (g == GuardianState.Awakening && lastGuardian != GuardianState.Awakening)
				guardianHits = 0;
			lastGuardian = g;
			if (Main.GameUpdateCount % 60 != 0)
				return;
			var depths = DepthsSystem.Instance;
			var lp = Legacy;
			if (depths.Plan == null || !lp.InRun)
				return;
			foreach (Quest q in Open)
				if (q.Type == QuestType.Cartographer && depths.Floor == q.Param && lp.VisitedRooms.Count >= depths.Plan.Rooms.Count)
					Complete(q);
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (Player.whoAmI != Main.myPlayer || !runActive)
				return;
			var depths = DepthsSystem.Instance;
			if (depths.Plan != null && depths.Guardian == GuardianState.Fighting && depths.RoomOf(Player)?.Id == depths.Plan.BossRoom)
				guardianHits++;
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (runActive && item.type != startWeapon && RefinedWeapon.IsWeapon(item))
				usedOtherWeapon = true;
			CheckChampion(target);
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (runActive && proj.TryGetGlobalProjectile(out WeaponSource src) && src.ItemType > 0 && src.ItemType != startWeapon)
				usedOtherWeapon = true;
			CheckChampion(target);
		}

		private void CheckChampion(NPC target)
		{
			if (Player.whoAmI != Main.myPlayer || target.life > 0 || !target.TryGetGlobalNPC(out DepthsNPC g) || !g.Champion)
				return;
			foreach (Quest q in Open)
				if (q.Type == QuestType.Slayer) {
					q.Progress++;
					if (q.Progress >= q.Param)
						Complete(q);
				}
		}
	}

	/// <summary>Remembers which weapon fired a projectile (or summoned a minion), so the Purist quest can tell.</summary>
	public class WeaponSource : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public int ItemType;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_ItemUse use && use.Item != null)
				ItemType = use.Item.type;
			else if (source is EntitySource_Parent parent && parent.Entity is Projectile p && p.TryGetGlobalProjectile(out WeaponSource ws))
				ItemType = ws.ItemType;
		}
	}
}
