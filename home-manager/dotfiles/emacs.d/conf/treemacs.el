;;; treemacs.el --- Project tree -*- lexical-binding: t; -*-
;;; Commentary:
;;; Code:

(use-package treemacs
  :bind (("M-0" . treemacs-select-window)
         ("C-x t 1" . treemacs-delete-other-windows)
         ("C-x t t" . treemacs)
         ("C-x t d" . treemacs-select-directory)
         ("C-x t B" . treemacs-bookmark)
         ("C-x t C-t" . treemacs-find-file)
         ("C-x t M-t" . treemacs-find-tag))
  :custom
  (treemacs-width 35)
  (treemacs-persist-file
   (expand-file-name ".cache/treemacs-persist" user-emacs-directory))
  :config
  (treemacs-follow-mode 1)
  (treemacs-filewatch-mode 1)
  (treemacs-fringe-indicator-mode 'always)
  (when (executable-find "git")
    (treemacs-git-mode (if treemacs-python-executable 'deferred 'simple))))

(use-package treemacs-projectile
  :after (treemacs projectile))

(use-package treemacs-magit
  :after (treemacs magit))

(use-package treemacs-tab-bar
  :after treemacs
  :config
  (treemacs-set-scope-type 'Tabs))

;;; treemacs.el ends here
