#ifndef CPP_WARMUP_CYCLE_DETECTION_H_
#define CPP_WARMUP_CYCLE_DETECTION_H_

#include <cstddef>
#include <vector>

namespace warmup {

const std::size_t default_size = 100;

class DisjointSetUnion {
 public:
  DisjointSetUnion();
  explicit DisjointSetUnion(std::size_t size);
  bool connected(std::size_t a, std::size_t b);
  void unite(std::size_t a, std::size_t b);

 private:
  std::vector<std::size_t> parent;
  std::vector<int> rank;
  std::size_t find(std::size_t i);
};

}  // namespace warmup

#endif  // CPP_WARMUP_CYCLE_DETECTION_H_
