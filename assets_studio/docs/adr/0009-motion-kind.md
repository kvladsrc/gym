# ADR-009. Motion as a separate asset kind

Status: accepted.

## Context

Studio's native motions and poses (`studio.smplx-motion.v1` from GVHMR, Kimodo
SMPL-X, PromptHMR and HTD-Refine; `studio.soma-motion.v1` from GEM-X) were stored
as `text/plain`. Only the producing task distinguished them from descriptions
and other JSON. The library displayed them as text, and every text input
accepted arbitrary JSON, including motion for a different body model.

## Decision

Introduce kind `motion` with one type per body model:

- `application/vnd.studio.smplx-motion+json`, format `studio.smplx-motion.v1`;
- `application/vnd.studio.soma-motion+json`, format `studio.soma-motion.v1`.

A pose is a single-frame motion. The signature is a JSON object with the
corresponding `format` value (`model_server_sdk.media`). Import checks these
types before `text/plain`. Files use the `.json` extension.

The kind has no canonical type. Studio does not convert SMPL-X to SOMA or vice
versa: they are different bodies, and each server declares which it accepts.
The input picker shows only motions of the declared type.

Motion servers declare these types for inputs and outputs. Kimodo constraints,
2D observations (`studio.pose-observations.v1`) and other auxiliary JSON remain
text.

Migration 0008 adds the kind to the `assets` table CHECK. Immediately afterward,
Studio scans text assets once (`Library.retype_motions`) and retypes native
motions, moving `.txt` files to `.json`.

The library's “Animations” filter shows `motion` assets and, as before, FBX clips
produced by motion tasks.
