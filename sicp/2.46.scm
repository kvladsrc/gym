;Упражнение 2.46.
;Двумерный вектор v, идущий от начала координат к точке, можно представить в виде пары,
;состоящей из x-координаты и y-координаты. Реализуйте абстракцию данных для векторов, написав
;конструктор make-vect и соответствующие селекторы xcor-vect и ycor-vect. В терминах
;своих селекторов и конструктора реализуйте процедуры add-vect, sub-vect и scale-vect,
;которые выполняют операции сложения, вычитания векторов и умножения вектора на скаляр:


(define make-vect cons)
(define xcor-vect car)
(define ycor-vect cdr)

(define (proc-vect p a b)
  (make-vect (p (xcor-vect a)
		(xcor-vect b))
	     (p (ycor-vect a)
		(ycor-vect b))))

(define (add-vect a b)
  (proc-vect + a b))

(define (sub-vect a b)
  (proc-vect - a b))

(define (scale-vect s v)
  (make-vect (* s (xcor-vect v))
	     (* s (ycor-vect v))))
