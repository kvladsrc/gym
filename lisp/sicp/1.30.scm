(define (sum term a next b)
  (define (iter result step)
    (if (> step b)
        result
        (iter (+ result (term step)) (next step))))
  (iter 0 a))

(define (sum-of-intigers a b)
  (define (inc x) (+ x 1))
  (define (identity x) x)
  (sum identity a inc b))
