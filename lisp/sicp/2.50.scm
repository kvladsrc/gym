;Упражнение 2.50.
;Определите преобразование flip-horiz, которое обращает изображение вокруг горизонтальной
;оси, а также преобразования, которые вращают рисовалки против часовой стрелки на 180 и 270
;градусов.

(define (flip-horiz painter)
  (transform-painter painter
		     (make-vect 1.0 0.0)
		     (make-vect 0.0 0.0)
		     (make-vect 1.0 1.0)))

(define (v180 painter)
  (transform-painter painter
		     (make-vect 1.0 1.0)
		     (make-vect 0.0 1.0)
		     (make-vect 1.0 0.0)))

(define (v270 painter)
  (transform-painter painter
		     (make-vect 0.0 1.0)
		     (make-vect 0.0 0.0)
		     (make-vect 1.0 1.0)))
