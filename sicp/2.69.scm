(define (generate-huffman-tree pairs)
  (successive-merge (make-leaf-set pairs)))

(define (successive-merge set)
  (if (null? cdr set)
      (car set)
      (successive-merge
       (make-leaf-set
	(cons (make-code-tree
	       (car set)
	       (cdr set))
	      (cdr (cdr set)))))))
