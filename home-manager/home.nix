{ config
, pkgs
, nixgl
, ...
}:
let
  user = "myuser";
  version = "25.11";
in
{
  home.username = user;
  home.homeDirectory = "/home/${user}";
  home.stateVersion = version;

  # Needed for GPU-enabled packages, like ghostty.
  nixGL = {
    packages = nixgl;
    defaultWrapper = "mesa";
    offloadWrapper = "nvidiaPrime";
    installScripts = [
      "mesa"
      "nvidiaPrime"
    ];
  };

  # Adding font packages is not enought to use the fonts.
  fonts.fontconfig.enable = true;

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
    dropbox
    eog
    evince
    fdupes
    firefox
    gocryptfs
    google-chrome
    graphviz
    nautilus
    pandoc
    rclone
    ryubing
    texliveTeTeX
    tor-browser
    totem
    usbutils
    xorg.xlsclients
    xournalpp

    # Hyprland & DE
    blueman
    brightnessctl
    cliphist
    grim
    hypridle
    hyprpaper
    hyprsunset
    kanshi
    nerd-fonts.fira-code
    nerd-fonts.mononoki
    nerd-fonts.symbols-only
    networkmanagerapplet
    pavucontrol
    slurp
    wl-clipboard
    wlogout

    # GPU enabled.
    (config.lib.nixGL.wrap zeal)
    (config.lib.nixGL.wrap celestia)
    (config.lib.nixGL.wrap easyeffects)
    (config.lib.nixGL.wrap antigravity)

    # Development
    clang-tools
    cloc
    delta
    emacs-pgtk
    git-review
    go
    golangci-lint
    gopls
    kubectl
    loccount
    podman
    racket
    uv
  ];

  home.file = {
    ".config/hypr" = {
      source = dotfiles/hypr;
      recursive = true;
    };
    ".config/kanshi" = {
      source = dotfiles/kanshi;
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
    ".emacs.d" = {
      source = dotfiles/emacs.d;
      recursive = true;
    };
    ".local/bin/window.sh" = {
      source = scripts/window.sh;
      executable = true;
    };
  };

  home.sessionVariables = {
    EDITOR = "nano";
  };

  programs.waybar.enable = true;

  programs.kitty = {
    enable = true;
    package = config.lib.nixGL.wrap pkgs.kitty;
    font = {
      name = "Mononoki Nerd Font";
      size = 16;
    };
    shellIntegration.enableBashIntegration = true;
    themeFile = "ayu";
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
      mv = "mv -i";
      rm = "rm -i";
      ll = "ls -l";
    };
  };

  programs.starship = {
    enable = true;
    settings = {
      add_newline = false;
      line_break = {
        disabled = true;
      };
      character = {
        success_symbol = "[➜](bold green)";
        error_symbol = "[➜](bold red)";
      };

      package.disabled = true;
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
    userName = "Vladimir Kim";
    userEmail = "myuser@your.domain";
    lfs.enable = true;
    extraConfig = {
      credential.helper = "cache";
      core.pager = "delta";
      interactive.diffFilter = "delta --color-only";
      delta.navigate = true;
      merge.conflictStyle = "zdiff3";
    };
  };

  programs.home-manager.enable = true;

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
