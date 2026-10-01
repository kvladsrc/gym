;;; ide.el --- Programming tools -*- lexical-binding: t; -*-
;;; Commentary:
;;; Code:

;; Увеличение лимитов для лучшей производительности LSP
(setq gc-cons-threshold (* 100 1024 1024)     ; Увеличиваем порог сборщика мусора
      read-process-output-max (* 1024 1024))  ; Увеличиваем максимальный размер вывода процесса

;; Общие настройки
(setq-default indent-tabs-mode nil)           ; Использовать пробелы вместо табов

(use-package google-c-style)

(use-package web-mode
  :mode ("\\.tpl\\'" . web-mode)
  :custom
  (web-mode-engines-alist '(("go" . "\\.tpl\\'"))))

(use-package terraform-mode)

(use-package gdscript-mode)

(use-package kdl-mode)

(use-package just-mode)

(use-package eldoc
  :init
  (global-eldoc-mode))

;; Which-key для подсказок по клавишам
(use-package which-key
  :config
  (which-key-mode))

;; Yasnippet для сниппетов
(use-package yasnippet
  :hook (prog-mode . yas-minor-mode)
  :config
  (yas-reload-all))

(use-package yasnippet-snippets
  :after yasnippet)

;; Eglot owns language-server connections; Flycheck displays diagnostics below.
(use-package eglot
  :hook ((c-mode . eglot-ensure)
         (c++-mode . eglot-ensure))
  :bind (:map eglot-mode-map
              ("C-c e r" . eglot-rename)
              ("C-c e a" . eglot-code-actions)
              ("C-c e f" . eglot-format-buffer)
              ("C-c e o" . eglot-code-action-organize-imports))
  :config
  (add-to-list 'eglot-server-programs
               '((c++-mode c-mode) . ("clangd" "--clang-tidy"))))

(defun my-eglot-format-before-save ()
  "Format only buffers managed by a server that supports formatting."
  (when (and (eglot-managed-p)
             (eglot-server-capable :documentFormattingProvider))
    (eglot-format-buffer)))

(defun my-c++-setup ()
  "Enable buffer-local formatting for C++."
  (add-hook 'before-save-hook #'my-eglot-format-before-save nil t))

;; Keep the existing Google indentation; project .clang-format owns formatting.
(use-package cc-mode
  :mode ("\\.tpp\\'" . c++-mode)
  :hook ((c-mode-common . google-set-c-style)
         (c++-mode . my-c++-setup)))

;; Настройка для Rust
(use-package rust-mode
  :hook (rust-mode . eglot-ensure))

(use-package cargo
  :hook (rust-mode . cargo-minor-mode))

(use-package flycheck-eglot
  :after (flycheck eglot)
  :config
  (global-flycheck-eglot-mode 1))

;; Настройка для Go
(use-package go-mode
  :preface
  (defun my-go-setup ()
    "Enable Go formatting only in this buffer."
    (add-hook 'before-save-hook #'gofmt-before-save nil t))
  :hook ((go-mode . eglot-ensure)
         (go-mode . my-go-setup))
  :config
  (setq gofmt-command "goimports"))

(use-package godoctor
  :after go-mode
  :config
  (setq godoctor-executable (executable-find "godoctor")))

(use-package yaml-mode)

(use-package docker-compose-mode)

(use-package dockerfile-mode)

(use-package jenkinsfile-mode)

(use-package json-mode
  :config
  (setq js-indent-level 2))

(use-package crystal-mode)

(use-package julia-mode)

(use-package bats-mode)

(use-package bazel)

;;; ide.el ends here
