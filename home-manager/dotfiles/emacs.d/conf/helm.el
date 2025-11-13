;;; Helm
;;; Commentary:
;;; Code:

;; Helm

(use-package swiper-helm
  :ensure t)

(use-package helm
  :ensure t
  :init
  (when (executable-find "curl")
    (setq helm-google-suggest-use-curl-p t))

  (setq helm-split-window-in-side-p           t
        helm-move-to-line-cycle-in-source     t
        helm-ff-search-library-in-sexp        t
        helm-ff-file-name-history-use-recentf t
        helm-echo-input-in-header-line        t)

  :config
  (helm-mode 1))

;;; helm.el ends here
