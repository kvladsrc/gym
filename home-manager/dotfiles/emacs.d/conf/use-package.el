;;; use-package.el --- config
;;; Commentary:
;;; Code:

;; Bootstrap `use-package`
(unless (package-installed-p 'use-package)
  (package-refresh-contents)
  (package-install 'use-package))
(require 'use-package)

;;; use-package.el ends here
