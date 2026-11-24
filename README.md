# Terralayer

A tModLoader mod that turns Terraria into its own lineage roguelite. You get a homestead you grow with gold, heirs that replace each other on death, and five procedurally generated floors whose rooms are carved across the whole world and joined by rifts.

> **Use a fresh character and a new world, and disable other content mods for Terralayer worlds.** Terralayer replaces world generation entirely. Any character entering a Terralayer world is stripped of all items, banks and consumed crystals, and is set to Softcore. A confirmation screen appears first, and you can leave instead. New characters also start with nothing, in any world.

## Building

This folder is linked into tModLoader as `Documents\My Games\Terraria\tModLoader\ModSources\Terralayer`.
- **In game:** Workshop → Develop Mods → Terralayer → **Build + Reload**.
- **Terminal:** `.\build.ps1`. Always build through the junction path; building from the real path names the mod `terralayer`.
- To regenerate the placeholder art: `python tools/make_art.py`.

## The loop

1. **The homestead.** You start in a valley with the **Steward** (Estate and Skill Tree), the **Gatekeeper** (the Depths), the **Herald** (quest board) on the plaza, the **Runeforge**, two **training dummies** east of spawn, and the **Shrine of Guardians** floating over the spawn point (two tiers of relics, 15 and 25 tiles up, reached by a platform ladder). A sign on the plaza credits Reshiro YT. You have no tools, and the terrain can't be edited.
2. **Pick an heir.** Each card shows a **class** (an armour set, a weapon, sometimes accessories), its **variant** (e.g. an Acolyte who tames a slime instead of a finch), 1–3 traits (burdens pay bonus gold) and any runes. Every heir also gets a grappling hook.
3. **Grow the estate.** Raise buildings in order, each unlocking upgrade nodes on the **Skill Tree**. The tree has two pages: *Upgrades* (Might, Survival, Agility, Arcane and Fortune branches) and *Classes*.
4. **Quests.** The Herald posts five notices; accept up to three. Each has a **1–5 star rating** (green, blue, purple, orange, crimson) and an array of rewards that grows with the stars: gold, a rune of matching rarity, healing potions, tempering (refinement on your heir's weapon) and boons for your next descent. Quests stay accepted across heirs until you claim or abandon them; reposting the board costs 2g. Types: slay a floor's guardian (plain, with only your starting weapon and no evolving, taking 3 hits or fewer, within a time limit, without Boons, without potions, or with a burdened heir), bring home gold earned in one descent, slay champions, enter every room of a floor, use shrines, and dive from floor 1 to a deeper floor in one go.
5. **The toll.** The Gatekeeper takes 50% of the coins in your purse and **half that rate (25%) of the coins in the Gatehouse safe**. Coins in the safe survive death and every descent.
6. **The Depths.** Every descent starts on **floor 1**. A combat room **seals its rifts to new rooms until it's cleared**, but you can always **fall back** through rifts to rooms already explored; room enemies never despawn, so the fight waits for you. The guardian's arena is just another room: no need to clear the whole floor first. Only **ambush** rooms send a second wave (with a champion); they look like ordinary chambers until it arrives. A room with no kills for 75 seconds (while you're in it) releases you. Rifts, shrines and forges are used by **right-clicking**.
   - **Map:** Terralayer's map replaces vanilla's. **M** (or N) opens it full screen with a sidebar showing your heir, traits, ability, the descent's progress, boons and quests; during a descent a minimap sits under the life bars.
   - **No natural regeneration:** life and ability charge don't come back on their own. Charge builds by hitting enemies; both refill fully when a descent starts and when you get home, and otherwise come from **restoration** rewards, potions and regeneration effects.
   - **Room rewards:** clearing a room offers three cards, each with a rarity colour. The first is always a **Common restoration** card (Mend, Recharge, Catch Your Breath, Bandage, Clarity, Field Rations, Spare Potion, Ironskin Tonic). The others roll: **Uncommon** (Refine, Reforge, Boon, Coin Purse, Potion Satchel, Full Restore, Charm), **Rare** (Arsenal: a new weapon, Twin Boon, Rune Stone, Armory, Trinket, Double Refine), **Epic** (Mystery Cache, Ascendant Rune, Triumph, Masterwork, Gilded Arsenal), **Legendary** (Ascension, Blessing of the Line, Legendary Rune).
   - **Ambushes** (one per floor, sometimes two from floor 3) are the champion rooms; they hold no chest, the champion's drop is the prize.
   - **Champions** have 7x life (8x from a Shrine of Trials or a trapped cache), +50% damage, heavy armour, no knockback, a bigger body, and an affix shown as a coloured aura: Brutal (red), Armored (blue), Vampiric (magenta), Regenerating (green: 25% less life, slow regeneration).
   - **Guardians** you've beaten sit out of their floor's pool until you've beaten every guardian on that floor. They are fixed per layout and each gets a themed arena (slime pit, crimson hollow, royal hive, frozen den, bone cathedral, iron foundry, crystal court, overgrowth, sun altar, prism sanctum); from floor 3 down they have +7000 life (+2000 on floor 2).
   - Enemies can't pick up coins in Terralayer worlds, and gold-find bonus coins per kill are capped at double.
   **Room rewards:** clearing a room offers three cards; pick one. *Refine* gives a found copy of your class weapon in its current form (+1 fusion); 40% of the time this card is *Arsenal* instead, a different weapon from your path at a better rarity. *Boon* gives a random minor stat for this descent (stacks; shown on the HUD). *Mystery Cache* is a rune, deeper accessory, gold, a double blessing, or a trap champion that drops a strong weapon.
7. **The guardian.** A boss is picked at random from the floor's list, except floor 3, which is always the **Wall of Flesh** on an ~860-tile hellbridge over lava. Midnight is forced during the fight. The first kill of each guardian raises its Master Mode relic in the Shrine of Guardians.
8. **Death.** A custom death screen shows the fallen heir's epitaph (cause, floor, rooms cleared, coins lost and kept) and the next three heirs; the one you pick rises at once. You lose 23% of the coins you carry (configurable); the safe is untouched. Everything else the heir carried is lost. **Coming home alive keeps your whole inventory** and the heir.

## Gear

- **The Smith** sells no weapons. His forge refines a weapon a level for **Refinement Shards** from chests (2 shards for +1, then 4, 6 ... 18, plus a gold fee), merges accessories using the Tinkerer's Workshop recipes (e.g. Hermes + Rocket Boots = Spectre Boots), and buys spare gear (never your class weapon, held or favourited items).
- **Accessories** never drop with a modifier. Two copies merge into one a **forge stage** higher, each stage a better modifier of one family: stage 1 green, 2 blue, 3 yellow. Traits like Cloud Step give a real **innate** accessory instead of an invisible effect.
- **No armour set bonuses** in Terralayer worlds: each piece counts only for its own stats and rune, so a better piece from a chest is always worth wearing.
- **Evolution lines:** every class weapon belongs to a 5-stage line, e.g. Copper Broadsword → Enchanted Sword → Muramasa → Excalibur → True Excalibur. Chests only give stages 1–3; stages 4–5 come from evolving.
- **Merging:** a found copy of a weapon you carry is one *fusion*. Going from Lv N to N+1 takes N fusions (Lv1→2 needs 1, 2→3 needs 2 …, 45 to max), and refined copies pass on everything invested in them. Damage per level falls off: +6% to Lv4, +4% to Lv7, +2% to Lv10 (+36% max). **Evolution** needs refinement +3; once eligible, each merge has a 40/25/15/8% chance to offer it as a popup, and evolving **resets refinement to +0** (rarity and runes stay). Weapons remember their peak mastery, and shop-bought copies can't be fused in.
- **Rarity:** Common 70%, Uncommon 22%, Rare 6.5%, Epic 1.4%, Legendary 0.1%. Rarity adds a modest bonus and 1–3 rune sockets.
- **Weapon runes** (11) and **armour runes** (10: Fortitude, Vigor, Swiftness, Warding, Renewal, Fury, Zephyr, Focus, Fortune, Thorns) are inscribed for gold at a **Runeforge**.
  - **Inscription odds:** every rune stone shows its odds as green success : red failure. A failed inscription shatters the rune (the fee is still paid). Never below 30%: Common runes roll 30–83%; each rarity up raises the floor by 5 and the ceiling by 4 (Legendary 50–99%). Shop runes top out 20 points lower and re-roll each visit.
  - The **Runeweaver** is a travelling merchant: six random runes (random type, rarity and odds) that change after every descent and sell once each, plus **Sealed Rune Pouches** (5g, right-click for one completely random rune).
  - Both the homestead forge and **Field Runeforge** rooms work on your hotbar weapons and worn armour (field forges at 1.5× the fee).
  - Three identical runes fuse into the next rarity, keeping the **best odds of the three +6%**.
- **Chests** are rare (15% of combat rooms). Each holds one main item (a weapon, accessory or armour piece) plus a potion and coins. Your path's weapons show up 85% of the time. Ranged weapons come with ammo. **Looted armour always comes with a random armour rune** already inscribed. The basic Grappling Hook is never loot (every heir starts with one).
- **Wings** don't drop before floor 4 and get only 35% of their normal flight time.
- Monsters drop only hearts, mana stars and coins.
- **Enemies scale with your family:** +3.5% enemy life per skill-tree level and +5% per guardian trophy (max 3.5×), locked in when a descent starts. Guardians take half of it. The HUD shows the current multiplier.

## Classes (Skill Tree → Classes)

Classes are armour sets plus a weapon; higher tiers add accessories. A path's identity is its passive and its ability (armour set bonuses are off). Each tier needs its parent class, the trophy from that floor's guardian, and gold: 15g, 45g, 120g, 300g, then 750g. Tier-0 and tier-1 classes have **variants** with different starting weapons.

| Path | Ability | Tier 0 → … → top |
|---|---|---|
| Guardian | **Aegis Charge**: shield-dash, untouchable, bowling enemies over, then 40% damage reduction for 3s | Squire → Knight → Templar → Molten Champion → Paladin → Juggernaut |
| Marksman | **Arrowstorm**: vault back and rain fire arrows on the cursor for 2s | Hunter → Marksman → Fossil Ranger → Necro Gunner → Frost Ranger → Shroomite Sniper |
| Arcane | **Arcane Orbit** (toggle): three orbs circle you, burning foes and firing bolts; spells cost 40% less | Apprentice → Magus → Meteor Mage → Crystal Sorcerer → Hallowed Mage → Spectre Archmage |
| Shadow | **Shade Step**: blink 20 tiles; the next hit within 2s is a 150% critical ambush | Footpad → Shade → Duelist → Crystal Assassin → Ninja Master |
| Warrior | **Earthshatter**: plunge and slam, shockwaves roll both ways, then 5s of rage (+20% damage, lifesteal) | Brawler → Gladiator → Berserker → Warlord |
| Beastmaster | **Spirit Pack**: three spectral wolves hunt for 10s; minions and whips +25% meanwhile | Acolyte → Beekeeper → Whipmaster → Spider Queen → Tiki Shaman |
| Riftwalker | **Rift Jump**: open the map and step into any explored room (a short blink at home); rift travel gives a burst of speed | Rift Initiate → Waystrider → Voidwalker → Rift Lord |
| Chronomancer | **Rewind**: snap back to where you were 3s ago, with that moment's life if higher, shedding debuffs | Timekeeper → Hourwarden → Chronarch → the Timeless |

## Shrines

Blood Altar (max life → strong rune) · Fortune Altar (gold → deeper gear) · Altar of Change (swap your ability for another path's) · Wayward Shrine (random teleport) · Shrine of Trials (champion → strong weapon) · Healing Spring · Gambler's Shrine · Shrine of Refinement (+1 refinement for max life)

No fallen stars fall in Terralayer worlds.

Each shrine's chamber is built in its own materials: crimtane and flesh (Blood), gold and platinum (Fortune), glowing amethyst (Change), sunplate and clouds (Wayward), hellstone and obsidian (Trials), marble with a pool and fountain (Spring), a ruby-and-obsidian checkerboard (Gambler), cobalt and silver with an anvil and forge (Refinement).

## Secrets

Hidden quests whose rewards are **eternal**: the family keeps them through every death (and they can't be sold).

- **The Key of Light.** On floors 4 and 5 a treasure room may hide a **Hallowed Mimic**; it drops a Key of Light. When the Empress of Light falls, a hallowed chest appears in her arena: put the key in it and it becomes the **Rod of Discord**.
- **The hidden sea.** A crimson chest waits at the far right end of the Wall of Flesh's hellbridge, holding a **Pirate Map**. Use it on the bridge once the Wall is dead and a pirate army boards; defeat 50 and the wind carries you to a hidden sea at the world's edge. The **Angler** waits there ("I never thought someone would make it here. Prove it wasn't a fluke.") and gives you a **Golden Bug Net**: from then on golden creatures turn up in Depths rooms, and a rift on the plaza (past the Gatehouse) leads back to the sea.
- **Duke Fishron.** Bring the Angler five golden creatures for a **Golden Fishing Rod** and a **Truffle Worm**; ask him to bait the sea and Duke Fishron rises (each later attempt costs one golden creature). Defeat him and his relic takes a pedestal beside the Shrine of Guardians, and the **Tidecaller** class can be bought on the Skill Tree: built-in Fishron Wings, the Typhoon ability (six seeking razor typhoons) and three weapon lines of its own (Swordfish to Anchor, Harpoon to Tactical Shotgun, Aqua Scepter to Tempest Staff).

## Rooms

No two connected rooms share a layout, and rooms are roughened (stalactites, wall bulges, low floor humps) with platforms in their own materials. Natural caves come in stone, mushroom, ice, desert, crystal, marble and granite.

General rooms: halls, caverns, shafts, lava forges, sky gardens, trap gauntlets and workshops (the flooded hall is gone, and caverns no longer have pools). **Themed halls** bring their own monsters:

| Room | Floors | Monsters |
|---|---|---|
| Hoplite Barracks (marble) | 1 | Hoplites, skeletons |
| Granite Hollow | 1–2 | Granite golems and elementals |
| Ossuary (bone galleries, graves) | 1 | Angry bones, cursed skulls, skeletons |
| Forbidden Library | 1 | Dark casters, cursed skulls |
| Slime Pit (gel floor, bounce pads) | 1, 4 | Slimes; illuminant slimes and toxic sludge deeper down |
| Hive | 2, 5 | Hornets and bees; moss hornets deeper down |
| Spider Nest | 2, 5 | Wall creepers, jungle creepers; black recluses deeper down |
| Imp Den (demon altar) | 3 | Fire imps, demons, hellbats |
| Unicorn Meadow (open sky) | 4 | Unicorns, pixies |
| Lihzahrd Sanctum (spear traps) | 5 | Lihzahrds, flying snakes |

## Floors

| # | Floor | Guardians |
|---|---|---|
| 1 | Forgotten Halls | King Slime, Eye of Cthulhu |
| 2 | Verdant Depths | Queen Bee, Deerclops, Skeletron |
| 3 | Cinder Keep | **Wall of Flesh** (fixed, hellbridge arena) |
| 4 | Prism Spire | The Destroyer, Skeletron Prime, The Twins, Queen Slime |
| 5 | Sunken Temple | Plantera, Golem, Empress of Light |

## Testing commands (chat)

`/tl enter [floor]` (skips the toll; the only way to start below floor 1) · `/tl home` · `/tl heir` · `/tl unlock <1-5>` · `/tl coins <gold>` · `/tl build` (raise every building) · `/tl boss` (warp to the arena) · `/tl chart` (reveal the map) · `/tl boss all` (toggle every guardian slain: relics and the Moon Lord portal) · `/tl max` (Zenith, Rod of Harmony, solar armor and endgame accessories)

## Status and known gaps

- Verified on a headless server (small and medium worlds):
  - World generation, including the shrine, training dummies and relics.
  - All five floors, with every rift reachable using a conservative 5-tile jump.
  - Room sealing and the guardian-rift gate.
  - A real Wall of Flesh spawning and advancing, with no hardmode conversion on its death.
  - Trial champions, and the Moon arena build.
- Also verified headlessly: back-travel and forward-sealing rifts and the open arena on every floor, the safe toll, rune odds and fusion, quest generation and saving, every shrine theme, the credit sign and the Herald.
- **Not yet verified in the game client:** the screens (skill tree, Runeforge, quest board, death screen, evolution popup), rift and portal drawing, abilities, shops, how the vanilla bosses behave in their arenas, and the Moon Lord fight itself.
- Multiplayer has networking code but is untested; treat the mod as singleplayer-first.
- NPCs use vanilla sprites. Override `Texture`/`HeadTexture` in `Content/NPCs`, or add PNGs, to use custom art.

## Code map

- `Common/World`: world gen, homestead structures, upgrades, tile protection, `Gen` editing helpers
- `Common/Depths`: floor definitions, `FloorPlan` (room graph and world placement), `Rooms/` (room types; `ThemedRooms.cs` for the monster halls), `DungeonBuilder`, `DepthsSystem` (run state, spawns, champions, guardian), `RewardCards` (room-clear card pool), `RiftRenderer`
- `Common/Items`: rarity, `RefinedWeapon` (refinement, merging, runes on weapons), rune effects, loot tables
- `Common/Heirs`, `Common/Players/LegacyPlayer.cs`: heirs, classes and variants, traits, charge, death, run start and end; `LegacyPlayer.Abilities.cs`: the eight class abilities; `LegacyPlayer.Rewards.cs`: rolling and applying room-clear cards
- `Common/Quests`: quest definitions, generation and rewards (`Quests.cs`), tracking and the board (`QuestPlayer.cs`)
- `UI`: heir select and death screen, quest board, Estate, wipe warning, floor map (and Rift Jump picker); HUD in `Common/Systems/UISystem.cs`; `GenTextures` draws the star, glow and vignette textures in code
- `Content`: NPCs (including the Herald), rune items, ability projectiles; `Assets` and `tools/make_art.py` for art
