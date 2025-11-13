;;; key-bindings.el --- Emacs configuration
;;; Commentary:
;;; Code:

(defun indent-buffer ()
  (interactive)
  (save-excursion
    (indent-region (point-min) (point-max) nil)))
(global-set-key [f12] 'indent-buffer)

;; Helm
(define-key global-map [remap find-file] #'helm-find-files)
(define-key global-map [remap execute-extended-command] #'helm-M-x)
(define-key global-map [remap switch-to-buffer] #'helm-mini)
(define-key global-map [remap isearch-backward] #'swiper-helm)

;; The default "C-x c" is quite close to "C-x C-c", which quits Emacs.
;; Changed to "C-c h". Note: We must set "C-c h" globally, because we
;; cannot change `helm-command-prefix-key' once `helm-config' is loaded.
(global-set-key (kbd "C-c h") 'helm-command-prefix)
(global-unset-key (kbd "C-x c"))
(define-key helm-map (kbd "<tab>") 'helm-execute-persistent-action) ; rebind tab to run persistent action
(define-key helm-map (kbd "C-i") 'helm-execute-persistent-action) ; make TAB work in terminal
(define-key helm-map (kbd "C-z")  'helm-select-action) ; list actions using C-z

;; Ace jump buffer
;; (global-set-key (kbd "C-x o") #'ace-window)

;; Avy
;; (global-set-key (kbd "M-g e") 'avy-goto-word-0)
;; (global-set-key (kbd "C-:") 'avy-goto-char)

;; Org-mode
(global-set-key (kbd "C-c l") 'org-store-link)
(global-set-key (kbd "C-c a") 'org-agenda)
(global-set-key (kbd "C-c c") 'org-capture)
(global-set-key "\C-cb" 'org-switchb)

;; Etc
(global-unset-key (kbd "M-<tab>"))
(global-set-key (kbd "C-x r r") 'org-roam-node-find)

;;; key-bindings.el ends here
