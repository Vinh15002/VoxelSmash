# Smash Master â€“ weapons + effects (extracted)

Each weapon folder is a Unity-importable subset (`Assets/...` with original GUIDs/.meta) plus FBX and flat PNG textures.
Shaders are AssetRipper dummies and game scripts (IL2CPP) are not recoverable, so particle/trail materials may need re-assigning to a URP Particles shader.
FBX cannot hold particle systems/trails: the effect definitions live in the .prefab files (YAML) â€“ summary per weapon below.

## Hammer
Prefabs: MODEL_CuteHammer, Hit_Hammer_Normal  |  files: 1x.anim, 2x.asset, 1x.controller, 3x.mat, 1x.png, 2x.prefab
**MODEL_CuteHammer** â€“ no particle/trail components
**Hit_Hammer_Normal**
- ParticleSystem on "Hit_Hammer_Normal": duration=0.05, looping=0, startLifetime=0.0001, startSpeed=0, startSize=0, maxParticles=0, rateOverTime=0
- ParticleSystemRenderer on "Hit_Hammer_Normal": renderMode=0
- ParticleSystem on "Hit": duration=0.5, looping=0, startLifetime=0.2, startSpeed=40, startSize=0.075, maxParticles=31, rateOverTime=0
- ParticleSystemRenderer on "Hit": renderMode=1
- ParticleSystem on "glow": duration=0.5, looping=0, startLifetime=0.15, startSpeed=0, startSize=3, maxParticles=2, rateOverTime=0
- ParticleSystemRenderer on "glow": renderMode=0

## BladeSaw
Prefabs: MODEL_BladeSaw, VFX_SawImpact  |  files: 1x.asset, 2x.mat, 1x.png, 2x.prefab
**MODEL_BladeSaw** â€“ no particle/trail components
**VFX_SawImpact**
- ParticleSystem on "VFX_SawImpact": duration=0.3, looping=0, startLifetime=0.3, startSpeed=4.5, startSize=0.055, maxParticles=16, rateOverTime=0
- ParticleSystemRenderer on "VFX_SawImpact": renderMode=1

## Knife
Prefabs: KnifeProjectile  |  files: 1x.asset, 4x.mat, 1x.prefab
**KnifeProjectile**
- TrailRenderer on "Trail": time=0.1

## Fist
Prefabs: MODEL_FirstPersonFist, Hit_Fist_Normal  |  files: 1x.asset, 5x.mat, 4x.png, 2x.prefab
**MODEL_FirstPersonFist** â€“ no particle/trail components
**Hit_Fist_Normal**
- ParticleSystem on "Hit_Fist_Normal": duration=0.05, looping=0, startLifetime=0.0001, startSpeed=0, startSize=0, maxParticles=0, rateOverTime=0
- ParticleSystemRenderer on "Hit_Fist_Normal": renderMode=0
- ParticleSystem on "Hit": duration=0.5, looping=0, startLifetime=0.18, startSpeed=0, startSize=2, maxParticles=2, rateOverTime=0
- ParticleSystemRenderer on "Hit": renderMode=0
- ParticleSystem on "Hit_2": duration=0.5, looping=0, startLifetime=0.2, startSpeed=0.5, startSize=0.05, maxParticles=11, rateOverTime=0
- ParticleSystemRenderer on "Hit_2": renderMode=1
- ParticleSystem on "glow": duration=0.5, looping=0, startLifetime=0.18, startSpeed=0, startSize=1, maxParticles=2, rateOverTime=0
- ParticleSystemRenderer on "glow": renderMode=0
- ParticleSystem on "Ring": duration=0.5, looping=0, startLifetime=0.15, startSpeed=0, startSize=1, maxParticles=2, rateOverTime=0
- ParticleSystemRenderer on "Ring": renderMode=0
- ParticleSystem on "Particle": duration=0.5, looping=0, startLifetime=0.25, startSpeed=10, startSize=0.05, maxParticles=21, rateOverTime=0
- ParticleSystemRenderer on "Particle": renderMode=0
- ParticleSystem on "Particle System (1)": duration=1, looping=0, startLifetime=0.5, startSpeed=0, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (1)": renderMode=0
- ParticleSystem on "Particle System": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System": renderMode=0
- ParticleSystem on "Particle System (2)": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (2)": renderMode=0

