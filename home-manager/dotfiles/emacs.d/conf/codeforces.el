;;; codeforces.el --- create new file to problem
;;; Commentary:
;;
;; create new file to codeforces problem
;;

;;; Code:

(defgroup codeforces nil
  "Codeforces helper functions."
  :group 'codeforces)

(defcustom codeforces:dir "~/src/cpp/codeforces/"
  "Codeforces code directory."
  :group 'codeforces
  :type 'string)

(defun convert-name-to-cc-file (name)
  "Convert a NAME to a .cc file format.
Replaces all capital letters with lowercase,
all spaces with '_', remove all special symbols and adds .cc to
 the end of the file."
  (let ((new-name (replace-regexp-in-string
                   "__" "_"
                   (replace-regexp-in-string
                    " " "_"
                    (replace-regexp-in-string
                     "[^a-zA-Z0-9]" " "
                     (downcase name))))))
    (concat new-name ".cc")))

;;;###autoload
(defun codeforces-cc-file (name)
  "Create a .cc file with the given NAME."
  (interactive "sEnter file name: ")
  (let* ((file-name (convert-name-to-cc-file name))
         (file-path (concat (file-name-as-directory codeforces:dir) file-name)))
    (unless (file-exists-p file-path)
      (with-temp-buffer
        (write-file file-path))
      (find-file file-path))))

(provide 'codeforces)
;;; codeforces.el ends here
