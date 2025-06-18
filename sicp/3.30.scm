(define (riple-carry-adder A B)
  (define (step a b s cin)
    (cond ((and (null? a) (null? b))
	   (cons cin s))
	  (else (let ((sum (make-wire))
		      (cout (make-wire)))
		  (begin (full-adder (car a)
				     (car b)
				     cin
				     sum
				     cout)
			 (step (cdr a)
			       (cdr b)
			       (cons sum
				     s)
			       cout))))))
  (step A B `() (make-wire)))
