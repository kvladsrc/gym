(define (solve-2nd a b dt y0 dy0)
  (define y
    (integral (delay dy) y0 dt))
  (define ddy
    (add-stream (scale-stream dy a)
		(scale-stream y b)))
  (define dy
    (integral (delay ddy) dy0 dt))
  y)
