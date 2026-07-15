;Упражнение 2.51.
;Определите для рисовалок операцию below. Below принимает в качестве аргументов две ри-
;совалки. Когда получившейся рисовалке передается рамка, она рисует в нижней ее половине при
;помощи первой рисовалки, а в верхней при помощи второй. Определите below двумя способами —
;один раз аналогично процедуре beside, как она приведена выше, а второй раз через beside и
;операции вращения (см. упражнение 2.50).

(define (below painter1 painter2)
  (let ((split-point (make-vect 0.0 0.5)))
    (let ((paint-down
           (transform-painter painter1
                              (make-vect 0.0 0.0)
                              (make-vect 1.0 0.0)
                              split-point)
	   (paint-up
	    (transform-painter painter2
			       split-point
			       (make-vect 1.0 0.5)
			       (make-vect 0.0 1.0))))
	  (lambda (frame)
	    (paint-down frame)
	    (paint-up frame))))))

(define (below painter1 painter2)
  (v90 (beside (v90 painter1)
	       (v90 painter2))))
