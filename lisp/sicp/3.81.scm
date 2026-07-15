(define (random-numbers args)
  (define (generat-stream-map proc numbers args)
    (cons-stream
     (proc (stream-car numbers)
	   (stream-car args))
     (generat-stream-map proc
			 (stream-cdr numbers)
			 (stream-cdr args))))
  (define rand-numers
    (cons-stream
     init-val
     (generat-stream-map
      (lambda (numer arg)
	(if (eq? arg `generate)
	    (rand-upgrade numer)
	    init-val))
      rand-numers args)))
  rand-numers)
