(define (stream-limit S tolerance)
  (define (limit Sn Sn+1)
    (let ((N (stream-car Sn))
	  (N+1 (stream-car Sn+1)))
      (if (< tolerance
	     (abs (- N N+1)))
	  N+1
	  (limit (stream-cdr Sn)
		 (stream-cdr Sn+1)))))
  (limit S (stream-cdr S)))
