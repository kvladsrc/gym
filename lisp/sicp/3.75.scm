(define (make-zero-crossings input-stream last-value prev-value)
  (let ((avpt (/ (+ (stream-car input-stream) prev-value) 2)))
    (cons-stream (sign-change-detector avpt last-value)
                 (make-zero-crossings (stream-cdr input-stream)
                                      avpt
				      (stream-car input-stream)))))
