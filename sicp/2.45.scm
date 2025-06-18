(define (split first second painter r)
  (if (= n 0)
      painter
      (let ((smaller (split painter (- n 1))))
	(first painter (second smaller smaller)))))
