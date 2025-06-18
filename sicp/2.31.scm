(define (tree-map procedure tree)
  (cond ((pair? tree) (cons (tree-map procedure (car tree))
			    (tree-map procedure (cdr tree))))
	((null? tree) null)
	(else (procedure tree))))

(define (square x)
  (* x x))

(define (tree-map-hight-level procedure tree)
  (map (lambda (x)
	 (if (pair? x)
	     (tree-map-hight-level procedure x)
	     (procedure x)))
       tree))

(define (square-tree tree)
  (tree-map square tree))
