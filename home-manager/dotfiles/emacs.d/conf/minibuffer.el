;;; minibuffer.el --- Minibuffer completion configuration
;;; Commentary:
;;; Code:

;; Vertico: Vertical completion UI
(use-package vertico
  :ensure t
  :init
  (vertico-mode))

;; Save history
(use-package savehist
  :init
  (savehist-mode))

;; Marginalia: Annotations in minibuffer
(use-package marginalia
  :ensure t
  :after vertico
  :init
  (marginalia-mode))

;; Orderless: Fuzzy matching
(use-package orderless
  :ensure t
  :custom
  (completion-styles '(orderless basic))
  (completion-category-overrides '((file (styles basic partial-completion)))))

;; Consult: Search and navigation
(use-package consult
  :ensure t
  :hook (completion-list-mode . consult-preview-at-point-mode)
  :init
  ;; Optionally configure the register formatting. This improves the register
  ;; preview for `consult-register', `consult-register-load',
  ;; `consult-register-store' and the Emacs built-in commands.
  (setq register-preview-delay 0.5
        register-preview-function #'consult-register-format)

  ;; Optionally tweak the register preview window.
  ;; This adds thin lines, sorting and hides the mode line of the window.
  (advice-add #'register-preview :override #'consult-register-window)

  ;; Use Consult to select xref locations with preview
  (setq xref-show-xrefs-function #'consult-xref
        xref-show-definitions-function #'consult-xref))

;;; minibuffer.el ends here
