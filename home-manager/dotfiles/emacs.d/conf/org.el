;;; Org-mode --- config
;;; Commentary:
;;; Code:

(use-package org
  :ensure t
  :config
  (setq org-agenda-files '("~/plain/org-roam/20230128124036-optional_notes.org"
                           "~/plain/org-roam/20230301211221-computer.org"
                           "~/plain/org-roam/20230304134400-health.org"))
  :hook ((org-mode . auto-fill-mode)
         (org-mode . flycheck-mode)))

;; Org-roam
(defvar org-roam-directory "~/plain/org-roam")
(defvar buffer-to-message "*scratch*")
(defvar org-roam-db-location (format "~/.emacs.d/%s-org-roam.db" (system-name)))

(use-package org-roam
  :ensure t)

(defun message-to-buffer (message-to-send)
  "Send MESSAGE-TO-SEND to buffer-to-message."
  (with-current-buffer buffer-to-message
    (insert message-to-send)))

(if (file-directory-p org-roam-directory)
    (progn (setq org-roam-directory (file-truename org-roam-directory))
           (org-roam-db-autosync-mode))
  (message-to-buffer (format ";; Directory %s do not exist." org-roam-directory)))

(use-package org-roam-ui
  :ensure t
  :after org-roam
  ;; normally we'd recommend hooking orui after org-roam, but since org-roam does not have
  ;; a hookable mode anymore, you're advised to pick something yourself
  ;; if you don't care about startup time, use
  ;; :hook (after-init . org-roam-ui-mode)
  :config
  (setq org-roam-ui-sync-theme t
        org-roam-ui-follow t
        org-roam-ui-update-on-save t
        org-roam-ui-open-on-start t))

;;; org.el ends here
