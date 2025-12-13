;;; Helm
;;; Commentary:
;;; Code:

;; Helm

(use-package swiper-helm
  :ensure t)

(use-package helm
  :ensure t
  :init
  ;; Использовать curl для helm-google-suggest, если доступно
  (when (executable-find "curl")
    (setq helm-google-suggest-use-curl-p t))

  ;; Общие настройки Helm
  (setq helm-split-window-in-side-p           t   ; Открывать буфер Helm внутри текущего окна
        helm-move-to-line-cycle-in-source     t   ; Циклически перемещаться по источникам
        helm-ff-search-library-in-sexp        t   ; Искать библиотеки в sexp
        helm-ff-file-name-history-use-recentf t   ; Использовать recentf для истории имен файлов
        helm-echo-input-in-header-line        t)  ; Показывать ввод в строке заголовка

  :config
  ;; Включить Helm Mode
  (helm-mode 1))

;;; helm.el ends here
