(define (addend s) (cadr s))

(define (augend s)
  (cond ((null? s) 0)
	(else (make-sum (car s)
			(augend (cdr s))))))

(define (multiplier p) (cadr p))

(define (multiplicand p)
  (cond ((null? p) 1)
	(else (make-product (car p)
			    (multiplicand p)))))
