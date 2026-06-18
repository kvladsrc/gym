{
  description = "A flake that loads some packages";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.11";
    flake-utils.url = "github:numtide/flake-utils";
    yaac-another-awesome-cv = {
      url = "github:darwiin/yaac-another-awesome-cv/31dcdba2e0ead49edd665a7c601d60f555d20341";
      flake = false;
    };
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
      yaac-another-awesome-cv,
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
          diffutils
          fd
          file
          gdb
          git
          gnugrep
          gnused
          jq
          openssh
          python314
          ripgrep
          shadow
          shellcheck
          tree
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

        cv-texlive = pkgs.texlive.combine {
          inherit (pkgs.texlive)
            scheme-small
            babel-french
            biblatex
            enumitem
            fontawesome5
            fontspec
            latexmk
            luainputenc
            parskip
            preprint
            tcolorbox
            tikzfill
            titlesec
            ;
        };

        yaac-cv = pkgs.stdenvNoCC.mkDerivation {
          pname = "yaac-another-awesome-cv";
          version = "2.3.1";
          src = yaac-another-awesome-cv;
          dontBuild = true;
          installPhase = ''
            runHook preInstall
            install -d "$out/share/yaac-another-awesome-cv"
            cp -r fonts "$out/share/yaac-another-awesome-cv/fonts"
            install -m444 yaac-another-awesome-cv.cls \
              "$out/share/yaac-another-awesome-cv/yaac-another-awesome-cv.cls"
            substituteInPlace "$out/share/yaac-another-awesome-cv/yaac-another-awesome-cv.cls" \
              --replace-fail "Path = fonts/," \
                "Path = $out/share/yaac-another-awesome-cv/fonts/, Extension = .otf,"
            runHook postInstall
          '';
        };

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
          gdtoolkit_4
          go
          go-critic
          gocyclo
          golangci-lint
          golint
          gotools
          haskell.compiler.ghc984Binary
          kubernetes-helm
          lcov
          nodejs
          nodePackages.prettier
          openjdk21_headless
          opentofu
          perl
          pre-commit
          renovate
          shfmt
          tflint
          cv-texlive
          yaac-cv
        ];

        workstation = with pkgs; [
          age
          fluxcd
          godotPackages_4_6.godot
          jujutsu
          just
          kubectl
          kubectx
          skopeo
          sops
          talosctl
          yq-go
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
              export TEXINPUTS="${yaac-cv}/share/yaac-another-awesome-cv//:"
              export SOPS_AGE_KEY_FILE=$HOME/plain/keys.txt
              export TALOSCONFIG="/home/myuser/src/production/kubernetes/talos/_out/talosconfig"
              export KUBECONFIG="/home/myuser/talos/kubeconfig"
            '';
          };
        };
      }
    );
}
