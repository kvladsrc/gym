(define unique-pairs
  (lambda (x)
    (define (iter result first next)
      (if (= first x)
	  result
	  (if (> next x)
	      (iter result
		    (+ first 1)
		    (+ first 2))
	      (iter (cons (cons first next)
			  result)
		    first
		    (+ next 1)))))
    (iter (list) 1 2)))
