;Упражнение 2.47.
;Вот два варианта конструкторов для рамок:

;1
(define (make-frame origin edge1 edge2)
  (list origin edge1 edge2))

(define (origin-frame frame)
  (car frame))

(define (edge1-frame frame)
  (cadr frame))

(define (edge2-frame frame)
  (caddr frame))

;2
(define (make-frame origin edge1 edge2)
  (cons origin (cons edge1 edge2)))

(define (origin-frame frame)
  (car frame))

(define (edge1-frame frame)
  (car (cdr frame)))

(define (edge2-frame frame)
  (cdr (cdr frame)))


;К каждому из этих конструкторов добавьте соответствующие селекторы, так, чтобы получить
;реализацию рамок.
