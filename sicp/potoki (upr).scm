(define the-empty-stream `())
(define stream-null? null?)

(define (stream-car s) (car s))
(define (stream-cdr s) (force (cdr s)))
(define (cons-stream a b) (cons a (delay b)))

(define (stream-map proc s)
  (if (stream-null? s)
      the-empty-stream
      (cons-stream (proc (stream-car s))
		   (stream-map proc (stream-cdr s)))))

(define (stream-filter pr S)
  (if (stream-null? S)
      the-empty-stream
      (if (pr (stream-car S))
	  (cons-stream (stream-car S)n
		       (stream-filter pr (stream-cdr S))))))

(define (stream-ref n s)
  (if (= n 0)
      (stream-car s)
      (stream-ref (- n 1)
		  (stream-cdr s))))

(define (stream-enumerate-interval low hight)
  (if (> low hight)
      the-empty-stream
      (cons-stream low
		   (stream-enumerate-interval (+ low 1)
					      hight))))

(define (display-stream S)
  (if (stream-null? S)
      "Done"
      (begin (display (stream-car S))
	     (newline)
	     (display-stream (stream-cdr S)))))

(define (interleave S T)
  (cond ((stream-null? S) T)
	((stream-null? T) S)
	(cons-stream (stream-car S)
		     (interleave T (stream-cdr S)))))

;(define (integers-starting-from n)
;  (cons-stream n (integers-starting-from (+ n 1))))

;(define integers (integers-starting-from 1))
