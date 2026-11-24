using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Terralayer.Common.Config
{
	public class TerralayerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static TerralayerConfig Instance => ModContent.GetInstance<TerralayerConfig>();

		[Range(0, 100)]
		[DefaultValue(50)]
		[Slider]
		public int TollPercent;

		[Range(0, 50)]
		[DefaultValue(20)]
		public int PinnedLayoutExtraToll;

		[Range(0, 100)]
		[DefaultValue(23)]
		[Slider]
		public int DeathCoinLossPercent;

		[Range(0.25f, 4f)]
		[Increment(0.25f)]
		[DefaultValue(1f)]
		public float EnemyStrength;

		[Range(0.5f, 5f)]
		[Increment(0.25f)]
		[DefaultValue(1f)]
		public float GoldMultiplier;
	}
}
