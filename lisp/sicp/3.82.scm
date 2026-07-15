(define (estimateintegral predicat rect)
  (define estimateintegral-test-series
    (stream-map
     (lambda (rp)
       (predicat (car rp) (cdr rp)))
     random-pairs-in-range))
  (stream-map
   (lambda (m)
     (* (area rect)
	m))
   (monte-carlo
    estimateintegral-test-series 0 0)))
