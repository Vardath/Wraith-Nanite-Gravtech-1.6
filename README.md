# Wraith & Nanite Gravtech — reconstruction workspace

This public `main` branch is the active WNG RimWorld 1.6 reconstruction workspace.

The rebuild is self-contained and does **not** require the obsolete original WNG source tree. Private historical builds remain comparison evidence for feature coverage only; newer project-chat requirements override older implementation details.

Locked current rules include:
- Biotech + Odyssey hard dependencies; CatCraft Stargates!, ONAC and RimGate optional/dependency-safe.
- Preserve the approved Replicator graphics, established faction structure and working split/recombine behavior.
- Replicator split-born recombination cooldown: **2,500 ticks (one in-game hour)**.
- Exact split ladder: Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones.
- Dangerous Replicator Matter minimum stack 10; dormancy 30,000 ticks.
- Child's Toy gestation exactly 90,000 ticks; if it goes feral it **transforms into a Replicator** and enters the swarm threat loop.
- No obsolete Gravcore progression. Canonical gravship fuels are `WNG_WraithBiofluidFuel` and `WNG_AsuranNaniteSlurry`.
- Final acceptance requires compile/package audits plus live RimWorld startup/gameplay/save-load and Player.log/RimDoctor review.

Migration/reconstruction is still in progress; individual commits are checkpoints, not completion claims.
