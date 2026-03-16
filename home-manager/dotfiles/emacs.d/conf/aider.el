;;; aider.el --- aider support in emacs
;;; Commentary:
;;; Code:

(use-package aidermacs
  :ensure t
  :bind (("C-c c a" . aidermacs-transient-menu))
  :config
  (setenv "OPENROUTER_API_KEY" (password-store-get "OpenRouter/lll-exp"))
  :custom
  (aidermacs-default-chat-mode 'code)
  (aidermacs-default-model "openrouter/stepfun/step-3.5-flash:free"))

;;; aider.el ends here
