(define (f n)
  (if (< n 3)
      n
      (+ (f (- n 1)) (f (- n 2)) (f (- n 3)))))

(define (iterf n)
  (define (f_iter first second third pos)
    (if (= pos n)
        (+ first second third)
        (f_iter (+ first second third) first second (+ pos 1))))
  (if (< n 3)
      n
      (f_iter 3 2 1 4)))
