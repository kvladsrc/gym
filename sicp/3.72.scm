(define sum-of-square
  (define (filter S)
    (let ((this (car S))
	  (next (cadr S)))
      (cond ((= this next)
	     (cons-stream this
			  (filter (stream-cdr S))))
	    (else (filter (stream-cdr S))))))
  (filter (weighted-pairs
	   integers integers
	   (lambda (l)
	     (let ((i (car l))
		   (j (caddr l)))
	       (+ (* i i)
		  (* j j)))))
