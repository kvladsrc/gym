(define (invert-unit-series S)
  (cons-stream 1
	       (minus (mul-series (stream-cdr S)
				   (invert-unit-series S)))))
