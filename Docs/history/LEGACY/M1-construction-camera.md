# LEGACY | 2026-11-11 | Walls - Construction Camera Orbit & Inversion

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-11: Construction Camera Angle, Orbit, and Transitions

- Lowered pitch angle from 45 to 30 degrees; adjusted orthographic size from 23 to 20 per feedback. Maintained 45-degree diagonal azimuth.
- Added Q/E 90-degree orbit around grid center, decoupled from R piece rotation. Screen picking coordinates and NavMesh preserved across all 4 quadrants.
- Implemented SmoothStep interpolation (0.3s per 90 degrees) driven by unscaled time. Chained inputs accumulate transition duration smoothly without visual snapping.
- Verified: 23 PlayMode passed (including coordinate invariance across 4 view angles, non-snapping interpolation, reverse inputs).

### 2026-11-11: Q/E Rotation Direction Inversion

- Inverted Q/E keys to match screen-space expectations: Q rotates board left, E rotates board right. Smooth interpolation and R piece rotation preserved.
- Verified: 2 camera PlayMode tests passed.
