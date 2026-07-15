(define (triples S T U)
  (cond ((or (stream-null? S)
	     (stream-null? T)
	     (stream-null? U))
	 the-empty-stream)
	(else
	 (cons-stream
	  (list (stream-car S)
		(stream-car T)
		(stream-car U))
	  (interleave
	   (interleave
	    (stream-map
	     (lambda (x) (list (stream-car S)
			  (stream-car T)
			  x))
	     (stream-cdr U))
	    (triples S
		     (stream-cdr T)
		     (stream-cdr U)))
	   (triples (stream-cdr S)
		    (stream-cdr T)
		    (stream-cdr U))))))

(define pythagoras
  (stream-filter
   (lambda (x)
     (= (+ (square (car x))
	   (square (cadr x)))
	(square (caddr x))))
   (triples integers integers integers)))
