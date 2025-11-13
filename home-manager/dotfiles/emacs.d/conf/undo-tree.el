(use-package undo-tree
  :ensure t
  :init
  (global-undo-tree-mode)
  :config

  (define-key undo-tree-map (kbd "C-x r U") nil)
  (define-key undo-tree-map (kbd "C-x r u") nil)

  (global-set-key (kbd "C-x u") 'undo-tree-visualize)

  (setq undo-tree-auto-save-history t)
  (setq undo-tree-history-directory-alist '(("." . "~/.emacs.d/undo-history"))))
