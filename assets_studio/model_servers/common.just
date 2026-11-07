# Shared by the model servers' justfiles: each one starts with `import "../common.just"`.

# Environments, model sources and weights are large: they live in the user's cache,
# outside the source tree.
_xdg_cache := env("XDG_CACHE_HOME", "")
cache := (if _xdg_cache == "" { env("HOME") / ".cache" } else { _xdg_cache }) / "assets-studio"

# A server's commands run in a shell with Python, uv and the host's NVIDIA driver: by
# default the repository flake's `assets-studio-gpu` dev shell. The CUDA shell adds the
# CUDA toolkit and a host compiler, for the servers whose setup builds native extensions.
# Without Nix, set ASSETS_STUDIO_GPU_SHELL and ASSETS_STUDIO_CUDA_SHELL to a wrapper of
# your own, or to "" to run the commands as they are. Nix itself starts without the
# caller's LD_LIBRARY_PATH (a parent GPU shell's host libraries break it); the dev shell
# sets its own.
_nix_develop := "env -u LD_LIBRARY_PATH nix develop " + quote(source_directory() / "../..")
gpu_shell := env("ASSETS_STUDIO_GPU_SHELL", _nix_develop + "#assets-studio-gpu --command")
cuda_shell := env("ASSETS_STUDIO_CUDA_SHELL", _nix_develop + "#assets-studio-cuda --command")

# Native CUDA extensions are built for one GPU architecture: 8.6 (an RTX 30xx) unless
# TORCH_CUDA_ARCH_LIST names another; `nvidia-smi --query-gpu=compute_cap --format=csv`
# shows the GPU's. MAX_JOBS limits parallel compilers, each of which takes GBs of RAM.
cuda_arch := env("TORCH_CUDA_ARCH_LIST", "8.6")
build_jobs := env("MAX_JOBS", "2")
