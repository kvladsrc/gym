(define (deep-reverse l)
  (define (iter-reverse s result)
    (if (null? s)
	result
	(if (pair? (car s))
	    (iter-reverse (cdr s)
			  (cons (iter-reverse (car s)
					      (list))
				result))
	    (iter-reverse (cdr s) (cons (car s)
					result)))))
  (iter-reverse l (list)))
