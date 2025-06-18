(define (reverse l)
  (define (reverse-iter s r)
    (if (null? s)
	r
	(reverse-iter (cdr s) (cons (car s) r))))
  (reverse-iter l (list)))
