(define (rc R C dt)
  (lambda (v0 i)
    (add-stream
     (scale-stream i R)
     (integral (scale-stream i (/ 1 C))
	       v0
	       dt))))
