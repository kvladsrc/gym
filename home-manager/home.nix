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
      version = "595.104.02";
      sha256_64bit = "sha256-5CHCAuTHn1jDx/MWG75xRU67PYiTb4ggWg4yfNBMWco="; # pragma: allowlist secret
      useSettings = false;
      usePersistenced = false;
    }
  ).override {
    libsOnly = true;
    acceptLicense = true;
  };

  # DeepSeek Harness (dsh): DeepSeek's agent harness, the model through DeepRelay
  # (DEEPRELAY_API_KEY from the shell; profile in dotfiles/dsh). Its loader addon patches
  # Node internals and recognises only the official nodejs.org build, not nixpkgs' Node.
  dshVersion = "0.2.1-rc.2";
  dshNode = pkgs.stdenvNoCC.mkDerivation {
    pname = "nodejs-official";
    version = "24.22.1";
    src = pkgs.fetchurl {
      url = "https://nodejs.org/dist/v24.22.1/node-v24.22.1-linux-x64.tar.xz";
      hash = "sha256-bBcqmrZxj3uAH7WnvUhq8u5eMnWAhqBLhPzOKqdwleY="; # pragma: allowlist secret
    };
    dontConfigure = true;
    dontBuild = true;
    dontFixup = true; # the official binary uses the system loader (glibc on Gentoo)
    installPhase = "cp -r . $out";
  };
  dsh = pkgs.writeShellScriptBin "dsh" ''
    # npm packages with native addons stay outside the store, pinned to dshVersion.
    set -euo pipefail
    prefix="''${XDG_DATA_HOME:-$HOME/.local/share}/dsh"
    export PATH="${dshNode}/bin:$PATH"
    installed="$(node -p "require('$prefix/node_modules/@deepseek-ai/dsh/package.json').version" 2>/dev/null || true)"
    if [ "$installed" != "${dshVersion}" ]; then
      npm install --no-fund --no-audit --prefix "$prefix" "@deepseek-ai/dsh@${dshVersion}" >&2
    fi
    exec "$prefix/node_modules/.bin/dsh" "$@"
  '';

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
    gvfs
    losslesscut
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
    yt-dlp

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
    (config.lib.nixGL.wrap obs-studio)
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
    butler # itch.io uploads (games/soulslike)
    uv
    dsh
  ];

  home.file = {
    ".config/blender/${lib.versions.majorMinor pkgs.blender.version}/scripts/addons/blender_mcp.py" = {
      source = blenderMcpAddon;
    };
    ".config/niri" = {
      source = dotfiles/niri;
      recursive = true;
    };
    # The portal is the system's (/usr), so it never sees niri's own
    # niri-portals.conf in the Nix store; without it no backend serves
    # ScreenCast under niri, and screen recorders (OBS) get nothing.
    ".config/xdg-desktop-portal/niri-portals.conf" = {
      source = "${pkgs.niri}/share/xdg-desktop-portal/niri-portals.conf";
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
    ".local/share/wallpapers/nothing-to-report-0244.jpg" = {
      source = dotfiles/wallpapers/nothing-to-report-0244-qwen.jpg;
    };
    ".emacs.d" = {
      source = dotfiles/emacs.d;
      recursive = true;
    };
    # Read-only: the headless profile is configured here, not from the dsh web UI.
    ".dsh/profiles/headless/cordis.patch.yml" = {
      source = dotfiles/dsh/headless.cordis.patch.yml;
    };
  };

  # Gentoo's session bus does not search the Nix profile for GVfs services.
  # Publish them in the standard user data directory for D-Bus activation.
  xdg.dataFile."dbus-1/services" = {
    source = "${pkgs.gvfs}/share/dbus-1/services";
    recursive = true;
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
    # Nautilus and command-line GIO also need the virtual filesystem module.
    GIO_EXTRA_MODULES = "${pkgs.gvfs}/lib/gio/modules";
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
    settings = {
      confirm_os_window_close = 0;
      term = "xterm-256color";
      # Quiet Orbit: keep terminal surfaces aligned with the desktop shell.
      background = "#181a1b";
      foreground = "#d9d3c7";
      cursor = "#b9aa8c";
      cursor_text_color = "#181a1b";
      selection_background = "#454037";
      selection_foreground = "#eee8dc";
      url_color = "#a8bcc0";
      active_tab_background = "#37342e";
      active_tab_foreground = "#d9d3c7";
      inactive_tab_background = "#181a1b";
      inactive_tab_foreground = "#aaa398";
      color0 = "#252321";
      color1 = "#ca9187";
      color2 = "#a3b89a";
      color3 = "#c7a06c";
      color4 = "#9caebb";
      color5 = "#b1a0b5";
      color6 = "#94b6af";
      color7 = "#d9d3c7";
      color8 = "#827c72";
      color9 = "#e0afa3";
      color10 = "#bbceb0";
      color11 = "#ddc397";
      color12 = "#b3c6d2";
      color13 = "#ccbacf";
      color14 = "#b0d0c8";
      color15 = "#eee8dc";
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
