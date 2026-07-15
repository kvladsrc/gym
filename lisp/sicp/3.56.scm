(define S (cons-stream 1 (merge (scale-stream 2 S)
				(merge (scale-stream 3 S)
				       (scale-stream 5 S)))))
