(define (attach-tag type-tag contents)
  (if (eq? type-tag `scheme-number)
      contents
      (cons type-tag contents)))

(define (type-tag datum)
  (if (pair? datum)
      (car datum)
      (if (number? datum)
	  `scheme-number
	  (error "Некорректные помеченные данные -- TYPE-TAG" datum))))

(define (contents datum)
  (if (pair? datum)
      (cdr datum)
      (if (number? datum)
	  datum
	  (error "Некорректные помеченные данные -- CONTENTS" datum))))
