using System.IO;
using Terralayer.Common.Net;
using Terraria.ModLoader;

namespace Terralayer
{
	public class Terralayer : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI) => TerralayerNet.Handle(reader, whoAmI);
	}
}
