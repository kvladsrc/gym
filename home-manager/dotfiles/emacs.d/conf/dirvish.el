;;; dirvish.el --- dired addon
;;; Commentary:

;;; Code:
(use-package diredfl
  :hook (dired-mode . diredfl-mode))

(use-package dirvish
  :config
  (dirvish-override-dired-mode))

;;; dirvish.el ends here
