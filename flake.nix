{
  description = "A flake that loads some packages";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.05";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    { self
    , nixpkgs
    , flake-utils
    ,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = nixpkgs.legacyPackages.${system};

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
          git
          gnugrep
          gnused
          openssh
          python312
          shadow
          which
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
          helmfile
          kubernetes-helm
          lcov
          nodejs
          openjdk21_headless
          perl
          pre-commit
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

          # Development
          just
          nixpkgs-fmt
          sapling
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
            '';
          };
        };
      }
    );
}
