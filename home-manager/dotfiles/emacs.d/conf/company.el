;;; company.el --- Summary
;;; Commentary:
;;; Code:

;; ;; Company
;; ;; Company is a text completion framework for Emacs. The name stands
;; ;; for "complete anything". It uses pluggable back-ends and front-ends
;; ;; to retrieve and display completion candidates.

(use-package company
  :ensure t
  :hook (after-init . global-company-mode)
  :config
  (setq company-idle-delay 0.2
        company-tooltip-idle-delay 0.2
        company-minimum-prefix-length 2
        company-tooltip-align-annotations t))

;; (use-package company-box
;;   :ensure t
;;   :hook (company-mode . company-box-mode))

;;; company.el ends here
