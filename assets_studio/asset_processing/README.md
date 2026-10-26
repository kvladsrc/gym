# Asset processing

Shared asset processing independent of the model servers' HTTP contract:
GLB → FBX conversion, rig preparation and motion transfer through Blender.
Model servers use this package directly; `animation_tools` exposes preparation
and transfer as ordinary Studio jobs.

The package does not depend on the SDK, PyTorch or an installed `bpy` Python
module. Blender is imported only when the relevant functions or scripts run
in a configured environment. `workspace` manages temporary processing files
and optionally retains them for diagnostics.

The SDK handles the server contract and lifecycle. New operations use existing
`TaskSpec` declarations and parameter schemas without changing the SDK.
