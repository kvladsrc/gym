(define (smooth S)
  (define (smoothed-stream Sn Sn+1)
    (cons-stream (average (stream-car Sn)
			  (stream-car Sn+1))
		 (smoothed-stream Sn+1 (stream-cdr Sn+1))))
  (smoothed-stream S (stream-cdr S)))

(define (average a b)
  (/ (+ a b) 2))


(define zero-crossings
  (stream-map sign-change-detector (smooth sense-data) (smooth (stream-cdr sense-data))))
