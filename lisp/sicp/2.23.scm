(define (for-each2 f l)
  (if (null? l)
      #t
      (if (f (car l))
	  (for-each2 f (cdr l))
	  (for-each2 f (cdr l)))))
