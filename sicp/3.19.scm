(define step 1)

(define (circle? x)
  (let ((element x)
	(remain 1))
    (define (circle-iter remain-in-iter seq)
      (if (null? seq)
	  #f
	  (if (eq? element seq)
	      #t
	      (if (= 0 remain-in-iter)
		  (begin (set! element seq)
			 (set! remain (+ remain step))
			 (circle-iter remain element))
		  (circle-iter (- remain-in-iter 1)
			       (cdr seq))))))
    (circle-iter remain element)))

(define (last-pair x)
  (if (null? (cdr x))
      x
      (last-pair (cdr x))))

(define (make-cycle x)
  (set-cdr! (last-pair x) x)
  x)

(define z (make-cycle (list `a `b `c)))
