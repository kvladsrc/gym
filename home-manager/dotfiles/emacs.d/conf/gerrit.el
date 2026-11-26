;;; gerrit.el --- Gerrit client
;;; Commentary:
;;; Code:

(use-package gerrit
  :custom
  (gerrit-host "cl.bonfirelit.dev")
  :config
  (progn
    (add-hook 'magit-status-sections-hook #'gerrit-magit-insert-status t)))

;;; gerrit.el ends here
