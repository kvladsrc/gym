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

        os = with pkgs; [
          bash
          cacert
          coreutils-full
          curl
          file
          git
          gnugrep
          gnused
          openssh
          python312
          shadow
          sudo
          which
        ];

        ci = with pkgs; [
          bazelisk
          clang-tools
          cppcheck
          cpplint
          emacs
          gcc14
          go
          go-critic
          gocyclo
          golangci-lint
          golint
          gotools
          kubernetes-helm
          nodejs
          perl
          pre-commit
        ];

        dynamicLibs = with pkgs; [
          glibc
          stdenv.cc.cc.lib
          zlib
        ];

        workstation = with pkgs; [
          # Admin
          buildah
          kubectl
          kubectx
          kubernetes-helm
          podman

          # Development
          git-review
          just
          nixpkgs-fmt
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
            '';
          };
        };

        packages.zuulContainer = pkgs.dockerTools.buildImage {
          name = "registry.bonfire.lit/build";
          tag = "1.0";

          copyToRoot = pkgs.buildEnv {
            name = "image-root";
            paths = [
              pkgs.nix-ld
            ]
            ++ os
            ++ ci
            ++ dynamicLibs;
            pathsToLink = [
              "/bin"
              "/etc"
              "/sbin"
            ];
          };

          runAsRoot = ''
            #!${pkgs.runtimeShell}
            ${pkgs.dockerTools.shadowSetup}

            mkdir -p lib64
            ln -s ${pkgs.nix-ld}/bin/nix-ld lib64/ld-linux-x86-64.so.2
            mkdir -p /usr/bin
            ln -s ${pkgs.coreutils-full}/bin/env /usr/bin/env

            # Create sshd system user for privilege separation
            groupadd --system --gid 74 sshd
            useradd --system --uid 74 --gid sshd --no-create-home --home-dir /var/empty --shell /bin/false sshd

            # Create zuul user
            groupadd --gid 9999 zuul
            useradd --uid 9999 --gid zuul --create-home --shell /bin/bash zuul

            # Setup sudo
            mkdir -p /etc/sudoers.d
            echo "zuul ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/zuul

            # Setup SSH
            mkdir -p /var/run/sshd /etc/ssh /var/empty
            chmod 755 /var/empty
            ssh-keygen -A

            # SSH config
            cat > /etc/ssh/sshd_config << EOF
            Port 22
            PermitRootLogin yes
            PasswordAuthentication yes
            PubkeyAuthentication yes
            AuthorizedKeysFile .ssh/authorized_keys
            Subsystem sftp internal-sftp
            EOF
          '';

          config = {
            Entrypoint = [
              "/bin/sshd"
              "-D"
            ];
            ExposedPorts = {
              "22/tcp" = { };
            };
            User = "root";
            Env = [
              "NIX_LD_LIBRARY_PATH=${pkgs.lib.makeLibraryPath dynamicLibs}"
              "NIX_LD=${pkgs.lib.fileContents "${pkgs.stdenv.cc}/nix-support/dynamic-linker"}"
              "SSL_CERT_FILE=/etc/ssl/certs/ca-bundle.crt"
              "GIT_SSL_CAINFO=/etc/ssl/certs/ca-bundle.crt"
              "NIX_SSL_CERT_FILE=/etc/ssl/certs/ca-bundle.crt"
              "CURL_CA_BUNDLE=/etc/ssl/certs/ca-bundle.crt"
            ];
          };
        };
      }
    );
}
