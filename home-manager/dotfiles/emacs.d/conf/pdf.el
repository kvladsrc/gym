;;; package --- Summary
;;; Commentary:
;;; Code:

(use-package pdf-tools
  :ensure t
  :magic ("%PDF" . pdf-view-mode)  ; Автоматически использовать pdf-view-mode для PDF-файлов
  :config
  ;; Инициализация pdf-tools и установка необходимых компонентов
  (pdf-loader-install)

  ;; Дополнительные настройки
  (setq-default pdf-view-display-size 'fit-page)  ; Автоматически подгонять страницу по размеру окна

  ;; Отключение предупреждения при открытии больших файлов
  (setq large-file-warning-threshold nil)

  ;; Настройка клавиш для удобной навигации
  (define-key pdf-view-mode-map (kbd "C-s") 'isearch-forward)
  (define-key pdf-view-mode-map (kbd "C-r") 'isearch-backward))

;;; pdf.el ends here
