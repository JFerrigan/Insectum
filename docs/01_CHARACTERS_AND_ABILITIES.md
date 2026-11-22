# 01 — Character & Ability Design

## Common contract (decided)

- All five characters walk, jump, have a **basic melee attack forward or upward**, can attack while moving, and may bypass enemies instead of fighting.
- **Only some** characters climb; not every bug can climb walls or ceilings.
- Attacks use animations and hit detection; successful hits generate knockback. Enemies attack through comparable action/attack logic.
- Characters have differentiated starting hearts; Beetle has the most and Dragonfly the least. Exact hearts are proposed below.
- Ability-energy bar fully replenishes upon entering each new level. Energy may be replenished during a level by food and items; ability-specific costs can be fixed per action, per-time drain, per hit absorbed, or by material.
- Fall damage exists and can be avoided through special abilities; collisions use continuous physics against a smoothed terrain boundary, not grid-cell hopping.

## Prototype-only numeric baselines (PROPOSED, not approved)

These are convenient initial values to test relative balance, and can be changed in CSV. Assume maximum energy **100** for all characters on the first playable pass; it does *not* establish mandatory identical energy capacities in the final design.

| Character | Hearts | Core ability | Suggested resource cost | Notes |
|---|---:|---|---|---|
| Jumping Spider | 3 | Powerful special leap and wall climb | Leap 16/action; climbing 3/sec | Ceiling cling, webs, venom still **open** |
| Beetle | 6 | Toggle invincibility at cost of movement control | 25 per heart of damage prevented | Zero-energy break behavior and impact tuning **open** |
| Dragonfly | 2 | Toggle sustained flight | 14/sec while active | ~7.1 total seconds at 100 energy; adjustable |
| Fire Ant | 4 | Stronger, wider special melee strike | 15/action | Default attack remains free |
| Worm | 4 | Instantly excavate hex forward/below | Material cost; see terrain table | Upward digging not specified |

## Jumping Spider — mobility specialist

**Decided:** Can climb walls; can jump unusually far via a big special leap. Ceiling climbing, web creation and venom attacks are ideas, not committed features.

**Proposed first pass:** Normal jump uses universal controller; special leap is button-press or brief charge followed by a directional impulse. Face direction sets the primary leap; upward/diagonal aim can be enabled. Wall climbing consumes energy continuously while actually climbing. A special leap may launch away from a wall. Movement speed in air must remain responsive without turning it into flight.

**Open:** Whether special leap needs a hold-to-charge animation; whether wall clinging is free when stationary; whether any ceiling climbing is present; whether web/venom belongs to innate kit or item pool; whether normal ledge grabbing applies to all.

**Potential item synergies (examples, not inventory commitments):** Second leap on enemy kill; kinetic shockwave on landing; silk trail that catches projectiles; leap energy discount.

## Beetle — tank / defensive momentum

**Decided:** Beetle can toggle an **invincible shell state**. While active it **loses movement control**. No energy is spent simply remaining in the state; energy is deducted only when damage that would normally affect hearts is absorbed. The state can prevent fall damage and allows landing on enemies to deal damage.

**Proposed first pass:** Shell mode prevents normal steering/jumping/attack commands but keeps gravity and horizontal velocity at activation (or dampens it by a tunable factor). Falling onto enemies in shell state deals fixed impact damage if downward speed exceeds a minimum. Fall impact energy is charged by the would-be fall-damage value. Toggle off to regain movement control. Shield runs out only when it cannot pay the required cost.

**Open and important:** What precisely happens if an incoming hit exceeds remaining energy? Suggested: shield breaks and the hit damages hearts normally. Whether the beetle bounces after an impact, may move horizontally at all after toggling, and whether shell mode cancels knockback are **not** finalized. Confirm edge cases with spikes, crushing hazards, boss contact, and falling onto enemies.

**Potential item synergies:** Shell slams emit shockwaves; reflect projectiles; blocking grants Nektar; a block-triggered explosion consumes terrain.

