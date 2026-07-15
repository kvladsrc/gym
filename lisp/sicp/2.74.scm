(define (get-record name file)
  ((get `record (which-file file)) name file))

(define (get-salary record)
  ((get `salary (which-record)) record))

(define (find-emloyee-record name file-list)
  (cond ((null? file-list) (error 1))
	((get-record name (car file-list))
	 (get-record name (car file-list)))
	(find-emloyee-record name (cdr file-list))))
