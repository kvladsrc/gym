(data-paths
 (registers
  ((name counter)
   (buttons
    ((name c<-inc)
     (source (operation inc)))))
  ((name product)
   (buttons
    ((name p<-mul)
     (source (operation mul)))))))

(operations
 ((name inc)
  (inputs (register counter)))
 ((name mul)
  (inputs (register counter) (register product)))
 ((name >)
  (inputs (register counter) (constant n))))

(controller
 test-counter
 (test (op >) (reg counter) (const n))
 (branch (label fact-done))
 (assign (op mul) (reg product) (reg counter))
 (assign (op inc) (reg counter))
 (goto (label test-counter))
 fact-done)
