(define (sum term a next b)
  (if (> a b)
      0
      (+ (term a)
         (sum term (next a) next b))))

(define (inc a) (+ a 1))

(define (sum-of-cube a b)
  (define (cube a) (* a a a))
  (sum cube a inc b))

(define (sum-intigers a)
  (define (identity a) a)
  (sum identity 1 inc a))

(define (pi-sum a b)
  (define (pi-term x)
    (/ 1.0 (* x (+ x 2))))
  (define (pi-next x)
    (+ x 4))
  (sum pi-term a pi-next b))
