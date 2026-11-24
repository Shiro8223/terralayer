using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terralayer.Common.Config;
using Terralayer.Common.Heirs;
using Terralayer.Common.Items;
using Terralayer.Common.Net;
using Terralayer.Common.Players;
using Terralayer.Common.Utilities;
using Terralayer.Common.World;
using Terralayer.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terralayer.Common.Depths
{
	public enum GuardianState : byte
	{
		Dormant,
		Awakening,
		Fighting,
		Defeated
	}

	public class RoomState
	{
		public bool Entered;
		public bool Cleared;
		public bool Locked;
		public int Wave;
		public int Planned;
		public int Killed;
		public int LastProgress; // tick of the last kill (or entry), for the softlock failsafe
		public bool Occupied;    // someone is inside right now (server-side only)
		public bool Sprung;      // an ambush's second wave has arrived
		public readonly List<int> Alive = new();
	}

	/// <summary>
	/// The current descent: which floor is built, room progress, ambushes and the guardian fight.
	/// Authoritative on the server (or in singleplayer); clients rebuild the plan from the synced seed.
	/// </summary>
	public class DepthsSystem : ModSystem
	{
		public static DepthsSystem Instance => ModContent.GetInstance<DepthsSystem>();

		/// <summary>Set while Terralayer itself spawns NPCs, so the stray-boss filter lets them through.</summary>
		public static bool Spawning { get; private set; }

		/// <summary>Runs a spawn that the stray-boss filter must let through.</summary>
		public static int SpawnGate(Func<int> spawn)
		{
			Spawning = true;
			try {
				return spawn();
			}
			finally {
				Spawning = false;
			}
		}

		public FloorPlan Plan;
		public int Floor;
		public int Streak;
		public RoomState[] Rooms = Array.Empty<RoomState>();
		public GuardianState Guardian;

		private HeirClass runClass;
		private float runGoldMult = 1f;

		/// <summary>Enemy life multiplier from the run starter's permanent upgrades (see LegacyPlayer.EnemyLifeScaling).</summary>
		public float RunLifeMult = 1f;

		/// <summary>Base life overrides for enemies that hit too hard for where they appear.</summary>
		private static readonly Dictionary<int, int> BaseLife = new() {
			[NPCID.GreekSkeleton] = 20, // Hoplite: brutal on floor 1 at its usual 70
		};
		private readonly List<Rectangle> builtBounds = new();
		private int guardianTimer;
		private int lastGuardian;
		private bool guardianKilled;
		private bool forcingNight;
		private bool savedDayTime;
		private double savedTime;
		private int tick;

		public float StreakMultiplier => 1f + 0.25f * Streak;

		public override void ClearWorld()
		{
			Plan = null;
			Floor = 0;
			Streak = 0;
			Rooms = Array.Empty<RoomState>();
			Guardian = GuardianState.Dormant;
			builtBounds.Clear();
			forcingNight = false;
		}

		// ------------------------------------------------------------------ sync

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Plan != null);
			if (Plan == null)
				return;
			writer.Write(Plan.Seed);
			writer.Write((byte)Floor);
			writer.Write((byte)Streak);
			writer.Write(RunLifeMult);
			writer.Write((byte)Guardian);
			writer.Write((byte)Rooms.Length);
			foreach (RoomState s in Rooms)
				writer.Write((byte)((s.Entered ? 1 : 0) | (s.Cleared ? 2 : 0) | (s.Locked ? 4 : 0) | (s.Sprung ? 8 : 0)));
		}

		public override void NetReceive(BinaryReader reader)
		{
			if (!reader.ReadBoolean()) {
				Plan = null;
				Rooms = Array.Empty<RoomState>();
				return;
			}
			int seed = reader.ReadInt32();
			int floor = reader.ReadByte();
			Streak = reader.ReadByte();
			RunLifeMult = reader.ReadSingle();
			Guardian = (GuardianState)reader.ReadByte();
			if (Plan == null || Plan.Seed != seed || Floor != floor) {
				Floor = floor;
				Plan = FloorPlan.Generate(seed, floor);
			}
			int n = reader.ReadByte();
			if (Rooms.Length != n)
				Rooms = Enumerable.Range(0, n).Select(_ => new RoomState()).ToArray();
			for (int i = 0; i < n; i++) {
				byte b = reader.ReadByte();
				Rooms[i].Entered = (b & 1) != 0;
				Rooms[i].Cleared = (b & 2) != 0;
				Rooms[i].Locked = (b & 4) != 0;
				Rooms[i].Sprung = (b & 8) != 0;
			}
		}

		private static void Sync()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// ------------------------------------------------------------------ queries

		public RoomPlan RoomOf(Player p) => Plan?.RoomAt(p.Center.ToTileCoordinates());

		public bool InDepths(Player p) => RoomOf(p) != null;

		/// <summary>
		/// Link rifts: an uncleared room lets you fall back to rooms someone has already entered, but every rift into a
		/// room nobody has entered stays sealed until this one is cleared. The guardian's arena counts like any other
		/// room: no need to clear the whole floor first. Nobody leaves the arena mid-fight.
		/// </summary>
		public bool RiftActive(RiftPlan rift)
		{
			if (Plan == null)
				return false;
			switch (rift.Kind) {
				case RiftKind.Link:
					if (rift.Room == Plan.BossRoom && (Guardian == GuardianState.Awakening || Guardian == GuardianState.Fighting))
						return false;
					if (rift.Room < Rooms.Length && Rooms[rift.Room].Locked)
						return rift.Target >= 0 && TargetExplored(rift);
					return true;
				case RiftKind.Retreat:
					return true;
				default:
					return Guardian == GuardianState.Defeated;
			}
		}

		/// <summary>Has anyone already entered the room this link rift leads to?</summary>
		public bool TargetExplored(RiftPlan rift)
		{
			if (Plan == null || rift.Target < 0)
				return false;
			int room = Plan.Rifts[rift.Target].Room;
			return room < Rooms.Length && Rooms[room].Entered;
		}

		/// <summary>Is the player's current room still sealed (so only back-travel works)?</summary>
		public bool RoomSealed(RoomPlan room) => room != null && room.Id < Rooms.Length && Rooms[room.Id].Locked;

		/// <summary>Combat and ambush rooms not yet cleared (shown on the HUD; no longer gates the guardian).</summary>
		public int ChambersRemaining()
		{
			if (Plan == null)
				return 0;
			int n = 0;
			for (int i = 0; i < Rooms.Length && i < Plan.Rooms.Count; i++)
				if (Plan.Rooms[i].Kind is RoomKind.Combat or RoomKind.Ambush && !Rooms[i].Cleared)
					n++;
			return n;
		}

		private List<Player> PlayersInDepths(int except = -1)
		{
			var list = new List<Player>();
			foreach (Player p in Main.ActivePlayers)
				if (p.whoAmI != except && !p.dead && InDepths(p))
					list.Add(p);
			return list;
		}

		// ------------------------------------------------------------------ runs

		/// <summary>Builds (or joins) the Depths for a player starting a descent. Returns where to stand.</summary>
		public Vector2 StartRun(int floor, Player player, HeirClass cls, float goldMult, float lifeMult = 1f)
		{
			floor = Math.Clamp(floor, 1, TerralayerWorld.Instance.MaxFloorUnlocked);
			if (Plan == null || PlayersInDepths(player.whoAmI).Count == 0) {
				Streak = 0;
				runClass = cls;
				runGoldMult = goldMult;
				RunLifeMult = Math.Max(1f, lifeMult);
				var world = TerralayerWorld.Instance;
				int seed = world.LayoutPinned && world.PinnedSeed != 0 ? world.PinnedSeed : Main.rand.Next(1, int.MaxValue);
				world.PinnedSeed = seed;
				BuildFloor(floor, seed);
			}
			return FloorPlan.StandPosition(Plan.Rooms[Plan.StartRoom].Feature, player);
		}

		private void BuildFloor(int floor, int seed)
		{
			EndForcedNight();
			Secrets.SecretEvents.Instance.LightChest = -1;
			Floor = floor;
			Plan = FloorPlan.Generate(seed, floor);
			Rooms = Plan.Rooms.Select(_ => new RoomState()).ToArray();
			Rooms[Plan.StartRoom].Entered = true;
			Rooms[Plan.StartRoom].Cleared = true;
			Guardian = GuardianState.Dormant;
			guardianKilled = false;

			DungeonBuilder.BuildFloor(Plan, builtBounds, runClass, runGoldMult * StreakMultiplier * TerralayerConfig.Instance.GoldMultiplier);
			builtBounds.Clear();
			builtBounds.AddRange(Plan.Rooms.Select(r => r.Bounds));
			Sync();
		}

		/// <summary>After the guardian falls: build the next floor and bring everyone in the Depths along.</summary>
		public void Descend()
		{
			if (Plan == null || Guardian != GuardianState.Defeated || Floor >= FloorDefs.Count)
				return;
			var travellers = PlayersInDepths();
			Streak++;
			BuildFloor(Floor + 1, Main.rand.Next(1, int.MaxValue));
			Chat.Announce($"Floor {Floor}: {Plan.Info.Name}. Gold streak x{StreakMultiplier:0.00}!", new Color(120, 220, 255));
			foreach (Player p in travellers)
				TerralayerNet.SendArrive(p, FloorPlan.StandPosition(Plan.Rooms[Plan.StartRoom].Feature, p), Floor);
		}

		// ------------------------------------------------------------------ per tick

		public override void PostUpdateWorld()
		{
			if (Plan == null || !TerralayerWorld.Active)
				return;

			if (forcingNight) {
				Main.dayTime = false;
				Main.time = 16200;
			}

			var inside = new List<(Player p, RoomPlan room)>();
			foreach (Player p in Main.ActivePlayers) {
				if (p.dead)
					continue;
				RoomPlan room = RoomOf(p);
				if (room != null && room.Interior.Contains(p.Center.ToTileCoordinates()))
					inside.Add((p, room));
			}

			foreach (var (p, room) in inside)
				OnPlayerInRoom(p, room);

			// A sealed room's softlock timer only runs while someone is inside: stepping out to rest doesn't clear it.
			foreach (RoomState rs in Rooms)
				if (rs.Locked)
					rs.Occupied = false;
			foreach (var (_, room) in inside)
				Rooms[room.Id].Occupied = true;
			if (++tick % 20 == 0)
				UpdateClears(inside);

			UpdateGuardian(inside.Where(x => x.room.Id == Plan.BossRoom).Select(x => x.p).ToList());
		}

		private void OnPlayerInRoom(Player p, RoomPlan room)
		{
			RoomState s = Rooms[room.Id];
			if (!s.Entered) {
				s.Entered = true;
				FloorInfo f = Plan.Info;
				switch (room.Kind) {
					case RoomKind.Combat:
						s.Wave = 1;
						s.Locked = true;
						SpawnWave(room, p, EnemyCount(room), false);
						break;
					case RoomKind.Ambush:
						// Looks like any other chamber until its first wave falls.
						s.Wave = 1;
						s.Locked = true;
						SpawnWave(room, p, (int)(EnemyCount(room) * 0.7f), false);
						break;
					case RoomKind.Treasure:
						if (DungeonBuilder.IsHallowedMimicVault(room, Floor)) {
							s.Wave = 1;
							Spawn(room, NPCID.BigMimicHallow, room.Feature.X * 16 + 16, room.Feature.Y * 16, false, mimic: true);
							if (s.Alive.Count > 0)
								Main.npc[s.Alive[^1]].GetGlobalNPC<DepthsNPC>().HallowedMimic = true;
						}
						else if (DungeonBuilder.IsMimicVault(room, Floor)) {
							s.Wave = 1;
							Spawn(room, NPCID.Mimic, room.Feature.X * 16 + 16, room.Feature.Y * 16, false, mimic: true);
						}
						else {
							s.Cleared = true;
						}
						break;
					case RoomKind.Merchant:
						if (!NPC.AnyNPCs(ModContent.NPCType<Peddler>())) {
							Spawning = true;
							NPC.NewNPC(Entity.GetSource_NaturalSpawn(), room.Feature.X * 16 + 8, room.Feature.Y * 16, ModContent.NPCType<Peddler>());
							Spawning = false;
						}
						s.Cleared = true;
						break;
					case RoomKind.Shrine:
					case RoomKind.Forge:
						s.Cleared = true;
						break;
				}
				s.LastProgress = tick;
				MaybeGoldenCritter(room, p);
				Sync();
			}
		}

		/// <summary>Secret: whoever carries the Angler's Golden Bug Net may find a golden creature in a room (about 1 in 8).</summary>
		private void MaybeGoldenCritter(RoomPlan room, Player p)
		{
			if (room.Kind is RoomKind.Boss or RoomKind.Start || !p.GetModPlayer<Secrets.SecretPlayer>().MetAngler || Main.rand.Next(8) != 0)
				return;
			if (!Rooms_FindSpot(room, new Random(Main.rand.Next()), out Point spot))
				return;
			int type = Secrets.SecretPlayer.GoldenCritters[Main.rand.Next(Secrets.SecretPlayer.GoldenCritters.Length)];
			SpawnGate(() => NPC.NewNPC(Entity.GetSource_NaturalSpawn(), spot.X * 16 + 8, spot.Y * 16, type));
			Chat.Announce("Something golden glints in this room...", new Color(255, 220, 90));
		}

		private static int EnemyCount(RoomPlan room) => Math.Clamp(room.Area / 1100, 4, 12);

		private void SpawnWave(RoomPlan room, Player trigger, int count, bool withChampion)
		{
			FloorInfo f = Plan.Info;
			var rng = new Random(Main.rand.Next());
			Point ptile = trigger.Center.ToTileCoordinates();
			for (int i = 0; i < count + (withChampion ? 1 : 0); i++) {
				Point spot = Point.Zero;
				bool found = false;
				for (int tries = 0; tries < 6 && !found; tries++) {
					if (Rooms_FindSpot(room, rng, out spot) && Math.Abs(spot.X - ptile.X) + Math.Abs(spot.Y - ptile.Y) > 14)
						found = true;
				}
				if (!found && !Rooms_FindSpot(room, rng, out spot))
					continue;

				int[] pool = EnemiesFor(room);
				int type = pool[rng.Next(pool.Length)];
				Spawn(room, type, spot.X * 16 + 8, spot.Y * 16, champion: withChampion && i == count);
			}
		}

		/// <summary>The monsters a room spawns: a themed room's own, else the floor's.</summary>
		private int[] EnemiesFor(RoomPlan room) => global::Terralayer.Common.Depths.Rooms.RoomArchetype.For(room.Arch).Enemies(Floor) ?? Plan.Info.Enemies;

		private static bool Rooms_FindSpot(RoomPlan room, Random rng, out Point spot) =>
			global::Terralayer.Common.Depths.Rooms.RoomArchetype.FindFloorSpot(room.Interior, rng, 1, out spot, 3, allowPlatforms: true);

		private void Spawn(RoomPlan room, int type, int x, int y, bool champion, bool mimic = false)
		{
			Spawning = true;
			int index = NPC.NewNPC(Entity.GetSource_NaturalSpawn(), x, y, type);
			Spawning = false;
			if (index >= Main.maxNPCs)
				return;

			NPC npc = Main.npc[index];
			FloorInfo f = Plan.Info;
			float strength = TerralayerConfig.Instance.EnemyStrength;
			var g = npc.GetGlobalNPC<DepthsNPC>();
			g.FromDepths = true;
			g.Room = room.Id;
			g.Floor = Floor;
			g.Champion = champion;
			g.Mimic = mimic;

			float life = f.EnemyLife * strength * RunLifeMult * (mimic ? 0.8f : 1f);
			int baseLife = BaseLife.TryGetValue(type, out int b) ? b : npc.lifeMax;
			npc.lifeMax = Math.Max(1, (int)(baseLife * life));
			npc.life = npc.lifeMax;
			npc.damage = (int)(npc.damage * f.EnemyDamage * strength);
			npc.value = f.EnemyValue * (0.8f + Main.rand.NextFloat() * 0.4f);
			if (champion)
				MakeChampion(npc, trial: false);
			g.SpawnValue = npc.value;
			npc.netUpdate = true;

			RoomState s = Rooms[room.Id];
			s.Alive.Add(index);
			s.Planned++;
		}

		/// <summary>Shrine of Trials: summon a champion next to the player. Its drop is the reward.</summary>
		public void SpawnTrialChampion(Player p)
		{
			RoomPlan room = RoomOf(p);
			if (room == null)
				return;
			FloorInfo f = Plan.Info;
			int[] pool = EnemiesFor(room);
			int type = pool[Main.rand.Next(pool.Length)];
			Spawning = true;
			int index = NPC.NewNPC(Entity.GetSource_NaturalSpawn(), (int)p.Center.X + 16 * 12 * (Main.rand.NextBool() ? 1 : -1), (int)p.Bottom.Y - 4, type);
			Spawning = false;
			if (index >= Main.maxNPCs)
				return;
			NPC npc = Main.npc[index];
			var g = npc.GetGlobalNPC<DepthsNPC>();
			g.FromDepths = true;
			g.Trial = true;
			g.Floor = Floor;
			int trialBase = BaseLife.TryGetValue(type, out int tb) ? tb : npc.lifeMax;
			npc.lifeMax = Math.Max(1, (int)(trialBase * f.EnemyLife * RunLifeMult));
			npc.life = npc.lifeMax;
			npc.damage = (int)(npc.damage * f.EnemyDamage);
			npc.value = f.EnemyValue;
			MakeChampion(npc, trial: true);
			g.SpawnValue = npc.value;
			npc.netUpdate = true;
		}

		/// <summary>
		/// Turns an enemy into a champion: seven times the life (eight for a trial), half again the damage, heavy armor,
		/// no knockback, a bigger body, and one affix (Brutal, Armored, Vampiric or Regenerating) shown as a coloured aura.
		/// </summary>
		private void MakeChampion(NPC npc, bool trial)
		{
			var g = npc.GetGlobalNPC<DepthsNPC>();
			g.Champion = true;
			g.Affix = (ChampionAffix)Main.rand.Next((int)ChampionAffix.Count);
			npc.lifeMax = Math.Max(1, (int)(npc.lifeMax * (trial ? 8f : 7f) * (g.Affix == ChampionAffix.Regenerating ? 0.75f : 1f)));
			npc.life = npc.lifeMax;
			npc.damage = (int)(npc.damage * 1.5f * (g.Affix == ChampionAffix.Brutal ? 1.35f : 1f));
			npc.defense += 6 + 4 * Floor + (g.Affix == ChampionAffix.Armored ? 15 : 0);
			npc.knockBackResist = 0f;
			npc.value *= trial ? 6f : 5f;
			DepthsNPC.Grow(npc, DepthsNPC.ChampionScale);
		}

		/// <summary>Called by <see cref="DepthsNPC"/> when one of a room's enemies dies.</summary>
		public void OnEnemyKilled(int room, int whoAmI)
		{
			if (room < 0 || room >= Rooms.Length || !Rooms[room].Alive.Contains(whoAmI))
				room = Array.FindIndex(Rooms, r => r.Alive.Contains(whoAmI));
			if (room < 0)
				return;
			Rooms[room].Killed++;
			Rooms[room].Alive.Remove(whoAmI);
			Rooms[room].LastProgress = tick;
		}

		private void UpdateClears(List<(Player p, RoomPlan room)> inside)
		{
			for (int i = 0; i < Rooms.Length; i++) {
				RoomState s = Rooms[i];
				if (s.Cleared || s.Wave == 0 || i == Plan.BossRoom)
					continue;

				RoomPlan room = Plan.Rooms[i];
				s.Alive.RemoveAll(n => !Main.npc[n].active);
				// Some enemies transform (creepers climbing onto walls, for one), which resets their tags: re-tag them so
				// they still count for this room and still don't despawn.
				foreach (int n in s.Alive) {
					var g = Main.npc[n].GetGlobalNPC<DepthsNPC>();
					if (!g.FromDepths) {
						g.FromDepths = true;
						g.Room = i;
						g.Floor = Floor;
					}
				}

				// Failsafe 1: an enemy that drifts out of the room (through walls, say). With someone fighting in the
				// room it's written off; with nobody there it's put back, so leaving can't clear a room for you.
				var rng = new Random(Main.rand.Next());
				foreach (int n in s.Alive.ToArray()) {
					NPC npc = Main.npc[n];
					if (room.Bounds.Contains(npc.Center.ToTileCoordinates()))
						continue;
					if (!s.Occupied && Rooms_FindSpot(room, rng, out Point back)) {
						npc.Bottom = new Vector2(back.X * 16 + 8, back.Y * 16);
						npc.velocity = Vector2.Zero;
						npc.netUpdate = true;
						continue;
					}
					npc.active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n);
					s.Alive.Remove(n);
					s.Killed++;
				}
				// Failsafe 2: no kills for 75 seconds while sealed in - the stragglers flee. The clock pauses while the
				// room is empty (players may fall back through an explored rift).
				if (s.Locked && !s.Occupied)
					s.LastProgress += 20; // UpdateClears runs every 20 ticks: this holds the clock still
				if (s.Locked && s.Occupied && s.Alive.Count > 0 && tick - s.LastProgress > 75 * 60) {
					foreach (int n in s.Alive) {
						Main.npc[n].active = false;
						if (Main.netMode == NetmodeID.Server)
							NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n);
					}
					s.Killed += s.Alive.Count;
					s.Alive.Clear();
					Chat.Announce("The last of the room's defenders flee into the dark.", Color.Gray);
				}

				if (s.Alive.Count > 0)
					continue;
				// Everything tracked is gone. Anything unaccounted for (removed by something else) is written off:
				// only ambushes ever send a second wave.
				s.Killed = Math.Max(s.Killed, s.Planned);

				Player trigger = inside.FirstOrDefault(x => x.room.Id == i).p;

				if (room.Kind == RoomKind.Ambush && s.Wave == 1) {
					if (trigger == null)
						continue; // springs when someone is back inside
					s.Wave = 2;
					s.Sprung = true;
					s.LastProgress = tick;
					SpawnWave(room, trigger, (int)(EnemyCount(room) * 0.5f), withChampion: true);
					Chat.Announce("Ambush! A second wave and a champion pour in. The way forward stays sealed until they fall.", new Color(255, 120, 80));
					Sync();
					continue;
				}

				s.Cleared = true;
				s.Locked = false;
				if (room.Kind == RoomKind.Ambush) {
					// The champion's own drop is the prize: no chest on top of it.
					Chat.Announce("Ambush cleared! The rifts reopen.", new Color(120, 255, 140));
				}
				else if (trigger != null) {
					long bonus = (long)(Plan.Info.EnemyValue * 2 * StreakMultiplier * TerralayerConfig.Instance.GoldMultiplier);
					Coins.Drop(trigger.GetSource_Misc("Terralayer"), trigger.Hitbox, bonus);
					CombatText.NewText(trigger.Hitbox, new Color(255, 215, 80), "Room cleared!", true);
				}
				// Everyone who fought here picks a reward: refine, boon, or the mystery cache.
				foreach (var (p, r) in inside)
					if (r.Id == i)
						TerralayerNet.SendRoomReward(p);

				if (ChambersRemaining() == 0 && Guardian != GuardianState.Defeated)
					Chat.Announce("Every chamber on this floor is cleared. Only the guardian remains.", new Color(255, 120, 80));
				Sync();
			}
		}

		// ------------------------------------------------------------------ guardian

		private void UpdateGuardian(List<Player> inArena)
		{
			RoomPlan arena = Plan.Rooms[Plan.BossRoom];
			switch (Guardian) {
				case GuardianState.Dormant:
					if (inArena.Count > 0) {
						Guardian = GuardianState.Awakening;
						guardianTimer = 180;
						StartForcedNight();
						Chat.Announce($"The rifts dim. The guardian of the {Plan.Info.Name} stirs...", new Color(255, 120, 80));
						Sync();
					}
					break;

				case GuardianState.Awakening:
					if (inArena.Count == 0) {
						ResetGuardian();
						break;
					}
					if (--guardianTimer <= 0)
						SpawnGuardian(arena, inArena[0]);
					break;

				case GuardianState.Fighting:
					bool alive = Main.npc.Any(n => n.active && n.GetGlobalNPC<DepthsNPC>().Guardian);
					if (!alive) {
						if (guardianKilled) {
							DefeatGuardian(arena, inArena);
						}
						else {
							Chat.Announce("The guardian retreats into the dark...", Color.Gray);
							ResetGuardian();
						}
					}
					else if (inArena.Count == 0) {
						DespawnGuardians();
						ResetGuardian();
					}
					break;
			}
		}

		private void SpawnGuardian(RoomPlan arena, Player target)
		{
			int[][] options = Plan.Info.Guardians;
			int[] pick = options[Math.Clamp(Plan.GuardianPick, 0, options.Length - 1)];
			int flatLife = GuardianFlatLife(Floor) / pick.Length;
			lastGuardian = pick[0];
			Rectangle I = arena.Interior;
			bool bridge = arena.Arch == Archetype.HellBridge;
			for (int i = 0; i < pick.Length; i++) {
				int x = bridge ? arena.Feature.X * 16 : I.Center.X * 16 + (i - (pick.Length - 1) / 2) * 20 * 16;
				int y = bridge ? (global::Terralayer.Common.Depths.Rooms.HellBridgeRoom.BridgeY(arena) - 14) * 16 : (I.Center.Y - 6) * 16;
				Spawning = true;
				int index = NPC.NewNPC(NPC.GetBossSpawnSource(target.whoAmI), x, y, pick[i], Target: target.whoAmI);
				Spawning = false;
				if (index >= Main.maxNPCs)
					continue;
				NPC npc = Main.npc[index];
				var g = npc.GetGlobalNPC<DepthsNPC>();
				g.Guardian = true;
				g.FromDepths = true;
				g.Floor = Floor;
				g.Room = arena.Id;
				// Guardians take half the upgrade scaling, so fights stay tense without becoming slogs.
				npc.lifeMax = Math.Max(1, (int)(npc.lifeMax * (1f + (RunLifeMult - 1f) * 0.5f)) + flatLife);
				npc.life = npc.lifeMax;
				npc.netUpdate = true;
				Chat.AnnounceKey("Announcement.HasAwoken", new Color(175, 75, 255), npc.TypeName);
			}
			Guardian = GuardianState.Fighting;
			guardianKilled = false;
			Sync();
		}

		public void OnGuardianKilled() => guardianKilled = true;

		/// <summary>
		/// Extra life for deeper guardians, which were melting to the gear you have by then: +7000 from floor 3 down
		/// (+2000 on floor 2). Split between bosses that come as a pair.
		/// </summary>
		public static int GuardianFlatLife(int floor) => floor >= 3 ? 7000 : floor == 2 ? 2000 : 0;

		private void DefeatGuardian(RoomPlan arena, List<Player> inArena)
		{
			Guardian = GuardianState.Defeated;
			EndForcedNight();

			// The chest appears where you are standing (the hellbridge is far too long to walk back across).
			int floorY = arena.Arch == Archetype.HellBridge ? global::Terralayer.Common.Depths.Rooms.HellBridgeRoom.BridgeY(arena) : arena.FloorY;
			int chestX = inArena.Count > 0 ? (int)(inArena[0].Center.X / 16) + 3 : arena.Feature.X;
			if (DungeonBuilder.PlaceLootChest(chestX, floorY, ChestKind.Guardian, arena.Interior) < 0)
				DungeonBuilder.DropLoot(new Rectangle(chestX * 16, (floorY - 4) * 16, 32, 32), ChestKind.Guardian, Floor, runClass, runGoldMult * StreakMultiplier);

			if (lastGuardian == NPCID.HallowBoss) {
				// Secret: an empty hallowed chest. A Key of Light placed inside becomes the Rod of Discord.
				int lc = DungeonBuilder.PlaceChest(arena.Feature.X, floorY, DungeonBuilder.ChestStyle(ItemID.HallowedChest), arena.Interior);
				Secrets.SecretEvents.Instance.LightChest = lc;
				if (lc >= 0)
					Chat.Announce("A hallowed chest shimmers into being. It looks like it's waiting for a key.", new Color(255, 180, 255));
			}

			var world = TerralayerWorld.Instance;
			world.RecordBossKill(lastGuardian);
			if (Floor == world.MaxFloorUnlocked && Floor < FloorDefs.Count) {
				world.MaxFloorUnlocked++;
				TerralayerWorld.Sync();
				Chat.Announce($"A new floor opens: {FloorDefs.Get(world.MaxFloorUnlocked).Name}. The Gatekeeper can now send you there.", new Color(120, 220, 255));
			}

			string next = Floor >= FloorDefs.Count ? "the Depths are conquered! Step into the gold rift to return in glory" : "step into the purple rift to descend, or the gold rift to bank your spoils and go home";
			Chat.Announce($"The guardian of the {Plan.Info.Name} has fallen! A chest appears; {next}.", new Color(255, 215, 80));
			foreach (Player p in inArena)
				TerralayerNet.SendGuardianSlain(p, Floor);
			Sync();
		}

		private void ResetGuardian()
		{
			Guardian = GuardianState.Dormant;
			guardianKilled = false;
			EndForcedNight();
			Sync();
		}

		private static void DespawnGuardians()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.GetGlobalNPC<DepthsNPC>().Guardian) {
					npc.active = false;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
				}
			}
		}

		/// <summary>Several vanilla bosses flee or enrage in daylight. The arena is deep underground, so we hold midnight during the fight.</summary>
		private void StartForcedNight()
		{
			if (forcingNight)
				return;
			forcingNight = true;
			savedDayTime = Main.dayTime;
			savedTime = Main.time;
		}

		private void EndForcedNight()
		{
			if (!forcingNight)
				return;
			forcingNight = false;
			Main.dayTime = savedDayTime;
			Main.time = savedTime;
			Sync();
		}
	}
}
