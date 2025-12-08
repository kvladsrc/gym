#ifndef CPP_WARMUP_LAZY_PROPAGATED_SEGMENT_TREE_H_
#define CPP_WARMUP_LAZY_PROPAGATED_SEGMENT_TREE_H_

#include <cstdint>
#include <vector>

namespace warmup {

class LazySegmentTree {
 public:
  explicit LazySegmentTree(int n);

  void build(const std::vector<int64_t>& sum);
  void update(int l, int r);
  int64_t query(int l, int r);

 private:
  int n_;
  std::vector<int64_t> tree_;
  std::vector<int64_t> lazy_;

  void build_tree(int v, int l, int r, const std::vector<int64_t>& sum);
  void push(int v, int l, int r);
  void update_tree(int v, int cl, int cr, int l, int r);
  int64_t query_tree(int v, int cl, int cr, int l, int r);
};

}  // namespace warmup

#endif  // CPP_WARMUP_LAZY_PROPAGATED_SEGMENT_TREE_H_
