(define (encode message tree)
  (if (null? message)
      `()
      (append (encode-symbol (car message) tree)
              (encode (cdr message) tree))))

(define (encode-symbol symbol tree)
  (if (leaf? tree)
      `()
      (if (element-of-set? symbol
			  (symbols tree))
	  (cons (bit symbol tree)
		(encode-symbol symbol
			       (next-branch-encode
				symbol
				tree)))
	  (error))))

(define (element-of-set? a set)
  (cond ((null? set) #f)
	((eq? a (car set)) #t)
	(else (element-of-set? a (cdr set)))))

(define (error)
  (begin (display "OHSHI~~")
	 (newline)))

(define (bit symbol tree)
  (let ((left (left-branch tree))
	(right (right-branch tree)))
    (cond ((element-of-set?
	    symbol (symbols left))
	   `0)
	  ((element-of-set?
	    symbol (symbols right))
	   `1)
	  (else (error)))))

(define (next-branch-encode symbol tree)
  (if (eq? (bit symbol tree) `0)
      (left-branch tree)
      (right-branch tree)))
