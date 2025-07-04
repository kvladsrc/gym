(define (lookup key tree)
  (cond ((null? tree) #f)
	((= key (get-key tree)) (get-recodt tree))
	((more? key (get-key tree))
	 (lookup key (right-branch tree)))
	(else (lookup key (left-branch tree)))))
