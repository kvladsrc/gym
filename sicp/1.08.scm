(define (cube_root x)
  (define (improve guess)
    (/ (+ (/ x (square guess)) (* 2 guess)) 3))
  (define (good_enough? guess next)
    (< (abs (- guess next)) 0.0001))
  (define (iter guess next)
    (if (good_enough? guess next)
        next
        (iter next (improve next))))
  (iter 1.0 (improve 1.0)))

(define (square a) (* a a))
