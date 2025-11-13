;;; package --- summary
;;; Commentary:
;;; Code:

(use-package epa-file
  :ensure nil
  :init
  (setq epg-gpg-program "gpg")
  (setq epg-pinentry-mode 'loopback)
  (setq epa-file-select-keys nil)
  :config
  (epa-file-enable))

;;; gpg.el ends here
