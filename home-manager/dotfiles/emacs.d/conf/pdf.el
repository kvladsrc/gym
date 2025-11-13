;;; package --- Summary
;;; Commentary:
;;; Code:

(use-package pdf-tools
  :ensure t
  :magic ("%PDF" . pdf-view-mode)
  :config
  (pdf-loader-install)

  (setq-default pdf-view-display-size 'fit-page)

  (setq large-file-warning-threshold nil)

  (define-key pdf-view-mode-map (kbd "C-s") 'isearch-forward)
  (define-key pdf-view-mode-map (kbd "C-r") 'isearch-backward))

;;; pdf.el ends here
