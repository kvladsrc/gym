#ifndef CPP_WARMUP_DSU_H_
#define CPP_WARMUP_DSU_H_

#include <cstddef>
#include <vector>

namespace warmup {

class DisjointSetUnion {
 public:
  explicit DisjointSetUnion(std::size_t size);
  bool connected(std::size_t a, std::size_t b);
  void unite(std::size_t a, std::size_t b);
  std::size_t find(std::size_t i);

 private:
  std::vector<std::size_t> parent;
  std::vector<int> rank;
};

}  // namespace warmup

#endif  // CPP_WARMUP_DSU_H_
