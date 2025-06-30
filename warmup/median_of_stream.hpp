#ifndef CPP_WARMUP_MEDIAN_OF_STREAM_H_
#define CPP_WARMUP_MEDIAN_OF_STREAM_H_

#include <functional>
#include <optional>
#include <queue>
#include <vector>

namespace warmup {

class median {
  std::priority_queue<int> max_heap;
  std::priority_queue<int, std::vector<int>, std::greater<>> min_heap;

 public:
  median();

  std::optional<int> get_median() const;

  void push(int el);

 private:
  void fixup();
};

}  // namespace warmup

#endif  // CPP_WARMUP_MEDIAN_OF_STREAM_H_
