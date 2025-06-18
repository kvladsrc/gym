(define (pairs s t)
  (cond ((or (stream-null? s)
	     (stream-null? t))
	 the-empty-stream)
	(else
	 (cons-stream
	  (list (stream-car s) (stream-car t))
	  (interleave
	   (stream-map (lambda (x) (list (stream-car s) x))
		       (stream-cdr t))
	   (pairs (stream-cdr s) (stream-cdr t)))))))
