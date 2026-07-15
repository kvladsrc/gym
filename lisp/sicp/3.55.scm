(define (partial-sums S)
  (cons-stream (stream-car S)
	       (add-streams S
			    (partial-sums (stream-cdr S))))))
