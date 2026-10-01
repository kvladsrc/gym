;;; corfu.el --- In-buffer completion -*- lexical-binding: t; -*-
;;; Commentary:
;;; Code:

(use-package corfu
  :custom
  (corfu-auto t)
  :init
  (global-corfu-mode 1)
  :config
  (corfu-history-mode 1)
  (corfu-popupinfo-mode 1))

(use-package corfu-terminal
  :if (< emacs-major-version 31)
  :after corfu
  :config
  (corfu-terminal-mode 1))

(use-package cape
  :init
  ;; Append fallbacks so mode-specific completion (including Eglot) wins.
  (add-hook 'completion-at-point-functions #'cape-file t)
  (add-hook 'completion-at-point-functions #'cape-dabbrev t))

(use-package emacs
  :custom
  (tab-always-indent 'complete)
  (text-mode-ispell-word-completion nil)
  (read-extended-command-predicate #'command-completion-default-include-p))

;;; corfu.el ends here
