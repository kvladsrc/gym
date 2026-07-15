(define (union-set a b)
  (list->tree (union (tree->list a)
		     (tree->list b))))

(define (intersection-set a b)
  (list->tree (intersection (tree->list a)
			    (tree->list b))))
