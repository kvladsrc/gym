(define (reverse sequence)
  (fold-right (lambda (x y)
		(append x y) nil sequence))

(define (reverse sequence)
  (fold-left (lambda (x y)
	       (cons y x)) nil sequence))