## Dragonfly — flight specialist

**Decided:** Toggleable flight with a **constant per-second energy drain** during flight. Dragonfly starts with the fewest hearts.

**Proposed first pass:** While flight is toggled, directional input controls vertical and horizontal acceleration and hovering is possible, with normal attacks allowed. Automatically disengage flight at zero energy. Ground contact does not independently refill energy. Include a gentle acceleration/deceleration curve to preserve insect-like movement rather than instantaneous no-physics translation.

**Open:** Flight speed, ascent ceiling, collision behavior during powered movement, whether takeoff requires a jump, and whether falling out of flight retains horizontal inertia.

**Potential item synergies:** Reduced flight drain; exhaust trail damages enemies; attacks create aerial lightning-like projectiles (VFX only; no conductor simulation); flight restores some energy after defeating enemies.

## Fire Ant — offensive specialist

**Decided:** Has a powerful **larger-area melee attack** that costs some fixed amount of energy. It also has the shared ordinary attack.

**Proposed first pass:** A frontal/upward wide mandible sweep with roughly double the normal attack damage, stronger knockback, and moderate cooldown. The special executes while moving. Suggested: normal attack 1 damage; special 2 damage, energy 15. Adding burn or poison is **not automatic** merely because it is a Fire Ant.

**Open:** Whether the strong attack is a separate button or a hold/alternate attack input, exact hitbox geometry, vertical special attack, cooldown, and stun effects.

**Potential item synergies:** Multi-target cleave; explosive enemy deaths; special attack summons a short-lived flame arc; attack restores ability energy on a perfect hit.

## Worm — terrain specialist

**Decided:** Can destroy a **single adjacent hex in front of or beneath the character**, instantly; material hardness controls its energy consumption. Other classes have no innate terrain excavation. Worm special action should be a legitimate alternative way to traverse a level rather than simply more combat power.

**Proposed first pass:** Hold dig to target successive neighboring cells with ~0.15 s between excavations (instant removal for each cell). Digging air or indestructible tiles uses no energy. Insufficient energy prevents removal. Distinct cells can cost 3–25 energy based on hardness. Permit digging while falling if there is a valid target and sufficient energy. Body collision should be sized such that a single-cell tunnel can be traversed; if impossible, solve explicitly via a small worm collider or a multi-cell clearance rule.

**Open:** Whether front/down digging can select both diagonal-down neighboring hexes; upward digging is not specified and should **not** be silently added; initial exact dig rate; worm body shape during jump and tunneling.

**Potential item synergies:** Excavation can reveal extra Nektar; destroy an extra adjacent soft tile; emit spikes on excavation; reduce hardness costs; trigger aftershock when encountering crystals.

## Shared movement prototype targets (PROPOSED)

- Fixed physics step (e.g. 60 Hz); horizontal acceleration over a short interval rather than full speed instantly; quick ground deceleration and distinct air control.
- Variable jump height through hold/release; collision geometry must support clean ledges and stable grounded state.
- Attack state uses an animation event/hit window, but does not lock horizontal walking unless special weapon properties require it.
- Damage pipeline: check invulnerability/shield, apply heart damage, apply knockback, begin brief i-frame period; fall damage based on landing velocity.
- Movement abilities can make optional paths easier; a mandatory path cannot require them or a random pickup.

## Ability component interface (suggested)

```text
Ability
  CanActivate(character, world, input) -> bool
  Activate(character, world, input)
  TickFixed(dt)
  Deactivate(reason)
  EnergyCost(context) -> float
  EmitEvents()  // OnLeap, OnShellBlock, OnFlightTick, OnSpecialHit, OnTileDestroyed
```

Do not model every skill as a flat `energyPerUse`: Beetle uses an **event-triggered cost**, Dragonfly uses **continuous drain**, Spider and Ant spend **fixed per-action costs**, and Worm uses a **target-material cost**.