## Bomb
Prefabs: MODEL_Bomb, BombImpect  |  files: 1x.asset, 5x.mat, 3x.png, 2x.prefab
**MODEL_Bomb**
- ParticleSystem on "Particle System": duration=0.5, looping=1, startLifetime=0.25, startSpeed=0.14, startSize=0.25, maxParticles=1000, rateOverTime=5
- ParticleSystemRenderer on "Particle System": renderMode=0
**BombImpect**
- ParticleSystem on "BombImpect": duration=1.5, looping=0, startLifetime=1, startSpeed=0, startSize=5, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "BombImpect": renderMode=0
- ParticleSystem on "Particle System": duration=2, looping=0, startLifetime=1.5, startSpeed=0.5, startSize=3, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System": renderMode=0
- ParticleSystem on "Particle System (1)": duration=1, looping=0, startLifetime=0.5, startSpeed=0, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (1)": renderMode=0
- ParticleSystem on "Particle System": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System": renderMode=0
- ParticleSystem on "Particle System (2)": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (2)": renderMode=0

## AcidBall
Prefabs: MODEL_AcidBall, VFX_AcidImpact  |  files: 5x.mat, 2x.png, 2x.prefab
**MODEL_AcidBall**
- ParticleSystem on "Mist (1)": duration=1, looping=1, startLifetime=2, startSpeed=0.05, startSize=0.75, maxParticles=8, rateOverTime=2
- ParticleSystemRenderer on "Mist (1)": renderMode=0
**VFX_AcidImpact**
- ParticleSystem on "VFX_AcidImpact": duration=0.2, looping=0, startLifetime=0.5, startSpeed=3.2, startSize=0.075, maxParticles=18, rateOverTime=0
- ParticleSystemRenderer on "VFX_AcidImpact": renderMode=0
- ParticleSystem on "Mist": duration=0.2, looping=0, startLifetime=2, startSpeed=0.6, startSize=0.28, maxParticles=8, rateOverTime=0
- ParticleSystemRenderer on "Mist": renderMode=0
- ParticleSystem on "Mist (1)": duration=0.5, looping=0, startLifetime=2, startSpeed=0.6, startSize=0.5, maxParticles=8, rateOverTime=0
- ParticleSystemRenderer on "Mist (1)": renderMode=0
- ParticleSystem on "Mist (2)delay": duration=1, looping=0, startLifetime=2, startSpeed=0.6, startSize=3, maxParticles=8, rateOverTime=5
- ParticleSystemRenderer on "Mist (2)delay": renderMode=0
- ParticleSystem on "Sizzle": duration=0.2, looping=0, startLifetime=0.3, startSpeed=1.15, startSize=0.035, maxParticles=12, rateOverTime=0
- ParticleSystemRenderer on "Sizzle": renderMode=0
- ParticleSystem on "Impact03": duration=1.5, looping=0, startLifetime=1, startSpeed=0, startSize=3, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Impact03": renderMode=0

## Missile
Prefabs: MissileProjectile, MissileTool  |  files: 1x.asset, 5x.mat, 1x.png, 2x.prefab
**MissileProjectile**
- TrailRenderer on "OrangeExhaustTrail": time=0.5
- ParticleSystem on "Particle System": duration=5, looping=1, startLifetime=1, startSpeed=1.0720826, startSize=0.75, maxParticles=1000, rateOverTime=20
- ParticleSystemRenderer on "Particle System": renderMode=0
**MissileTool** â€“ no particle/trail components

## MissileRain
Prefabs: MissileRainProjectile, MissileRainTool, MissileRainImpactVfx  |  files: 6x.mat, 3x.prefab
**MissileRainProjectile**
- TrailRenderer on "OrangeExhaustTrail": time=0.25
**MissileRainTool** â€“ no particle/trail components
**MissileRainImpactVfx**
- ParticleSystem on "ExplosionBurst": duration=0.12, looping=0, startLifetime=0.42, startSpeed=4.8, startSize=0.13, maxParticles=30, rateOverTime=10
- ParticleSystemRenderer on "ExplosionBurst": renderMode=0
- ParticleSystem on "Shockwave": duration=0.12, looping=0, startLifetime=0.32, startSpeed=6.2, startSize=0.09, maxParticles=16, rateOverTime=10
- ParticleSystemRenderer on "Shockwave": renderMode=0
- ParticleSystem on "Flash": duration=0.12, looping=0, startLifetime=0.12, startSpeed=0.35, startSize=0.62, maxParticles=4, rateOverTime=10
- ParticleSystemRenderer on "Flash": renderMode=0
- ParticleSystem on "Smoke": duration=0.12, looping=0, startLifetime=0.62, startSpeed=1.25, startSize=0.24, maxParticles=12, rateOverTime=10
- ParticleSystemRenderer on "Smoke": renderMode=0

