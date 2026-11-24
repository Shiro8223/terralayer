using System.IO;
using Microsoft.Xna.Framework;
using Terralayer.Common.Depths;
using Terralayer.Common.Heirs;
using Terralayer.Common.Players;
using Terralayer.Common.World;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Terralayer.Common.Net
{
	public enum PacketType : byte
	{
		RequestRun,     // client -> server: floor, class, gold multiplier, enemy life multiplier
		Arrive,         // server -> client: x, y, floor
		WorldAction,    // client -> server: action, arg, text
		RequestDescend, // client -> server
		GuardianSlain,  // server -> client: floor
		SyncPlayer,     // both ways
		RequestTrial,   // client -> server: a Shrine of Trials was used
		RequestMoon,    // client -> server: the secret portal was used
		MoonArrive,     // server -> client: x, y
		RoomReward,     // server -> client: you cleared a room, pick a reward
	}

	public enum WorldAction : byte
	{
		Build,
		TogglePin,
		SetMaxFloor,
		BuildAll,
		ToggleAllBosses
	}

	public static class TerralayerNet
	{
		private static Mod Mod => ModContent.GetInstance<Terralayer>();

		public static void Handle(BinaryReader reader, int whoAmI)
		{
			var type = (PacketType)reader.ReadByte();
			switch (type) {
				case PacketType.RequestRun: {
					int floor = reader.ReadByte();
					var cls = (HeirClass)reader.ReadByte();
					float gold = reader.ReadSingle();
					float life = reader.ReadSingle();
					if (Main.netMode != NetmodeID.Server)
						return;
					Player p = Main.player[whoAmI];
					Vector2 pos = DepthsSystem.Instance.StartRun(floor, p, cls, gold, life);
					SendArrive(p, pos, DepthsSystem.Instance.Floor);
					break;
				}

				case PacketType.Arrive: {
					var pos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
					int floor = reader.ReadByte();
					Main.LocalPlayer.GetModPlayer<LegacyPlayer>().ArriveInDepths(pos, floor);
					break;
				}

				case PacketType.WorldAction: {
					var action = (WorldAction)reader.ReadByte();
					int arg = reader.ReadInt32();
					string text = reader.ReadString();
					if (Main.netMode == NetmodeID.Server)
						TerralayerWorld.Instance.ApplyAction(action, arg, text);
					break;
				}

				case PacketType.RequestDescend:
					if (Main.netMode == NetmodeID.Server)
						DepthsSystem.Instance.Descend();
					break;

				case PacketType.GuardianSlain:
					Main.LocalPlayer.GetModPlayer<LegacyPlayer>().OnGuardianSlain(reader.ReadByte());
					break;

				case PacketType.RequestTrial:
					if (Main.netMode == NetmodeID.Server)
						DepthsSystem.Instance.SpawnTrialChampion(Main.player[whoAmI]);
					break;

				case PacketType.RequestMoon:
					if (Main.netMode == NetmodeID.Server) {
						Vector2 pos = TerralayerWorld.Instance.StartMoonFight(Main.player[whoAmI]);
						ModPacket reply = Mod.GetPacket();
						reply.Write((byte)PacketType.MoonArrive);
						reply.Write(pos.X);
						reply.Write(pos.Y);
						reply.Send(whoAmI);
					}
					break;

				case PacketType.MoonArrive:
					Main.LocalPlayer.GetModPlayer<LegacyPlayer>().Teleport(new Vector2(reader.ReadSingle(), reader.ReadSingle()), TeleportationStyleID.TeleporterTile);
					break;

				case PacketType.RoomReward:
					Main.LocalPlayer.GetModPlayer<LegacyPlayer>().OnRoomCleared();
					break;

				case PacketType.SyncPlayer: {
					int player = reader.ReadByte();
					var lp = Main.player[player].GetModPlayer<LegacyPlayer>();
					lp.ReceiveSync(reader);
					if (Main.netMode == NetmodeID.Server)
						lp.SyncPlayer(-1, whoAmI, false);
					break;
				}
			}
		}

		public static void RequestRun(int floor, HeirClass cls, float goldMult, float lifeMult)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.RequestRun);
			p.Write((byte)floor);
			p.Write((byte)cls);
			p.Write(goldMult);
			p.Write(lifeMult);
			p.Send();
		}

		public static void RequestTrial()
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.RequestTrial);
			p.Send();
		}

		public static void RequestMoonFight()
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.RequestMoon);
			p.Send();
		}

		public static void RequestDescend()
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.RequestDescend);
			p.Send();
		}

		/// <summary>Teleports a player into the Depths (server or singleplayer).</summary>
		private static bool Connected(Player player) => Netplay.Clients[player.whoAmI].IsActive;

		public static void SendArrive(Player player, Vector2 pos, int floor)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) {
				player.GetModPlayer<LegacyPlayer>().ArriveInDepths(pos, floor);
				return;
			}
			if (!Connected(player))
				return;
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.Arrive);
			p.Write(pos.X);
			p.Write(pos.Y);
			p.Write((byte)floor);
			p.Send(player.whoAmI);
		}

		public static void SendGuardianSlain(Player player, int floor)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) {
				player.GetModPlayer<LegacyPlayer>().OnGuardianSlain(floor);
				return;
			}
			if (!Connected(player))
				return;
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.GuardianSlain);
			p.Write((byte)floor);
			p.Send(player.whoAmI);
		}

		public static void SendRoomReward(Player player)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) {
				player.GetModPlayer<LegacyPlayer>().OnRoomCleared();
				return;
			}
			if (!Connected(player))
				return;
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.RoomReward);
			p.Send(player.whoAmI);
		}

		/// <summary>Moves a player somewhere (server or singleplayer); reuses the Moon-arrival message.</summary>
		public static void SendTeleport(Player player, Vector2 pos)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) {
				player.GetModPlayer<LegacyPlayer>().Teleport(pos, TeleportationStyleID.TeleporterTile);
				return;
			}
			if (!Connected(player))
				return;
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.MoonArrive);
			p.Write(pos.X);
			p.Write(pos.Y);
			p.Send(player.whoAmI);
		}

		public static void SendWorldAction(WorldAction action, int arg, string text)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.WorldAction);
			p.Write((byte)action);
			p.Write(arg);
			p.Write(text ?? "");
			p.Send();
		}

		public static ModPacket StartPlayerSync(int player)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PacketType.SyncPlayer);
			p.Write((byte)player);
			return p;
		}
	}

	public static class Chat
	{
		public static void Announce(string text, Color color)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				Main.NewText(text, color);
			else if (Main.netMode == NetmodeID.Server)
				ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text), color);
		}

		public static void AnnounceKey(string key, Color color, params object[] args)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				Main.NewText(Language.GetTextValue(key, args), color);
			else if (Main.netMode == NetmodeID.Server)
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key, args), color);
		}
	}
}
