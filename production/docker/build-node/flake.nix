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

        container = with pkgs; [
          buildah
          fuse3
          fuse-overlayfs
          skopeo
        ];

        # Only `python3.11`, for the asset studio's SDK check: the whole package
        # would win `python3` over python314 in the join below.
        python311Only = pkgs.runCommand "python3.11" { } ''
          mkdir -p $out/bin
          ln -s ${pkgs.python311}/bin/python3.11 $out/bin/python3.11
        '';

        gcov = pkgs.runCommand "gcov" { } ''
          mkdir -p $out/bin
          ln -s ${pkgs.gcc14.cc}/bin/gcov $out/bin/gcov
          ln -s ${pkgs.gcc14.cc}/bin/gcov-dump $out/bin/gcov-dump
          ln -s ${pkgs.gcc14.cc}/bin/gcov-tool $out/bin/gcov-tool
        '';

        ci = with pkgs; [
          ansible-lint
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
          just
          kubernetes-helm
          lcov
          nodejs
          openjdk21_headless
          opentofu
          perl
          pre-commit
          python311Only
          renovate
          tflint
          shfmt
          uv
        ];
      in
      {
        packages = rec {
          buildNodeTools = pkgs.symlinkJoin {
            name = "build-node-tools";
            paths = ci ++ os ++ container;
          };

          buildNodeStore = pkgs.symlinkJoin {
            name = "build-node-store";
            paths = [
              pkgs.nix
              buildNodeTools
            ];
          };
        };
      }
    );
}
