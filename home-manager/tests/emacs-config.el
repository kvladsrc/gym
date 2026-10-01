;;; emacs-config.el --- Configuration regression checks -*- lexical-binding: t; -*-
;;; Commentary:
;; Run with the Nix-packaged Emacs, --batch -q -l this-file.
;;; Code:

(require 'ert)
(require 'cl-lib)
(require 'package)

;; Batch Emacs skips normal startup activation.  Activate only the wrapper's
;; package directory, as the local early-init.el does for interactive startup.
(setq package-user-dir
      (file-name-directory
       (directory-file-name (file-name-directory (locate-library "corfu")))))
(setq package-directory-list nil)
(package-initialize)
(require 'use-package)
(setq use-package-always-ensure nil)

(defconst my-test-config-directory
  (expand-file-name "../dotfiles/emacs.d/" (file-name-directory load-file-name)))
(defconst my-test-state-directory (make-temp-file "emacs-config-" t))
(setq user-emacs-directory (file-name-as-directory my-test-state-directory)
      default-directory user-emacs-directory
      org-roam-directory (expand-file-name "missing-notes" user-emacs-directory)
      org-roam-db-location (expand-file-name "org-roam.db" user-emacs-directory))

(defvar my-test-config-errors nil)
(defun my-test-record-warning (type message &optional level &rest _)
  "Record configuration errors for TYPE with MESSAGE and LEVEL."
  (when (eq level :error)
    (push (list type message) my-test-config-errors)))
(advice-add 'display-warning :before #'my-test-record-warning)

;; Exercise the existing unmanaged init.el's load order, without its private
;; Customize settings, agenda startup, or any real Org-roam database.
(dolist (module '(avy codeforces corfu
                      dirvish flycheck gerrit gpg ide key-bindings magit
                      minibuffer nix org path pdf projectile style
                      treemacs undo-tree))
  (load (expand-file-name (format "conf/%s.el" module) my-test-config-directory)
        nil t))

(ert-deftest my-config-loads-without-errors ()
  (should-not my-test-config-errors)
  (should-not (featurep 'aidermacs)))

(ert-deftest my-config-hooks-and-bindings ()
  (should (memq #'diredfl-mode dired-mode-hook))
  (should-not (bound-and-true-p dired-mode-hook-hook))
  (should (eq (key-binding (kbd "C-c c")) #'org-capture))
  (should (eq (key-binding (kbd "M-s r")) #'consult-ripgrep))
  (should (eq (key-binding (kbd "C-.")) #'embark-act))
  (require 'eglot)
  (should (eq (lookup-key eglot-mode-map (kbd "C-c e r")) #'eglot-rename)))

(ert-deftest my-format-requires-a-capable-server ()
  (let ((formats 0))
    (cl-letf (((symbol-function 'eglot-format-buffer)
               (lambda (&rest _) (cl-incf formats))))
      (with-temp-buffer
        (my-eglot-format-before-save)
        (should (= formats 0)))
      (cl-letf (((symbol-function 'eglot-managed-p) (lambda () t))
                ((symbol-function 'eglot-server-capable) (lambda (&rest _) nil)))
        (my-eglot-format-before-save)
        (should (= formats 0)))
      (cl-letf (((symbol-function 'eglot-managed-p) (lambda () t))
                ((symbol-function 'eglot-server-capable) (lambda (&rest _) t)))
        (my-eglot-format-before-save)
        (should (= formats 1))))))

(ert-deftest my-go-formatting-is-buffer-local ()
  (require 'go-mode)
  (should-not (memq #'gofmt-before-save (default-value 'before-save-hook)))
  (cl-letf (((symbol-function 'eglot-ensure) #'ignore))
    (with-temp-buffer
      (go-mode)
      (should (local-variable-p 'before-save-hook))
      (should (memq #'gofmt-before-save before-save-hook))))
  (with-temp-buffer
    (should-not (memq #'gofmt-before-save before-save-hook))))

(ert-deftest my-codeforces-normalizes-and-opens-existing-files ()
  (should (equal (convert-name-to-cc-file " A..... Test! ") "a_test.cc"))
  (should-error (convert-name-to-cc-file "... ") :type 'user-error)
  (let* ((codeforces:dir (expand-file-name "problems" user-emacs-directory))
         (file (expand-file-name "a_test.cc" codeforces:dir)))
    (make-directory codeforces:dir t)
    (with-temp-file file (insert "// keep existing contents\n"))
    (cl-letf (((symbol-function 'eglot-ensure) #'ignore))
      (unwind-protect
          (progn
            (codeforces-cc-file "A Test")
            (should (equal (buffer-file-name) file))
            (should (equal (buffer-string) "// keep existing contents\n")))
        (when-let* ((buffer (get-file-buffer file)))
          (kill-buffer buffer))))))

(ert-run-tests-batch-and-exit)
;;; emacs-config.el ends here
