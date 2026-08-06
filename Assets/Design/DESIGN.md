# Game Design

This document outlines the design for the top-down tactical shooter, focusing on a "smooth and appealing" aesthetic and diverse gameplay mechanics.

## UI Design

- **Color System:** 
    - **Primary:** Deep Navy (#1A1A2E) for backgrounds.
    - **Secondary:** Cool Cyan (#16213E) for panels.
    - **Accent:** Electric Blue (#0F3460) for highlights.
    - **Danger:** Vibrant Orange/Red (#E94560) for enemy indicators and low health.
- **Typography:** Bold sans-serif for numbers and headers; clean, monospaced font for technical labels.
- **Layout:** Minimalist HUD with rounded corners. Depth is created through subtle drop shadows and tonal shifts.

## Asset Design

- **Visual Identity:** "Tactical Smoothness" — High-contrast, clean silhouettes with rounded corners. Minimalist detail to ensure readability at a glance.
- **Palette:** 
    - **Player:** Blue/Teal accents.
    - **Enemies:** Orange/Red accents.
    - **Environment:** Neutral greys and dark blues.
- **Composition Rules:** 
    - **Silhouettes:** Enemies should have distinct shapes (Scouts are thin/pointed, Tanks are bulky/square).
    - **Readability:** High contrast between interactive elements and the floor.

### Enemy Archetypes

| Type | Tier | Behavior | Visual Style |
|------|------|----------|--------------|
| **Scout** | core | Fast, flanking movement. Weak but numerous. | Small, triangular, vibrant trail. |
| **Sharpshooter** | core | Long-range stationary fire. Telegraphed shots. | Long, sleek rectangle with a glowing "eye". |
| **Sentinel** | core | Slow, heavy advance. Shielded front. | Large, octagonal, metallic textures. |

### Bullet Variations

| Type | Tier | Mechanics | Visual Feedback |
|------|------|-----------|-----------------|
| **Standard** | core | Linear, medium speed. | Pulse glow, short trail. |
| **Ricochet** | optional | Bounces off walls up to 3 times. | Gold streak, spark on impact. |
| **Plasma** | optional | Slow, explodes on impact (AOE). | Growing energy sphere, distortion effect. |
| **Beam** | optional | Persistent line of damage. | Constant laser with particle core. |

## Game Feedback

- **Genre Profile:** High-Energy Tactical Shooter.
- **Interaction Map:**

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Rationale |
|------------|------|-----------|--------|------|-----------|--------|-------|-----------|
| **Shot Fired** | core | Medium | Slight kickback | — | Recoil scale | Muzzle flash | Sharp snap | Feel the power of the weapon. |
| **Enemy Hit** | core | Medium | — | — | Squash on hit | White flash | Metallic thud | Confirmation of successful hit. |
| **Enemy Kill** | core | Heavy | Shake | 0.05s Hitstop | Expansion | Explosion | Low bass boom | Satisfying conclusion to combat. |
| **Player Damage** | core | Heavy | Directional shake | — | — | Red vignette | Grunt/Statue | Urgent danger signal. |

- **Sequences:**
    - **Explosion:** Flash (0ms) → Radial expansion (20ms) → Particle burst (50ms) → Lingering smoke (200ms).

## Customization

- **Modular Equipment & Durability System:**
    - **Player Character Breakdown:**
        - **Core Body:** A vibrant red triangular arrowhead shape.
        - **Shield Attachment:** A blue V-shaped energy barrier held in the "left hand" (forefront).
        - **Weapons:** 
            - **Spear Rail:** Low-slung projectile launcher.
            - **Heavy Rail:** Top-mounted structural weapon system.
        - **Enhancements:** Rear-mounted thrusters (flame effects) for speed increases.
    - **Item Mechanics:**
        - **Drops:** Enemies have a chance to drop equipment crates upon death.
        - **Pickups:**
            - **Shield:** Adds the energy barrier. Blocks damage.
            - **Speed Boost:** Adds rear thrusters. Increases movement speed.
            - **Weapon Upgrade:** Switches to specialized firing modes.
        - **Durability:**
            - Items have finite usage (e.g., 20 blocks, 50 shots, or 10s duration).
            - Durability is similar to Minecraft; item is lost when value reaches zero.
- **Enemy Tiers:** Visual complexity increases with rank (e.g., Elites have glowing outlines).
- **Bullet Color:** Player weapons can be customized via "Core" color shifts, while enemy bullets remain strictly in the danger palette (Orange/Red) for clarity.
