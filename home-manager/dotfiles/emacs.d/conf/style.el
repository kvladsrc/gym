;;; style.el --- setting the default view
;;; Commentary:
;;; Code:

;; Theme
(when (display-graphic-p)
  (set-face-attribute 'default nil
                      :family "Mononoki Nerd Font"
                      :height 160
                      :weight 'normal
                      :width 'normal)

  (use-package ef-themes
    :ensure t)

  (use-package almost-mono-themes
    :ensure t)

  (load-theme 'almost-mono-gray t))

(use-package guru-mode
  :ensure t
  :config
  (guru-global-mode 1))

(use-package xclip
  :ensure t
  :config
  (xclip-mode 1))

(tool-bar-mode -1)
(scroll-bar-mode -1)
(menu-bar-mode -1)
(fringe-mode 1)
(setq inhibit-startup-screen t)

;;; style.el ends here
