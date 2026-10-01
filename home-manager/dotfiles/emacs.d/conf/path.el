;;; path.el --- Project environments -*- lexical-binding: t; -*-
;;; Commentary:
;; Enable envrc after the other global modes so new buffers receive their
;; project environment before checkers and language servers start.
;;; Code:

(use-package envrc
  :hook (after-init . envrc-global-mode))

;;; path.el ends here
