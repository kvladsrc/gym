# ADR-008. Saved graphs and animation processing

Status: accepted.

## Context

Rigging and motion generation are separate. MIA creates humanoid skeletons
and weights; SkinTokens and UniRig create shape-specific skeletons. Kimodo
generates motion on SOMA renamed to Mixamo. Matching names alone is insufficient:
proportions, rest poses, axes and bone lengths differ. Applying Kimodo to a
character required a separate Blender script. The chain lacked a saved
definition, although Studio already stored its jobs and asset provenance.

Guard inspection found no missing weights or broken hierarchy, but did find
unnormalized weight sums (up to 1.7%), extra SOMA joint-name collisions with
Mixamo, lost end-bone rotations in aim mode and potential frame loss from
rounding imported ranges down. These are preparation/transfer problems and
should not require per-asset scripts after jobs.

## Decision

`studio flow` is a separate HTTP service/UI using Studio's public API. It
stores DAG definitions, versions and immutable run snapshots. Running creates
ordinary jobs with existing dependencies; scheduling and library stay in Studio,
process startup in the launcher. Reruns create new jobs. Interrupted submissions
recover using stable idempotency keys.

`animation_tools` is an ordinary contract v1 server using Blender, without a
neural model. It declares `prepare-rig` and `retarget-motion`: the former checks
and normalizes rigs, the latter prepares rigs internally and transfers motion.
These operations automatically appear in UI, API and graphs. Separate preparation
before transfer is optional.

Shared Blender code lives in `asset_processing`, independent of SDK and HTTP
contract. Model servers use it as a library; `animation_tools` exposes its
operations as jobs. The SDK neither depends on it nor processes rigs/animation.

The package resets old pose/animation, validates structure, normalizes weights
and transfers full rotations. Different rest poses are handled through matched
bone directions. Explicit positions mode remains for sources with useful joint
positions only. GVHMR transfers rotations; MoCapAnything keeps transfer onto
its own rig skeleton. Frame counts round to the nearest integer, and export
does not simplify animation keys.

Servers declare `text-to-motion`, `video-to-motion`, `prepare-rig` and
`retarget-motion` through existing `TaskSpec`. No new HTTP fields or `KnownTask`
additions are needed.

## Boundaries

These operations do not infer missing weights, rename arbitrary animal
skeletons into humanoids, or restore weapon contacts absent from inputs.
Unknown structures and unweighted vertices fail with explanations. Neural
prediction quality and foot placement must be checked separately from correct
transfer and key preservation.
