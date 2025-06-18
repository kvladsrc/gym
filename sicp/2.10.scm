(define (div-interval a b)
  (if (> 0 (lower-bound b))
      (print "div by null")
      (mul-interval x
		    (make-interval
		     (/ 1 (upper-bound b))
		     (/ 1 (lower-bound b))))))
