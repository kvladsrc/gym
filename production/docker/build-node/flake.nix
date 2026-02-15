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
          python314
          shadow
          sudo
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
      in
      rec {
        packages.buildNodeTools = pkgs.symlinkJoin {
          name = "build-node-tools";
          paths = ci ++ cd ++ os;
        };

        packages.helmPlugins = helm-plugins-dir;
      }
    );
}
