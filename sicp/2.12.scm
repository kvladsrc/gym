(define (make-center-percent c w)
  (make-interval (- c (* (/ (abs c) 100) w))
		 (+ c (* (/ (abs c) 100) w))))

(define (percent i)
  (let ((w (width i))
	(c (center )))
    (/ 100 (/ c w))))
