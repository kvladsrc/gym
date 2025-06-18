(define (multiplication a b)
  (cond ((= b 0) 0)
	((even? b) (multiplication (double a) (half b)))
	(else (+ a (multiplication a (- b 1))))))
