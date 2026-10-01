{
  config,
  lib,
  pkgs,
  nixgl,
  ...
}:
let
  user = "myuser";
  version = "26.05";

  # HDMI is wired to NVIDIA; eDP/USB-C use AMD. Keep these userspace
  # libraries in sync with Gentoo's loaded NVIDIA kernel driver.
  nvidiaLibraries = (
    pkgs.linuxPackages.nvidiaPackages.mkDriver {
      version = "595.99.02";
      sha256_64bit = "sha256-6HR3lYv3YwcFSTJL1a1slI66btIQ5EAFs+/4SUD24ew="; # pragma: allowlist secret
      useSettings = false;
      usePersistenced = false;
    }
  ).override {
    libsOnly = true;
    acceptLicense = true;
  };

  niriWithHybridGraphics = config.lib.nixGL.wrap (
    pkgs.symlinkJoin {
      name = "niri-hybrid-${pkgs.niri.version}";
      paths = [ pkgs.niri ];
      nativeBuildInputs = [ pkgs.makeWrapper ];
      postBuild = ''
        wrapProgram "$out/bin/niri" \
          --prefix __EGL_VENDOR_LIBRARY_FILENAMES : ${nvidiaLibraries}/share/glvnd/egl_vendor.d/10_nvidia.json \
          --prefix GBM_BACKENDS_PATH : ${nvidiaLibraries}/lib/gbm \
          --prefix __EGL_EXTERNAL_PLATFORM_CONFIG_DIRS : ${pkgs.egl-gbm}/share/egl/egl_external_platform.d \
          --prefix LD_LIBRARY_PATH : ${nvidiaLibraries}/lib
      '';
    }
  );

  # Blender + MCP
  blenderMcpAddon = pkgs.fetchurl {
    url = "https://raw.githubusercontent.com/ahujasid/blender-mcp/da4e16d2069ce5154eaa2535bf995e843caf5c73/addon.py";
    hash = "sha256-ymlVu1hNeOIp8CCoudcBFECtxulNqwrI4BqyeU2xncA="; # pragma: allowlist secret
  };

  blenderWithMcp = config.lib.nixGL.wrap (
    pkgs.symlinkJoin {
      name = "blender-with-mcp-${pkgs.blender.version}";
      paths = [ pkgs.blender ];
      nativeBuildInputs = [ pkgs.makeWrapper ];
      postBuild = ''
        wrapProgram "$out/bin/blender" \
          --add-flags "--addons blender_mcp"
      '';
    }
  );

  blenderMcp = pkgs.writeShellApplication {
    name = "blender-mcp";
    runtimeInputs = [ pkgs.uv pkgs.python311 ];
    text = ''
      export DISABLE_TELEMETRY=true
      export BLENDER_HOST=127.0.0.1 BLENDER_PORT=9876
      exec uvx --python ${pkgs.python311}/bin/python3.11 \
        --with 'mcp==1.30.0' 'blender-mcp==1.6.4' "$@"
    '';
  };

  # Godot + MCP
  godot = config.lib.nixGL.wrap pkgs.godotPackages_4_6.godot;

  godotMcp = pkgs.writeShellApplication {
    name = "godot-mcp";
    runtimeInputs = [ pkgs.nodejs ];
    text = ''
      exec npx -y @coding-solo/godot-mcp@0.1.1
    '';
  };

  # Unity + MCP
  unityCli = import ./packages/unity-cli.nix { inherit pkgs; };

  unityhub = config.lib.nixGL.wrap (
    pkgs.unityhub.override {
      # Unity 6.6's shader compiler needs libtinfo.so.6 inside the FHS env.
      extraLibs = pkgs: [
        # A separate libtinfo SONAME is needed for the FHS ldconfig cache.
        (pkgs.ncurses.override {
          withTermlib = true;
          unicodeSupport = false;
        })
      ];
      extraPkgs = pkgs: [ pkgs.which ];
    }
  );
