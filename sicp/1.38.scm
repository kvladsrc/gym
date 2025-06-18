(define (cont-frac-e x)
  (cont-frac-iter
   (lambda (x) 1.0)
   (lambda (x)
     (if (= 2 (remainder x 3))
	 (+ 2 (* 2 (/ (- x 2) 2)))
	 1))
   +
   x))
