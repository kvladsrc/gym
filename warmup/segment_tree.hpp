#ifndef CPP_WARMUP_FENWICK_TREE_H_
#define CPP_WARMUP_FENWICK_TREE_H_

#include <climits>
#include <vector>

namespace warmup {

struct meta {
  int minimal{};
  int maximal{};

  meta();
  meta(int mn, int mx);
  meta operator+(const meta &other) const;
};

struct node {
  meta m;
  node *left{};
  node *right{};
};

meta query(node *root, int ql, int qr, int l, int r);
node *build_tree(std::vector<int> &a, int start, int end);

}  // namespace warmup

#endif  // CPP_WARMUP_SEGMENT_TREE_H_
