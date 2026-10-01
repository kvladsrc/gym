;;; key-bindings.el --- Emacs configuration
;;; Commentary:
;;; Code:

(defun indent-buffer ()
  (interactive)
  (save-excursion
    (indent-region (point-min) (point-max) nil)))
(global-set-key [f12] 'indent-buffer)

;; Org-mode
(global-set-key (kbd "C-c l") 'org-store-link)
(global-set-key (kbd "C-c a") 'org-agenda)
(global-set-key (kbd "C-c c") 'org-capture)
(global-set-key "\C-cb" 'org-switchb)

;; Etc
(global-set-key (kbd "C-x r r") 'org-roam-node-find)

;;; key-bindings.el ends here
