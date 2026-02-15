{
  description = "A flake that loads some packages";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.11";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs {
          inherit system;
          config.allowUnfree = true;
          config.cudaSupport = true;
          config.cudaVersion = "12";
        };

        gpu = with pkgs; [
          onnxruntime
        ];

        helm-plugins-dir = pkgs.symlinkJoin {
          name = "helm-plugins";
          paths = with pkgs.kubernetes-helmPlugins; [
            helm-diff
            helm-secrets
          ];
        };

        os = with pkgs; [
          bash
          coreutils-full
          curl
          file
          gdb
          git
          gnugrep
          gnused
          openssh
          python314
          shadow
          which
          zip
          zlib
        ];

        gcov = pkgs.runCommand "gcov" { } ''
          mkdir -p $out/bin
          ln -s ${pkgs.gcc14.cc}/bin/gcov $out/bin/gcov
          ln -s ${pkgs.gcc14.cc}/bin/gcov-dump $out/bin/gcov-dump
          ln -s ${pkgs.gcc14.cc}/bin/gcov-tool $out/bin/gcov-tool
        '';

        ci = with pkgs; [
          bazelisk
          buildifier
          cabal-install
          clang-tools
          cppcheck
          cpplint
          emacs
          gcc14
          gcov
          go
          go-critic
          gocyclo
          golangci-lint
          golint
          gotools
          grpc-health-probe
          haskell.compiler.ghc984Binary
          helmfile
          kubernetes-helm
          lcov
          nodejs
          openjdk21_headless
          perl
          pre-commit
          renovate
        ];

        cd = with pkgs; [
          age
          kubectl
          sops
        ];

        workstation = with pkgs; [
          # Admin
          buildah
          kubectx
          podman
          talosctl
          yq

          # Development
          just
          shfmt
        ];
      in
      rec {
        devShells = {
          default = pkgs.mkShell.override { stdenv = pkgs.gcc14Stdenv; } {
            packages =
              with pkgs;
              [
                netcat
                bash-completion
                openssh
              ]
              ++ gpu
              ++ ci
              ++ cd
              ++ workstation
              ++ os;

            shellHook = ''
              PS1='\[\e[01;32m\]\u@nix\[\e[01;34m\] \w \$\[\e[00m\] '

              alias bazel=${pkgs.bazelisk}/bin/bazelisk
              alias k='kubectl'
              alias kg='kubectl get'
              alias kgp='kubectl get pods'
              alias kgpa='kubectl get pods --all-namespaces'

              source ${pkgs.bash-completion}/etc/profile.d/bash_completion.sh

              export CONTROL_PLANE_IP=192.168.1.128
              export HELM_PLUGINS="${helm-plugins-dir}"
              export SOPS_AGE_KEY_FILE=$HOME/plain/keys.txt
              export TALOSCONFIG="/home/myuser/src/production/kubernetes/talos/_out/talosconfig"
              export KUBECONFIG="/home/myuser/talos/kubeconfig"
              export LD_LIBRARY_PATH="/nix/store/bzc78zhlja8kvpq2lxlh2f9y622bcpfd-libglvnd-1.7.0/lib:/nix/store/8lpy1bh5crqil9frzzl4pm4kj2jlvlbn-nvidia-x11-580.126.09-nixGL/lib:/nix/store/71lv2yyac9j4zcxk82yhb64qmqd3ry4m-nvidia-x11-580.126.09-nixGL-lib32/lib:/nix/store/vyv7m0vq34xvd4paqfd6r1j0v36pyzjf-libglvnd-1.7.0/lib:${pkgs.onnxruntime}/lib:${pkgs.zlib}/lib:$LD_LIBRARY_PATH"

              # Generate ONNX configuration for Bazel
              # This avoids global flags which break standard library linking
              echo "Generating pet_project/onnx_config.bzl..."
              cat <<EOF > pet_project/onnx_config.bzl
ONNX_COPTS = [
    "-I${pkgs.onnxruntime.dev}/include",
]

ONNX_LINKOPTS = [
    "${pkgs.onnxruntime}/lib/libonnxruntime.so",
]
EOF
            '';
          };
        };
      }
    );
}
