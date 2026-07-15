(define (sum? exp)
  (eq? (cadr exp) `+))

(define (append exp)
  (car exp))

(define (augend exp)
  (caddr exp))

(define (make-sum a b)
  (cond ((=number? a 0) b)
	((=number? b 0) a)
	((and (number? b) (number? a))
	 (+ a b))
	(else (list a `+ b))))

(define (product? exp)
  (eq? (cadr exp) `*))

(define (multipler p)
  (car p))

(define (multiplicant p)
  (caddr p))

(define (make-product a b)
  (cond ((=number? a 1) b)
	((=number? b 1) a)
	((and (number? a) (number? b))
	 (* a b))
	(else (list a `* b))))
