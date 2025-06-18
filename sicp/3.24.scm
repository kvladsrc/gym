(define (make-table predicat)
  (let ((local-table (list ’*table*))
	(key-equal? predicat))
    (define (lookup key-1 key-2)
      (let ((subtable (assoc key-1 (cdr local-table)
			     key-equal?)))
        (if subtable
            (let ((record (assoc key-2 (cdr subtable)
				 key-equal?)))
	      (if record
		  (cdr record)
		  false))
	    false)))
    (define (insert! key-1 key-2 value)
      (let ((subtable (assoc key-1 (cdr local-table)
			     key-equal?)))
	(if subtable
	    (let ((record (assoc key-2 (cdr subtable)
				 key-equal?)))
	      (if record
		  (set-cdr! record value)
		  (set-cdr! subtable
			    (cons (cons key-2 value)
				  (cdr subtable)))))
	    (set-cdr! local-table
		      (cons (list key-1
				  (cons key-2 value))
			    (cdr local-table)))))
      ’ok)
    (define (dispatch m)
      (cond ((eq? m ’lookup-proc) lookup)
	    ((eq? m ’insert-proc!) insert!)
	    (else (error "Неизвестная операция -- TABLE" m))))
    dispatch))

(define (assoc key records predicat)
  (cond ((null? records) false)
        ((predicat key (caar records)) (car records))
        (else (assoc key (cdr records predicat)))))
