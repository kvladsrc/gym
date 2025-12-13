;;; kubernetes.el --- Summary
;;; Commentary:
;;; Code:

(use-package kubernetes
  :ensure t
  :commands (kubernetes-overview)
  :config
  ;; реже опрашивать API, чтобы не шуметь на кластере
  (setq kubernetes-redraw-frequency 5
        kubernetes-poll-frequency 3600))

;;; kubernetes.el ends here
