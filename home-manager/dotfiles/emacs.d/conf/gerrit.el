;;; gerrit.el --- Gerrit client
;;; Commentary:
;;; Code:

(use-package gerrit
  :ensure t
  :custom
  (gerrit-host "cl.your.domain")
  :config
  (progn
    (add-hook 'magit-status-sections-hook #'gerrit-magit-insert-status t)))

;;; gerrit.el ends here
