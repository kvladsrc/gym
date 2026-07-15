(define (monte-carlo trials experiment)
  (define (iter trials-remaining trials-passed)
    (cond ((= trials-remaining 0)
           (/ trials-passed trials))
          ((experiment)
           (iter (- trials-remaining 1) (+ trials-passed 1)))
          (else
           (iter (- trials-remaining 1) trials-passed))))
  (iter trials 0))

(define (random-in-range low high)
  (let ((range (- high low)))
    (+ low (random range))))

(define (estimateintegral predicat rect trials)
  (* (monte-carlo trials (lambda ()
			   (predicat (rand-point rect))))
     (area rect)))

(define (rand-point rect)
  (make-point (random-in-range (get-x1 rect) (get-x2 rect))
	      (random-in-range (get-y1 rect) (get-y2 rect))))

(define (make-rect point1 point2)
  (cons point1 point2))

(define get-point1 car)

(define get-point2 cdr)

(define (get-x1 rect)
  (get-x (get-point1 rect)))

(define (get-y1 rect)
  (get-y (get-point1 rect)))

(define (get-x2 rect)
  (get-x (get-point2 rect)))

(define (get-y2 rect)
  (get-y (get-point2 rect)))

(define (make-point x y)
  (cons x y))

(define get-x car)

(define get-y cdr)

(define (rect-area rect)
  (* (- (get-x2 rect) (get-x1 rect))
     (- (get-y2 rect) (get-y1 rect))))