in
{
  home.username = user;
  home.homeDirectory = "/home/${user}";
  home.stateVersion = version;

  # Needed for GPU-enabled packages, like ghostty.
  targets.genericLinux.nixGL = {
    packages = nixgl;
    defaultWrapper = "mesa";
    offloadWrapper = "mesaPrime";
    installScripts = [
      "mesa"
      "mesaPrime"
    ];
  };

  # Adding font packages is not enought to use the fonts.
  fonts.fontconfig.enable = true;

  # GTK Theme
  gtk = {
    enable = true;

    theme = {
      name = "adw-gtk3-dark";
      package = pkgs.adw-gtk3;
    };

    gtk3.extraConfig = {
      gtk-application-prefer-dark-theme = 1;
    };

    gtk4.extraConfig = {
      gtk-application-prefer-dark-theme = 1;
    };
  };

  dconf.settings = {
    "org/gnome/desktop/interface" = {
      gtk-theme = "adw-gtk3-dark";
      color-scheme = "prefer-dark";
    };
  };

  home.pointerCursor = {
    gtk.enable = true;
    x11.enable = true;
    package = pkgs.bibata-cursors;
    name = "Bibata-Modern-Classic";
    size = 32;
  };

  home.packages = with pkgs; [
    # Apps
    amberol
    baobab
    dig
    dropbox
    eog
    evince
    fdupes
    firefox
    gocryptfs
    google-chrome
    graphviz
    mg
    nautilus
    nemo-with-extensions
    pandoc
    rclone
    texliveTeTeX
    tor-browser
    totem
    usbutils
    xorg.xlsclients
    xournalpp

    # Wayland & DE
    adw-gtk3
    blueman
    brightnessctl
    cliphist
    grim
    kanshi
    libnotify
    mako
    mission-center
    nerd-fonts.fira-code
    nerd-fonts.mononoki
    nerd-fonts.noto
    nerd-fonts.symbols-only
    networkmanagerapplet
    noto-fonts-color-emoji
    pavucontrol
    slurp
    swaybg
    wl-clipboard
    wlogout
    xwayland-satellite

    # GPU enabled.
    (config.lib.nixGL.wrap blockbench)
    (config.lib.nixGL.wrap celestia)
    (config.lib.nixGL.wrap easyeffects)
    (config.lib.nixGL.wrap f3d)
    (config.lib.nixGL.wrap gamescope)
    (config.lib.nixGL.wrap gthumb)
    (config.lib.nixGL.wrap kooha)
    (config.lib.nixGL.wrap ryubing)
    (config.lib.nixGL.wrap zeal)
    blenderWithMcp
    godot
    niriWithHybridGraphics
    unityhub

    # Password management
    gnupg
    pass

    # Development
    blenderMcp
    clang-tools
    claude-code
    cloc
    delta
    go
    godotMcp
    golangci-lint
    gopls
    gotools # goimports, used by go-mode
    loccount
    podman
    python311
    racket
    ripgrep # consult-ripgrep
    rust-analyzer
    unityCli
    uv
  ];

  home.file = {
    ".config/blender/${lib.versions.majorMinor pkgs.blender.version}/scripts/addons/blender_mcp.py" = {
      source = blenderMcpAddon;
    };
    ".config/niri" = {
      source = dotfiles/niri;
      recursive = true;
    };
    ".config/kanshi" = {
      source = dotfiles/kanshi;
      recursive = true;
    };
    ".config/mako" = {
      source = dotfiles/mako;
      recursive = true;
    };
    ".config/wofi" = {
      source = dotfiles/wofi;
      recursive = true;
    };
    ".config/waybar" = {
      source = dotfiles/waybar;
      recursive = true;
    };
    ".local/share/wallpapers/discordia.png" = {
      source = dotfiles/wallpapers/discordia.png;
    };
    ".emacs.d" = {
      source = dotfiles/emacs.d;
      recursive = true;
    };
  };

  # Make the login callback discoverable even without Nix in XDG_DATA_DIRS.
  # Use bin/unityhub so browser callbacks also go through the nixGL wrapper.
  xdg.dataFile."applications/unityhub.desktop".text = ''
    [Desktop Entry]
    Name=Unity Hub
    Exec=${unityhub}/bin/unityhub %U
    Terminal=false
    Type=Application
    Icon=unityhub
    Categories=Development;
    MimeType=x-scheme-handler/unityhub;
  '';

  home.activation.registerUnityHub = lib.hm.dag.entryAfter [ "linkGeneration" ] ''
    run ${pkgs.desktop-file-utils}/bin/update-desktop-database ${lib.escapeShellArg "${config.xdg.dataHome}/applications"}
    run ${pkgs.xdg-utils}/bin/xdg-mime default unityhub.desktop x-scheme-handler/unityhub
  '';

  home.sessionVariables = {
    EDITOR = "mg";
    VISUAL = "mg";
  };

  programs.waybar.enable = true;

  programs.jujutsu = {
    enable = true;
    settings = {
      user = {
        email = "myuser@gmail.com";
        name = "Vladimir Kim";
      };
      ui = {
        paginate = "never";
      };
    };
  };

  programs.kitty = {
    enable = true;
    package = config.lib.nixGL.wrap pkgs.kitty;
    font = {
      name = "Mononoki Nerd Font";
      size = 16;
    };
    shellIntegration.enableBashIntegration = true;
    themeFile = "yorumi-abyss";
    settings = {
      confirm_os_window_close = 0;
      term = "xterm-256color";
    };
  };

  programs.wofi.enable = true;

  programs.bash = {
    enable = true;
    historyFileSize = 100000;
    historyIgnore = [
      "ls"
      "pwd"
      "clear"
      "exit"
    ];
    initExtra = ''
      if ! [[ "$PATH" =~ "$HOME/.local/bin" ]]
      then
          PATH="$HOME/.local/bin:$PATH"
      fi

      if ! [[ "$PATH" =~ "$HOME/.npm-global/bin" ]]
      then
          PATH="$HOME/.npm-global/bin:$PATH"
      fi

      if [ -d ~/.bashrc.d ]; then
          for rc in ~/.bashrc.d/*; do
              if [ -f "$rc" ]; then
                  . "$rc"
              fi
          done
      fi
      unset rc
    '';
    shellAliases = {
      em = "emacs -nw";
      ll = "ls -l --color";
      ls = "ls --color";
      mv = "mv -i";
      rm = "rm -i";
    };
  };

  programs.bat = {
    enable = true;
    config = {
      map-syntax = [
        "*.jenkinsfile:Groovy"
        "*.props:Java Properties"
      ];
      pager = "never";
      paging = "never";
      theme = "TwoDark";
    };
  };

  programs.git = {
    enable = true;
    lfs.enable = true;
    settings = {
      user = {
        name = "Vladimir Kim";
        email = "myuser@gmail.com";
      };
      credential.helper = "cache";
      core.pager = "delta";
      interactive.diffFilter = "delta --color-only";
      delta.navigate = true;
      merge.conflictStyle = "zdiff3";
      http.cookiefile = "~/.gitcookies";
    };
  };

  programs.home-manager.enable = true;

  # Codex manages its mutable config.toml itself.
  programs.codex.enable = true;

  programs.emacs = {
    enable = true;
    package = pkgs.emacs-pgtk;
    extraPackages =
      epkgs: with epkgs; [
        avy
        bazel
        bats-mode
        cape
        cargo
        consult
        corfu
        corfu-terminal
        crystal-mode
        diredfl
        dirvish
        docker-compose-mode
        dockerfile-mode
        ef-themes
        embark
        embark-consult
        envrc
        flycheck
        flycheck-eglot
        flycheck-languagetool
        flycheck-yamllint
        gdscript-mode
        gerrit
        go-mode
        godoctor
        google-c-style
        guru-mode
        jenkinsfile-mode
        json-mode
        julia-mode
        just-mode
        kdl-mode
        kubernetes
        magit
        marginalia
        nix-mode
        orderless
        org-roam
        org-roam-ui
        password-store
        pdf-tools
        projectile
        rust-mode
        terraform-mode
        treemacs
        treemacs-magit
        treemacs-projectile
        treemacs-tab-bar
        undo-tree
        vertico
        web-mode
        wgrep
        xclip
        yaml-mode
        yasnippet
        yasnippet-snippets
      ];
  };

  programs.direnv = {
    enable = true;
    nix-direnv.enable = true;
  };

  programs.yazi = {
    enable = true;
    enableBashIntegration = true;
  };
}
