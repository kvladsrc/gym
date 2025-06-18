(define (union-set a b)
  (cond ((null? a) b)
	((null? b) a)
	(else
	 (let ((xa (car a))
	       (xb (car b)))
	   (cond ((= xa xb)
		  (cons xa
			(union-set (cdr a) (cdr b))))
		 ((< xa xb)
		  (cons xa
			(union-set (cdr a) b)))
		 (else (cons xb
			     (union-set a (cdr b)))))))))
