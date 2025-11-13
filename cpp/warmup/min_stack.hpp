#ifndef CPP_WARMUP_MIN_STACK_H_
#define CPP_WARMUP_MIN_STACK_H_

#include <stack>

namespace warmup {

class MinStack {
  std::stack<int> main;
  std::stack<int> min;

 public:
  MinStack();

  int get_min();

  void push(int el);

  bool empty();

  void pop();
};

}  // namespace warmup

#endif  // CPP_WARMUP_MIN_STACK_H_
