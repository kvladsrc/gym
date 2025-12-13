#ifndef CPP_WARMUP_SEGMENT_TREE_H_
#define CPP_WARMUP_SEGMENT_TREE_H_

#include <climits>
#include <cstddef>
#include <vector>

namespace warmup {

/*
  The tree is stored in an array (or vector) of size 4 * N. While a
  complete binary tree with N leaves has exactly 2 * N - 1 nodes, we
  allocate 4 * N elements. The reason is that child indices are
  calculated as 2 * v and 2 * v + 1, which creates gaps in the array
  representation. As a result, some node indices may exceed 2 * N - 1,
  requiring the larger allocation.
*/

struct meta {
  int minimal{};
  int maximal{};

  meta();
  meta(int mn, int mx);
  meta operator+(const meta& other) const;
};

class segment_tree {
 public:
  explicit segment_tree(const std::vector<int>& v);
  meta query(std::size_t l, std::size_t r);

 private:
  std::vector<meta> tree;
  void build(std::size_t l, std::size_t r, std::size_t v,
             const std::vector<int>& vec);
  meta query(std::size_t cl, std::size_t cr, std::size_t l, std::size_t r,
             std::size_t v);
};

}  // namespace warmup

#endif  // CPP_WARMUP_SEGMENT_TREE_H_
