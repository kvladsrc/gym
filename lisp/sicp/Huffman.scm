(define (make-leaf symbol weight)
  (list `leaf symbol weight))

(define (leaf? object)
  (eq? (car object) `leaf))

(define (symbol-leaf x) (cadr x))

(define (weight-leaf x) (caddr x))

(define (make-code-tree left right)
  (list left
        right
        (append (symbols left) (symbols right))
        (+ (weight left) (weight right))))

(define (left-branch tree) (car tree))

(define (right-branch tree) (cadr tree))

(define (symbols tree)
  (if (leaf? tree)
      (list (symbol-leaf tree))
      (caddr tree)))

(define (weight tree)
  (if (leaf? tree)
      (weight-leaf tree)
      (cadddr tree)))

(define (decode bits tree)
  (define (decode-1 bits current-branch)
    (if (null? bits)
        `()
        (let ((next-branch
               (choose-branch (car bits) current-branch)))
          (if (leaf? next-branch)
              (cons (symbol-leaf next-branch)
                     (decode-1 (cdr bits) tree))
              (decode-1 (cdr bits) next-branch)))))
  (decode-1 bits tree))

(define (choose-branch bit branch)
  (cond ((= bit 0) (left-branch branch))
        ((= bit 1) (right-branch branch))
        (else (error "плохой бит -- CHOOSE-BRANCH" bit))))

(define (adjoin-set x set)
  (cond ((null? set) (list x))
        ((< (weight x) (weight (car set))) (cons x set))
        (else (cons (car set)
                    (adjoin-set x (cdr set))))))

(define (make-leaf-set pairs)
  (if (null? pairs)
      `()
      (let ((pair (car pairs)))
        (adjoin-set (make-leaf (car pair)
                               (cadr pair))
                    (make-leaf-set (cdr pairs))))))

(define sample-tree
  (make-code-tree (make-leaf `A 4)
                  (make-code-tree
                   (make-leaf `B 2)
                   (make-code-tree (make-leaf `D 1)
                                    (make-leaf `C 1)))))

(define sample-message `(0 1 1 0 0 1 0 1 0 1 1 1 0))

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

(define (generate-huffman-tree pairs)
  (successive-merge (make-leaf-set pairs)))

(define (successive-merge set)
  (if (null? cdr set)
      (car set)
      (successive-merge
       (make-leaf-set
	(cons (make-code-tree
	       (car set)
	       (cdr set))
	      (cdr (cdr set)))))))
