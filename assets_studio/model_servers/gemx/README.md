# GEM-X model server

NVIDIA GEM-X Body Motion: whole-body (hands included) motion from one video, as
SOMA
parameters in camera and world coordinates. Task `video-to-soma-motion` returns
the studio's
native SOMA motion, `studio.soma-motion.v1` (text JSON): `rig` (joint names,
parents, T-pose),
`camera` and `world` (`global_orient`, `body_pose` 76×3 axis-angle, `transl`,
`joints` 77×3),
`identity_coeffs`, `scale_params`, `K_fullimg`, `bbx_xys`, `kp2d_native77`.

    just setup      # pinned source + server and worker environments (PyTorch
    2.10, cu128)
    just download   # ~7.2 GB: GEM-X weights, SOMA v0.3.3 assets, YOLOX person detector
    just serve      # port 9129; the studio's launcher starts it on demand
    (recipe gemx)

Code Apache-2.0; weights under the NVIDIA Open Model License; SOMA assets per nvidia/SOMA-X.
