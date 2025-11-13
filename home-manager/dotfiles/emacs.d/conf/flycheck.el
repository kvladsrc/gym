;;; flycheck.el --- Summary
;;; Commentary:
;;; Code:

;; Flycheck
;; Flycheck is a modern on-the-fly syntax checking extension for GNU
;; Emacs, intended as replacement for the older Flymake extension
;; which is part of GNU Emacs.

(use-package flycheck
  :ensure t
  :config
  (global-flycheck-mode))

(use-package flycheck-languagetool
  :ensure t
  :after flycheck
  :hook (org-mode . flycheck-languagetool-setup)
  :init
  (setq flycheck-languagetool-server-jar "~/.local/bin/LanguageTool-6.2/languagetool-server.jar"))

(use-package flycheck-yamllint
  :ensure t
  :after flycheck
  :config
  (add-hook 'flycheck-mode-hook 'flycheck-yamllint-setup))

;;; flycheck.el ends here
