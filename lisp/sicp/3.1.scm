(define make-accumulator
  (lambda (initval)
    (define n initval)
    (lambda (acc)
      (begin (set! n (+ n acc))
	     n))))
