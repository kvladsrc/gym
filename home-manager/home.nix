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
  godot = config.lib.nixGL.wrap pkgs.godotPackages_4_6.godot;
  godotMcp = pkgs.writeShellApplication {
    name = "godot-mcp";
    runtimeInputs = [ pkgs.nodejs ];
    text = ''
      exec npx -y @coding-solo/godot-mcp@0.1.1
    '';
  };
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
    aider-chat
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
    blenderWithMcp
    (config.lib.nixGL.wrap celestia)
    (config.lib.nixGL.wrap easyeffects)
    (config.lib.nixGL.wrap f3d)
    (config.lib.nixGL.wrap gamescope)
    godot
    (config.lib.nixGL.wrap gthumb)
    (config.lib.nixGL.wrap kooha)
    (config.lib.nixGL.wrap niri)
    (config.lib.nixGL.wrap ryubing)
    (config.lib.nixGL.wrap zeal)

    # Password management
    gnupg
    pass

    # Development
    clang-tools
    claude-code
    cloc
    delta
    emacs-pgtk
    go
    golangci-lint
    gopls
    loccount
    podman
    racket
    uv
  ];

  home.file = {
    # Codex was configured imperatively before Home Manager took ownership.
    ".codex/config.toml".force = true;
    ".config/blender/${lib.versions.majorMinor pkgs.blender.version}/scripts/addons/blender_mcp.py" = {
      source = blenderMcpAddon;
    };
    ".config/niri" = {
      source = dotfiles/niri;
      recursive = true;
    };
    ".config/xdg-desktop-portal/niri-portals.conf" = {
      text = ''
        [preferred]
        default=gnome;gtk;
        org.freedesktop.impl.portal.Access=gtk;
        org.freedesktop.impl.portal.Notification=gtk;
        org.freedesktop.impl.portal.Secret=gnome-keyring;
      '';
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

  programs.codex = {
    enable = true;
    settings = {
      model = "gpt-5.6-sol";
      model_reasoning_effort = "high";
      personality = "pragmatic";
      approvals_reviewer = "user";

      projects."/home/${user}/Dropbox/src".trust_level = "trusted";

      notice."hide_gpt-5.1-codex-max_migration_prompt" = true;

      tui.model_availability_nux = {
        "gpt-5.5" = 4;
        "gpt-5.6-sol" = 4;
      };

      mcp_servers = {
        blender = {
          command = "${pkgs.uv}/bin/uvx";
          args = [
            "--python"
            "${pkgs.python311}/bin/python"
            "blender-mcp==1.6.4"
          ];
          env = {
            BLENDER_HOST = "127.0.0.1";
            BLENDER_PORT = "9876";
            DISABLE_TELEMETRY = "true";
          };
          startup_timeout_sec = 60;
          tool_timeout_sec = 300;
        };
        godot = {
          command = "${godotMcp}/bin/godot-mcp";
          env.GODOT_PATH = "${godot}/bin/godot";
          startup_timeout_sec = 60;
          tool_timeout_sec = 300;
        };
      };
    };
  };

  programs.direnv = {
    enable = true;
    nix-direnv.enable = true;
  };

  programs.yazi = {
    enable = true;
    enableBashIntegration = true;
  };

  programs.zed-editor = {
    enable = true;
    package = config.lib.nixGL.wrapOffload pkgs.zed-editor;
    extensions = [
      "nix"
      "toml"
      "rust"
    ];
    extraPackages = [
      pkgs.go
      pkgs.nil
      pkgs.nixd
    ];
    userSettings = {
      buffer_font_family = "Mononoki Nerd Font Mono";
      ui_font_family = "Mononoki Nerd Font Mono";
      ui_font_size = 19.0;
      buffer_font_size = 19.0;
      theme = {
        dark = "Monosami Dark";
        light = "Monosami Light";
        mode = "dark";
      };
      hour_format = "hour24";

      agent_servers = {
        "Kimi CLI" = {
          command = "kimi";
          args = [ "--acp" ];
          env = { };
        };
      };
    };
  };
}
