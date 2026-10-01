;;; codeforces.el --- create new file to problem
;;; Commentary:
;;
;; create new file to codeforces problem
;;

;;; Code:

(require 'subr-x)

(defgroup codeforces nil
  "Codeforces helper functions."
  :group 'tools)

(defcustom codeforces:dir "~/src/cpp/codeforces/"
  "Codeforces code directory."
  :group 'codeforces
  :type 'directory)

(defun convert-name-to-cc-file (name)
  "Convert a NAME to a .cc file format.
Replaces all capital letters with lowercase,
all spaces with '_', remove all special symbols and adds .cc to
 the end of the file."
  (let ((stem (string-trim
               (replace-regexp-in-string "[^a-z0-9]+" "_" (downcase name))
               "_+" "_+")))
    (when (string-empty-p stem)
      (user-error "Enter a name containing letters or digits"))
    (concat stem ".cc")))

;;;###autoload
(defun codeforces-cc-file (name)
  "Create a .cc file with the given NAME."
  (interactive "sEnter file name: ")
  (let ((file-path (expand-file-name (convert-name-to-cc-file name)
                                     codeforces:dir)))
    (make-directory codeforces:dir t)
    (find-file file-path)))

(provide 'codeforces)
;;; codeforces.el ends here
