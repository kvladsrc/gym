
(define (cont-frac n d term k)
  (define (sub-expr i)
    (if (= i k)
      (/ (n i) (d i))
      (/ (n i) (term (d i) (sub-expr (+ i 1))))))
  (sub-expr 1))

(define (cont-frac-iter n d term k)
  (define (iter result i)
    (if (= i 0)
	result
	(iter (/ (n i) (term (d i) result)) (- i 1))))
  (iter (/ (n k) (d k)) (- k 1)))


(define (cont-frac-gr x)
  (cont-frac
   (lambda (x) 1.0)
   (lambda (x) 1.0)
   +
   x))
