;Упражнение 2.49.
;С помощью segments->painter определите следующие элементарные рисовалки:
;  а. Рисовалку, которая обводит указанную рамку.
;  б. Рисовалку, которая рисует «Х», соединяя противоположные концы рамки.
;  в. Рисовалку, которая рисует ромб, соединяя между собой середины сторон рамки.
;  г. Рисовалку wave.


(define a
  (segments->painter
   (list (make-segment (make-vect 0 0)
		       (make-vect 1 0))
	 (make-segment (make-vect 1 0)
		       (make-vect 1 1))
	 (make-segment (make-vect 1 1)
		       (make-vect 0 1))
	 (make-segment (make-vect 0 1)
		       (make-vect 0 0)))))

(define b
  (segments->painter
   (list (make-segment (make-vect 0 0)
		       (make-vect 1 1))
	 (make-segment (make-vect 1 0)
		       (make-vect 0 1)))))

(define v
  (segments->painter
   (list (make-segment (make-vect (/ 1 2) 0)
		       (make-vect 1 (/ 1 2)))
	 (make-segment (make-vect 1 (/ 1 2))
		       (make-vect (/ 1 2) 1))
	 (make-segment (make-vect (/ 1 2) 1)
		       (make-vect 0 (/ 1 2)))
	 (make-segment (make-vect 0 (/ 1 2))
		       (make-vect (/ 1 2) 0)))))

(define wave
  (segments->painter
   (list ... )))