## SpikedBall
Prefabs: SpikedBallTool  |  files: 2x.asset, 2x.mat, 1x.png, 1x.prefab
**SpikedBallTool** â€“ no particle/trail components

## Thunder
Prefabs: MODEL_ThunderBolt, ThunderImpact  |  files: 1x.asset, 7x.mat, 6x.png, 2x.prefab
**MODEL_ThunderBolt**
- LineRenderer on "Exact Beam Core"
- LineRenderer on "Beam Outer Glow"
- Light on "Thunder Impact Light": type=2, intensity=2.8, range=2.6
- ParticleSystem on "Upper Strike Flash": duration=0.28, looping=0, startLifetime=0.28, startSpeed=0, startSize=0.72, maxParticles=1, rateOverTime=10
- ParticleSystemRenderer on "Upper Strike Flash": renderMode=0
- ParticleSystem on "Bolt Sprite 01": duration=1, looping=0, startLifetime=0.95, startSpeed=0, startSize=2.5, maxParticles=1, rateOverTime=10
- ParticleSystemRenderer on "Bolt Sprite 01": renderMode=0
- ParticleSystem on "Natural Lightning Overlay": duration=1, looping=0, startLifetime=0.95, startSpeed=0, startSize=5, maxParticles=1, rateOverTime=10
- ParticleSystemRenderer on "Natural Lightning Overlay": renderMode=0
**ThunderImpact**
- ParticleSystem on "ImpactFlash": duration=0.15, looping=0, startLifetime=0.18, startSpeed=0, startSize=1.15, maxParticles=1, rateOverTime=0
- ParticleSystemRenderer on "ImpactFlash": renderMode=0
- ParticleSystem on "ImpactRing": duration=0.3, looping=0, startLifetime=0.28, startSpeed=0, startSize=0.48, maxParticles=1, rateOverTime=0
- ParticleSystemRenderer on "ImpactRing": renderMode=2
- ParticleSystem on "LightningBranches": duration=0.28, looping=0, startLifetime=0.26, startSpeed=0, startSize=0.34, maxParticles=3, rateOverTime=0
- ParticleSystemRenderer on "LightningBranches": renderMode=0
- ParticleSystem on "ImpactSparks": duration=0.35, looping=0, startLifetime=0.28, startSpeed=5.5, startSize=0.065, maxParticles=8, rateOverTime=0
- ParticleSystemRenderer on "ImpactSparks": renderMode=1
- ParticleSystem on "GroundArcs": duration=0.34, looping=0, startLifetime=0.3, startSpeed=0, startSize=0.24, maxParticles=6, rateOverTime=0
- ParticleSystemRenderer on "GroundArcs": renderMode=2
- ParticleSystem on "GroundElectricity": duration=0.3, looping=0, startLifetime=0.22, startSpeed=0, startSize=0.05, maxParticles=6, rateOverTime=0
- ParticleSystemRenderer on "GroundElectricity": renderMode=1
- ParticleSystem on "ElectricHaze": duration=0.42, looping=0, startLifetime=0.42, startSpeed=0.42, startSize=0.85, maxParticles=3, rateOverTime=0
- ParticleSystemRenderer on "ElectricHaze": renderMode=0
- ParticleSystem on "Particle System": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System": renderMode=0
- ParticleSystem on "Particle System (1)": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (1)": renderMode=0

## _SharedDestruction
Prefabs: VoxelChunkDestructionImpect 1  |  files: 2x.mat, 2x.png, 1x.prefab
**VoxelChunkDestructionImpect 1**
- ParticleSystem on "VoxelChunkDestructionImpect 1": duration=1.5, looping=0, startLifetime=1, startSpeed=0, startSize=2.5, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "VoxelChunkDestructionImpect 1": renderMode=0
- ParticleSystem on "Particle System (1)": duration=1, looping=0, startLifetime=0.5, startSpeed=0, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (1)": renderMode=0
- ParticleSystem on "Particle System": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=0.75, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System": renderMode=0
- ParticleSystem on "Particle System (2)": duration=5, looping=0, startLifetime=4.31, startSpeed=1, startSize=1.25, maxParticles=1000, rateOverTime=0
- ParticleSystemRenderer on "Particle System (2)": renderMode=0
