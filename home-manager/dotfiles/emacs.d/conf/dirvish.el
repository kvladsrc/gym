;;; dirvish.el --- dired addon
;;; Commentary:

;;; Code:
(use-package all-the-icons
  :ensure t)

(use-package diredfl
  :ensure t
  :hook (dired-mode-hook . diredfl-mode))

(use-package dirvish
  :ensure t
  :config
  (dirvish-override-dired-mode))

;;; dirvish.el ends here
