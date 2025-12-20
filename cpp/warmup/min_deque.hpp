#ifndef CPP_WARMUP_MIN_DEQUE_H_
#define CPP_WARMUP_MIN_DEQUE_H_

#include <cstddef>
#include <deque>
#include <utility>

namespace warmup {

class MinDeque {
 public:
  explicit MinDeque(std::size_t size);

  void push_back(int el);

  int get_min();

 private:
  std::size_t max_size;
  std::size_t cur_pos;
  std::deque<std::pair<std::size_t, int>> deq;
};

}  // namespace warmup

#endif  // CPP_WARMUP_MIN_DEQUE_H_
