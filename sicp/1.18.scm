(define (multiplication a b)
  (define (iter result a b)
    (cond ((= b 0) result)
	  ((even? b) (iter result (double a) (half b)))
	  (else (iter (+ result a) a (- b 1)))))
  (iter 0 a b))
