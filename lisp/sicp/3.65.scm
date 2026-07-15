(define (ln-2-summary n)
  (cons-stream (/ 1 n)
	       (stream-map - (ln-2-summary (+ n 1)))))

(define ln-2-stream
  (partial-sums (ln-2-summary 1)))

(define (euler-transform s)
  (let ((s0 (stream-ref s 0))
	(s1 (stream-ref s 1))
	(s2 (stream-ref s 2)))
    (cons-stream (- s2 (/ (square (- s2 s1))
                          (+ s0 (* -2 s1) s2)))
                 (euler-transform (stream-cdr s)))))

(define (accelerated-sequence transform s)
  (stream-map stream-car
              (make-tableau transform s)))
