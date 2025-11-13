#ifndef CPP_WARMUP_BINARY_HEAP_H_
#define CPP_WARMUP_BINARY_HEAP_H_

#include <vector>

namespace warmup {

class BinaryHeap {
  std::vector<int> heap;

 public:
  BinaryHeap() = default;
  void push(int el);
  void pop();
  int top() const { return empty() ? -1 : heap.front(); }
  bool empty() const { return heap.empty(); }

 private:
  static std::size_t parent(std::size_t idx) { return idx / 2; }
  static std::size_t l_child(std::size_t idx) { return idx * 2; }
  static std::size_t r_child(std::size_t idx) { return (idx * 2) + 1; }
};

}  // namespace warmup

#endif  // CPP_WARMUP_BINARY_HEAP_H_
