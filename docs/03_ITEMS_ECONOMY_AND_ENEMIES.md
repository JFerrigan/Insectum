# 03 — Items, Nektar, Shops, Enemies & Encounters

## Inventory and item acquisition (DECIDED)

- **One active item** held at once.
- **Unlimited passive items**; no conventional limited-slot equipment system.
- Each item has **fixed effects**, not per-item upgrade tiers.
- A run's collection is lost on normal death; a revive item can avert a death.
- Sources: **chests around the map, shops, boss rewards, rare enemy drops**.
- A few items per world, approximately **3 on average**; no hard cap or hard guarantee.
- Distinct pools: universally available, biome/world-specific, and enemy-specific. Some enemy drops thematically match the enemy (e.g., a moth can drop its eyes).
- Powerful, even occasionally absurd, item **synergies** are desired.

### Item categories (theme, not forced power ranking)

- Primitive/natural: ropes, bombs, picks, biological materials, basic traps.
- Industrial/modern: mechanical drills, grapples, explosives, jetpack-like gear.
- Cybernetic: implants, shield amplifiers, automated weapons, biological augmentations.
- Alien: advanced/strange artifacts and organic-tech abilities.

Equipment may alter class abilities, grant an entirely new ability, or give other classes access to terrain destruction and ranged combat. A world-restricted drop **is not necessarily stronger** than a common early-world item.

## Example item catalog (only Moth Eyes is user-given; others are illustrative)

| Item | Type | Example effect | Pool | Status |
|---|---|---|---|---|
| **Moth Eyes** | Passive | Night vision for dark/night levels | Moth enemy-specific | **Named by designer** |
| Excavator Claw | Active | Removes a target destructible hex | General/industrial | Proposed |
| Wing Booster | Passive | Reduces flight energy drain | Tech | Proposed |
| Burrow Sense | Passive | Shows nearby hidden Nektar in destructible tiles | Forest/Caves | Proposed |
| Shield Echo | Passive | Beetle block releases a short shockwave | Cybernetic | Proposed |
| Piercing Mandibles | Passive | Melee strike hits more targets | General | Proposed |
| Volatile Blood | Passive | Killed enemies produce a small explosion | Alien/industrial | Proposed |
| Flame Glands | Passive | Attacks ignite the enemy as a status effect | General | Proposed |
| Emergency Reboot | Passive | Resurrects player once this run | Cybernetic | Proposed |
| Silk Reactor | Passive | Spider leap emits a trailing snare | Alien/organic | Proposed |

Examples are for discussing synergy architecture, **not approved inventory commitments**. The main rules about acquisition and item behavior remain the authority.

## Event-driven item synergy system (PROPOSED)

Suggested published events:

```text
OnAttackStarted / OnAttackHit
OnEnemyKilled
OnDamageAttempted / OnDamageTaken / OnDamageBlocked
OnShellImpact
OnAbilityActivated / OnAbilityEnded
OnEnergySpent / OnEnergyRestored
OnTileDestroyed
OnNektarCollected
OnEnterLevel / OnExitLevel
OnRevive
```

Apply modifiers in deterministic stages. For example: base attack -> numeric modifiers -> hit geometry/status -> damage and knockback -> on-hit reactions -> on-kill reactions. Avoid a global chain reaction without safeguards; use an event count / recursion depth budget to protect performance while retaining wild combos.

### Example build stories (PROPOSED)

- Fire Ant + Flame Glands + Volatile Blood -> area-clearing chain reactions when melee kills ignite and detonate foes.
- Worm + Burrow Sense + excavator refunds -> route scouting and rich Nektar extraction.
- Beetle + Shield Echo + damage-block rewards -> shell-dive defensive offense.
- Dragonfly + Wing Booster + aerial-attack proc -> ranged pressure in hard-to-reach spaces.
- Spider + Silk Reactor + leaping shockwaves -> aerial control and crowd trapping.

## Economy (DECIDED name and collection mechanisms)

Currency is spelled **Nektar**. It is found visibly, unearthed by digging terrain, and sometimes dropped by enemies. It is spent in shops.

**Open:** Starting Nektar, pickups per level, whether balances carry across worlds (reasonable proposal: yes, throughout a run), shop frequency, rerolls, theft mechanics, price scaling, damage-dealing shopkeepers, and exact currency UX.

**First-pass suggestion:** Aim for one accessible shop opportunity per world or a comparable soft frequency; keep shop item offers driven by relevant pool weights. Offer item choices and at least one affordable purchase during a typical run without forcing grind/exploration. This suggestion is not an approved fixed schedule.

## Enemy behavior (DECIDED foundation)

- Enemies occur in most chunks, but combat is not mandatory if a player can safely traverse past them.
- Some enemies patrol a platform; some fly semi-randomly and engage when player enters range; others use distinct patterns.
- Enemies are biome-weighted, with some shared across worlds.
- Some enemies can destroy terrain; attacks animate and can knock players back, just as player attacks do.
- Specific wildlife should be suitable for the biome where realistic; Alien world permits completely invented flora/fauna.

### Example AI archetypes (PROPOSED)

| Archetype | Behavior | Suitable examples (not final enemy roster) |
|---|---|---|
| Patrol | Walk between ledges or walls; turn back | Ant / beetle |
| Proximity flier | Wander, target on range trigger, disengage beyond range | Moth / mosquito |
| Ambush | Wait in cover or at an opening; lunge | Spider / antlion |
| Turret/trap-adjacent | Stationary wind-up projectile or melee | Alien sentry / stinging organism |
| Digger | Alters certain soft tiles during pursuit | Burrowing invertebrate |

## Bosses (DECIDED foundation)

One world-ending boss per world (provisionally level 4); areas may mix destructible and indestructible terrain. Boss victory grants an item. Environments/boss enemies change with biome; certain recurring enemy motifs are allowed.

Mire Lich is the 40-health Swamp boss on level 8. Her abdomen takes double
damage and grows after feeding. Her defeat drops four Nektar after the death
animation. Claiming the Reed Flask unlocks the exit to Caves.

**Open:** Boss identities, phases, health, destruction rules, unlock effects, double-boss variant implementation, and whether every boss is always mandatory. Concept boss art is **illustrative** only; no named 'Hive Matriarch' is finalized.

## Traps and level variants

- Deadly traps are permitted and should be readable enough to preserve fair platforming.
- **Night** variant: dark environment, limited player-centered visibility radius (and Moth Eyes can help).
- Possible variants: **heavy enemy spawns**, **double boss**. Others TBD. No permanent map or fog-of-war rule has been finalized.
- No countdown/time limit in first revision. No assumption of guaranteed secrets/alternate exits in first revision.

## Spawn and reward balancing (PROPOSED)

- Separate `BiomeSpawnPool` tables for enemies, traps, materials, chest loot, item pools, and level modifiers.
- A generator can spend enemy/threat budgets per chunk while keeping the protected traversal route fair.
- Enemy-specific rare drops are checked per eligible kill, independently from ordinary Nektar rewards.
- Distinguish item spawn *attempts* from item acquisition: a chest behind a hard side route may be skipped by a player.
- Track run-level item count for analytics, but **do not force it to three items each world**.
