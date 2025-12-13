#ifndef CPP_WARMUP_LCA_H_
#define CPP_WARMUP_LCA_H_

#include <cstddef>
#include <vector>

namespace warmup {

/*
  Finding the Lowest Common Ancestor (LCA) by reducing the problem to
  a Range Minimum Query (RMQ) on the Euler Tour of the
  tree. (Alternative solution exists using Binary Lifting for LCA.)

  RMQ is computed using a sparce table with O(1) time on each query.
*/

using graph = std::vector<std::vector<std::size_t>>;

class lca {
 public:
  explicit lca(const graph& g);
  std::size_t find(std::size_t a, std::size_t b);

 private:
  std::vector<std::vector<std::size_t>> st;
  std::vector<int> depths;
  std::vector<size_t> logs;
  std::vector<std::size_t> path;
  std::vector<std::size_t> pre;

  void ett(const graph& g, std::size_t v, std::size_t p);
  void build_st();
  std::size_t query(std::size_t l, std::size_t r);
  int depth(std::size_t v);
};

}  // namespace warmup

#endif  // CPP_WARMUP_LCA_H_
