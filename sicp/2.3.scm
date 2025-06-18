(define (make-rectangle x1 y1 x2 y2)
  (cons (make-segment x1 y1 x1 y2)
	(make-segment x2 y1 x2 y2)))

(define (l-side x)
  (car x))

(define (r-side x)
  (cdr x))

(define (t-side x)
  (cons (end-segment (l-side x))
	(end-segment (r-side x))))

(define (d-side x)
  (cons (start-segment (l-side x))
	(start-segment (r-side x))))

(define (rect-perimetr x)
  (+ (* 2 (segment-length (l-side x)))
     (* 2 (segment-length (t-side x)))))

(define (segment-length x)
  (sqrt (+ (square (abs (- (y-point (end-segment x))
			   (y-point (start-segment x)))))
	   (square (abs (- (x-point (end-segment x))
			   (x-point (start-segment x))))))))

(define (square x)
  (* x x))

(define (rect-area x)
  (* (segment-length (l-side x))
     (segment-length (t-side x))))
