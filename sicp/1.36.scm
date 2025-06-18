(define fpx
  (fixed-point-v (lambda (x)
		   (/ (log 1000) (log x)))
		 2.0))

(define fpx_d
  (fixed-point-v (lambda (x)
		   (average x (/ (log 1000) (log x))))
		 2.0))
