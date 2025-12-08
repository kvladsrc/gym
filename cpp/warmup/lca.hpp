#ifndef CPP_WARMUP_LCA_H_
#define CPP_WARMUP_LCA_H_

#include <cstddef>
#include <vector>

namespace warmup {

/*
  Finding the Lowest Common Ancestor (LCA) by reducing the problem to
  a Range Minimum Query (RMQ) on the Euler Tour of the
  tree. (Alternative solution exists using Binary Lifting for LCA.)

  RMQ is computed using a segment tree. The tree is stored in an array
  (or vector) of size 4 * N. While a complete binary tree with N
  leaves has exactly 2 * N - 1 nodes, we allocate 4 * N elements. The
  reason is that child indices are calculated as 2 * v and 2 * v + 1,
  which creates gaps in the array representation. As a result, some
  node indices may exceed 2 * N - 1, requiring the larger allocation.
*/

using graph = std::vector<std::vector<std::size_t>>;

// I just like to use tree[v] = tree[v * 2] + tree[v * 2 + 1], so use
// struct with overloaded + operator.
struct meta {
  int d;
  std::size_t idx;

  meta();
  meta(int ad, std::size_t aidx);
  meta operator+(const meta& other) const;
};

class lca {
 public:
  explicit lca(const graph& g);
  std::size_t find(std::size_t a, std::size_t b);

 private:
  std::vector<std::size_t> path;
  std::vector<int> dists;
  std::vector<meta> tree;
  std::vector<std::size_t> pre;

  void ett(const graph& g, std::size_t v, std::size_t p);
  void build_tree(std::size_t l, std::size_t r, std::size_t v);
  meta query_tree(std::size_t l, std::size_t r, std::size_t cl, std::size_t cr,
                  std::size_t v);
};

}  // namespace warmup

#endif  // CPP_WARMUP_LCA_H_
