(define (tan-cf x k)
  (define square-x (* x x))
  (cont-frac-iter
   (lambda (a)
     (if (= a 1)
	 x
	 square-x))
   (lambda (a)
     (- (* a 2) 1))
   -
   k))
