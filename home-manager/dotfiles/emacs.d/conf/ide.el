;;; package --- Summary
;;; Commentary:
;;; Code:

(setq gc-cons-threshold (* 100 1024 1024)
      read-process-output-max (* 1024 1024))

(setq-default indent-tabs-mode nil)

(setq c-default-style "linux"
      c-basic-offset 4)

(use-package google-c-style
  :ensure t)

(use-package web-mode
  :ensure t
  :mode ("\\.tpl\\'" . web-mode)
  :config
  (add-hook 'web-mode-hook
            (lambda ()
              (when (string-match "\\.tpl\\'" (buffer-file-name))
                (setq web-mode-engine "go")))))

(use-package terraform-mode
  :ensure t)

(use-package just-mode
  :ensure t)

(use-package eldoc
  :init
  (global-eldoc-mode))

(use-package which-key
  :ensure t
  :config
  (which-key-mode))

(use-package yasnippet
  :ensure t
  :hook (prog-mode . yas-minor-mode)
  :config
  (yas-reload-all))

(use-package yasnippet-snippets
  :ensure t
  :after yasnippet)

(use-package eglot
  :hook ((c-mode          . eglot-ensure)
         (c++-mode        . eglot-ensure)
         (rust-mode       . eglot-ensure)
         (go-mode         . eglot-ensure))
  :config
  (add-to-list 'eglot-server-programs
               '((c++-mode c-mode) . ("clangd" "--clang-tidy")))
  (define-key eglot-mode-map (kbd "C-c e") 'eglot-command-map))

(use-package cc-mode
  :ensure nil
  :mode ("\\.tpp\\'" . c++-mode)
  :hook ((c-mode-common . google-set-c-style)
         (c-mode-common . google-make-newline-indent))
  :config
  (defun my-c++-mode-before-save-hook ()
    (when (eq major-mode 'c++-mode)
      (eglot-format-buffer)))
  (add-hook 'c++-mode-hook
            (lambda ()
              (add-hook 'before-save-hook #'my-c++-mode-before-save-hook nil t))))

(use-package rust-mode
  :ensure t
  :hook (rust-mode . eglot-ensure))

(use-package cargo
  :ensure t
  :hook (rust-mode . cargo-minor-mode))

(use-package flycheck-eglot
  :ensure t
  :after (flycheck eglot)
  :config
  (global-flycheck-eglot-mode 1))

(use-package go-mode
  :ensure t
  :hook ((go-mode . eglot-ensure)
         (before-save . gofmt-before-save))
  :config
  (setq gofmt-command "goimports"))

(use-package godoctor
  :ensure t
  :after go-mode
  :config
  (setq godoctor-executable (executable-find "godoctor")))

(add-hook 'c-mode-common-hook 'google-set-c-style)

(use-package yaml-mode
  :ensure t)

(use-package docker-compose-mode
  :ensure t)

(use-package dockerfile-mode
  :ensure t)

(use-package jenkinsfile-mode
  :ensure t)

(use-package json-mode
  :ensure t
  :config
  (setq js-indent-level 2))

(use-package crystal-mode
  :ensure t)

(use-package julia-mode
  :ensure t)

(use-package bats-mode
  :ensure t)

(use-package bazel
  :ensure t)

;;; ide.el ends here
