(define (div-series num den)
  (let ((den0 (stream-car den)))
    (if (= den0 0)
        "Error"
        (scale-stream
         (mul-series
          num (invert-unit-series
               (scale-stream den (/ 1 den0))))
         den0))))
