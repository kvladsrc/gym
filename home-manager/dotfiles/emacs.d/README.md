# Emacs configuration

`init.el` and `early-init.el` are local files, not managed by Home Manager.
The local `init.el` requires the built-in `use-package`, loads an explicit
ordered list of modules from `conf/`, then loads local `custom.el`.
The old `melpa.el`, `autoupdate.el`, `aider.el`, and bootstrap
`use-package.el` entry points have been removed.

## Packages

`programs.emacs.extraPackages` in `home-manager/home.nix` supplies the Lisp
packages from the pinned nixpkgs input. Startup does not install or update
packages. Package changes take effect after a Home Manager activation and an
Emacs restart. No activation is performed by configuration checks.

Keep the following in the local `~/.emacs.d/early-init.el` so existing ELPA
packages do not override Nix packages. The condition preserves package.el
startup for a plain, unwrapped Emacs:

```elisp
(when-let* ((site-lisp (getenv "emacsWithPackages_siteLisp")))
  (setq package-user-dir (expand-file-name "elpa" site-lisp)
        package-directory-list nil
        package-quickstart-file (expand-file-name "package-quickstart.el" site-lisp)))
```

Existing `~/.emacs.d/elpa` files do not need to be deleted. Future Customize
writes go to local `custom.el`, which also contains the settings migrated
from the old init. Neither file is managed by Home Manager.

## Workflow

- `M-s r`: search project files with `consult-ripgrep`.
- `M-s l`: search across buffers with `consult-line-multi`.
- `C-.`: Embark actions on the current candidate or object at point.
- From a Consult search, use `embark-export`, then
  `wgrep-change-to-wgrep-mode` to edit matches across files.
- `C-c e r/a/f/o`: Eglot rename, code actions, format, organize imports.
- Corfu supplies completion in GUI and terminal Emacs, with documentation
  popups and completion history. Cape adds file and word completion fallbacks.
- envrc imports the project's direnv environment before language tools start.
  Each project's `.envrc` still needs the usual explicit `direnv allow`.
- C++ formatting on save requires a connected Eglot server supporting
  formatting. Project `.clang-format` files control the resulting style.
- Go formatting is buffer-local and uses `goimports` from `gotools`.
- Codeforces filenames are normalized; existing files are opened and new
  files are only written when saved. Snippets have editable fields and final
  cursor positions. The gcd snippet uses C++17 `std::gcd` (`<numeric>`).

Projectile and Flycheck remain configured. Tree-sitter major-mode migration
is a separate choice: grammars, hooks, indentation, and snippet inheritance
must be migrated together rather than changing major modes implicitly.

## Checks

From the repository root, run `nix develop -c just emacs-check`. This builds
the declared Emacs package and runs ERT checks in a temporary state directory,
without loading private init settings or accessing the real Org-roam database.
