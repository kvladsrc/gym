                                        ;good-enough, improve как примитивы
(data-paths
 (registers
  ((name guess)
   (buttons
    ((name g<-i)
     (source (operation improve)))
    ((name g<-c)
     (source (constant 1)))))
  ((name x)
   (buttons
    ((name x<-r)
     (source (operation read)))))))

(operations
 ((name improve)
  (inputs (register guess) (register x)))
 ((name square)
  (inputs (register guess)))
 ((name print)
  (inputs (register guess)))
 ((name read))
 ((name good-enough?)
  (inputs (operation square) (register x))))

(controller
 loop
 (assign x (op read))
 (assign guess (const 1))
 good-enough?
 (test (op good-enough?) (op square) (reg x))
 (branch (label done))
 (assign guess (op improve) (reg guess))
 (goto (label good-enough?))
 done
 (perform (op print) (reg guess))
 (goto (label loop)))

                                        ;good-enought, improve как регистровая машина
(data-paths
 (registers
  ((name guess)
   (buttons
    ((name g<-c)
     (source (constant 1)))
    ((name g<-i)
     (source (operation improve)))))
  ((name x)
   (buttons
    ((name x<-r)
     (source (operation read)))))
  ((name t)
   (buttons
    ((name t<-i)
     (source (operation /)))))))

(operations
 ((name /)
  (inputs (register guess)
          (register x)
          (constant 2)
          (operation +)))
 ((name +)
  (inputs (register guess)
          (operation /)))
 ((name square)
  (inputs (register guess)))
 ((name -)
  (inputs (register x)
          (operation square)))
 ((name abs)
  (inputs (operation -)))
 ((name read))
 ((name print)
  (inputs (register guess)))
 ((name <)
  (inputs (operation abs))))

(controller
 loop
 (assign guess (const 1))
 (assign x (op read))
 test
 (assign t (op square) (reg guess))
 (assign t (op -) (reg t) (reg x))
 (assign t (op abs) (reg t))
 (test (op <) (reg t) (const 0.0001))
 (branch (label done))
 (assign t (op /) (reg x) (reg guess))
 (assign guess (op +) (reg guess) (reg t))
 (assign guess (op /) (reg guess) (const 2))
 (goto (label test))
 done
 (preform (op print) (reg guess))
 (goto loop))
