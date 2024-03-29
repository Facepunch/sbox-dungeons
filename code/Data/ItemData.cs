﻿
using Dungeons.Items;
using System.Collections.Generic;

namespace Dungeons.Data;

internal class ItemData
{

	public string Identity { get; set; }
	public int StashSlot { get; set; }
	public int Quantity { get; set; }
	public int Durability { get; set; }
	public int Seed { get; set; }
	public int Level { get; set; }
	public List<AffixData> Affixes { get; set; } = new();
	public ItemRarity Rarity { get; set; }

}

internal struct AffixData
{
	public string Identifier { get; set; }
	public int Tier { get; set; }
	public float Roll { get; set; }
}
